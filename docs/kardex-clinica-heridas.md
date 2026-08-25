# Requisiciones de insumos de clínica de heridas

La sección 3 "Manejo de la herida" pregunta por cuatro atenciones —**PICC, VAC, NPT y manejo de la
herida**— y por cada una que quede en **Sí** genera una requisición de insumos independiente. Cada
requisición se edita, se le adjuntan archivos y se envía a farmacia. El OK de farmacia cierra la
edición desde el censo, y el pedido continúa por el mismo ciclo de despacho que agudos y crónicos.

```
CENSO (sección 3)                          FARMACIA
 PICC / VAC / NPT / Manejo = Sí
        │
        ├─ una tarjeta por atención
        │     └─ modal editable ──── Enviar ────► bandeja, etiqueta "Clínica de heridas · <tipo>"
        │                                              │
        │                                              └─ OK Kardex ──► kardex cerrado en el censo
        │                                                   y el pedido sigue: parcial → Facturado →
        │                                                   Empacado → firma → Despachado
        └─ Duración + Frecuencia ──► número de aplicaciones (columnas y cantidades)
```

---

## 1. Qué lleva cada requisición

| Atención | Insumos |
|---|---|
| **Manejo de herida** | Los apósitos/medicamentos elegidos (hasta 4) + cloruro de sodio 0.9% 100ml, aguja hipodérmica 18x1, gasa estéril paquete 10x10 x5, guante vinilo talla M, guante estéril talla 7.0, gasa adhesiva (electofix) 10x10 |
| **VAC** | Lo mismo que manejo de herida + hoja de bisturí n°11 |
| **NPT** | Lista fija de 18 insumos. **No** arrastra apósitos |
| **PICC** | Lista fija de 12 insumos. **No** arrastra apósitos |

Las listas viven en `Helpers/ClinicaHeridasKardexBuilder.cs`. Agregar o quitar un insumo de una
atención es editar el arreglo correspondiente.

Como NPT y PICC no usan apósitos, **los campos "Apósito/Medicamento" se ocultan cuando manejo de la
herida y VAC están los dos en No**, y lo que hubiera quedado guardado se descarta al guardar la
sección.

---

## 2. De dónde salen las cantidades

El número de **aplicaciones** sale de la duración del tratamiento dividida por la frecuencia de
visita, con división entera y mínimo 1:

| Frecuencia | Intervalo | 30 días |
|---|---|---|
| Cada 24 horas | 1 | 30 aplicaciones |
| Cada 48 horas | 2 | 15 aplicaciones |
| Cada 72 horas | 3 | 10 aplicaciones |
| Una vez a la semana | 7 | **4 aplicaciones** |

Cada aplicación es una columna de la tabla, con cantidad 1 por defecto, y el total por insumo es la
suma de la fila. Con 30 días una vez a la semana salen 4 columnas de a 1 y un total de 4 por insumo,
que es el ejemplo con el que se especificó la función.

**Tope de 60 columnas** (`ClinicaHeridasKardexBuilder.MaximoAplicaciones`): un tratamiento diario muy
largo generaría una tabla inmanejable. La tabla tiene scroll horizontal propio, así que 30 columnas
se navegan sin romper el modal ni la página.

---

## 3. El documento

Título fijo: **REQUISICION DE INSUMOS Y/O DISPOSITIVOS MEDICOS - CLINICA DE HERIDAS**, con la
estructura tabulada del kardex de agudos: encabezado con logo y metadatos del formato, datos del
paciente, datos del tratamiento, detalle de la requisición y observaciones.

"**Elaborado por**" se toma del perfil que abre el kardex (claim `full_name` de la sesión).

**Los títulos de las columnas de aplicación son editables.** Salen como "Aplicación 1…N" pero el
usuario los reemplaza normalmente por la fecha real de cada visita, y se guardan con el documento
(campo `Encabezados` del JSON, sin migración). Farmacia los ve tal como quedaron. Las requisiciones
guardadas antes de este campo se muestran con los títulos por defecto.

