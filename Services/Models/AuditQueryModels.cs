namespace Nexa.Services.Models;

public class AuditLogSearchRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Username { get; set; }
    public string? Action { get; set; }
    public string? Category { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
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
    public IReadOnlyDictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class AuditTodayStatsDto
{
    public int TotalEvents { get; set; }
    public int UniqueUsers { get; set; }
    public int FailedLogins { get; set; }
    public int OperationalEvents { get; set; }
}
