# Puente Nexa → Supabase (clínica de heridas)

Fase 1: sincronizar los pacientes del censo de **clínica de heridas** hacia una base
puente en Supabase, guardando allí **únicamente dos HMAC** (documento y nombre).

Proyecto Supabase: `toportal` — `https://qlmglhygiyykyhyzjczr.supabase.co`
Estado: esquema aplicado, secretos cargados y Edge Function desplegada (v1, `verify_jwt = false`).

```
INTRANET NEXA (Azure PostgreSQL)
        │  HTTPS + Bearer + firma HMAC + timestamp + requestId
        ▼
SUPABASE EDGE FUNCTION  sync-pacientes-heridas   (única puerta de escritura)
        │  RPC public.bridge_sync_pacientes_heridas (SECURITY DEFINER, service_role)
        ▼
SUPABASE POSTGRESQL  bridge.pacientes_heridas
        documento_hmac TEXT PRIMARY KEY
        nombre_hmac    TEXT NOT NULL
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

### Modalidad alternativa

Por defecto la intranet envía documento y nombre reales por HTTPS y la Edge Function
los normaliza y convierte en HMAC (los valores en claro solo viven en memoria durante
la petición). Si prefieres que el dato real **nunca salga de la intranet**, activa
`SupabaseBridge:HashInIntranet = true` y define `SupabaseBridge:HmacSecret`: entonces
se envían `documentHmac`/`nameHmac` ya calculados y la función solo valida el formato.
En ambos casos Supabase almacena exactamente lo mismo.

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
| `BRIDGE_HMAC_SECRET` | Derivar `documento_hmac` y `nombre_hmac` | Secreto de la Edge Function (y en la intranet solo si `HashInIntranet = true`) |

Son secretos **distintos**. Ninguno se guarda en la tabla, en el repositorio ni en los
logs, y `appsettings.json` los versiona vacíos.

**Local (User Secrets):**

```bash
dotnet user-secrets set "SupabaseBridge:ProjectUrl" "https://<ref>.supabase.co"
```

```bash
dotnet user-secrets set "SupabaseBridge:ApiSecret" "<BRIDGE_API_SECRET>"
```

**Azure App Service** → Configuración → Variables de entorno (doble guion bajo):

```
SupabaseBridge__ProjectUrl = https://<ref>.supabase.co
SupabaseBridge__ApiSecret  = <BRIDGE_API_SECRET>
```

Si más adelante se adopta Azure Key Vault, basta con montar estas dos claves como
secretos del vault: la aplicación las lee por configuración, no por código.

---

## 6. Rotación de secretos

`BRIDGE_API_SECRET` (sin downtime si se hace en este orden):

1. Genera el nuevo valor: `openssl rand -base64 48`.
2. Actualízalo en Supabase: `npx supabase secrets set BRIDGE_API_SECRET=<nuevo> --project-ref <ref>`.
3. Actualízalo en la intranet (User Secrets o variable de entorno) y reinicia la app.
4. Verifica con el **Paso 1** de la pantalla *Puente*.

Entre los pasos 2 y 3 la sincronización responde 401; como es idempotente, basta con
volver a ejecutarla.

`BRIDGE_HMAC_SECRET`: **cambiarlo invalida todos los HMAC existentes**. Los pacientes
ya sincronizados quedarían huérfanos con hashes antiguos. Para rotarlo hay que vaciar
la tabla y volver a sincronizar todo:

```sql
truncate table bridge.pacientes_heridas;
```

Hazlo solo en una ventana controlada y coordinado con la fase 2.

---

## 7. Pruebas

**Edge Function** (29 pruebas, sin Docker ni Deno):

```bash
node --test supabase/functions/sync-pacientes-heridas/normalize.test.ts supabase/functions/sync-pacientes-heridas/handler.test.ts
```

Cubren: paciente nuevo, paciente existente, mismo paciente dos veces, nombre
modificado, documento vacío, nombre vacío, payload inválido, firma inválida, secret
incorrecto, timestamp expirado, requestId repetido, error de base de datos, ausencia de
datos reales en la base y ausencia de datos reales en los logs.

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

Lo que se prueba desde la pantalla *Puente* (no automatizable sin iniciar sesión):
Supabase no disponible, timeout y error 500 se reflejan como lote fallido con
reintentos controlados; los pacientes de otros censos no aparecen porque la consulta
solo lee `censo_clinica_heridas`.

---

## 8. Primera sincronización

Entra como administrador a **Puente** en el menú superior y sigue los tres pasos:

1. **Enviar 1 paciente** → comprueba en Supabase que la fila tiene solo dos valores hex.
2. **Enviar 5 pacientes** → confirma que no se duplican.
3. **Enviar todos** → sincroniza el censo completo en lotes de 100.

Cada paso tiene además *Simular sin enviar*, que cuenta y arma los lotes sin llamar a
Supabase.

Verificación en el SQL Editor de Supabase:

```sql
select count(*) from bridge.pacientes_heridas;
select * from bridge.pacientes_heridas limit 5;
select count(*) from bridge.pacientes_heridas where documento_hmac = '1234567890';
```

La última consulta siempre devuelve 0: el documento real no está almacenado.

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

- **PII en tránsito hacia Supabase.** En la modalidad por defecto, documento y nombre
  reales viajan por HTTPS y se procesan en memoria en la Edge Function (nunca se
  persisten ni se registran). Si el criterio de privacidad exige que el dato real no
  salga de la intranet, activa `HashInIntranet = true`.
- **Se sincronizan todos los pacientes del censo**, activos e inactivos, porque no se
  pidió ningún filtro de estado y añadirlo sería inventar reglas de negocio. Si el
  portal solo debe conocer a los activos, hay que definir ese criterio.
- **La tabla puente no tiene borrado.** Si un paciente sale del programa, su HMAC
  permanece. Definir si hace falta un mecanismo de baja.
- **`BRIDGE_HMAC_SECRET` no se puede rotar sin resincronizar** (ver §6).
- **La sincronización es manual.** No se programó ninguna tarea automática; si se
  necesita, el servicio ya es idempotente y se puede invocar desde un
  `IHostedService` como los que ya existen en el proyecto.
