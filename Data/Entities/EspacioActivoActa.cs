using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

/// <summary>
/// Acta firmada de entrega o devolucion de un activo. Deja constancia de quien entrega,
/// quien recibe y en que condiciones, con la foto de los datos del equipo en ese momento
/// (para que el acta siga siendo valida aunque el activo se edite despues).
/// </summary>
public class EspacioActivoActa
{
    public long Id { get; set; }

    public long EspacioActivoId { get; set; }

    public EspacioActivo EspacioActivo { get; set; } = null!;

    /// <summary>Entrega | Devolucion</summary>
    [Required]
    [StringLength(20)]
    public string Tipo { get; set; } = string.Empty;

    // ── Quien entrega (area de TI) ───────────────────────────────────────────

    public Guid? EntregaPorUserId { get; set; }

    [Required]
    [StringLength(160)]
    public string EntregaPorNombre { get; set; } = string.Empty;

    [StringLength(120)]
    public string? EntregaPorCargo { get; set; }

    [Required]
    public string FirmaEntregaDataUrl { get; set; } = string.Empty;

    // ── Quien recibe (colaborador) ───────────────────────────────────────────

    public Guid? RecibePorUserId { get; set; }

    [Required]
    [StringLength(160)]
    public string RecibePorNombre { get; set; } = string.Empty;

    [StringLength(30)]
    public string? RecibePorDocumento { get; set; }

    [Required]
    public string FirmaRecibeDataUrl { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Observaciones { get; set; }

    // ── Foto del equipo al momento de firmar ─────────────────────────────────

    [Required]
    [StringLength(300)]
    public string EquipoDescripcion { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Serial { get; set; } = string.Empty;

    [StringLength(60)]
    public string? CodigoActivo { get; set; }

    [StringLength(2000)]
    public string? Especificaciones { get; set; }

    public DateTime FirmadaAtUtc { get; set; } = DateTime.UtcNow;
}
