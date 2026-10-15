using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

/// <summary>
/// Archivo que acompaña el kardex de una agudización de crónicos (la orden médica, un soporte) y que
/// farmacia ve junto al pedido. Cuelga de la agudización, no del registro del paciente: cada
/// agudización es un pedido propio.
/// </summary>
public class CensoCronicoAgudizacionAdjunto
{
    public long Id { get; set; }

    public long CensoCronicoAgudizacionId { get; set; }

    public CensoCronicoAgudizacion Agudizacion { get; set; } = null!;

    [Required]
    [StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public byte[] FileData { get; set; } = [];

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
