-- ============================================================================
-- Puente Nexa (intranet) -> Supabase :: esquema "bridge"
--
-- Objetivo: guardar UNICAMENTE dos columnas seudonimizadas por paciente de
-- clinica de heridas. Nunca se almacena documento real, nombre real, fechas,
-- estado, telefonos ni informacion clinica.
--
-- Modelo de acceso:
--   * El esquema "bridge" NO esta expuesto en la Data API (PostgREST solo
--     publica "public" y "graphql_public"), por lo que la tabla no es
--     alcanzable por HTTP ni con la anon key ni con la service_role key.
--   * RLS habilitado y CERO politicas -> ningun rol con RLS puede leer/escribir.
--   * Se revocan todos los privilegios a public/anon/authenticated/service_role,
--     de modo que la unica via de escritura es la funcion SECURITY DEFINER
--     public.bridge_sync_pacientes_heridas, que solo puede ejecutar service_role
--     (la Edge Function).
-- ============================================================================

create schema if not exists bridge;

revoke all on schema bridge from public;
revoke all on schema bridge from anon;
revoke all on schema bridge from authenticated;
revoke all on schema bridge from service_role;

-- ---------------------------------------------------------------------------
-- Tabla puente: solo dos columnas.
--
-- documento_hmac es PRIMARY KEY (no se agrega columna "id"): en PostgreSQL la
-- PK ya implica NOT NULL + UNIQUE + indice, asi que cumple el requisito de
-- unicidad sin necesidad de una columna extra. No hay timestamps ni estado.
--
-- Los CHECK garantizan que solo entren digest HMAC-SHA256 en hexadecimal
-- minuscula (64 caracteres): si alguna vez se intentara insertar un documento
-- o un nombre en claro, la base de datos lo rechaza.
-- ---------------------------------------------------------------------------
create table if not exists bridge.pacientes_heridas
(
    documento_hmac text not null,
    nombre_hmac    text not null,
    constraint pacientes_heridas_pkey primary key (documento_hmac),
    constraint pacientes_heridas_documento_hmac_chk check (documento_hmac ~ '^[0-9a-f]{64}$'),
    constraint pacientes_heridas_nombre_hmac_chk check (nombre_hmac ~ '^[0-9a-f]{64}$')
);

comment on table bridge.pacientes_heridas is
    'Puente seudonimizado de pacientes del censo de clinica de heridas de Nexa. Solo HMAC-SHA256; sin datos personales.';
comment on column bridge.pacientes_heridas.documento_hmac is
    'HMAC-SHA256(BRIDGE_HMAC_SECRET, documento_normalizado) en hex minuscula. Clave unica del paciente.';
comment on column bridge.pacientes_heridas.nombre_hmac is
    'HMAC-SHA256(BRIDGE_HMAC_SECRET, nombre_normalizado) en hex minuscula.';

alter table bridge.pacientes_heridas enable row level security;

revoke all on table bridge.pacientes_heridas from public;
revoke all on table bridge.pacientes_heridas from anon;
revoke all on table bridge.pacientes_heridas from authenticated;
revoke all on table bridge.pacientes_heridas from service_role;

-- ---------------------------------------------------------------------------
-- Anti-replay: guarda el requestId de cada peticion aceptada durante una hora.
-- No contiene datos personales (solo un UUID generado por la intranet).
-- Es necesario porque la ventana de timestamp (5 min) por si sola no impide
-- reenviar la misma peticion firmada dentro de esa ventana.
-- ---------------------------------------------------------------------------
create table if not exists bridge.sync_request_nonces
(
    request_id  text not null,
    recibido_en timestamptz not null default now(),
    constraint sync_request_nonces_pkey primary key (request_id)
);

comment on table bridge.sync_request_nonces is
    'Nonces (requestId) de sincronizaciones aceptadas. Solo para impedir reenvio (replay). Sin datos personales.';

alter table bridge.sync_request_nonces enable row level security;

revoke all on table bridge.sync_request_nonces from public;
revoke all on table bridge.sync_request_nonces from anon;
revoke all on table bridge.sync_request_nonces from authenticated;
revoke all on table bridge.sync_request_nonces from service_role;

-- ---------------------------------------------------------------------------
-- Unica puerta de escritura. La Edge Function la invoca por RPC con la
-- service_role key; la funcion corre como su propietario (postgres) y por eso
-- puede escribir en "bridge" aunque service_role no tenga privilegios ahi.
--
-- p_pacientes: jsonb array de objetos {"d": <documento_hmac>, "n": <nombre_hmac>}
-- Devuelve conteos tecnicos; nunca devuelve filas de la tabla.
-- ---------------------------------------------------------------------------
create or replace function public.bridge_sync_pacientes_heridas(
    p_request_id text,
    p_pacientes  jsonb
)
    returns jsonb
    language plpgsql
    security definer
    set search_path = ''
as
$$
declare
    v_recibidos    integer;
    v_unicos       integer;
    v_insertados   integer;
    v_actualizados integer;
begin
    if p_request_id is null or length(p_request_id) = 0 or length(p_request_id) > 64 then
        raise exception 'request_id_invalido' using errcode = '22023';
    end if;

    if p_pacientes is null or jsonb_typeof(p_pacientes) <> 'array' then
        raise exception 'payload_invalido' using errcode = '22023';
    end if;

    v_recibidos := jsonb_array_length(p_pacientes);

    -- Limpieza perezosa de nonces vencidos (ventana de firma 5 min, margen 1 h).
    delete from bridge.sync_request_nonces where recibido_en < now() - interval '1 hour';

    begin
        insert into bridge.sync_request_nonces (request_id) values (p_request_id);
    exception
        when unique_violation then
            return jsonb_build_object('replay', true);
    end;

    with entrada as (
        select item ->> 'd' as documento_hmac,
               item ->> 'n' as nombre_hmac,
               orden
        from jsonb_array_elements(p_pacientes) with ordinality as t(item, orden)
    ),
    -- Si el mismo documento llega dos veces en el mismo lote gana el ultimo,
    -- para que ON CONFLICT no falle con "affect row a second time".
    deduplicado as (
        select distinct on (documento_hmac) documento_hmac, nombre_hmac
        from entrada
        order by documento_hmac, orden desc
    ),
    aplicado as (
        insert into bridge.pacientes_heridas as p (documento_hmac, nombre_hmac)
        select documento_hmac, nombre_hmac
        from deduplicado
        on conflict (documento_hmac)
            do update set nombre_hmac = excluded.nombre_hmac
            where p.nombre_hmac is distinct from excluded.nombre_hmac
        returning (xmax = 0) as fue_insercion
    )
    select (select count(*) from deduplicado),
           (select count(*) from aplicado where fue_insercion),
           (select count(*) from aplicado where not fue_insercion)
    into v_unicos, v_insertados, v_actualizados;

    return jsonb_build_object(
        'replay', false,
        'recibidos', v_recibidos,
        'unicos', v_unicos,
        'insertados', v_insertados,
        'actualizados', v_actualizados
    );
end;
$$;

comment on function public.bridge_sync_pacientes_heridas(text, jsonb) is
    'Upsert idempotente del puente de clinica de heridas. Unica via de escritura sobre bridge.pacientes_heridas.';

revoke all on function public.bridge_sync_pacientes_heridas(text, jsonb) from public;
revoke all on function public.bridge_sync_pacientes_heridas(text, jsonb) from anon;
revoke all on function public.bridge_sync_pacientes_heridas(text, jsonb) from authenticated;
grant execute on function public.bridge_sync_pacientes_heridas(text, jsonb) to service_role;
