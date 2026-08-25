# Catálogo CIE-10 propio de clínica de heridas

Desde el 25 de agosto de 2026 el campo **CIE10** de la sección 1 del censo de clínica de heridas
ya no consulta el catálogo general del censo (miles de códigos con búsqueda por código): es un
**desplegable cerrado de 23 diagnósticos**, los únicos que el programa admite.

## Dónde vive el listado

`Controllers/CensoClinicaHeridasController.cs` → `ClinicaHeridasCie10Values`, un diccionario
`código → descripción` con `StringComparer.OrdinalIgnoreCase`. **Para agregar o retirar un
diagnóstico basta con editar esa lista**: la vista, la validación y el autocompletado del
diagnóstico salen todos de ahí. No hay tabla ni parametrización en base de datos.

| CIE-10 | Diagnóstico |
| --- | --- |
| E105 | Diabetes mellitus, no especificada con complicaciones circulatorias periféricas |
| I771 | Estrechez arterial |
| I830 | Venas varicosas de los miembros inferiores con úlcera |
| K604 | Fístula rectal |
| K632 | Fístula del intestino |
| L020 | Absceso en cara |
| L023 | Absceso del glúteo |
| L024 | Absceso región axilar |
| L039 | Dermatitis, no especificada |
| L89X | Úlcera de decúbito |
| L97X | Úlcera del miembro inferior no clasificada en otra parte |
| L984 | Úlcera crónica de la piel no clasificada en otra parte |
| M868 | Otras osteomielitis |
| N322 | Fístula de la vejiga |
| S311 | Herida de la pared abdominal |
| T141 | Herida de región no especificada del cuerpo |
| T813 | Desgarro de herida operatoria, no clasificado en otra parte |
| T958 | Secuelas de otras quemaduras, corrosiones y congelamientos especificados |
| Z430 | Atención de traqueostomía |
| Z431 | Atención de gastrostomía |
| Z432 | Atención de ileostomía |
| Z433 | Atención de colostomía |
| Z452 | Contacto para ajuste y mantenimiento de dispositivo de acceso vascular |

## Cómo se comporta

- **El diagnóstico ya no viaja al servidor para resolverse.** Cada `<option>` se pinta como
  `CÓDIGO - DESCRIPCIÓN`, y el `change` del desplegable parte ese texto por `" - "` para llenar el
  campo Diagnóstico. Desapareció el `fetch` a `buscarDiagnosticoCie10Url` que usaba el campo de
  texto anterior. Al guardar, el servidor **reescribe** la descripción desde el catálogo, así que
  un diagnóstico manipulado en el navegador no llega a la base de datos.
- **Códigos heredados.** Los registros anteriores al recorte pueden tener un código que ya no está
  en la lista (los pacientes de prueba tenían `N390`). En ese caso `BuildClinicaHeridasCie10Options`
  agrega la opción al final rotulada `(fuera del listado actual)` y `EsCie10HeredadoDelRegistro`
  permite guardar la ficha sin tocar el diagnóstico. La excepción es **estrecha a propósito**: solo
  vale si el código enviado es exactamente el que ya estaba guardado en ese registro; cualquier otro
  código fuera del catálogo se rechaza con *"Selecciona un diagnóstico del listado de clínica de
  heridas."*. Una vez que el usuario elige un código válido, la opción heredada desaparece.
- **Ojo con el patrón del ViewModel.** `CodigoCie10` tenía
  `[RegularExpression(@"^[A-Za-z][0-9]{3}$")]`, heredado del censo general. **`L89X` y `L97X`
  terminan en X**, así que esa anotación bloqueaba dos diagnósticos válidos tanto en cliente
  (jquery-validation impedía el submit) como en servidor. Se retiró: quien valida ahora es el
  catálogo, no la forma del código. Los otros programas (agudos, NPT, terapia ambulatoria) conservan
  su patrón porque siguen usando el catálogo general.

## Verificado

Contra el paciente de prueba 303030, con el servidor en `localhost:5086`:

| Caso | Resultado |
| --- | --- |
| El desplegable pinta los 23 códigos y ninguno del catálogo general | ✅ |
| Elegir una opción llena el diagnóstico sin ir al servidor | ✅ |
| Guardar `L89X` (código con X) y `Z452` persiste tras recargar | ✅ |
| Enviar un código fuera del catálogo (`A001`, `J440`, `N390`) | ✅ rechazado, no altera lo guardado |
| Un registro que ya tenía `N390` se sigue pudiendo guardar | ✅ con la opción `(fuera del listado actual)` |
