-- ============================================================================
-- Puente Nexa -> Supabase :: estado de los ingresos al programa
--
-- PROBLEMA QUE RESUELVE
-- A un paciente de clinica de heridas se le puede dar el alta y volver a
-- ingresar al mismo programa. El puente solo conocia "existe el paciente", asi
-- que el portal no tenia forma de saber (a) si el programa esta cerrado, y por
-- tanto no deberia aceptar mas seguimientos, ni (b) sobre cual ingreso debe
-- cargar los seguimientos cuando el paciente reingresa.
--
-- QUE SE GUARDA
-- Por paciente, una fila por ingreso con dos datos: su numero de orden (1 = la
-- primera atencion) y si sigue abierto. NO se guardan fechas, motivos de
-- egreso, diagnosticos ni ningun otro dato clinico: el portal solo necesita
-- saber si puede registrar y sobre que ingreso.
--
-- MISMAS REGLAS DE SEGURIDAD QUE LA TABLA DE PACIENTES
--   * Vive en el esquema "bridge", que no esta expuesto en la Data API.
--   * RLS habilitado y CERO politicas.
--   * Todos los privilegios revocados a public/anon/authenticated/service_role.
--   * La unica escritura posible es la funcion SECURITY DEFINER de abajo, que
--     solo puede ejecutar service_role (la Edge Function).
--   * La clave es documento_hmac: aqui tampoco hay documentos en claro.
-- ============================================================================

begin;

-- ---------------------------------------------------------------------------
-- Tabla de ingresos.
--
-- La PK compuesta (documento_hmac, ingreso) evita una columna "id" y ya expresa
-- la regla: un paciente no puede tener dos veces el ingreso numero 2.
--
-- La FK con ON DELETE CASCADE ata los ingresos a su paciente: si algun dia se
-- da de baja un paciente del puente, sus ingresos se van con el y no quedan
-- filas huerfanas apuntando a un HMAC inexistente.
--
-- actualizado_en NO es un dato del paciente: es la marca de tiempo del servidor
-- que permite al portal detectar si el puente lleva tiempo sin actualizarse.
-- ---------------------------------------------------------------------------
create table if not exists bridge.ingresos_heridas
(
    documento_hmac text        not null,
    ingreso        smallint    not null,
    estado         text        not null,
    actualizado_en timestamptz not null default now(),
    constraint ingresos_heridas_pkey primary key (documento_hmac, ingreso),
    constraint ingresos_heridas_documento_hmac_chk check (documento_hmac ~ '^[0-9a-f]{64}$'),
    constraint ingresos_heridas_ingreso_chk check (ingreso between 1 and 999),
    constraint ingresos_heridas_estado_chk check (estado in ('activo', 'cerrado')),
    constraint ingresos_heridas_paciente_fk foreign key (documento_hmac)
        references bridge.pacientes_heridas (documento_hmac) on delete cascade
);

comment on table bridge.ingresos_heridas is
    'Ingresos al programa de clinica de heridas por paciente seudonimizado. Solo numero de orden y estado; sin fechas ni datos clinicos.';
comment on column bridge.ingresos_heridas.ingreso is
    'Numero de orden del ingreso dentro del paciente: 1 es la primera atencion, 2 el primer reingreso. Estable porque en el censo las filas no se borran.';
comment on column bridge.ingresos_heridas.estado is
    'activo = el programa sigue abierto y admite seguimientos. cerrado = el paciente recibio el alta de ese ingreso.';
comment on column bridge.ingresos_heridas.actualizado_en is
    'Marca de tiempo del servidor, no del paciente. Sirve para detectar sincronizaciones detenidas.';

alter table bridge.ingresos_heridas enable row level security;

revoke all on table bridge.ingresos_heridas from public;
revoke all on table bridge.ingresos_heridas from anon;
revoke all on table bridge.ingresos_heridas from authenticated;
revoke all on table bridge.ingresos_heridas from service_role;