**Paleta:** las barras de sección usan el rojo corporativo **#D93111** sobre blanco, y el resto de la
hoja (subtítulo, cabeceras de tabla, etiquetas, totales y bordes) usa grises claros neutros.

Todo es editable: se pueden cambiar cantidades, corregir descripciones, **agregar** insumos que no
estaban en la plantilla y **quitar** los que sobren. Al guardar, el documento completo se serializa a
JSON en `censo_clinica_heridas_kardex.KardexJson`; al reabrirlo manda esa versión sobre la generada.
Si el JSON quedara ilegible, se vuelve al generado en vez de dejar la pantalla en blanco.

---

## 3.b Planes de requisiciones

Las requisiciones se agrupan en **planes**. Un plan cubre a **todas** las atenciones del paciente a
la vez: es la unidad que se abre, se cierra y se navega.

- **Solo hay un plan abierto** por paciente. "Abrir plan nuevo" cierra el vigente y crea el
  siguiente, numerado 1, 2, 3…
- Cada plan guarda **quién lo abrió y cuándo**, y al cerrarse, **quién lo cerró y cuándo**. Ambos
  salen del perfil de la sesión.
- Al abrir un plan nuevo, los **apósitos/medicamentos se limpian** en la sección 3 para capturar los
  del plan nuevo. Los del plan que se cierra quedan congelados en su propia copia.
- Un plan cerrado queda **completo de consulta**: sus requisiciones no se editan, ni siquiera las que
  farmacia nunca aprobó, y sus apósitos se muestran de solo lectura con la etiqueta "Plan N · cerrado".

### Navegación

En el encabezado del bloque "Requisiciones de insumos" hay una fila de fichas, una por plan, del más
reciente al más antiguo, con su estado (Vigente / Cerrado). Al elegir una:

- Se repintan las tarjetas de requisición de ese plan. En un plan cerrado, la etiqueta pasa a
  "Plan N" y el botón dice "Ver requisición".
- El bloque de apósitos **cambia de forma**: en el plan vigente son campos editables; en uno cerrado
  se sustituye por una lista de solo lectura encabezada por "Plan N · cerrado". Los campos del
  formulario nunca cambian de valor, así que guardar la sección siempre escribe el plan vigente.
- Debajo se lee quién abrió el plan y cuándo, y si está cerrado, quién y cuándo lo cerró.

**Diálogos propios, no los del navegador.** Confirmar la apertura de un plan y avisar de un error usan
un modal de Bootstrap con la identidad del censo, no confirm()/alert(). El de confirmación explica la
consecuencia antes de ejecutarla —qué se conserva, qué se limpia— con tono de advertencia; el de
error va en tono rojo y con un solo botón. La promesa se resuelve cuando el modal **termina** de
ocultarse: resolverla al pulsar el botón hacía que un diálogo encadenado se abriera a mitad de la
animación de cierre y Bootstrap lo descartara.

El modal distingue los dos motivos de bloqueo, que son distintos: **"Plan N cerrado"** (pertenece a
un plan anterior) y **"Kardex cerrado"** (farmacia lo aprobó).

### Cómo se comunican la sección 3 y las requisiciones

Mientras el plan está abierto, cada guardado de la sección 3 **sincroniza** en el plan los apósitos,
la duración y la frecuencia (`SincronizarPlanVigenteAsync`). Al cerrarse, esa copia queda fija y es
la que usan tanto el censo como farmacia para regenerar el documento de ese plan: un plan viejo
nunca se repinta con los datos que el censo tenga hoy.

En la bandeja de farmacia la etiqueta incluye el plan —**"Clínica de heridas · Manejo de herida ·
Plan 2"**— porque un mismo paciente puede tener varias requisiciones del mismo tipo en planes
distintos, y conviven como pedidos independientes.

Migración: `20260825154447_AddClinicaHeridasPlanRequisiciones`, aplicada. Incluye el backfill que
convierte lo que ya existía en el **Plan 1** de cada paciente.

---

## 4. Datos

