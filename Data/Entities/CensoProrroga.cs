using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

public class CensoProrroga
{
    [Key]
    public long Id { get; set; }

    public long CensoRecordId { get; set; }

    public int Numero { get; set; }

    public string ProrrogaJson { get; set; } = string.Empty;

    public string? KardexEdicionJson { get; set; }

    public string? RequisicionFarmaciaJson { get; set; }

    public long? FarmaciaDispatchRecordId { get; set; }

    public DateTime? CerradaAtUtc { get; set; }

    public long? CerradaPorFarmaciaId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public CensoRecord CensoRecord { get; set; } = null!;
}
