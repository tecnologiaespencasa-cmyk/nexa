# Puente Nexa → Supabase (clínica de heridas)

Fase 1: sincronizar los pacientes del censo de **clínica de heridas** hacia una base
puente en Supabase, guardando allí **únicamente dos HMAC** (documento y nombre).

Proyecto Supabase: `toportal` — `https://qlmglhygiyykyhyzjczr.supabase.co`
Estado: esquema aplicado, secretos cargados y Edge Function desplegada (v1, `verify_jwt = false`).

```
INTRANET NEXA (Azure PostgreSQL)
        │  HTTPS + Bearer + firma HMAC + timestamp + requestId
        │  payload: { document, name }   ← reales, solo en tránsito
        ▼
SUPABASE EDGE FUNCTION  sync-pacientes-heridas   (única puerta de escritura)
        ├── HMAC-SHA256(documento normalizado) → documento_hmac
        ├── HMAC-SHA256(nombre normalizado)    → nombre_hmac
        └── AES-256-GCM(nombre real)           → nombre_encrypted
        │  RPC public.bridge_sync_pacientes_heridas (SECURITY DEFINER, service_role)
        ▼
SUPABASE POSTGRESQL  bridge.pacientes_heridas
        documento_hmac   TEXT PRIMARY KEY
        nombre_hmac      TEXT NOT NULL
        nombre_encrypted TEXT NOT NULL
```

La intranet **nunca** se conecta al PostgreSQL de Supabase, no usa la `service_role`
key ni la contraseña del usuario `postgres`.

---

## 1. Cómo se identifica un paciente de clínica de heridas

No existe ninguna columna `servicio` ni un catálogo de programas: **el censo de
clínica de heridas es una tabla propia**.

| Concepto | Dónde vive |
|---|---|
| Tabla | `censo_clinica_heridas` |
| Entidad | `Nexa.Data.Entities.CensoClinicaHeridasRecord` |
| DbSet | `ApplicationDbContext.CensoClinicaHeridas` |
| Documento | `NumeroIdentificacion` (string) |
| Nombre | `NombrePaciente` (un solo campo; la aplicación ya lo guarda en mayúsculas) |

Pertenecer al programa **equivale a tener registro en esa tabla**, así que la consulta
de sincronización no lleva ningún filtro por servicio y ningún paciente de agudos,
crónicos, NPT o terapia ambulatoria puede entrar. Ver
`Services/ClinicaHeridasBridgeSyncService.LoadPatientsAsync`.

Reglas aplicadas al leer:

- Se deduplica por documento normalizado; gana el registro más reciente
  (`CreatedAtUtc` desc, `Id` desc), que es el que tiene el nombre vigente.
- Se descartan filas sin documento o sin nombre utilizable.
- **No se modifica ningún dato en la base de la intranet** y no se agregó ninguna
  columna nueva.

---

## 2. Normalización canónica (contrato para la fase 2)

Definida en dos implementaciones equivalentes:

- C#: `Helpers/BridgeIdentityNormalizer.cs`
- TypeScript: `supabase/functions/sync-pacientes-heridas/normalize.ts`

**Documento**

1. Normalizar a Unicode NFD y eliminar las marcas diacríticas (`Ñ`→`N`, `á`→`a`).
2. Eliminar todo carácter que no sea `[A-Za-z0-9]` (espacios, puntos, guiones, barras…).
3. Pasar a mayúsculas. Resultado: `^[A-Z0-9]+$`.

**Nombre**

1. Normalizar a Unicode NFD y eliminar las marcas diacríticas.
2. Sustituir por espacio todo carácter que no sea `[A-Za-z0-9]`.
3. Colapsar espacios repetidos y recortar extremos.
4. Pasar a mayúsculas. Resultado: `^[A-Z0-9]+( [A-Z0-9]+)*$`.

**HMAC**

```
documento_hmac = HMAC-SHA256(BRIDGE_HMAC_SECRET, documento_normalizado)
nombre_hmac    = HMAC-SHA256(BRIDGE_HMAC_SECRET, nombre_normalizado)
```

