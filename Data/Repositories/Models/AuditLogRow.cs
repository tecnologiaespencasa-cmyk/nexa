namespace IntranetPrueba.Data.Repositories.Models;

public class AuditLogRow
{
    public long Id { get; set; }
    public DateTime PerformedAtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }
}
