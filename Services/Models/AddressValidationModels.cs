namespace Nexa.Services.Models;

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
    /// <summary>Apartamento, torre, interior… separado de la vía y la placa.</summary>
    public string? Detail { get; set; }
    /// <summary>"Urbana" o "Rural".</summary>
    public string? Area { get; set; }
    public bool RequiresSelection { get; set; }
    public IReadOnlyList<AddressValidationCandidate> Candidates { get; set; } = [];
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// La dirección normalizada con su detalle al final. Es la que se guarda cuando el formulario
    /// no tiene campo de detalle aparte, para no perder el «APTO 202» que el validador separó.
    /// </summary>
    public string? FormattedAddressWithDetail =>
        string.IsNullOrWhiteSpace(Detail) ? FormattedAddress : $"{FormattedAddress} {Detail}";
}

public class AddressValidationCandidate
{
    public string FormattedAddress { get; set; } = string.Empty;
    public string? Municipality { get; set; }
    public string? Neighborhood { get; set; }
    public string? District { get; set; }
    public string? Detail { get; set; }
    public string? Area { get; set; }
    public bool IsReliable { get; set; }
}