- Clave: el secreto en UTF-8, tal cual (no se decodifica base64).
- Salida: hexadecimal **en minúscula**, 64 caracteres.
- Mismo secreto para ambos campos.
- No es SHA-256 simple: sin la clave el digest no se puede reproducir.

### Cifrado del nombre (`nombre_encrypted`)

El HMAC no es reversible, así que no sirve para devolverle el nombre al portal
administrativo. Por eso la Edge Function guarda además el nombre cifrado con
**AES-256-GCM** (`supabase/functions/sync-pacientes-heridas/crypto.ts`).

```
nombre_encrypted = v1.<nonce>.<ciphertext+tag>
```

- `v1`: versión del sobre; permite rotar clave o algoritmo más adelante.
- `nonce`: 12 bytes aleatorios **distintos en cada cifrado**, base64url sin relleno (16 caracteres).
- `ciphertext+tag`: texto cifrado seguido del tag de 16 bytes, base64url sin relleno.
- **AAD = `documento_hmac`**: el ciphertext solo descifra en su propia fila, así que
  nadie con acceso de escritura puede intercambiar nombres entre pacientes.
- Se cifra el nombre **tal como lo envía la intranet**, con sus tildes y mayúsculas; no
  la versión normalizada (esa pierde tildes y solo sirve para el HMAC).
- La clave es `BRIDGE_ENCRYPTION_KEY`, 32 bytes, y vive **solo** como secreto de la Edge
  Function. La intranet no la conoce. El sobre lleva todo lo necesario para descifrar
  excepto la clave.

Para la fase 2, el portal no debe descifrar por su cuenta: la clave no sale de Supabase.
Necesitará una Edge Function de consulta que reciba el documento, calcule
`documento_hmac`, lea la fila y devuelva el nombre descifrado.

Los vectores de referencia están en
`supabase/functions/sync-pacientes-heridas/test-vectors.json` y los verifican las dos
implementaciones. **El portal Next.js debe pasar esos mismos vectores** antes de
consultar la tabla puente.

---

## 3. Contrato HTTP

`POST https://<proyecto>.supabase.co/functions/v1/sync-pacientes-heridas`

Cabeceras:

| Cabecera | Valor |
|---|---|
| `Authorization` | `Bearer <BRIDGE_API_SECRET>` |
| `X-Bridge-Timestamp` | epoch en segundos |
| `X-Bridge-Request-Id` | UUID (uno nuevo por intento) |
| `X-Bridge-Signature` | `HMAC-SHA256(BRIDGE_API_SECRET, timestamp + "." + requestId + "." + rawBody)` en hex |
| `Content-Type` | `application/json` |

Cuerpo:

```json
{
  "requestId": "3f6a…",
  "timestamp": 1760000000,
  "patients": [{ "document": "123456789", "name": "NOMBRE APELLIDO" }]
}
```

Respuesta (solo información técnica, nunca pacientes):

```json
{ "success": true, "processed": 10, "inserted": 7, "updated": 3, "requestId": "3f6a…" }
```

Validaciones de la Edge Function, en orden: método → Content-Type → cabeceras →
bearer (comparación en tiempo constante) → timestamp dentro de ±5 min → tamaño del
cuerpo (≤1 MiB) → firma → estructura del payload → coincidencia de `requestId` y
`timestamp` entre cuerpo y cabecera → máximo 500 registros → documento y nombre no
vacíos. El `requestId` se guarda como nonce: repetirlo devuelve **409**.

### Por qué el documento y el nombre viajan en claro (dentro de HTTPS)

La Edge Function necesita el nombre real para poder cifrarlo, y la clave de cifrado no
sale de Supabase. Por eso la intranet envía `document` y `name` reales por HTTPS; ahí
solo viven en memoria durante la petición y jamás se persisten ni se registran.

