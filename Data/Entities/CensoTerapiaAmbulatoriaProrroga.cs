using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

public class CensoTerapiaAmbulatoriaProrroga
{
    [Key]
    public long Id { get; set; }

    public long CensoTerapiaAmbulatoriaRecordId { get; set; }

    public CensoTerapiaAmbulatoriaRecord? CensoTerapiaAmbulatoriaRecord { get; set; }

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
