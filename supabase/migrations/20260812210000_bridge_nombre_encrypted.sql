-- ============================================================================
-- Puente Nexa -> Supabase :: nombre cifrado
--
-- El HMAC no es reversible, asi que nombre_hmac no sirve para devolverle el
-- nombre real al portal administrativo. Se agrega nombre_encrypted con el
-- nombre cifrado por AES-256-GCM dentro de la Edge Function, usando
-- BRIDGE_ENCRYPTION_KEY (secreto que solo conoce la Edge Function).
--
-- Formato del sobre:  v1.<nonce base64url>.<ciphertext+tag base64url>
-- El AAD del cifrado es documento_hmac, asi que un ciphertext solo descifra en
-- su propia fila.
--
-- REGISTROS EXISTENTES: las 5 filas presentes provienen de las pruebas de
-- puesta en marcha (documentos del censo de prueba) y no tienen forma de
-- obtener nombre_encrypted sin el nombre original, que aqui no existe. Se
-- vacian y se vuelven a sincronizar desde la intranet, que es la fuente de
-- verdad; asi la columna puede nacer NOT NULL sin inventar ningun valor.
-- ============================================================================

begin;

-- 1. Se vacia la tabla de pruebas. La intranet la repuebla con una pasada de
--    reconciliacion (SupabaseBridge__Enabled) o al guardar cada paciente.
truncate table bridge.pacientes_heridas;

-- 2. Nueva columna. NOT NULL: una fila sin nombre cifrado seria inservible
--    para el portal y ocultaria un fallo de configuracion de la clave.
alter table bridge.pacientes_heridas
    add column if not exists nombre_encrypted text not null;

comment on column bridge.pacientes_heridas.nombre_encrypted is
    'Nombre real cifrado con AES-256-GCM por la Edge Function. Formato v1.<nonce>.<ciphertext+tag> en base64url; AAD = documento_hmac. Solo se descifra con BRIDGE_ENCRYPTION_KEY.';

-- 3. El CHECK obliga al formato del sobre: un nombre en claro no lo cumple, de
--    modo que la base de datos rechaza cualquier intento de guardar texto plano.
alter table bridge.pacientes_heridas
    drop constraint if exists pacientes_heridas_nombre_encrypted_chk;
alter table bridge.pacientes_heridas
    add constraint pacientes_heridas_nombre_encrypted_chk
        check (nombre_encrypted ~ '^v1\.[A-Za-z0-9_-]{16}\.[A-Za-z0-9_-]+$');

-- 4. El upsert ahora escribe tambien el nombre cifrado.
--
--    La condicion de actualizacion sigue siendo el cambio de nombre_hmac (que
--    es determinista). El ciphertext cambia en cada envio porque el nonce es
--    aleatorio, asi que compararlo obligaria a reescribir todas las filas en
--    cada sincronizacion; con nombre_hmac se reescribe solo cuando el nombre
--    cambio de verdad, y el conteo de "actualizados" sigue siendo informativo.
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
               item ->> 'e' as nombre_encrypted,
               orden
        from jsonb_array_elements(p_pacientes) with ordinality as t(item, orden)
    ),
    deduplicado as (
        select distinct on (documento_hmac) documento_hmac, nombre_hmac, nombre_encrypted
        from entrada
        order by documento_hmac, orden desc
    ),
    aplicado as (
        insert into bridge.pacientes_heridas as p (documento_hmac, nombre_hmac, nombre_encrypted)
        select documento_hmac, nombre_hmac, nombre_encrypted
        from deduplicado
        on conflict (documento_hmac)
            do update set nombre_hmac = excluded.nombre_hmac,
                          nombre_encrypted = excluded.nombre_encrypted
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

commit;
