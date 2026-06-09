using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

public class CensoAdjunto
{
    public long Id { get; set; }

    public long CensoRecordId { get; set; }

    public CensoRecord CensoRecord { get; set; } = null!;

    [Required]
    [StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public byte[] FileData { get; set; } = [];

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
