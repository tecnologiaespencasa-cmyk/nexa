using System.Security.Claims;
using System.Text;
using System.Text.Json;
using IntranetPrueba.Helpers;
using IntranetPrueba.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace IntranetPrueba.Controllers;

public partial class CensoController
{
    [HttpPost]
    [RequestSizeLimit(15L * 1024 * 1024)]
    public async Task<IActionResult> ExtraerDatosRemision(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No se proporcionó ningún archivo." });
        }

        var extension = Path.GetExtension(file.FileName);
        var isPdf = string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
        var isSpreadsheet = SpreadsheetFileSupport.IsSupportedSpreadsheet(file.FileName);
        if (!isPdf && !isSpreadsheet)
        {
            return BadRequest(new { message = $"Solo se permiten archivos PDF o {SpreadsheetFileSupport.SupportedFormatsDescription}." });
        }

        const long maxBytes = 10L * 1024 * 1024;
        if (file.Length > maxBytes)
        {
            return BadRequest(new { message = "El archivo no puede superar 10 MB." });
        }

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, cancellationToken);
            bytes = ms.ToArray();
        }

        string documentText;
        try
        {
            documentText = isPdf
                ? ExtractPdfText(bytes)
                : RemisionExcelTextExtractor.ExtractFormatoRemisionText(bytes, file.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No fue posible leer el archivo de remisión {FileName}.", file.FileName);
            return BadRequest(new
            {
                message = isPdf
                    ? "No fue posible leer el PDF. Verifica que el archivo no esté dañado o protegido."
                    : "No fue posible leer la hoja de cálculo. Verifica que el archivo sea válido y tenga información de la remisión o del paciente."
            });
        }

        if (string.IsNullOrWhiteSpace(documentText))
        {
            return BadRequest(new
            {
                message = isPdf
                    ? "El PDF no contiene texto extraíble. Puede tratarse de un documento escaneado como imagen."
                    : "La hoja de cálculo no contiene información para analizar."
            });
        }

        var result = await _remisionExtractionService.ExtractRemisionDataAsync(documentText, cancellationToken);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Json))
        {
            return BadRequest(new { message = result.ErrorMessage ?? "No fue posible extraer los datos del documento." });
        }

        var extraccionAuditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var extraccionUid)
            ? (Guid?)extraccionUid
            : null;
        await _auditService.LogAsync("CENSO_EXTRACCION_REMISION", "Censo",
            $"Archivo {(isPdf ? "PDF" : "hoja de cálculo")}: {file.FileName}",
            extraccionAuditUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        using var document = JsonDocument.Parse(result.Json);
        return Json(new { success = true, data = document.RootElement.Clone() });
    }

    [HttpPost]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> ExtraerDatosRemisionTexto(
        [FromBody] RemisionTextExtractionRequest? request,
        CancellationToken cancellationToken)
    {
        var documentText = request?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return BadRequest(new { message = "Pega el contenido del correo o la remisión antes de procesarlo." });
        }

        if (documentText.Length > 50000)
        {
            return BadRequest(new { message = "El texto pegado es demasiado largo. Usa máximo 50.000 caracteres." });
        }

        var result = await _remisionExtractionService.ExtractRemisionDataAsync(documentText, cancellationToken);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Json))
        {
            return BadRequest(new { message = result.ErrorMessage ?? "No fue posible extraer los datos del texto." });
        }

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? (Guid?)userId
            : null;
        await _auditService.LogAsync("CENSO_EXTRACCION_REMISION", "Censo", "Texto pegado", auditUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        using var document = JsonDocument.Parse(result.Json);
        return Json(new { success = true, data = document.RootElement.Clone() });
    }

    private static string ExtractPdfText(byte[] bytes)
    {
        var builder = new StringBuilder();
        using var pdf = PdfDocument.Open(bytes);
        foreach (var page in pdf.GetPages())
        {
            builder.AppendLine(ContentOrderTextExtractor.GetText(page, addDoubleNewline: true));
        }

        return builder.ToString();
    }
}
