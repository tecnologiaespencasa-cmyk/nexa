using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

/// <summary>
/// Trazabilidad de cambios de un activo (creacion, asignacion, estado, novedades).
/// </summary>
public class EspacioActivoMovimiento
{
    public long Id { get; set; }

    public long EspacioActivoId { get; set; }

    public EspacioActivo EspacioActivo { get; set; } = null!;

    [Required]
    [StringLength(40)]
    public string Tipo { get; set; } = string.Empty;

    [Required]
    [StringLength(600)]
    public string Detalle { get; set; } = string.Empty;

    [StringLength(160)]
    public string? RegistradoPorNombre { get; set; }

    public DateTime RegistradoAtUtc { get; set; } = DateTime.UtcNow;
}
