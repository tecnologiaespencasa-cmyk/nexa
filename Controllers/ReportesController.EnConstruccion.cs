namespace Nexa.Controllers;

// =================================================================================================
// TEMPORAL — Aviso "Tablero en construcción" de Reportes.
//
// Mientras el nuevo tablero no esté aprobado, se sube a producción cubierto: se ve difuminado y no
// responde a clics ni al teclado. El tabulado del censo y sus exportables (al final de la página)
// siguen funcionando normalmente.
//
//   true  → tablero cubierto con el aviso (para producción).
//   false → tablero normal, sin aviso (para mostrarlo en local).
//
// Para retirar el aviso definitivamente, borrar:
//   1. Este archivo.
//   2. Views/Reportes/_RpEnConstruccion.cshtml.
//   3. En Views/Reportes/Index.cshtml, las líneas marcadas "TEMPORAL" (la variable enConstruccion,
//      el div "rp-obra" y su cierre, y el partial del aviso), dejando el contenido que envuelven.
//   4. En wwwroot/css/reportes.css, el bloque "TEMPORAL: tablero en construcción".
// =================================================================================================
public partial class ReportesController
{
    public static readonly bool TableroEnConstruccion = true;
}
