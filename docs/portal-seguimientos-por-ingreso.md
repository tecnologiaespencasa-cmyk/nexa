# Seguimientos de clínica de heridas por ingreso — instrucciones para el portal

Documento para el equipo del **Portal Administrativo** (el que captura los seguimientos
de herida en Neon + SharePoint).

## 1. El problema

A un paciente de clínica de heridas se le puede dar el alta y **volver a ingresar al
mismo programa**. Hoy el portal no tiene forma de saberlo:

- Sigue aceptando seguimientos de un paciente que ya recibió el alta y cuyo programa
  está cerrado.
- Cuando el paciente reingresa, los seguimientos nuevos quedan mezclados con los de la
  atención anterior, porque en Neon todos cuelgan del mismo `pacienteRef` y en SharePoint
  de la misma carpeta `{NOMBRE} - {DOCUMENTO}`.

La intranet ya lo notó desde su lado: al abrir el segundo ingreso de un paciente, la
sección "Características de la herida" mostraba los seguimientos del primero.

## 2. Qué cambia

La intranet publica ahora, en el puente de Supabase y con las **mismas reglas de
seguridad** que ya usa para los pacientes, el estado de cada ingreso al programa:

| Dato | Significado |
|---|---|
| `numero` | Orden del ingreso dentro del paciente. 1 es la primera atención, 2 el primer reingreso. |
| `estado` | `activo` = el programa sigue abierto y admite seguimientos. `cerrado` = ese ingreso recibió el alta. |

No se publican fechas, motivos de egreso, diagnósticos ni ningún otro dato clínico. La
clave sigue siendo `documento_hmac`: en Supabase no hay documentos en claro.

El número de ingreso es **estable**: en el censo las filas nunca se borran, así que el
ingreso 2 seguirá siendo el 2 para siempre.

## 3. Lo que tiene que hacer el portal

### 3.1 Consultar el estado antes de permitir un seguimiento

Nueva Edge Function **`estado-paciente-heridas`**. Se llama con el documento real; ella
calcula el HMAC internamente, porque `BRIDGE_HMAC_SECRET` no sale de Supabase.

```
POST https://qlmglhygiyykyhyzjczr.supabase.co/functions/v1/estado-paciente-heridas
Authorization: Bearer <BRIDGE_QUERY_API_SECRET>
Content-Type: application/json
X-Bridge-Timestamp: 1760000000
X-Bridge-Request-Id: 9f1c2d3e-4a5b-6c7d-8e9f-0a1b2c3d4e5f
X-Bridge-Signature: <firma>

{"requestId":"9f1c2d3e-4a5b-6c7d-8e9f-0a1b2c3d4e5f","timestamp":1760000000,"document":"71234567"}
```

Respuesta:

```json
{
  "success": true,
  "found": true,
  "canRegister": true,
  "currentAdmission": 2,
  "admissions": [
    { "number": 1, "state": "cerrado" },
    { "number": 2, "state": "activo" }
  ],
  "requestId": "9f1c2d3e-4a5b-6c7d-8e9f-0a1b2c3d4e5f"
}
```

**Cómo firmar** (idéntico a la función de escritura que ya existe):

```
firma = HMAC-SHA256(BRIDGE_QUERY_API_SECRET, `${timestamp}.${requestId}.${cuerpoCrudo}`)   // hex minúscula
```

**El secreto es el de lectura, `BRIDGE_QUERY_API_SECRET`, no el de escritura.** Una
versión anterior de este documento decía `BRIDGE_API_SECRET`; era un error y está
corregido en la función. `BRIDGE_API_SECRET` autoriza a *sincronizar pacientes*, así que
entregárselo al portal —que solo necesita consultar— le daría de paso permiso de
escritura sobre el puente. Con dos secretos los roles quedan separados y rotar uno no
obliga a rotar el otro. `BRIDGE_HMAC_SECRET` sí es el mismo de la escritura, y no puede
ser otro porque el HMAC del documento tiene que coincidir con el guardado; nunca sale de
Supabase, así que el portal no lo ve.

