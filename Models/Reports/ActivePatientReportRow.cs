namespace IntranetPrueba.Models.Reports;

public class ActivePatientReportRow
{
    public DateTime CurrentDate { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string IdentificationType { get; init; } = string.Empty;

    public string IdentificationNumber { get; init; } = string.Empty;

    public string Zone { get; init; } = string.Empty;

    public DateTime AdmissionDate { get; init; }

    public int LengthOfStayDays { get; init; }

    public string Diagnosis { get; init; } = string.Empty;

    public string Program { get; init; } = string.Empty;

    public string Insurer { get; init; } = string.Empty;
}
