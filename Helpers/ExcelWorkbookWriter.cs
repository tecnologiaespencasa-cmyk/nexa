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

    public static byte[] BuildTableWorkbook(
        string worksheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        DateTime generatedAt)
    {
        if (headers.Count == 0)
        {
            throw new ArgumentException("El libro debe tener al menos una columna.", nameof(headers));
        }

        if (rows.Any(row => row.Count != headers.Count))
        {
            throw new ArgumentException("Cada fila debe tener el mismo número de columnas que el encabezado.", nameof(rows));
        }

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteTextEntry(archive, "[Content_Types].xml", BuildContentTypes());
            WriteTextEntry(archive, "_rels/.rels", BuildRootRelationships());
            WriteTextEntry(archive, "docProps/app.xml", BuildAppProperties());
            WriteTextEntry(archive, "docProps/core.xml", BuildCoreProperties(generatedAt));
            WriteTextEntry(archive, "xl/workbook.xml", BuildWorkbook(worksheetName));
            WriteTextEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
            WriteTextEntry(archive, "xl/styles.xml", BuildStyles());
            WriteTableWorksheetEntry(archive, headers, rows);
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
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
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
                WriteInlineStringCell(writer, $"{GetColumnName(columnIndex + 1)}{rowNumber}", rows[rowIndex][columnIndex], styleIndex: 4);
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
        writer.WriteString(value?.Trim() ?? string.Empty);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteDateCell(XmlWriter writer, string reference, DateTime value)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        writer.WriteAttributeString("s", "2");
        writer.WriteStartElement("v");
        writer.WriteString(value.Date.ToOADate().ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
        writer.WriteEndElement();
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

    private static string BuildContentTypes() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
          <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
          <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
        </Types>
        """;

    private static string BuildRootRelationships() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
        </Relationships>
        """;

    private static string BuildWorkbook(string worksheetName = "ACTIVOS")
    {
        var safeWorksheetName = worksheetName.Trim();
        if (safeWorksheetName.Length > 31)
        {
            safeWorksheetName = safeWorksheetName[..31];
        }

        safeWorksheetName = SecurityElement.Escape(safeWorksheetName) ?? "Hoja 1";

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <bookViews><workbookView/></bookViews>
              <sheets><sheet name="{safeWorksheetName}" sheetId="1" r:id="rId1"/></sheets>
            </workbook>
            """;
    }

    private static string BuildWorkbookRelationships() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private static string BuildStyles() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="1"><numFmt numFmtId="164" formatCode="dd/mm/yyyy"/></numFmts>
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
          <cellXfs count="5">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
            <xf numFmtId="164" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf>
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

    private static string BuildCoreProperties(DateTime generatedAt)
    {
        var timestamp = generatedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <dc:title>Informe de pacientes activos</dc:title>
              <dc:creator>Nexa</dc:creator>
              <dcterms:created xsi:type="dcterms:W3CDTF">{timestamp}</dcterms:created>
              <dcterms:modified xsi:type="dcterms:W3CDTF">{timestamp}</dcterms:modified>
            </cp:coreProperties>
            """;
    }
}