- `cuerpoCrudo` son **exactamente los bytes que se envían**. Serializa una sola vez y
  firma esa misma cadena; volver a serializar puede cambiar el orden de las claves y la
  firma dejaría de coincidir.
- `timestamp` es epoch en segundos y debe estar dentro de ±5 minutos del reloj de
  Supabase. Sincroniza el reloj del servidor del portal.
- `requestId` es un UUID por petición: `^[A-Za-z0-9-]{8,64}$`.
- `requestId` y `timestamp` van **a la vez** en cabecera y en cuerpo, y deben coincidir.
- El documento se normaliza dentro de la función (se le quitan acentos y todo lo que no
  sea `A-Za-z0-9`, y se pasa a mayúsculas), así que puedes enviarlo tal como lo tengas.
- A diferencia de la escritura, esta función **no consume nonce anti-replay**: solo lee y
  reenviarla no cambia nada.

**Reglas de decisión:**

| Respuesta | Qué hace el portal |
|---|---|
| `canRegister: true` | Permite registrar. El seguimiento se marca con `currentAdmission`. |
| `canRegister: false` y `found: true` | Bloquea. El paciente recibió el alta; muestra "Este paciente no tiene un ingreso activo en clínica de heridas". |
| `found: false` | Bloquea. El paciente no está en el censo de clínica de heridas. |
| Error HTTP o red | **Bloquea y reintenta**, no asumas que se puede registrar. Un fallo del puente no debe abrir la puerta. |

Conviene consultar al **abrir** el formulario de seguimiento (para no dejar que el
auxiliar llene todo y falle al final) y otra vez al **guardar** (porque el alta pudo
ocurrir mientras llenaba).

### 3.2 Guardar el número de ingreso en cada seguimiento

En Neon, la tabla de seguimientos (`ClinicaHeridas`) necesita una columna nueva:

```sql
alter table "ClinicaHeridas"
    add column if not exists "ingreso" smallint not null default 1;

comment on column "ClinicaHeridas"."ingreso" is
    'Numero de ingreso al programa sobre el que se registro este seguimiento. Lo entrega la Edge Function estado-paciente-heridas como currentAdmission.';
```

**El `default 1` es correcto para lo existente**: a hoy ningún paciente tiene más de una
fila en el censo de clínica de heridas, así que todos los seguimientos ya registrados
pertenecen al ingreso 1. No hace falta ningún backfill adicional.

Al crear un seguimiento, escribe en `ingreso` el `currentAdmission` que devolvió la
consulta. **No lo calcules en el portal**: la única fuente de verdad del número de
ingreso es el censo.

### 3.3 No hace falta tocar SharePoint

La carpeta del paciente sigue siendo la misma. Separar carpetas por ingreso rompería el
vínculo `{NOMBRE} - {DOCUMENTO}` del que depende la intranet para encontrarla. La
distinción vive en la columna `ingreso` de Neon, no en la estructura de carpetas.

## 4. Lo que hace la intranet

- **Publica** los ingresos en cada sincronización del puente. Envía siempre la lista
  completa del paciente, así que un ingreso que pasa de `activo` a `cerrado` viaja en la
  misma pasada.
- **Avisa al puente en cuanto se da el alta.** Hasta el 2026-09-07 solo se encolaba el
  envío al guardar los datos básicos del paciente; el alta del programa, que se guarda
  por otra acción, **no llegaba nunca a Supabase**. Corregido: ahora encolan también el
  alta y las demás secciones de clínica de heridas.
- **Muestra** en "Características de la herida" solo los seguimientos del ingreso que se
  está viendo, con un distintivo "Ingreso 2 de 2" y un aviso de cuántos pertenecen a
  otros ingresos.
- **Lee la columna `ingreso` de cada seguimiento** (desde 2026-09-08). Ya no estima por
  fecha. Las dos numeraciones coinciden por construcción: el portal escribe el número que
  le entrega el puente, y ese número es la posición de la fila en `censo_clinica_heridas`
  ordenada por fecha de ingreso al programa — exactamente el mismo criterio con el que la
  intranet numera el ingreso que muestra en pantalla.
