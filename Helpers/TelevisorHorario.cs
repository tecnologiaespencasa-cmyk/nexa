namespace Nexa.Helpers;

/// <summary>
/// Horario de admisiones y analistas asistenciales, como lo definió la operación (2026-10-01):
///
/// - Lunes a viernes: turno mañana 6:00–14:00 y turno tarde 14:00–22:00 en las dos áreas, más un
///   turno único de 8:00 a 17:00 para una persona de admisiones. La entrega de turno es a las
///   2:00 p. m.; lo que se registra desde la 1:45 p. m. ya cuenta para la tarde, y a la 1:30 p. m.
///   el Televisor avisa por voz que el corte es en quince minutos.
/// - Sábados, domingos y festivos: un turno único de 7:00 a 19:00 para todos, sin entrega.
///
/// Los festivos se tratan como fin de semana (supuesto: la operación no hace entrega de turno ese
/// día). Se calculan aquí, sin tabla, con la Ley Emiliani.
/// </summary>
public static class TelevisorHorario
{
    public static readonly TimeSpan Aviso = new(13, 30, 0);
    public static readonly TimeSpan Corte = new(13, 45, 0);
    public static readonly TimeSpan Entrega = new(14, 0, 0);

    /// <summary>Franja en la que trabajan las analistas: la meta del 100 % se mide sobre lo que entra en ella.</summary>
    public static readonly TimeSpan InicioFranja = new(6, 0, 0);
    public static readonly TimeSpan FinFranja = new(22, 0, 0);

    /// <summary>
    /// Un correo que llega entre las 10:00 p. m. y las 6:00 a. m. no tiene a nadie de admisiones en
    /// turno: se muestra, pero no suma a la promesa de quien lo registró (decisión del 2026-10-01).
    /// </summary>
    public static bool EsHoraNocturna(TimeSpan hora) => hora >= FinFranja || hora < InicioFranja;

    public static bool EsHabil(DateTime dia) =>
        dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && FestivosColombia.Nombre(dia) is null;

    /// <summary>Rango de la cinta del día en la pantalla.</summary>
    public static (TimeSpan Inicio, TimeSpan Fin) RangoCinta(DateTime dia) =>
        EsHabil(dia) ? (new TimeSpan(6, 0, 0), new TimeSpan(22, 0, 0)) : (new TimeSpan(7, 0, 0), new TimeSpan(19, 0, 0));
}

public static class FestivosColombia
{
    /// <summary>Nombre del festivo si la fecha lo es, o null.</summary>
    public static string? Nombre(DateTime fecha)
    {
        var dia = fecha.Date;
        foreach (var (f, nombre) in DelAnio(dia.Year))
        {
            if (f == dia)
            {
                return nombre;
            }
        }

        return null;
    }

    public static IEnumerable<(DateTime Fecha, string Nombre)> DelAnio(int anio)
    {
        var pascua = DomingoDePascua(anio);

        yield return (new DateTime(anio, 1, 1), "Año Nuevo");
        yield return (AlLunes(new DateTime(anio, 1, 6)), "Reyes Magos");
        yield return (AlLunes(new DateTime(anio, 3, 19)), "San José");
        yield return (pascua.AddDays(-3), "Jueves Santo");
        yield return (pascua.AddDays(-2), "Viernes Santo");
        yield return (new DateTime(anio, 5, 1), "Día del Trabajo");
        yield return (AlLunes(pascua.AddDays(39)), "Ascensión del Señor");
        yield return (AlLunes(pascua.AddDays(60)), "Corpus Christi");
        yield return (AlLunes(pascua.AddDays(68)), "Sagrado Corazón");
        yield return (AlLunes(new DateTime(anio, 6, 29)), "San Pedro y San Pablo");
        yield return (new DateTime(anio, 7, 20), "Día de la Independencia");
        yield return (new DateTime(anio, 8, 7), "Batalla de Boyacá");
        yield return (AlLunes(new DateTime(anio, 8, 15)), "Asunción de la Virgen");
        yield return (AlLunes(new DateTime(anio, 10, 12)), "Día de la Raza");
        yield return (AlLunes(new DateTime(anio, 11, 1)), "Todos los Santos");
        yield return (AlLunes(new DateTime(anio, 11, 11)), "Independencia de Cartagena");
        yield return (new DateTime(anio, 12, 8), "Inmaculada Concepción");
        yield return (new DateTime(anio, 12, 25), "Navidad");
    }

    /// <summary>Ley Emiliani: el festivo que no cae en lunes se pasa al lunes siguiente.</summary>
    private static DateTime AlLunes(DateTime fecha)
    {
        var dias = ((int)DayOfWeek.Monday - (int)fecha.DayOfWeek + 7) % 7;
        return fecha.AddDays(dias);
    }

    /// <summary>Domingo de Pascua (algoritmo de Meeus/Jones/Butcher, calendario gregoriano).</summary>
    private static DateTime DomingoDePascua(int anio)
    {
        var a = anio % 19;
        var b = anio / 100;
        var c = anio % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var diaMes = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(anio, mes, diaMes);
    }
}
