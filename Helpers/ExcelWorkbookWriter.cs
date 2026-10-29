using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml;
using Nexa.Models.Reports;

namespace Nexa.Helpers;

public static class ExcelWorkbookWriter
{
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly string[] Headers =
    [
        "FECHA ACTUAL",
        "NOMBRE COMPLETO",
        "TIPO DE ID",
        "NÚMERO DE ID",
        "ZONA",
        "FECHA DE INGRESO",
        "DÍAS DE ESTANCIA",
        "DIAGNÓSTICO",
        "PROGRAMA",
        "ASEGURADOR"
    ];

    private static readonly double[] ColumnWidths = [12.33, 39.44, 11.89, 20.44, 21.66, 16, 14.44, 33.44, 19.66, 28];

    public static byte[] BuildActivePatientsWorkbook(IReadOnlyList<ActivePatientReportRow> rows, DateTime generatedAt)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteTextEntry(archive, "[Content_Types].xml", BuildContentTypes());
            WriteTextEntry(archive, "_rels/.rels", BuildRootRelationships());
            WriteTextEntry(archive, "docProps/app.xml", BuildAppProperties());
            WriteTextEntry(archive, "docProps/core.xml", BuildCoreProperties(generatedAt));
            WriteTextEntry(archive, "xl/workbook.xml", BuildWorkbook());
            WriteTextEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
            WriteTextEntry(archive, "xl/styles.xml", BuildStyles());
            WriteWorksheetEntry(archive, rows);
        }

        return output.ToArray();
    }

    /// <param name="dateColumns">
    /// Columnas (desde 0) cuyos valores llegan como fecha (dd/MM/yyyy, con hora o sin ella, o el
    /// yyyy-MM-dd interno) y se escriben como fecha de Excel: así ordenan y filtran por día en vez
    /// de alfabéticamente, y se siguen viendo en dd/mm/aaaa por el formato de celda. Un valor que no
    /// sea una fecha se deja como texto.
    /// </param>
    /// <param name="numberColumns">
    /// Columnas (desde 0) cuyos valores son números enteros y se escriben como número de Excel: así
    /// se pueden sumar, promediar y ordenar. Un valor que no sea un entero se deja como texto.
    /// </param>
    public static byte[] BuildTableWorkbook(
        string worksheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        DateTime generatedAt,
        string documentTitle = "Informe de pacientes activos",
        IReadOnlyCollection<int>? dateColumns = null,
        IReadOnlyCollection<int>? numberColumns = null)
    {
        return BuildMultiSheetWorkbook(
            [new TableSheet(worksheetName, headers, rows, dateColumns, numberColumns)],
            generatedAt,
            documentTitle);
    }

    /// <summary>
    /// Una hoja de un libro de varias hojas.
    /// </summary>
    /// <param name="DetectDates">
    /// Cualquier celda cuyo contenido sea exactamente una fecha de los exportables (dd/MM/yyyy, con
    /// hora o sin ella) se escribe como fecha de Excel, en cualquier columna. Para hojas de muchas
    /// columnas donde marcarlas una por una sería frágil (el censo de agudos).
    /// </param>
    public sealed record TableSheet(
        string Name,
        IReadOnlyList<string> Headers,
        IReadOnlyList<IReadOnlyList<string?>> Rows,
        IReadOnlyCollection<int>? DateColumns = null,
        IReadOnlyCollection<int>? NumberColumns = null,
        bool DetectDates = false);

    /// <summary>
    /// Libro con varias hojas de tabla (encabezado fijo, filtro y fechas dd/mm/aaaa en cada una).
    /// Excel no admite dos hojas con el mismo nombre ni nombres de más de 31 caracteres.
    /// </summary>
    public static byte[] BuildMultiSheetWorkbook(
        IReadOnlyList<TableSheet> sheets,
        DateTime generatedAt,
        string documentTitle)
    {
        if (sheets.Count == 0)
        {
            throw new ArgumentException("El libro debe tener al menos una hoja.", nameof(sheets));
        }

        foreach (var sheet in sheets)
        {
            if (sheet.Headers.Count == 0)
            {
                throw new ArgumentException($"La hoja '{sheet.Name}' debe tener al menos una columna.", nameof(sheets));
            }

            if (sheet.Rows.Any(row => row.Count != sheet.Headers.Count))
            {
                throw new ArgumentException($"Cada fila de la hoja '{sheet.Name}' debe tener el mismo número de columnas que el encabezado.", nameof(sheets));
            }
        }

        var names = sheets.Select(x => SafeWorksheetName(x.Name)).ToList();
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
        {
            throw new ArgumentException("Dos hojas no pueden llamarse igual.", nameof(sheets));
        }

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteTextEntry(archive, "[Content_Types].xml", BuildContentTypes(sheets.Count));
            WriteTextEntry(archive, "_rels/.rels", BuildRootRelationships());
            WriteTextEntry(archive, "docProps/app.xml", BuildAppProperties());
            WriteTextEntry(archive, "docProps/core.xml", BuildCoreProperties(generatedAt, documentTitle));
            WriteTextEntry(archive, "xl/workbook.xml", BuildWorkbook(names));
            WriteTextEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(sheets.Count));
            WriteTextEntry(archive, "xl/styles.xml", BuildStyles());
            for (var index = 0; index < sheets.Count; index++)
            {
                var sheet = sheets[index];
                WriteTableWorksheetEntry(
                    archive,
                    $"xl/worksheets/sheet{index + 1}.xml",
                    sheet.Headers,
                    sheet.Rows,
                    sheet.DateColumns,
                    sheet.NumberColumns,
                    sheet.DetectDates);
            }
        }

        return output.ToArray();
    }

    private static void WriteWorksheetEntry(ZipArchive archive, IReadOnlyList<ActivePatientReportRow> rows)
    {
        var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, CreateXmlSettings());
        var lastRow = Math.Max(1, rows.Count + 1);

        writer.WriteStartDocument(true);
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);

        writer.WriteStartElement("dimension");
        writer.WriteAttributeString("ref", $"A1:J{lastRow}");
        writer.WriteEndElement();

        writer.WriteStartElement("sheetViews");
        writer.WriteStartElement("sheetView");
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane");
        writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", "A2");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("sheetFormatPr");
        writer.WriteAttributeString("defaultRowHeight", "18");
        writer.WriteEndElement();

        writer.WriteStartElement("cols");
        for (var index = 0; index < ColumnWidths.Length; index++)
        {
            writer.WriteStartElement("col");
            writer.WriteAttributeString("min", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", ColumnWidths[index].ToString("0.##", CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();

        writer.WriteStartElement("sheetData");
        WriteHeaderRow(writer);
        for (var index = 0; index < rows.Count; index++)
        {
            WriteDataRow(writer, index + 2, rows[index]);
        }
        writer.WriteEndElement();

        writer.WriteStartElement("autoFilter");
        writer.WriteAttributeString("ref", $"A1:J{lastRow}");
        writer.WriteEndElement();

        writer.WriteStartElement("pageMargins");
        writer.WriteAttributeString("left", "0.7");
        writer.WriteAttributeString("right", "0.7");
        writer.WriteAttributeString("top", "0.75");
        writer.WriteAttributeString("bottom", "0.75");
        writer.WriteAttributeString("header", "0.3");
        writer.WriteAttributeString("footer", "0.3");
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteTableWorksheetEntry(
        ZipArchive archive,
        string path,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        IReadOnlyCollection<int>? dateColumns,
        IReadOnlyCollection<int>? numberColumns,
        bool detectDates)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, CreateXmlSettings());
        var lastRow = rows.Count + 1;
        var lastColumn = GetColumnName(headers.Count);

        writer.WriteStartDocument(true);
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);

        writer.WriteStartElement("dimension");
        writer.WriteAttributeString("ref", $"A1:{lastColumn}{lastRow}");
        writer.WriteEndElement();

        writer.WriteStartElement("sheetViews");
        writer.WriteStartElement("sheetView");
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane");
        writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", "A2");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("sheetFormatPr");
        writer.WriteAttributeString("defaultRowHeight", "18");
        writer.WriteEndElement();

        writer.WriteStartElement("cols");
        for (var index = 0; index < headers.Count; index++)
        {
            var width = Math.Clamp(headers[index].Length + 4d, 14d, 34d);
            writer.WriteStartElement("col");
            writer.WriteAttributeString("min", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", width.ToString("0.##", CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();

        writer.WriteStartElement("sheetData");
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", "1");
        writer.WriteAttributeString("ht", "31.2");
        writer.WriteAttributeString("customHeight", "1");
        for (var index = 0; index < headers.Count; index++)
        {
            WriteInlineStringCell(writer, $"{GetColumnName(index + 1)}1", headers[index], styleIndex: 1);
        }
        writer.WriteEndElement();

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var rowNumber = rowIndex + 2;
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("ht", "30");
            writer.WriteAttributeString("customHeight", "1");
            for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
            {
                var reference = $"{GetColumnName(columnIndex + 1)}{rowNumber}";
                var value = rows[rowIndex][columnIndex];
                if ((detectDates || dateColumns?.Contains(columnIndex) == true) && TryParseDate(value, out var date, out var withTime))
                {
                    WriteDateCell(writer, reference, date, withTime);
                }
                else if (numberColumns?.Contains(columnIndex) == true
                    && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero))
                {
                    WriteNumberCell(writer, reference, numero, styleIndex: 3);
                }
                else
                {
                    WriteInlineStringCell(writer, reference, value, styleIndex: 4);
                }
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();

        writer.WriteStartElement("autoFilter");
        writer.WriteAttributeString("ref", $"A1:{lastColumn}{lastRow}");
        writer.WriteEndElement();

        writer.WriteStartElement("pageMargins");
        writer.WriteAttributeString("left", "0.7");
        writer.WriteAttributeString("right", "0.7");
        writer.WriteAttributeString("top", "0.75");
        writer.WriteAttributeString("bottom", "0.75");
        writer.WriteAttributeString("header", "0.3");
        writer.WriteAttributeString("footer", "0.3");
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteHeaderRow(XmlWriter writer)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", "1");
        writer.WriteAttributeString("ht", "31.2");
        writer.WriteAttributeString("customHeight", "1");

        for (var index = 0; index < Headers.Length; index++)
        {
            WriteInlineStringCell(writer, $"{GetColumnName(index + 1)}1", Headers[index], styleIndex: 1);
        }

        writer.WriteEndElement();
    }

    private static void WriteDataRow(XmlWriter writer, int rowNumber, ActivePatientReportRow row)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("ht", "30");
        writer.WriteAttributeString("customHeight", "1");

        WriteDateCell(writer, $"A{rowNumber}", row.CurrentDate);
        WriteInlineStringCell(writer, $"B{rowNumber}", row.FullName, 4);
        WriteInlineStringCell(writer, $"C{rowNumber}", row.IdentificationType, 4);
        WriteInlineStringCell(writer, $"D{rowNumber}", row.IdentificationNumber, 4);
        WriteInlineStringCell(writer, $"E{rowNumber}", row.Zone, 4);
        WriteDateCell(writer, $"F{rowNumber}", row.AdmissionDate);
        WriteNumberCell(writer, $"G{rowNumber}", row.LengthOfStayDays, 3);
        WriteInlineStringCell(writer, $"H{rowNumber}", row.Diagnosis, 4);
        WriteInlineStringCell(writer, $"I{rowNumber}", row.Program, 4);
        WriteInlineStringCell(writer, $"J{rowNumber}", row.Insurer, 4);

        writer.WriteEndElement();
    }

    private static void WriteInlineStringCell(XmlWriter writer, string reference, string? value, int styleIndex)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("t", "inlineStr");
        writer.WriteStartElement("is");
        writer.WriteStartElement("t");
        writer.WriteString(SinCaracteresDeControl(value));
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    /// <summary>
    /// Quita los caracteres de control que a veces llegan al pegar texto de Word o de un PDF: no son
    /// XML válido y harían fallar la escritura del archivo. Se conservan tabulador y saltos de línea.
    /// </summary>
    private static string SinCaracteresDeControl(string? value)
    {
        var limpio = (value ?? string.Empty).Trim();
        return limpio.Any(char.IsControl)
            ? new string(limpio.Where(c => c is '\t' or '\n' or '\r' || !char.IsControl(c)).ToArray())
            : limpio;
    }

    private static void WriteDateCell(XmlWriter writer, string reference, DateTime value, bool withTime = false)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        writer.WriteAttributeString("s", withTime ? "5" : "2");
        writer.WriteStartElement("v");
        writer.WriteString((withTime ? value : value.Date).ToOADate().ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    /// <summary>
    /// Las fechas que escriben los exportables: dd/MM/yyyy, dd/MM/yyyy HH:mm[:ss] y el yyyy-MM-dd que
    /// usan internamente algunos informes. Un texto que solo empieza por una fecha no cuenta.
    /// </summary>
    private static bool TryParseDate(string? value, out DateTime date, out bool withTime)
    {
        withTime = false;
        date = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Una fecha anterior a 1900 no existe para Excel (ToOADate ni siquiera la convierte): se deja
        // como texto para no dañar el archivo. Ver FechaExportable.CabeEnExcel.
        if (DateTime.TryParseExact(value, ["dd/MM/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return FechaExportable.CabeEnExcel(date);
        }

        if (DateTime.TryParseExact(value, ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            withTime = true;
            return FechaExportable.CabeEnExcel(date);
        }

        return false;
    }

    private static void WriteNumberCell(XmlWriter writer, string reference, int value, int styleIndex)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
        writer.WriteStartElement("v");
        writer.WriteString(value.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static string GetColumnName(int columnNumber)
    {
        var name = string.Empty;
        while (columnNumber > 0)
        {
            columnNumber--;
            name = (char)('A' + columnNumber % 26) + name;
            columnNumber /= 26;
        }
        return name;
    }

    private static void WriteTextEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static XmlWriterSettings CreateXmlSettings()
    {
        return new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            CloseOutput = false
        };
    }

    private static string BuildContentTypes(int sheetCount = 1)
    {
        var sheets = string.Concat(Enumerable.Range(1, sheetCount).Select(index =>
            $"""<Override PartName="/xl/worksheets/sheet{index}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>"""));
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              {sheets}
              <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
              <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
              <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
            </Types>
            """;
    }

    private static string BuildRootRelationships() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
        </Relationships>
        """;

    /// <summary>Excel no admite en el nombre de una hoja \ / ? * [ ] : ni más de 31 caracteres.</summary>
    private static string SafeWorksheetName(string worksheetName)
    {
        var safe = new string(worksheetName.Trim().Where(c => c is not ('\\' or '/' or '?' or '*' or '[' or ']' or ':')).ToArray());
        if (safe.Length > 31)
        {
            safe = safe[..31];
        }

        return string.IsNullOrWhiteSpace(safe) ? "Hoja" : safe;
    }

    private static string BuildWorkbook(string worksheetName = "ACTIVOS") => BuildWorkbook([SafeWorksheetName(worksheetName)]);

    private static string BuildWorkbook(IReadOnlyList<string> worksheetNames)
    {
        // Las hojas se enlazan con rId1..rIdN; los estilos van después (ver BuildWorkbookRelationships).
        var sheets = string.Concat(worksheetNames.Select((name, index) =>
            $"""<sheet name="{SecurityElement.Escape(name)}" sheetId="{index + 1}" r:id="rId{index + 1}"/>"""));

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <bookViews><workbookView/></bookViews>
              <sheets>{sheets}</sheets>
            </workbook>
            """;
    }

    private static string BuildWorkbookRelationships(int sheetCount = 1)
    {
        var sheets = string.Concat(Enumerable.Range(1, sheetCount).Select(index =>
            $"""<Relationship Id="rId{index}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{index}.xml"/>"""));

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              {sheets}
              <Relationship Id="rId{sheetCount + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
            </Relationships>
            """;
    }

    private static string BuildStyles() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="2"><numFmt numFmtId="164" formatCode="dd/mm/yyyy"/><numFmt numFmtId="165" formatCode="dd/mm/yyyy hh:mm:ss"/></numFmts>
          <fonts count="2">
            <font><sz val="11"/><name val="Calibri"/><family val="2"/></font>
            <font><b/><color rgb="FFFFFFFF"/><sz val="11"/><name val="Calibri"/><family val="2"/></font>
          </fonts>
          <fills count="3">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF0F766E"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="2">
            <border><left/><right/><top/><bottom/><diagonal/></border>
            <border><left style="thin"><color rgb="FFD6DEE8"/></left><right style="thin"><color rgb="FFD6DEE8"/></right><top style="thin"><color rgb="FFD6DEE8"/></top><bottom style="thin"><color rgb="FFD6DEE8"/></bottom><diagonal/></border>
          </borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="6">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
            <xf numFmtId="164" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf>
            <xf numFmtId="165" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
          </cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;

    private static string BuildAppProperties() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
          <Application>Nexa</Application>
        </Properties>
        """;

    private static string BuildCoreProperties(DateTime generatedAt, string documentTitle = "Informe de pacientes activos")
    {
        var timestamp = generatedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var title = SecurityElement.Escape(documentTitle) ?? string.Empty;
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <dc:title>{title}</dc:title>
              <dc:creator>Nexa</dc:creator>
              <dcterms:created xsi:type="dcterms:W3CDTF">{timestamp}</dcterms:created>
              <dcterms:modified xsi:type="dcterms:W3CDTF">{timestamp}</dcterms:modified>
            </cp:coreProperties>
            """;
    }
}
