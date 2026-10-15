namespace Nexa.Helpers;

/// <summary>
/// Tipo de contenido con el que se devuelve un adjunto del kardex, según su extensión. Los PDF y las
/// imágenes salen con su tipo propio para que el navegador los reconozca; los de hoja de cálculo
/// reutilizan <see cref="SpreadsheetFileSupport"/>.
/// </summary>
public static class AdjuntoArchivo
{
    public static string ContentType(string? fileName) => Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".csv" => "text/csv",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => SpreadsheetFileSupport.GetContentType(fileName)
    };
}
