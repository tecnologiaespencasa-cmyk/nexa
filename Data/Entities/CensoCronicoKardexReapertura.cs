using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

/// <summary>
/// Solicitud de reapertura del kardex de una agudización del censo de Programa Crónicos.
/// Tabla totalmente independiente de las reaperturas del censo de agudos
/// (censo_kardex_reaperturas) para evitar cruces de información entre censos.
/// </summary>
public class CensoCronicoKardexReapertura
{
    [Key]
    public long Id { get; set; }

    public long CensoCronicoAgudizacionId { get; set; }

    public CensoCronicoAgudizacion CensoCronicoAgudizacion { get; set; } = null!;

    [Required]
    [StringLength(80)]
    public string Motivo { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Estado { get; set; } = ReaperturaKardexEstado.Pendiente;

    public Guid SolicitadoPorUserId { get; set; }

    [Required]
    [StringLength(200)]
    public string SolicitadoPorNombre { get; set; } = string.Empty;

    public DateTime SolicitadoAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? ResueltoPorUserId { get; set; }

    [StringLength(200)]
    public string? ResueltoPorNombre { get; set; }

    public DateTime? ResueltoAtUtc { get; set; }

    [StringLength(500)]
    public string? ObservacionResolucion { get; set; }
}
