using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

public class CensoTerapiaAmbulatoriaProrroga
{
    [Key]
    public long Id { get; set; }

    public long CensoTerapiaAmbulatoriaRecordId { get; set; }

    public CensoTerapiaAmbulatoriaRecord? CensoTerapiaAmbulatoriaRecord { get; set; }

    [Required]
    [StringLength(200)]
    public string TipoTerapia { get; set; } = string.Empty;

    public DateTime FechaSolicitudProrroga { get; set; }

    public DateTime FechaSolicitudAsegurador { get; set; }

    public DateTime FechaEntregaAutorizacion { get; set; }

    [Required]
    [StringLength(100)]
    public string CodigoAutorizacion { get; set; } = string.Empty;

    public int Frecuencia { get; set; }

    [Required]
    [StringLength(100)]
    public string Cantidad { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
