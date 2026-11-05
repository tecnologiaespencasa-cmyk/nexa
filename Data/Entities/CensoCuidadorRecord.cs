using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexa.Data.Entities;

/// <summary>Estados del programa Cuidadores.</summary>
public static class CuidadorEstados
{
    public const string Activo = "Activo";
    public const string Suspendido = "Suspendido";
    public const string Cancelado = "Cancelado";
    public const string Pendiente = "Pendiente";

    public static readonly string[] Todos = [Activo, Suspendido, Cancelado, Pendiente];
}

/// <summary>
/// Una atención del programa Cuidadores. Solo guarda lo propio del programa: los datos básicos del
/// paciente viven en el maestro y el exportable los lee de allí. Identificación y nombre se copian
/// porque el censo, la conciliación de episodios y el tabulado buscan por documento en cada tabla.
///
/// «¿Está en programa permanentes?» y su fecha de ingreso no se guardan: se calculan contra Programa
/// crónicos cada vez que se leen, para que no puedan quedar desactualizados.
/// </summary>
public class CensoCuidadorRecord
{
    [Key]
    public long Id { get; set; }

    public long? CensoPacienteId { get; set; }

    [Required]
    [StringLength(3)]
    public string TipoIdentificacion { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string NumeroIdentificacion { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string NombrePaciente { get; set; } = string.Empty;

    // ----- Recepción del ingreso -----
    public DateTime FechaReporteEps { get; set; }

    public DateTime? FechaInicioAtencion { get; set; }

    /// <summary>Días entre el reporte de la EPS y el inicio de la atención.</summary>
    [NotMapped]
    public int? OportunidadInicioDias => FechaInicioAtencion.HasValue
        ? (FechaInicioAtencion.Value.Date - FechaReporteEps.Date).Days
        : null;

    // ----- Responsable -----
    [StringLength(200)]
    public string? NombreResponsable { get; set; }

    [StringLength(80)]
    public string? ParentescoResponsable { get; set; }

    [StringLength(10)]
    public string? TelefonoResponsable { get; set; }

    /// <summary>
    /// Con <see cref="DireccionMismaDelPaciente"/> es copia de la dirección del maestro y se
    /// actualiza cuando esta cambia.
    /// </summary>
    [StringLength(300)]
    public string? DireccionResponsable { get; set; }

    public bool DireccionMismaDelPaciente { get; set; }

    // ----- Plan de manejo -----
    public int? TiempoCuidadorHorasDia { get; set; }

    /// <summary>Días de la semana separados por ", ", en orden de lunes a domingo.</summary>
    [StringLength(80)]
    public string? DiasAtencion { get; set; }

    [StringLength(20)]
    public string? Horario { get; set; }

    public int? EscalaBarthel { get; set; }

    public DateTime? FechaCertificacionDependencia { get; set; }

    public int? TiempoOrdenamientoMeses { get; set; }

    public DateTime? FechaVencimientoCertificado { get; set; }

    [StringLength(2000)]
    public string? Observacion { get; set; }

    [StringLength(2000)]
    public string? VerificacionDerechos { get; set; }

    public DateTime? FechaVerificacionDerechos { get; set; }

    [StringLength(200)]
    public string? AuxiliarAsignado { get; set; }

    // ----- Estado -----
    [Required]
    [StringLength(20)]
    public string Estado { get; set; } = CuidadorEstados.Activo;

    public DateTime? FechaEgreso { get; set; }

    [StringLength(500)]
    public string? MotivoEgreso { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