| Tabla | Para qué |
|---|---|
| `censo_clinica_heridas` | Se agregaron `Npt` y `ManejoHerida` (Si/No, obligatorios en la sección 3) |
| `censo_clinica_heridas_plan` | Un plan por paciente y número, con su creador, fechas de apertura/cierre y la copia de apósitos y tratamiento |
| `censo_clinica_heridas_kardex` | Una fila por (plan, tipo de atención), con índice único. Guarda el JSON editado, el elaborado por, el estado frente a farmacia y la fecha de cierre |
| `censo_clinica_heridas_kardex_adjuntos` | Archivos que viajan con la requisición (PDF, Excel, CSV o imagen, máximo 10 MB) |

Migración: `20260824193023_AddClinicaHeridasKardex`, aplicada.

Si una atención pasa de Sí a No, su tarjeta desaparece del censo pero **la fila del kardex se
conserva**: no se pierde el histórico de lo que ya se envió a farmacia.

---

## 5. Farmacia

La bandeja de farmacia ya mezclaba dos fuentes (agudos y agudizaciones de crónicos); las
requisiciones de clínica de heridas son la tercera. Llegan con la etiqueta
**"Clínica de heridas · &lt;tipo de atención&gt;"** para que se distingan de un vistazo.

- Los botones de documento abren `Farmacia/DocumentoClinicaHeridas`, que muestra la requisición en
  solo lectura con sus adjuntos descargables.
- **OK Kardex** aprueba la requisición: `KardexCerradoAtUtc` queda con fecha y el censo ya no la
  puede editar (bloqueado en pantalla **y** rechazado en el servidor). El pedido pasa a Recepcionado
  y **sigue avanzando por la bandeja**.
- Desde ahí recorre **el mismo ciclo que agudos y crónicos**: entrega parcial (con avance entrega a
  entrega), Facturado, Empacado, firma de entrega y recibo, Despachado, y el vencimiento a
  Por desempacar a las 72 horas en Empacado. Los endpoints son los mismos nombres con el sufijo
  `ClinicaHeridas`; el front los elige con `data-pedido-heridas`.

Ojo con el cierre: "kardex cerrado" significa que **el censo** ya no lo edita, no que el pedido
terminó. Son dos cosas distintas que conviven.

### Notificaciones

Mismo esquema que agudos, con el **auxiliar de enfermería asignado** al paciente como destinatario
(se resuelve por nombre contra el catálogo de auxiliares OPS):

| Momento | Destinatario | Contenido |
|---|---|---|
| Se envía la requisición a farmacia | Auxiliar asignado | Copia de la requisición en HTML + los adjuntos del kardex |
| Farmacia firma el despacho | Auxiliar asignado | "Bolsa lista para reclamar" |
| Cada 24 h mientras está en Empacado | Auxiliar asignado | "Bolsa pendiente de reclamar", con dirección y teléfonos |
| Quedan ≤24 h de las 72 h | Gerencia | Aviso de despacho por vencer |

Los dos recordatorios los emite `EmpacadoNotificationHostedService`, que ya recorría agudos y ahora
también las requisiciones de heridas, con dos columnas de control en el kardex
(`FarmaciaNotifAuxiliarUltimaUtc` y `FarmaciaNotif24hRestanteUtc`) que evitan repetir el correo.

El adjunto es la requisición completa —datos del paciente, tratamiento y la tabla de insumos con los
títulos de columna que el usuario haya escrito— con la paleta corporativa. Se arma con
`ClinicaHeridasKardexBuilder.Resolver`, así que refleja el documento **tal como se envió**: la versión
editada si existe, y si no, la generada con los apósitos del plan.

Si el correo falla, la operación **no se cae**: el envío a farmacia y la firma se completan igual y el
motivo vuelve en el campo `avisos` de la respuesta y queda en el log.

Migración: `20260825165748_AddClinicaHeridasNotificaciones`, aplicada.

Ambos lados quedan auditados: `CENSO_CLINICA_HERIDAS_KARDEX_GUARDADO`,
`CENSO_CLINICA_HERIDAS_KARDEX_ENVIADO_FARMACIA`, `FARMACIA_OK_KARDEX_CLINICA_HERIDAS`,
`FARMACIA_CLINICA_HERIDAS_FACTURADO`, `FARMACIA_CLINICA_HERIDAS_EMPACADO` y
`FARMACIA_CLINICA_HERIDAS_DESPACHADO`.

