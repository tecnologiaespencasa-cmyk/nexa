using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Solo se permiten archivos PDF." });
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
            documentText = ExtractPdfText(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No fue posible leer el PDF de remisión {FileName}.", file.FileName);
            return BadRequest(new { message = "No fue posible leer el PDF. Verifica que el archivo no esté dañado o protegido." });
        }

        if (string.IsNullOrWhiteSpace(documentText))
        {
            return BadRequest(new { message = "El PDF no contiene texto extraíble. Puede tratarse de un documento escaneado como imagen." });
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
            $"Archivo: {file.FileName}",
            extraccionAuditUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

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
