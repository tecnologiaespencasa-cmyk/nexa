namespace IntranetPrueba.Services.Models;

public class AuditLogSearchRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Username { get; set; }
    public string? Action { get; set; }
    public int Take { get; set; } = 300;
}

public class AuditLogListItemDto
{
    public long Id { get; set; }
    public DateTime PerformedAtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }
}

public class AuditLogSearchResultDto
{
    public IReadOnlyList<string> AvailableActions { get; set; } = [];
    public IReadOnlyList<AuditLogListItemDto> Logs { get; set; } = [];
}
