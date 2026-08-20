# Sección 2 "Manejo de la herida": seguimientos leídos del Portal Administrativo

La sección 2 del censo de clínica de heridas es **de solo lectura**. Muestra las curaciones que
los auxiliares registran en campo desde la aplicación del Portal Administrativo: sus medidas, el
exudado, las cuatro fotos obligatorias, quién la hizo y cuándo. La intranet no crea, edita ni
borra nada de eso.

```
INTRANET NEXA (censo de clínica de heridas)
   │  documento del paciente
   ▼
SHAREPOINT  Documentos/ClinicaDeHeridas/{NOMBRE} - {DOCUMENTO}      ← carpeta del paciente
   │  driveItemId de la carpeta
   ▼
NEON  "ClinicaHeridasPaciente".carpetaDriveItemId → pacienteRef
   │
   ├── "ClinicaHeridas"       seguimientos (numero, medidas, exudado…) + "User" (auxiliar)
   └── "ClinicaHeridasFoto"   4 fotos por seguimiento → driveItemId en SharePoint
```

---

## 1. Por qué el enlace pasa por SharePoint

En Neon **no existe el documento del paciente**. La migración `20260813151000_clinica_heridas_paciente_ref`
lo reemplazó por `pacienteRef`, un UUID aleatorio (versión 4) que el portal asigna al paciente y
que la intranet no puede calcular ni deducir: no es un hash del documento y no hay ninguna tabla
que relacione ambos.

El único dato no seudonimizado que comparten los dos sistemas es la **carpeta de SharePoint**. El
portal la crea por paciente con el nombre `{NOMBRE} - {DOCUMENTO}` y guarda su `driveItemId` en
`ClinicaHeridasPaciente.carpetaDriveItemId`. La intranet, entonces:

1. Lista `Documentos/ClinicaDeHeridas` y busca la carpeta cuyo **último bloque del nombre** sea el
   documento del paciente (`Services/SharePointDocumentService.FindClinicaHeridasPatientFolderAsync`).
   Se comparan solo letras y dígitos, así que también reconoce las carpetas antiguas que creaba la
   propia intranet con la forma `{NOMBRE} - CC {DOCUMENTO}`.
2. Con ese `driveItemId` consulta Neon
   (`Data/Repositories/NeonClinicaHeridasRepository.GetSeguimientosPorCarpetaAsync`).

**Si el portal expone algún día una función de consulta que devuelva el `pacienteRef` a partir del
documento, ese paso 1 sobra** y basta con cambiar la resolución. El resto del código no cambia.

Consecuencia operativa: **si alguien renombra o mueve la carpeta del paciente en SharePoint, la
intranet deja de encontrar sus seguimientos** (no los borra: deja de verlos). El nombre de la
carpeta es parte del contrato entre los dos sistemas.

---

## 2. Qué se lee de Neon

| Tabla | Para qué |
|---|---|
| `ClinicaHeridasPaciente` | `carpetaDriveItemId` → `pacienteRef` |
| `ClinicaHeridas` | Un registro por seguimiento: `numero`, `origen`, `ubicacion`, `diametroVerticalCm`, `diametroHorizontalCm`, `profundidadCm`, `fondo`, `lecho`, `tejido`, `exudadoCantidad`, `exudadoCaracteristicas`, `createdAt` |
| `User` | El auxiliar que registró el seguimiento (`usuarioId`): nombre, cédula, correo, profesión |
| `ClinicaHeridasFoto` | Las fotos: `tipo` (enum `TipoFotoHerida`), `driveItemId`, `nombre`, `mimeType` |

Los seguimientos se ordenan por `numero` descendente: primero el más reciente.

`createdAt` es un `timestamp without time zone` que el portal **guarda en UTC**; la intranet lo
convierte a hora de Colombia antes de mostrarlo (`ToColombiaTime`).

La conexión se toma de `DATABASE_URL` con `NeonConnectionString.FromConfiguration`, la misma que ya
usaba `NeonOpsAssistantUserRepository` para el listado de auxiliares OPS. **Todas las consultas son
de lectura.**

### Las cuatro fotos

El enum `TipoFotoHerida` tiene exactamente cuatro valores y hay un índice único
`(seguimientoId, tipo)`, así que un seguimiento no puede tener dos fotos del mismo tipo:

| Valor en Neon | Etiqueta en la intranet |
|---|---|
| `PLANO_GENERAL` | Plano general |
| `MEDIDA_VERTICAL` | Medida vertical |
| `MEDIDA_HORIZONTAL` | Medida horizontal |
| `LATERAL` | Lateral |

La vista siempre dibuja las cuatro posiciones. Si falta alguna se muestra el marco vacío con
"Sin foto", para que se note que ese seguimiento quedó incompleto.

---

## 3. Cómo llegan las fotos al navegador

Las imágenes viven en SharePoint y el navegador del usuario no tiene acceso, así que la intranet
las descarga con sus credenciales de aplicación y las reenvía:

`GET /Censo/FotoSeguimientoClinicaHeridas?driveItemId=<id>&miniatura=true|false`