Hubo una modalidad opcional en la que la intranet calculaba los HMAC y enviaba solo
digest (`HashInIntranet`). Se retiró: con ella la función nunca vería el nombre y no
podría producir `nombre_encrypted`, así que la columna quedaría vacía sin que nadie se
diera cuenta. La intranet ya no conoce `BRIDGE_HMAC_SECRET`.

---

## 4. Seguridad en Supabase

- El esquema `bridge` **no está expuesto en la Data API** (PostgREST solo publica
  `public` y `graphql_public`): la tabla no es alcanzable por HTTP, ni con la anon key
  ni con la service_role key.
- RLS habilitado y **cero políticas**. No hay ninguna política `USING (true)`.
- Se revocan todos los privilegios sobre el esquema y las tablas a `public`, `anon`,
  `authenticated` y `service_role`.
- La única vía de escritura es `public.bridge_sync_pacientes_heridas`
  (`SECURITY DEFINER`, `search_path = ''`), con `EXECUTE` concedido solo a
  `service_role`, que solo usa la Edge Function.
- `CHECK` en las dos columnas: solo se aceptan digest `^[0-9a-f]{64}$`. Si algún día se
  intentara insertar un documento o nombre en claro, la base de datos lo rechaza.
- La Edge Function se despliega con `verify_jwt = false` porque la autenticación es la
  propia (bearer + firma + timestamp + nonce), no un JWT de Supabase.

### Columnas y tablas adicionales

- `bridge.pacientes_heridas` tiene **solo las dos columnas pedidas**. No se agregó un
  `id`: `documento_hmac` es la PRIMARY KEY, que en PostgreSQL ya implica NOT NULL +
  UNIQUE + índice.
- Sí se añadió una **segunda tabla**, `bridge.sync_request_nonces` (`request_id`,
  `recibido_en`), imprescindible para detectar reenvíos: la ventana de 5 minutos por sí
  sola no impide reenviar la misma petición firmada dentro de esa ventana. No contiene
  ningún dato personal y se purga sola cada hora.

---

## 5. Secretos

| Secreto | Para qué | Dónde vive |
|---|---|---|
| `BRIDGE_API_SECRET` | Autenticar y firmar la petición | Intranet (`SupabaseBridge:ApiSecret`) **y** secreto de la Edge Function |
| `BRIDGE_HMAC_SECRET` | Derivar `documento_hmac` y `nombre_hmac` | Solo secreto de la Edge Function |
| `BRIDGE_ENCRYPTION_KEY` | Cifrar `nombre_encrypted` (AES-256, 32 bytes) | Solo secreto de la Edge Function |

Son tres secretos **distintos**. Ninguno se guarda en la tabla, en el repositorio ni en
los logs. La intranet solo conoce `BRIDGE_API_SECRET`: no puede derivar los HMAC ni
descifrar nombres.

**Local (User Secrets):**

```bash
dotnet user-secrets set "SupabaseBridge:ProjectUrl" "https://<ref>.supabase.co"
```

```bash
dotnet user-secrets set "SupabaseBridge:ApiSecret" "<BRIDGE_API_SECRET>"
```

**Azure App Service** → Configuración → Variables de entorno (doble guion bajo):

```
SupabaseBridge__ProjectUrl = https://qlmglhygiyykyhyzjczr.supabase.co
SupabaseBridge__ApiSecret  = <BRIDGE_API_SECRET>
```

Con eso basta: `PushOnSave` ya viene en `true`, así que cada paciente guardado viaja al
puente enseguida. Añade `SupabaseBridge__Enabled = true` solo si además quieres la
reconciliación periódica.

Si más adelante se adopta Azure Key Vault, basta con montar estas dos claves como
secretos del vault: la aplicación las lee por configuración, no por código.

---

## 6. Rotación de secretos

`BRIDGE_API_SECRET` (sin downtime si se hace en este orden):

1. Genera el nuevo valor: `openssl rand -base64 48`.
2. Actualízalo en Supabase: `npx supabase secrets set BRIDGE_API_SECRET=<nuevo> --project-ref <ref>`.
3. Actualízalo en la intranet (User Secrets o variable de entorno) y reinicia la app.
4. Verifica: pon `SupabaseBridge__MaxPatientsPerRun = 1`, reinicia y revisa el log.