-- ---------------------------------------------------------------------------
-- Escritura: se amplia la MISMA funcion de upsert en lugar de crear otra.
--
-- Por que dentro de la misma funcion y no en una aparte: la Edge Function
-- atiende una sola peticion firmada, y el anti-replay consume el requestId una
-- unica vez. Dos funciones obligarian a que la segunda viera su propio nonce ya
-- insertado (y la rechazara como replay) o a saltarse la comprobacion. Con una
-- sola funcion todo el cambio entra ademas en una transaccion: o se aplican
-- paciente e ingresos, o no se aplica nada.
--
-- p_pacientes: [{"d": documento_hmac, "n": nombre_hmac, "e": nombre_encrypted,
--                "i": [{"n": 1, "s": "cerrado"}, {"n": 2, "s": "activo"}]}]
--
-- "i" es OPCIONAL. Una intranet que todavia no envie ingresos sigue funcionando
-- exactamente igual y no se le borra nada: la reconciliacion solo toca a los
-- pacientes que si trajeron la lista.
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
    v_ingresos     integer;
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

    -- --- Ingresos --------------------------------------------------------
    -- Solo de los pacientes que trajeron la lista. Se materializa en una tabla
    -- temporal porque hace falta dos veces: para el upsert y para borrar los
    -- ingresos que ya no existen.
    create temporary table pg_temp._ingresos_lote
    (
        documento_hmac text,
        ingreso        smallint,
        estado         text
    ) on commit drop;

    insert into pg_temp._ingresos_lote (documento_hmac, ingreso, estado)
    select distinct on (e.documento_hmac, (i ->> 'n')::smallint)
           e.documento_hmac,
           (i ->> 'n')::smallint,
           i ->> 's'
    from (
             select item ->> 'd' as documento_hmac,
                    item -> 'i'  as ingresos,
                    orden
             from jsonb_array_elements(p_pacientes) with ordinality as t(item, orden)
             where jsonb_typeof(item -> 'i') = 'array'
         ) e
             cross join lateral jsonb_array_elements(e.ingresos) as i
    order by e.documento_hmac, (i ->> 'n')::smallint, e.orden desc;

    insert into bridge.ingresos_heridas as g (documento_hmac, ingreso, estado, actualizado_en)
    select l.documento_hmac, l.ingreso, l.estado, now()
    from pg_temp._ingresos_lote l
    -- El paciente tiene que existir: la FK lo exige y el upsert de arriba ya lo
    -- dejo escrito en esta misma transaccion.
    where exists (select 1
                  from bridge.pacientes_heridas p
                  where p.documento_hmac = l.documento_hmac)
    on conflict (documento_hmac, ingreso)
        do update set estado = excluded.estado,
                      actualizado_en = now()
        where g.estado is distinct from excluded.estado;

    get diagnostics v_ingresos = row_count;

    -- Reconciliacion: la intranet envia SIEMPRE la lista completa del paciente,
    -- asi que un ingreso que ya no aparece dejo de existir en el censo.
    delete from bridge.ingresos_heridas g
    where exists (select 1
                  from pg_temp._ingresos_lote l
                  where l.documento_hmac = g.documento_hmac)
      and not exists (select 1
                      from pg_temp._ingresos_lote l
                      where l.documento_hmac = g.documento_hmac
                        and l.ingreso = g.ingreso);

    return jsonb_build_object(
        'replay', false,
        'recibidos', v_recibidos,
        'unicos', v_unicos,
        'insertados', v_insertados,
        'actualizados', v_actualizados,
        'ingresos', coalesce(v_ingresos, 0)
    );
end;
$$;

comment on function public.bridge_sync_pacientes_heridas(text, jsonb) is
    'Upsert idempotente del puente de clinica de heridas: paciente seudonimizado y sus ingresos al programa. Unica via de escritura sobre el esquema bridge.';

revoke all on function public.bridge_sync_pacientes_heridas(text, jsonb) from public;
revoke all on function public.bridge_sync_pacientes_heridas(text, jsonb) from anon;
revoke all on function public.bridge_sync_pacientes_heridas(text, jsonb) from authenticated;
grant execute on function public.bridge_sync_pacientes_heridas(text, jsonb) to service_role;

-- ---------------------------------------------------------------------------
-- Lectura: unica puerta de consulta para el portal.
--
-- Recibe el documento YA convertido a HMAC por la Edge Function, para que el
-- secreto siga sin salir de Supabase, igual que en la escritura.
--
-- Devuelve lo justo para decidir: si el paciente esta en el puente, sus
-- ingresos, cual es el vigente y si se pueden registrar seguimientos. No
-- devuelve el nombre ni ningun otro dato.
-- ---------------------------------------------------------------------------
create or replace function public.bridge_estado_paciente_heridas(
    p_documento_hmac text
)
    returns jsonb
    language plpgsql
    security definer
    set search_path = ''
as
$$
declare
    v_existe   boolean;
    v_ingresos jsonb;
    v_activo   smallint;
begin
    if p_documento_hmac is null or p_documento_hmac !~ '^[0-9a-f]{64}$' then
        raise exception 'documento_hmac_invalido' using errcode = '22023';
    end if;

    select true
    into v_existe
    from bridge.pacientes_heridas
    where documento_hmac = p_documento_hmac;

    if v_existe is null then
        return jsonb_build_object(
            'encontrado', false,
            'puede_registrar', false,
            'ingreso_actual', null,
            'ingresos', '[]'::jsonb);
    end if;

    select coalesce(
                   jsonb_agg(jsonb_build_object('numero', ingreso, 'estado', estado) order by ingreso),
                   '[]'::jsonb)
    into v_ingresos
    from bridge.ingresos_heridas
    where documento_hmac = p_documento_hmac;

    -- El ingreso vigente es el activo de mayor numero. Si no hay ninguno activo,
    -- el paciente esta de alta y no debe recibir mas seguimientos.
    select max(ingreso)
    into v_activo
    from bridge.ingresos_heridas
    where documento_hmac = p_documento_hmac
      and estado = 'activo';

    return jsonb_build_object(
        'encontrado', true,
        'puede_registrar', v_activo is not null,
        'ingreso_actual', v_activo,
        'ingresos', v_ingresos);
end;
$$;

comment on function public.bridge_estado_paciente_heridas(text) is
    'Consulta el estado de los ingresos de un paciente del puente. Unica via de lectura sobre bridge.ingresos_heridas.';

revoke all on function public.bridge_estado_paciente_heridas(text) from public;
revoke all on function public.bridge_estado_paciente_heridas(text) from anon;
revoke all on function public.bridge_estado_paciente_heridas(text) from authenticated;
grant execute on function public.bridge_estado_paciente_heridas(text) to service_role;

commit;