- **Antes de bajar nada**, se comprueba en Neon que ese `driveItemId` esté registrado como foto de
  seguimiento (`GetFotoPorDriveItemIdAsync`). Cualquier otro identificador devuelve **404 aunque el
  archivo exista** en la biblioteca: el proxy no sirve de puerta trasera al resto de SharePoint.
  Está verificado: pedir la carpeta del propio paciente por esta ruta responde 404.
- `miniatura=true` usa `/thumbnails/0/large/content` (unos 800 px). Es lo que carga la galería. Si
  SharePoint todavía no generó la miniatura, reintenta con el original.
- Sin `miniatura` baja el archivo original. Es lo que abre el visor al hacer clic.
- La respuesta lleva `Cache-Control: private, max-age=3600`. Cada foto tiene su propio
  `driveItemId` y el portal nunca la reemplaza, así que el contenido es inmutable.

El token de aplicación de Graph se cachea en memoria hasta un minuto antes de caducar
(`SharePointDocumentService`). Sin eso, una pantalla con 12 fotos pedía 12 tokens.

---

## 4. Configuración

No hay claves nuevas. Se reutilizan las que ya existían:

| Clave | Variable de entorno | Valor |
|---|---|---|
| `SharePoint:SiteId` | `SHAREPOINT_SITE_ID` | Sitio **Consentimientos** |
| `SharePoint:LibraryId` | `SHAREPOINT_LIBRARY_ID` | Biblioteca **Documentos** de ese sitio |
| `SharePoint:Library` | `SHAREPOINT_LIBRARY` | `Documentos` |
| `SharePoint:ClinicaHeridasFolder` | `SHAREPOINT_CLINICA_HERIDAS_FOLDER` | `ClinicaDeHeridas` (valor por defecto) |
| — | `GRAPH_TENANT_ID` / `GRAPH_CLIENT_ID` / `GRAPH_CLIENT_SECRET` | Credenciales de la app de Graph |
| `DATABASE_URL` | `DATABASE_URL` | Cadena de la base Neon del portal |

Es la misma biblioteca donde la intranet ya subía las fotos antes, así que en Azure no hay nada que
cambiar.

---

## 5. Lo que se eliminó

La sección 2 tenía un formulario propio y un cargue de fotos hacia SharePoint. Ambos desaparecieron:

- Acciones `GuardarClinicaHeridasManejoHerida` y `SubirClinicaHeridasAdjuntos`.
- Métodos `UploadClinicaHeridasDocumentsAsync` y `ListClinicaHeridasDocumentsAsync` del servicio de
  SharePoint.
- Campos del formulario "Descripción de la herida", "Ubicación de la herida" y "Frecuencia de
  visitas a la semana", con sus catálogos y validaciones.

**Las columnas `DescripcionHerida`, `UbicacionHerida` y `FrecuenciaVisitasSemana` de
`censo_clinica_heridas` siguen existiendo** con los datos que ya se habían capturado (3 de los 5
pacientes tenían algo). No se escriben más. No se generó una migración que las elimine porque eso
borraría esa información; si se decide descartarla, hay que hacerlo de forma explícita.

---

## 6. Pruebas realizadas (2026-08-14)

Contra el paciente de prueba **CC 303030 — EMMANUEL PRUEBA DOS**, con 3 seguimientos y 12 fotos
reales en Neon y SharePoint:

| Caso | Resultado |
|---|---|
| Carga de los 3 seguimientos, del más reciente al más antiguo | ✅ |
| Todos los campos: origen, ubicación, 3 medidas, fondo, lecho, tejido, exudado (cantidad y características) | ✅ |
| Auxiliar y profesión por seguimiento (`Emmanuel Estrada Calderon`) | ✅ |
| Conversión de hora UTC → Colombia (16:12 UTC se muestra 11:12) | ✅ |
| Las 12 fotos se sirven por el proxy (HTTP 200, `image/jpeg`) | ✅ |
| Visor: abre el original, no la miniatura, con su título | ✅ |
| Variación de área entre seguimientos (8 → 1 cm² = −87,5 %; 1 → 4 cm² = +300 %) | ✅ |
| Pista de evolución a escala (4 cm → 72 px, 1 cm → 18 px, 2 cm → 36 px) | ✅ |
| Paciente con carpeta pero sin seguimientos (CC 44444, carpeta antigua `- CC 44444`) → estado vacío, sin error | ✅ |
| Paciente sin carpeta (CC 101010) → estado vacío, sin error | ✅ |
| Paciente sin guardar todavía → aviso de guardar primero los datos básicos | ✅ |
| Proxy con la carpeta del paciente como `driveItemId` → 404 | ✅ |
| Proxy con un identificador inventado o vacío → 404 | ✅ |
| Guardado de la sección 4 tras el cambio → sigue funcionando y no altera otros datos | ✅ |
| Móvil 375 px: 2 fotos por fila, sin desbordamiento horizontal | ✅ |
| Consola del navegador y log del servidor sin errores | ✅ |
