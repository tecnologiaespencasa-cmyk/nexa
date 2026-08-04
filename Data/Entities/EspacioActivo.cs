using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

/// <summary>
/// Activo de TI administrado desde "Mi espacio corporativo".
/// </summary>
public class EspacioActivo
{
    public long Id { get; set; }

    [Required]
    [StringLength(60)]
    public string TipoActivo { get; set; } = string.Empty;

    [StringLength(150)]
    public string? NombreEquipo { get; set; }

    [Required]
    [StringLength(80)]
    public string Marca { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Serie { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Serial { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Especificaciones { get; set; }

    [StringLength(60)]
    public string? CodigoActivo { get; set; }

    public Guid? ResponsableUserId { get; set; }

    public AppUser? ResponsableUser { get; set; }

    /// <summary>Nombre del responsable al momento de la asignacion (historico).</summary>
    [StringLength(160)]
    public string? ResponsableNombre { get; set; }

    public DateTime? FechaAsignacionUtc { get; set; }

    [Required]
    [StringLength(40)]
    public string Estado { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Nota { get; set; }

    public bool Eliminado { get; set; }

    public DateTime? EliminadoAtUtc { get; set; }

    [StringLength(160)]
    public string? CreadoPorNombre { get; set; }

    [StringLength(160)]
    public string? ActualizadoPorNombre { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<EspacioActivoNovedad> Novedades { get; set; } = new List<EspacioActivoNovedad>();

    public ICollection<EspacioActivoMovimiento> Movimientos { get; set; } = new List<EspacioActivoMovimiento>();

    public ICollection<EspacioActivoActa> Actas { get; set; } = new List<EspacioActivoActa>();
}
