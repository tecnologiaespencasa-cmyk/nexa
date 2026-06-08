using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

public class AuditLog
{
    public long Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Entity { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Details { get; set; }

    public Guid? PerformedByUserId { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;

    public AppUser? PerformedByUser { get; set; }
}