Entre los pasos 2 y 3 la sincronización responde 401; como es idempotente, basta con
volver a ejecutarla.

`BRIDGE_HMAC_SECRET`: **cambiarlo invalida todos los HMAC existentes**. Los pacientes
ya sincronizados quedarían huérfanos con hashes antiguos. Para rotarlo hay que vaciar
la tabla y volver a sincronizar todo:

```sql
truncate table bridge.pacientes_heridas;
```

Hazlo solo en una ventana controlada y coordinado con la fase 2.

`BRIDGE_ENCRYPTION_KEY`: **cambiarla deja ilegibles los nombres ya cifrados** (el
descifrado falla por el tag de autenticación; no devuelve basura). El mismo remedio:
vaciar y resincronizar, que vuelve a cifrar todo con la clave nueva. Si algún día hace
falta rotarla sin downtime, para eso está el prefijo `v1` del sobre: se añade `v2` y se
descifra con la clave que corresponda a cada versión.

---

## 7. Pruebas

**Edge Function** (41 pruebas, sin Docker ni Deno):

```bash
node --test supabase/functions/sync-pacientes-heridas/normalize.test.ts supabase/functions/sync-pacientes-heridas/crypto.test.ts supabase/functions/sync-pacientes-heridas/handler.test.ts
```

Cubren: paciente nuevo, paciente existente, mismo paciente dos veces, nombre
modificado, documento vacío, nombre vacío, payload inválido, firma inválida, secret
incorrecto, timestamp expirado, requestId repetido, error de base de datos, ausencia de
datos reales en la base y en los logs, y sobre el cifrado: nonce único por operación,
descifrado correcto conservando tildes, fallo con otra clave, fallo al mover el
ciphertext a otra fila, fallo al alterar el ciphertext y rechazo de claves que no midan
32 bytes.

**Intranet** — 35 comprobaciones que confirman que C# genera exactamente los mismos
HMAC que la Edge Function (carga por reflexión el `Nexa.dll` ya compilado):

```bash
pwsh tools/bridge-selftest.ps1
```

**Extremo a extremo contra la función desplegada** (crea y luego borras un paciente
ficticio):

```bash
BRIDGE_API_SECRET=... SUPABASE_PROJECT_URL=https://qlmglhygiyykyhyzjczr.supabase.co node supabase/functions/sync-pacientes-heridas/smoke-test.mjs
```

Escenarios que no se automatizan (Supabase no disponible, timeout, error 500): el
servicio los trata como lote fallido con reintentos controlados (5xx, 408, 429 y fallos
de red se reintentan con espera exponencial; 400/401/403 no se reintentan nunca) y los
registra en el log y en la auditoría. Los pacientes de otros censos no pueden aparecer
porque la consulta solo lee `censo_clinica_heridas`.

---

## 8. Ejecución: todo en backend

No hay pantalla ni endpoint. Hay dos caminos, y el principal es el inmediato.

### Vía principal: al guardar el registro

Cuando se guardan los datos básicos de un paciente en el censo de clínica de heridas
(`CensoController.ClinicaHeridas` POST), el registro se encola en `BridgeSyncQueue` y
`BridgeSyncPushHostedService` lo empuja al puente en un par de segundos.

El guardado **no espera a Supabase**: solo encola. Si Supabase no responde, el registro
del censo se guarda igual y el error queda en el log y en la auditoría
(`BRIDGE_SUPABASE_PUSH_FALLIDO`). Las demás secciones del formulario (manejo de la
herida, activo fijo, seguimiento…) no encolan nada porque no cambian documento ni nombre.

La cola es acotada (500) y descarta lo más antiguo si el consumidor se atasca. Perder un
encolado no pierde al paciente: sigue en el censo y lo recuperan el próximo guardado o la
reconciliación.

### Vía secundaria: reconciliación periódica