- **Reconcilia el puente cada 24 horas** (`SupabaseBridge:Enabled = true` desde
  2026-09-08). El envío al guardar sigue siendo la vía principal; la reconciliación es la
  red que repara un envío perdido, para que un alta no deje al paciente como activo
  indefinidamente.

## 5. Orden de despliegue

> **Estado al 2026-09-08: TODOS los pasos están HECHOS.** El portal escribe la columna
> `ingreso` (verificado en Neon: `smallint NOT NULL DEFAULT 1`, 9 seguimientos, ninguno
> nulo), la intranet ya la lee en lugar de estimar por fecha, y la reconciliación
> periódica del puente quedó activada.
>
> **Estado al 2026-09-07: los pasos 1, 2 y 3 quedaron HECHOS y verificados en producción.**
> `bridge.ingresos_heridas` existe con RLS y cero políticas, las dos funciones de base de
> datos solo las ejecuta `service_role`, `estado-paciente-heridas` está desplegada con
> `verify_jwt = false`, y la intranet ya publicó los ingresos de los 108 pacientes del
> puente (107 activos, 1 cerrado). Los 108 pacientes que ya había siguen intactos.
> **El portal puede pasar al paso 4 y luego encender `BRIDGE_VALIDACION_INGRESO`.**

Cada paso es compatible hacia atrás; se puede parar en cualquiera sin romper lo anterior.

1. **Supabase — migración.** Aplicar
   `supabase/migrations/20260907170000_bridge_ingresos_heridas.sql`.
   Crea `bridge.ingresos_heridas`, amplía la función de escritura y añade la de lectura.
   Mientras la intranet no envíe ingresos, la tabla queda vacía y nada cambia.

   **Se aplicó pegando solo ese archivo, no con `supabase db push`, y hay que seguir
   haciéndolo así.** La migración anterior del puente
   (`20260812210000_bridge_nombre_encrypted.sql`) empieza con
   `truncate table bridge.pacientes_heridas;`, que en su momento era correcto porque solo
   había 5 filas de prueba. **Se comprobó que el proyecto remoto NO tiene tabla de
   historial de migraciones** (`supabase_migrations.schema_migrations` no existe), así que
   `db push` habría considerado pendientes las tres migraciones y habría vuelto a ejecutar
   ese `truncate`, **borrando los pacientes del puente**. Mientras no exista ese historial,
   `supabase db push` es peligroso en este proyecto.

   El archivo es una sola transacción y se puede volver a ejecutar sin daño
   (`create table if not exists`, `create or replace function`, `revoke` y `comment` son
   todos idempotentes).

   Para comprobar que quedó bien:

   ```sql
   -- 1. La tabla existe, con RLS y sin políticas.
   select relrowsecurity as rls_activo,
          (select count(*) from pg_policies
            where schemaname = 'bridge' and tablename = 'ingresos_heridas') as politicas
   from pg_class where oid = 'bridge.ingresos_heridas'::regclass;
   -- esperado: rls_activo = true, politicas = 0

   -- 2. Las dos funciones existen y solo las puede ejecutar service_role.
   select p.proname,
          has_function_privilege('service_role', p.oid, 'execute') as service_role,
          has_function_privilege('anon',         p.oid, 'execute') as anon,
          has_function_privilege('authenticated', p.oid, 'execute') as authenticated
   from pg_proc p join pg_namespace n on n.oid = p.pronamespace
   where n.nspname = 'public'
     and p.proname in ('bridge_sync_pacientes_heridas', 'bridge_estado_paciente_heridas');
   -- esperado: service_role = true, anon = false, authenticated = false

   -- 3. Los pacientes del puente siguen ahí (no se truncó nada).
   select count(*) from bridge.pacientes_heridas;
   ```
2. **Supabase — Edge Functions.** Desplegar `sync-pacientes-heridas` (ya acepta el campo
   `admissions`, opcional) y la nueva `estado-paciente-heridas`, ambas con
   `verify_jwt = false`. La segunda necesita `BRIDGE_QUERY_API_SECRET` y
   `BRIDGE_HMAC_SECRET`; **no** necesita `BRIDGE_ENCRYPTION_KEY` ni `BRIDGE_API_SECRET`.
