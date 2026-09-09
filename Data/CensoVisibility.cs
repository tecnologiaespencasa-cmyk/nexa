using System.Linq.Expressions;
using Nexa.Data.Entities;

namespace Nexa.Data;

// Definición única del filtro de visibilidad de la tabla censo. Cualquier consulta que cuente o reporte
// pacientes (pantalla, exportables y tablero de reportes) debe aplicarlo: sin él, las copias internas de
// despacho a farmacia se cuentan como atenciones reales e inflan las cifras.
public static class CensoVisibility
{
    // Un registro con FarmaciaProrrogaDeId/FarmaciaProrrogaVersionId es normalmente una copia interna
    // de despacho a farmacia y se oculta porque el registro base sigue visible en su lugar. Pero si ese
    // registro base fue borrado (quedó huérfana la copia), ocultarla también dejaría al paciente sin
    // ningún registro visible en el censo. Por eso solo se oculta cuando el padre referenciado todavía existe.
    /// <summary>
    /// Una fecha de egreso anterior a esto no es un egreso: es el valor por defecto de
    /// DateTime (0001-01-01) que dejó alguna carga antigua. Se descubrió con el paciente
    /// 3380199, un crónico activo cuyo registro traía esa fecha: la reconciliación lo daba
    /// por egresado, el carril escondía el programa y el informe de activos lo dejaba fuera.
    /// Como la fecha nunca cambiaba, volvía a cerrarse en cada visita.
    /// </summary>
    public static readonly DateTime FechaEgresoMinima = new(1900, 1, 1);

    /// <summary>True solo si la fecha de egreso es una fecha real, no el centinela.</summary>
    public static bool HayEgreso(DateTime? fechaEgreso) =>
        fechaEgreso.HasValue && fechaEgreso.Value >= FechaEgresoMinima;

    public static Expression<Func<CensoRecord, bool>> EditableRecord(ApplicationDbContext context)
    {
        return x =>
            (x.FarmaciaProrrogaDeId == null || !context.Censos.Any(p => p.Id == x.FarmaciaProrrogaDeId))
            && (x.FarmaciaProrrogaVersionId == null || !context.CensoProrrogas.Any(p => p.Id == x.FarmaciaProrrogaVersionId));
    }
}