`BridgeSyncHostedService` recorre el censo completo cada `IntervalHours`. Está **apagada
por defecto**; sirve para recuperar envíos que fallaron con Supabase caído y para la
carga inicial de un censo que ya tenía pacientes.

| Clave | Por defecto | Para qué |
|---|---|---|
| `SupabaseBridge:PushOnSave` | `true` | Envío inmediato al guardar. Es la vía principal |
| `SupabaseBridge:Enabled` | `false` | Reconciliación periódica del censo completo |
| `SupabaseBridge:MaxPatientsPerRun` | `0` | `0` = todos. Permite una carga inicial escalonada: 1, luego 5, luego 0 |
| `SupabaseBridge:DryRun` | `false` | `true` cuenta y arma los lotes sin llamar a Supabase |
| `SupabaseBridge:IntervalHours` | `24` | Horas entre reconciliaciones |
| `SupabaseBridge:InitialDelaySeconds` | `60` | Espera tras arrancar la aplicación |

Ambas vías dejan traza en la auditoría de la intranet (`BRIDGE_SUPABASE_PUSH_EJECUTADO` /
`_PUSH_FALLIDO` para el envío inmediato; `BRIDGE_SUPABASE_SYNC_EJECUTADA` / `_SIMULADA` /
`_FALLIDA` para la reconciliación) y una línea técnica en el log.

Verificación en el SQL Editor de Supabase:

```sql
select count(*) from bridge.pacientes_heridas;
select * from bridge.pacientes_heridas limit 5;
select count(*) from bridge.pacientes_heridas where documento_hmac = '1234567890';
```

La última consulta siempre devuelve 0: el documento real no está almacenado. En la
segunda, `nombre_encrypted` debe empezar por `v1.` y no contener ninguna letra del
nombre: el `CHECK` de la columna rechaza cualquier cosa que no tenga forma de sobre.

---

## 9. Logging

Se registran: inicio de sincronización, pacientes encontrados, enviados, procesados,
estado HTTP, `requestId`, duración, lotes correctos y fallidos. **Nunca** documento,
nombre, `documento_hmac`, `nombre_hmac`, payload ni secretos. La auditoría de la
intranet guarda el mismo resumen técnico bajo las acciones
`BRIDGE_SUPABASE_SYNC_EJECUTADA`, `BRIDGE_SUPABASE_SYNC_SIMULADA` y
`BRIDGE_SUPABASE_SYNC_FALLIDA`.

---

## 10. Riesgos y decisiones pendientes

- **PII en tránsito hacia Supabase.** Documento y nombre reales viajan por HTTPS y se
  procesan en memoria en la Edge Function. Es inevitable ahora: el nombre tiene que
  llegar allí para poder cifrarse, porque la clave no sale de Supabase.
- **El nombre sí es recuperable, por diseño.** `nombre_encrypted` es reversible con
  `BRIDGE_ENCRYPTION_KEY`. Quien tenga a la vez la fila y esa clave lee el nombre. La
  protección real es que la clave solo existe como secreto de la Edge Function: no está
  en la tabla, ni en la intranet, ni en el repositorio.
- **Se sincronizan todos los pacientes del censo**, activos e inactivos, porque no se
  pidió ningún filtro de estado y añadirlo sería inventar reglas de negocio. Si el
  portal solo debe conocer a los activos, hay que definir ese criterio.
- **La tabla puente no tiene borrado.** Si un paciente sale del programa, su HMAC
  permanece. Definir si hace falta un mecanismo de baja.
- **`BRIDGE_HMAC_SECRET` no se puede rotar sin resincronizar** (ver §6).
- **Si Supabase está caído cuando se guarda un paciente, ese envío se pierde** hasta que
  alguien vuelva a guardar ese registro. Queda registrado como
  `BRIDGE_SUPABASE_PUSH_FALLIDO`. Para cubrirlo, activa la reconciliación periódica
  (`SupabaseBridge__Enabled = true`).
- **La cola vive en memoria del proceso.** Si la aplicación se reinicia con envíos
  pendientes, esos envíos se pierden; aplica el mismo remedio.
