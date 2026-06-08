namespace IntranetPrueba.Services.Models;

public enum AddressValidationOutcome
{
    Valid,
    Doubtful,
    Invalid,
    Unavailable
}

public class AddressValidationResult
{
    public AddressValidationOutcome Outcome { get; set; }
    public string? FormattedAddress { get; set; }
    public string? SuggestedAddress { get; set; }
    public string? Municipality { get; set; }
    public string? Neighborhood { get; set; }
    public string? District { get; set; }
    public bool RequiresSelection { get; set; }
    public IReadOnlyList<AddressValidationCandidate> Candidates { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}

public class AddressValidationCandidate
{
    public string FormattedAddress { get; set; } = string.Empty;
    public string? Municipality { get; set; }
    public string? Neighborhood { get; set; }
    public string? District { get; set; }
    public bool IsReliable { get; set; }
}
