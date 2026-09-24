using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

public static class FarmaciaReporteDiarioEstados
{
    public const string Enviando = "Enviando";
    public const string Enviado = "Enviado";
    public const string Fallido = "Fallido";
}

/// <summary>
/// Un envío del reporte diario de farmacia con los despachos que siguen por desempacar al corte
/// de las 11:00 p. m. Hay una sola fila por día: el índice único sobre <see cref="Dia"/> es lo que
/// impide que el correo salga dos veces cuando corren a la vez la instancia de Azure y una local
/// (comparten la base) o cuando la aplicación se reinicia después de enviarlo.
/// </summary>
public class FarmaciaReporteDiarioEnvio
{
    [Key]
    public long Id { get; set; }

    /// <summary>Día, en hora Colombia, del corte que cubre el reporte.</summary>
    public DateTime Dia { get; set; }

    /// <summary>Inicio de la ventana: el corte del día anterior.</summary>
    public DateTime DesdeUtc { get; set; }

    /// <summary>Fin de la ventana: el corte de este día.</summary>
    public DateTime HastaUtc { get; set; }

    [Required]
    [StringLength(20)]
    public string Estado { get; set; } = FarmaciaReporteDiarioEstados.Enviando;

    public int Intentos { get; set; }

    /// <summary>Despachos que llevó el correo (cero también se envía: confirma que el día cerró limpio).</summary>
    public int Despachos { get; set; }

    [Required]
    [StringLength(500)]
    public string Destinatarios { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Error { get; set; }

    /// <summary>Máquina que hizo el último intento, para saber si salió de Azure o de un equipo local.</summary>
    [StringLength(200)]
    public string? Instancia { get; set; }

    public DateTime CreadoAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime IntentoAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? EnviadoAtUtc { get; set; }
}
