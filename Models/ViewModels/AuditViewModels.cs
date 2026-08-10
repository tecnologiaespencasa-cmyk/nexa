using System.ComponentModel.DataAnnotations;

namespace Nexa.Models.ViewModels;

public class AuditLogItemViewModel
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

public class AuditFilterViewModel
{
    [Display(Name = "Fecha inicial")]
    [DataType(DataType.Date)]
    public DateTime? FromDate { get; set; }

    [Display(Name = "Fecha final")]
    [DataType(DataType.Date)]
    public DateTime? ToDate { get; set; }

    [Display(Name = "Usuario")]
    [StringLength(80)]
    public string? Username { get; set; }

    [Display(Name = "Acción")]
    [StringLength(100)]
    public string? Action { get; set; }

    public string? Category { get; set; }

    public int Page { get; set; } = 1;
}

public class AuditStatsViewModel
{
    public int TodayTotal { get; set; }
    public int TodayUniqueUsers { get; set; }
    public int TodayFailedLogins { get; set; }
    public int TodayOperational { get; set; }
}

public class AuditIndexViewModel
{
    public AuditFilterViewModel Filter { get; set; } = new();
    public DateTime EarliestAllowedDate { get; set; }
    public DateTime LatestAllowedDate { get; set; }
    public List<string> AvailableActions { get; set; } = [];
    public List<AuditLogItemViewModel> Logs { get; set; } = [];
    public AuditStatsViewModel Stats { get; set; } = new();
    public IReadOnlyDictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 100;
    public int TotalPages { get; set; } = 1;
}
