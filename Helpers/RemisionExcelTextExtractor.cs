using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ExcelDataReader;

namespace IntranetPrueba.Helpers;

public static class RemisionExcelTextExtractor
{
    private const string OdsOfficeNamespace = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private const string OdsTableNamespace = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private const string OdsTextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private const long MaxUncompressedWorksheetBytes = 20L * 1024 * 1024;
    private const string ExtractedTextHeader = "Contenido de la hoja Formato Remisión:";

    static RemisionExcelTextExtractor()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string ExtractFormatoRemisionText(byte[] workbookBytes, string fileName)
    {
        if (string.Equals(Path.GetExtension(fileName), ".ods", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractOdsFormatoRemisionText(workbookBytes);
        }

        using var stream = new MemoryStream(workbookBytes, writable: false);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        do
        {
            if (string.Equals(NormalizeSheetName(reader.Name), NormalizeSheetName("Formato Remisión"), StringComparison.Ordinal))
            {
                return BuildTextFromExcelReader(reader);
            }
        }
        while (reader.NextResult());

        throw new InvalidDataException("No se encontró la hoja 'Formato Remisión' en el archivo.");
    }

    private static string BuildTextFromExcelReader(IExcelDataReader reader)
    {
        var result = new StringBuilder(ExtractedTextHeader);
        while (reader.Read())
        {
            var values = Enumerable.Range(0, reader.FieldCount)
                .Select(index => FormatExcelValue(reader.GetValue(index)))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (values.Length > 0)
            {
                result.AppendLine();
                result.Append(string.Join(" | ", values));
            }
        }

        return ValidateExtractedText(result);
    }

    private static string ExtractOdsFormatoRemisionText(byte[] workbookBytes)
    {
        using var stream = new MemoryStream(workbookBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var document = ReadXml(archive, "content.xml");
        var officeNs = (XNamespace)OdsOfficeNamespace;
        var tableNs = (XNamespace)OdsTableNamespace;
        var textNs = (XNamespace)OdsTextNamespace;
        var sheet = document.Descendants(tableNs + "table")
            .FirstOrDefault(item => string.Equals(
                NormalizeSheetName((string?)item.Attribute(tableNs + "name")),
                NormalizeSheetName("Formato Remisión"),
                StringComparison.Ordinal));

        if (sheet is null)
        {
            throw new InvalidDataException("No se encontró la hoja 'Formato Remisión' en el archivo.");
        }

        var result = new StringBuilder(ExtractedTextHeader);
        foreach (var row in sheet.Elements(tableNs + "table-row"))
        {
            var values = row.Elements(tableNs + "table-cell")
                .Select(cell => GetOdsCellValue(cell, officeNs, textNs))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (values.Length > 0)
            {
                result.AppendLine();
                result.Append(string.Join(" | ", values));
            }
        }

        return ValidateExtractedText(result);
    }

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path)
            ?? throw new InvalidDataException("El archivo de hoja de cálculo no tiene la estructura esperada.");

        if (entry.Length > MaxUncompressedWorksheetBytes)
        {
            throw new InvalidDataException("El contenido de la hoja de cálculo es demasiado grande para procesarlo.");
        }

        using var entryStream = entry.Open();
        using var reader = XmlReader.Create(entryStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        return XDocument.Load(reader);
    }

    private static string GetOdsCellValue(XElement cell, XNamespace officeNs, XNamespace textNs)
    {
        var text = string.Join(" ", cell.Descendants(textNs + "p")
            .Select(paragraph => paragraph.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return cell.Attribute(officeNs + "value")?.Value
            ?? cell.Attribute(officeNs + "date-value")?.Value
            ?? cell.Attribute(officeNs + "boolean-value")?.Value
            ?? string.Empty;
    }

    private static string FormatExcelValue(object? value) => value switch
    {
        null => string.Empty,
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
        _ => value.ToString()?.Trim() ?? string.Empty
    };

    private static string ValidateExtractedText(StringBuilder result)
    {
        if (result.Length == ExtractedTextHeader.Length)
        {
            throw new InvalidDataException("La hoja 'Formato Remisión' no contiene información para analizar.");
        }

        return result.ToString();
    }

    private static string NormalizeSheetName(string? value)
        => string.Concat((value ?? string.Empty).Normalize(NormalizationForm.FormD)
            .Where(character => char.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
            .Trim()
            .ToUpperInvariant();
}
