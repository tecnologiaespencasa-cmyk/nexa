using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Models.ViewModels;

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

    [Display(Name = "Accion")]
    [StringLength(100)]
    public string? Action { get; set; }
}

public class AuditIndexViewModel
{
    public AuditFilterViewModel Filter { get; set; } = new();
    public List<string> AvailableActions { get; set; } = [];
    public List<AuditLogItemViewModel> Logs { get; set; } = [];
}
