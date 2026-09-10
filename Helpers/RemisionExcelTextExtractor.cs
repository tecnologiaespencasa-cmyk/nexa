using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ExcelDataReader;

namespace Nexa.Helpers;

public static class RemisionExcelTextExtractor
{
    private const string OdsOfficeNamespace = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private const string OdsTableNamespace = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private const string OdsTextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private const long MaxUncompressedWorksheetBytes = 20L * 1024 * 1024;
    private const string PreferredSheetName = "Formato Remisión";
    private const int MinimumEvidenceScore = 6;
    private const int MinimumFilledAnswerRows = 5;
    private static readonly string[] RemisionIndicators =
    {
        "FORMATOREMISION",
        "DATOSDELPACIENTE",
        "PACIENTE",
        "NOMBREYAPELLIDOS",
        "NOMBREDEL PACIENTE",
        "TIPODEIDENTIFICACION",
        "NUMERODEIDENTIFICACION",
        "DOCUMENTODEIDENTIDAD",
        "DATOSIPSREMITENTE",
        "IPSREMITENTE",
        "NOMBREIPS",
        "DIAGNOSTICO",
        "DIRECCION",
        "CUIDADOR",
        "MEDICAMENTOS"
    };

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
        var candidates = new List<SheetExtractionCandidate>();
        do
        {
            candidates.Add(BuildTextFromExcelReader(reader));
        }
        while (reader.NextResult());

        return SelectSheet(candidates).Text;
    }

    private static SheetExtractionCandidate BuildTextFromExcelReader(IExcelDataReader reader)
    {
        var sheetName = reader.Name;
        var result = new StringBuilder(BuildExtractedTextHeader(sheetName));
        var nonEmptyRows = 0;
        var answerRows = 0;
        var evidenceScore = ScoreSheetName(sheetName);
        while (reader.Read())
        {
            var values = Enumerable.Range(0, reader.FieldCount)
                .Select(index => FormatExcelValue(reader.GetValue(index)))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (values.Length > 0)
            {
                var rowText = string.Join(" | ", values);
                result.AppendLine();
                result.Append(rowText);
                nonEmptyRows++;
                if (values.Length > 1)
                {
                    answerRows++;
                }
                evidenceScore += ScoreRow(rowText);
            }
        }

        return new SheetExtractionCandidate(sheetName, result.ToString(), nonEmptyRows, answerRows, evidenceScore);
    }

    private static string ExtractOdsFormatoRemisionText(byte[] workbookBytes)
    {
        using var stream = new MemoryStream(workbookBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var document = ReadXml(archive, "content.xml");
        var officeNs = (XNamespace)OdsOfficeNamespace;
        var tableNs = (XNamespace)OdsTableNamespace;
        var textNs = (XNamespace)OdsTextNamespace;
        var candidates = document.Descendants(tableNs + "table")
            .Select(sheet => BuildTextFromOdsSheet(sheet, officeNs, tableNs, textNs))
            .ToList();

        return SelectSheet(candidates).Text;
    }

    private static SheetExtractionCandidate BuildTextFromOdsSheet(
        XElement sheet,
        XNamespace officeNs,
        XNamespace tableNs,
        XNamespace textNs)
    {
        var sheetName = (string?)sheet.Attribute(tableNs + "name") ?? "Hoja sin nombre";
        var result = new StringBuilder(BuildExtractedTextHeader(sheetName));
        var nonEmptyRows = 0;
        var answerRows = 0;
        var evidenceScore = ScoreSheetName(sheetName);
        foreach (var row in sheet.Elements(tableNs + "table-row"))
        {
            var values = row.Elements(tableNs + "table-cell")
                .Select(cell => GetOdsCellValue(cell, officeNs, textNs))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (values.Length > 0)
            {
                var rowText = string.Join(" | ", values);
                result.AppendLine();
                result.Append(rowText);
                nonEmptyRows++;
                if (values.Length > 1)
                {
                    answerRows++;
                }
                evidenceScore += ScoreRow(rowText);
            }
        }

        return new SheetExtractionCandidate(sheetName, result.ToString(), nonEmptyRows, answerRows, evidenceScore);
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

    private static SheetExtractionCandidate SelectSheet(IEnumerable<SheetExtractionCandidate> candidates)
    {
        var populatedSheets = candidates
            .Where(candidate => candidate.NonEmptyRows > 0)
            .ToList();
        if (populatedSheets.Count == 0)
        {
            throw new InvalidDataException("El archivo no contiene información para analizar.");
        }

        if (populatedSheets.Count == 1)
        {
            return populatedSheets[0];
        }

        // Algunas IPS diligencian la remisión sobre otra pestaña del libro (el instructivo, una
        // copia de trabajo…) y dejan "Formato Remisión" en blanco. Por eso no basta con quedarse
        // con la hoja del nombre esperado: entre las que tienen estructura de remisión se elige la
        // que de verdad trae respuestas (filas con etiqueta + dato), no solo el rótulo del campo.
        var isPreferredName = (SheetExtractionCandidate candidate) => string.Equals(
            NormalizeSheetName(candidate.SheetName),
            NormalizeSheetName(PreferredSheetName),
            StringComparison.Ordinal);

        var remisionLikeSheets = populatedSheets
            .Where(candidate => candidate.EvidenceScore >= MinimumEvidenceScore || isPreferredName(candidate))
            .ToList();

        if (remisionLikeSheets.Count > 0)
        {
            // Si la hoja con el nombre esperado ya viene diligenciada, es la buena: no se cambia
            // por otra aunque otra pestaña tenga más celdas (p. ej. un instructivo con un ejemplo).
            var preferredSheet = remisionLikeSheets.FirstOrDefault(isPreferredName);
            if (preferredSheet is not null && preferredSheet.AnswerRows >= MinimumFilledAnswerRows)
            {
                return preferredSheet;
            }

            return remisionLikeSheets
                .OrderByDescending(candidate => candidate.AnswerRows)
                .ThenByDescending(candidate => isPreferredName(candidate))
                .ThenByDescending(candidate => candidate.EvidenceScore)
                .ThenByDescending(candidate => candidate.NonEmptyRows)
                .First();
        }

        var bestCandidate = populatedSheets
            .OrderByDescending(candidate => candidate.EvidenceScore)
            .ThenByDescending(candidate => candidate.NonEmptyRows)
            .First();

        if (bestCandidate.EvidenceScore >= MinimumEvidenceScore)
        {
            return bestCandidate;
        }

        throw new InvalidDataException("No se encontró una hoja con información de remisión o del paciente.");
    }

    private static string BuildExtractedTextHeader(string sheetName)
        => $"Contenido de la hoja {sheetName.Trim()}:";

    private static int ScoreSheetName(string sheetName)
        => NormalizeSheetName(sheetName).Contains(NormalizeSheetName(PreferredSheetName), StringComparison.Ordinal) ? 20 : 0;

    private static int ScoreRow(string rowText)
    {
        var normalizedText = NormalizeSheetName(rowText);
        return RemisionIndicators.Count(indicator => normalizedText.Contains(NormalizeSheetName(indicator), StringComparison.Ordinal));
    }

    private static string NormalizeSheetName(string? value)
        => string.Concat((value ?? string.Empty).Normalize(NormalizationForm.FormD)
            .Where(character => char.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character)))
            .ToUpperInvariant();

    private sealed record SheetExtractionCandidate(string SheetName, string Text, int NonEmptyRows, int AnswerRows, int EvidenceScore);
}
