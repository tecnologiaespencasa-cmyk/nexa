using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

public class Medicamento
{
    [Key]
    public long Id { get; set; }

    [Required]
    [StringLength(300)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string NormalizedNombre { get; set; } = string.Empty;

    [StringLength(120)]
    public string? PresentacionRequisicion { get; set; }

    [StringLength(50)]
    public string? ConcentracionMiligramos { get; set; }

    [StringLength(50)]
    public string? Jeringa { get; set; }

    [StringLength(120)]
    public string? SolucionParaDilucion { get; set; }

    [StringLength(50)]
    public string? DilucionRecomendada { get; set; }

    [StringLength(120)]
    public string? VehiculoReconstitucion { get; set; }

    [StringLength(120)]
    public string? TiempoEstabilidad { get; set; }

    [StringLength(80)]
    public string? TiempoInfusionMinutos { get; set; }

    [StringLength(10)]
    public string? BombaInfusion { get; set; }

    [StringLength(50)]
    public string? MarcacionRiesgo { get; set; }

    [StringLength(10)]
    public string? Flebozantes { get; set; }

    [StringLength(10)]
    public string? EquipoFotosensible { get; set; }

    [StringLength(10)]
    public string? CadenaFrio { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