Migración del ciclo de despacho: `20260824214204_AddClinicaHeridasKardexDespacho`, aplicada.

---

## 6. Pruebas realizadas (2026-08-24)

Sobre el paciente de prueba **CC 303030**:

| Caso | Resultado |
|---|---|
| NPT y manejo de la herida son obligatorios al guardar la sección 3 | ✅ |
| Con valor sin guardar, el select muestra "Selecciona..." y no "Si" | ✅ |
| Con las 4 atenciones en Sí se generan 4 tarjetas (9, 10, 18 y 12 insumos) | ✅ |
| Manejo de herida y VAC arrastran los apósitos; VAC agrega hoja de bisturí | ✅ |
| NPT (18) y PICC (12) no arrastran apósitos | ✅ |
| 30 días × una vez a la semana = 4 aplicaciones, 4 columnas de a 1, total 4 | ✅ |
| Resto de frecuencias: 24h→30, 48h→15, 72h→10; 5 días semanal→1 | ✅ |
| Editar cantidad recalcula el total en vivo (3+1+1+1 = 6) | ✅ |
| Agregar y quitar insumos, con renumeración de items | ✅ |
| Guardar y reabrir conserva ediciones, altas, bajas y observaciones | ✅ |
| Subir adjunto y verlo listado | ✅ |
| Enviar a farmacia: llega a la bandeja con su etiqueta | ✅ |
| Farmacia ve el documento editado, las observaciones y el adjunto | ✅ |
| OK desde el documento y desde la bandeja: ambos cierran el kardex | ✅ |
| Cerrado: 64 inputs en solo lectura, sin botones de guardar/enviar/adjuntar/quitar | ✅ |
| Cerrado: el servidor rechaza el guardado (400) aunque se salte la interfaz | ✅ |
| Manejo y VAC a No: se ocultan y limpian los apósitos, y desaparecen sus tarjetas | ✅ |
| Paciente con una sola atención en Sí: una sola tarjeta | ✅ |
| Sin duración ni frecuencia: 1 aplicación | ✅ |
| 30 columnas: la tabla scrollea dentro del modal sin romper la página | ✅ |
| Auditoría registrada en los dos lados | ✅ |
| Build sin errores ni advertencias; log del servidor limpio | ✅ |

### Colores y encabezados editables (2026-08-24)

| Caso | Resultado |
|---|---|
| Barras de sección en #D93111 (rgb 217,49,17) con texto blanco | ✅ |
| Subtítulo, cabeceras, etiquetas y bordes en grises claros | ✅ |
| Los 4 títulos de columna son inputs editables | ✅ |
| Escribir fechas y guardar: persisten al reabrir | ✅ |
| Farmacia ve las fechas en lugar de "Aplicación N" | ✅ |
| Requisiciones guardadas antes del cambio: títulos por defecto | ✅ |
| Kardex cerrado: los títulos quedan de solo lectura | ✅ |
| Los cuatro tipos con la misma paleta (9, 18 y 12 insumos) | ✅ |

### Ciclo completo en farmacia (2026-08-24)

| Caso | Resultado |
|---|---|
| Tras el OK, el pedido queda en Recepcionado con entrega parcial y Facturado disponibles | ✅ |
| Facturado → Empacado → firma → Despachado, cada uno con su mensaje | ✅ |
| Firma desde el modal real de la bandeja (dibujando en los dos canvas) | ✅ |
| Firma guardada en base: nombre, fecha/hora y las dos imágenes | ✅ |
| Entrega parcial: configurar 3 entregas y avanzar 1→2→3 | ✅ |
| Avanzar más allá de la última entrega → 400 "Ya se alcanzo la ultima entrega" | ✅ |
| Entrega parcial con 1 entrega → 400 "debe ser al menos 2" | ✅ |
| Empacar sin facturar → 404; firmar en Recepcionado → 400 | ✅ |
| Los pedidos aparecen en la columna que corresponde a su estado | ✅ |
| Auditoría de facturado, empacado y despachado | ✅ |
