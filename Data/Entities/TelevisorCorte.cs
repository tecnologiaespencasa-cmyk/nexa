using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

public static class TelevisorCorteTipos
{
    /// <summary>Turno de la mañana, congelado a la 1:45 p. m. (solo días hábiles).</summary>
    public const string Manana = "MANANA";

    /// <summary>Cierre del día: todo lo del día con el estado que tenía a la medianoche.</summary>
    public const string Dia = "DIA";

    /// <summary>Saldo de novedades pendientes del portal a las 8:00 a. m.</summary>
    public const string Novedades8am = "NOVEDADES_8AM";
}

/// <summary>
/// Foto de los indicadores del Televisor en un corte. Lo que se ve de un día anterior, o de la
/// mañana después de la 1:45 p. m., sale de aquí y no de recalcular: los datos del censo siguen
/// cambiando (alguien marca la gestión completa al otro día) y el indicador tiene que quedar como
/// terminó el turno.
///
/// <see cref="DatosJson"/> es el tablero completo serializado, para que la pantalla lo vuelva a
/// pintar tal cual. Una sola fila por día y tipo: el índice único es lo que evita dos fotos
/// cuando la instancia de Azure y un equipo local (comparten la base) llegan al corte a la vez.
/// </summary>
public class TelevisorCorte
{
    [Key]
    public long Id { get; set; }

    /// <summary>Día, en hora Colombia.</summary>
    public DateTime Dia { get; set; }

    [Required]
    [StringLength(20)]
    public string Tipo { get; set; } = TelevisorCorteTipos.Dia;

    /// <summary>Momento al que corresponde la foto (el corte), en UTC.</summary>
    public DateTime EstadoAlUtc { get; set; }

    /// <summary>Cuándo se tomó de verdad. Si va muy por detrás del corte, la foto es recuperada.</summary>
    public DateTime GeneradoAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Se tomó tarde (la aplicación estaba caída en el corte, o es un día anterior a que existiera
    /// el Televisor): los estados de gestión se reconstruyeron con las horas guardadas.
    /// </summary>
    public bool Recuperado { get; set; }

    /// <summary>Versión del formato de <see cref="DatosJson"/>.</summary>
    public int Version { get; set; } = 1;

    [Required]
    public string DatosJson { get; set; } = "{}";

    /// <summary>Máquina que la tomó, para saber si salió de Azure o de un equipo local.</summary>
    [StringLength(200)]
    public string? Instancia { get; set; }
}

/// <summary>
/// Turno de una persona en un día, puesto a mano desde el Televisor cuando el que se detecta por
/// su conexión no es el real (una sesión que quedó abierta desde la víspera, un doble turno).
/// </summary>
public class TelevisorTurnoAjuste
{
    [Key]
    public long Id { get; set; }

    public DateTime Dia { get; set; }

    public int NursingAssistantId { get; set; }

    [Required]
    [StringLength(20)]
    public string Turno { get; set; } = string.Empty;

    [StringLength(80)]
    public string? AjustadoPor { get; set; }

    public DateTime AjustadoAtUtc { get; set; } = DateTime.UtcNow;
}