3. **Intranet.** Al desplegarse empieza a enviar `admissions` en cada sincronización.
   Para poblar todo de una vez, activar la reconciliación periódica
   (`SupabaseBridge__Enabled = true`) o esperar a que se guarde cada paciente.
4. **Portal.** Columna `ingreso` en Neon, consulta a `estado-paciente-heridas` y bloqueo
   del formulario, todo detrás de la bandera `BRIDGE_VALIDACION_INGRESO`, apagada.
5. **Portal.** `BRIDGE_VALIDACION_INGRESO=true`. Recién aquí empieza a bloquear, y solo
   cuando los pasos 1 a 3 están hechos: con la función inexistente, fallar cerrado
   dejaría el módulo entero sin poder registrar.
6. **Intranet, cierre del ciclo.** Hecho: la intranet lee la columna `ingreso` (§4).

Si se hace el paso 3 antes del 1, la escritura falla porque la función de base de datos
no existe todavía: la intranet lo registra como lote fallido y reintenta. No se pierde
nada, pero conviene respetar el orden.

## 6. Lista de comprobación

- [ ] Paciente activo: `canRegister: true` y el seguimiento se guarda con el ingreso correcto.
- [ ] Paciente con alta: `canRegister: false` y el formulario queda bloqueado.
- [ ] Documento que no está en el censo: `found: false` y bloqueo.
- [ ] Reingreso: tras reabrir el programa en el censo, el portal devuelve
      `currentAdmission: 2` y los seguimientos nuevos quedan separados de los del ingreso 1.
- [ ] Alta durante el llenado: la consulta al guardar bloquea aunque la de apertura permitiera.
- [ ] Firma mal calculada: 401 `invalid_signature`.
- [ ] `timestamp` desfasado más de 5 minutos: 401 `timestamp_out_of_window`.
- [ ] Supabase caído: el portal bloquea y reintenta, nunca da por bueno el registro.
- [ ] Los vectores de `supabase/functions/sync-pacientes-heridas/test-vectors.json` siguen
      pasando en la implementación del portal.

## 7. Advertencias

- **El estado del programa se vuelve visible en el puente.** El diseño original decía que
  la tabla no guardaría estado. Se relaja a propósito, porque el portal no puede decidir
  sin él. Lo que se publica es el mínimo: un número de orden y `activo`/`cerrado`, sin
  fechas y sin nada clínico, y sigue estando atado a un HMAC, no a un documento.
- **El puente no es instantáneo.** Entre el alta en el censo y su llegada a Supabase pasa
  el tiempo de la cola de envío —segundos en condiciones normales—. El portal puede
  aceptar seguimientos de un paciente recién dado de alta durante esa ventana, y no es
  algo que el portal pueda cerrar por su cuenta.
- **Si Supabase está caído en ese momento, el envío se pierde.** La cola vive en memoria
  del proceso de la intranet. Para eso está la reconciliación periódica, **activada el
  2026-09-08** (`SupabaseBridge:Enabled = true`): repara los envíos perdidos sin que nadie
  tenga que volver a guardar la ficha.
- **La reconciliación corre cada 24 horas** (`IntervalHours`). Ese es el peor caso para que
  un alta perdida llegue al puente. La pasada completa de los ~110 pacientes tarda menos de
  dos segundos, así que bajar el intervalo a 4 o 6 horas es barato si se quiere estrechar
  esa ventana; queda a criterio de operaciones.
- **La numeración depende de que en el censo no se borren filas.** Es la regla vigente:
  la intranet impide eliminar un programa que ya tiene información guardada. Si alguna vez
  se borrara una fila a mano en base de datos, los ingresos posteriores se recorrerían y
  los seguimientos quedarían apuntando al ingreso equivocado.

Ver también `docs/puente-supabase.md` (arquitectura y seguridad del puente) y
`docs/seguimientos-clinica-heridas.md` (cómo la intranet lee Neon y SharePoint).
