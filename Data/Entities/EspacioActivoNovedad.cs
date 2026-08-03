using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

/// <summary>
/// Novedad reportada por un colaborador sobre un activo asignado.
/// </summary>
public class EspacioActivoNovedad
{
    public long Id { get; set; }

    public long? EspacioActivoId { get; set; }

    public EspacioActivo? EspacioActivo { get; set; }

    /// <summary>Referencia libre del equipo cuando la novedad no esta ligada a un activo registrado.</summary>
    [StringLength(200)]
    public string? EquipoReferencia { get; set; }

    public Guid? ReportadoPorUserId { get; set; }

    [Required]
    [StringLength(160)]
    public string ReportadoPorNombre { get; set; } = string.Empty;

    [StringLength(150)]
    public string? ReportadoPorEmail { get; set; }

    [Required]
    [StringLength(60)]
    public string Tipo { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string Estado { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Prioridad { get; set; }

    [StringLength(60)]
    public string? Clasificacion { get; set; }

    [StringLength(2000)]
    public string? RespuestaAdmin { get; set; }

    [StringLength(160)]
    public string? AtendidoPorNombre { get; set; }

    public DateTime? ResueltoAtUtc { get; set; }

    public bool NotificacionEnviada { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
