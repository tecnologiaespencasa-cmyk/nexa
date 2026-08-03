namespace IntranetPrueba.Helpers;

public static class SpreadsheetFileSupport
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls", ".xlsx", ".xlsm", ".xlsb", ".xlt", ".xltx", ".xltm", ".ods"
    };

    public const string InputAccept = ".xls,.xlsx,.xlsm,.xlsb,.xlt,.xltx,.xltm,.ods";
    public const string SupportedFormatsDescription = "Excel (.xls, .xlsx, .xlsm, .xlsb y plantillas) u OpenDocument (.ods)";

    public static bool IsSupportedSpreadsheet(string? fileName)
        => SupportedExtensions.Contains(Path.GetExtension(fileName ?? string.Empty));

    public static string GetContentType(string? fileName) => Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant() switch
    {
        ".xls" or ".xlt" => "application/vnd.ms-excel",
        ".xlsx" or ".xltx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".xlsm" or ".xltm" => "application/vnd.ms-excel.sheet.macroEnabled.12",
        ".xlsb" => "application/vnd.ms-excel.sheet.binary.macroEnabled.12",
        ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
        _ => "application/octet-stream"
    };
}
