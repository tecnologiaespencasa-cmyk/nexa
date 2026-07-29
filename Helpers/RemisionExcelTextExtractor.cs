using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace IntranetPrueba.Helpers;

public static class RemisionExcelTextExtractor
{
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const long MaxUncompressedWorksheetBytes = 20L * 1024 * 1024;

    public static string ExtractFormatoRemisionText(byte[] workbookBytes)
    {
        using var stream = new MemoryStream(workbookBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        var workbook = ReadXml(archive, "xl/workbook.xml");
        var workbookRelationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
        var ns = (XNamespace)SpreadsheetNamespace;
        var relNs = (XNamespace)RelationshipNamespace;
        var packageRelNs = (XNamespace)PackageRelationshipNamespace;

        var sheet = workbook
            .Descendants(ns + "sheet")
            .FirstOrDefault(item => string.Equals(
                NormalizeSheetName((string?)item.Attribute("name")),
                NormalizeSheetName("Formato Remisión"),
                StringComparison.Ordinal));

        if (sheet is null)
        {
            throw new InvalidDataException("No se encontró la hoja 'Formato Remisión' en el archivo.");
        }

        var relationshipId = (string?)sheet.Attribute(relNs + "id");
        var target = workbookRelationships
            .Descendants(packageRelNs + "Relationship")
            .FirstOrDefault(item => string.Equals((string?)item.Attribute("Id"), relationshipId, StringComparison.Ordinal))?
            .Attribute("Target")?.Value;

        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidDataException("No fue posible leer la hoja 'Formato Remisión'.");
        }

        var worksheetPath = ResolveWorkbookPart(target);
        var sharedStrings = ReadSharedStrings(archive, ns);
        var worksheet = ReadXml(archive, worksheetPath);
        var result = new StringBuilder("Contenido de la hoja Formato Remisión:");

        foreach (var row in worksheet.Descendants(ns + "sheetData").Elements(ns + "row"))
        {
            var values = row.Elements(ns + "c")
                .Select(cell => GetCellValue(cell, ns, sharedStrings))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (values.Length > 0)
            {
                result.AppendLine();
                result.Append(string.Join(" | ", values));
            }
        }

        if (result.Length == "Contenido de la hoja Formato Remisión:".Length)
        {
            throw new InvalidDataException("La hoja 'Formato Remisión' no contiene información para analizar.");
        }

        return result.ToString();
    }

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path)
            ?? throw new InvalidDataException("El archivo Excel no tiene la estructura esperada.");

        if (entry.Length > MaxUncompressedWorksheetBytes)
        {
            throw new InvalidDataException("El contenido de la hoja Excel es demasiado grande para procesarlo.");
        }

        using var entryStream = entry.Open();
        using var reader = XmlReader.Create(entryStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        return XDocument.Load(reader);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive, XNamespace ns)
    {
        if (archive.GetEntry("xl/sharedStrings.xml") is null)
        {
            return Array.Empty<string>();
        }

        var sharedStrings = ReadXml(archive, "xl/sharedStrings.xml");
        return sharedStrings.Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string GetCellValue(XElement cell, XNamespace ns, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(cell.Element(ns + "v")?.Value, out var sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex].Trim();
        }

        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(cell.Descendants(ns + "t").Select(text => text.Value)).Trim();
        }

        return cell.Element(ns + "v")?.Value.Trim() ?? string.Empty;
    }

    private static string ResolveWorkbookPart(string target)
    {
        var normalizedTarget = target.Replace('\\', '/').TrimStart('/');
        if (normalizedTarget.Contains("..", StringComparison.Ordinal)
            || !normalizedTarget.StartsWith("worksheets/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("La ubicación de la hoja Excel no es válida.");
        }

        return $"xl/{normalizedTarget}";
    }

    private static string NormalizeSheetName(string? value)
        => string.Concat((value ?? string.Empty).Normalize(NormalizationForm.FormD)
            .Where(character => char.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark))
            .Trim()
            .ToUpperInvariant();
}
