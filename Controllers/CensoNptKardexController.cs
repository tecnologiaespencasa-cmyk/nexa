using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Models.ViewModels;

namespace Nexa.Controllers;

/// <summary>
/// Requisición de insumos del censo de NPT.
///
/// Estuvo dentro de clínica de heridas como un tipo más de kardex; se trajo a su programa. Un
/// registro de NPT tiene una sola requisición —no hay tipos ni planes que agrupar— y el ciclo con
/// farmacia es el mismo de siempre: se edita hasta que farmacia le da el OK y ahí queda cerrada.
/// </summary>
public partial class CensoController
{
    /// <summary>
    /// Requisición del registro, creándola si todavía no existe, y comprobando que se pueda editar.
    /// </summary>
    private async Task<NptKardexEditableResultado> ResolverKardexNptEditableAsync(
        long recordId,
        CancellationToken cancellationToken)
    {
        var record = await _context.CensoNpt
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);

        if (record is null)
        {
            return new NptKardexEditableResultado
            {
                Error = NotFound(new { message = "No se encontró el registro de NPT del paciente." })
            };
        }

        var kardex = await _context.CensoNptKardex
            .FirstOrDefaultAsync(x => x.CensoNptRecordId == recordId, cancellationToken);

        if (kardex is null)
        {
            kardex = new CensoNptKardex
            {
                CensoNptRecordId = recordId,
                ElaboradoPor = PerfilQueAbreKardex(),
                CreatedAtUtc = DateTime.UtcNow
            };

            await _context.CensoNptKardex.AddAsync(kardex, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (kardex.KardexCerradoAtUtc is not null)
        {
            return new NptKardexEditableResultado
            {
                Error = BadRequest(new
                {
                    message = "Requisición cerrada. Farmacia ya la aprobó y queda solo para consulta."
                })
            };
        }

        return new NptKardexEditableResultado { Record = record, Kardex = kardex };
    }

    [HttpGet]
    public async Task<IActionResult> KardexNpt(long recordId, CancellationToken cancellationToken)
    {
        var record = await _context.CensoNpt
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "No se encontró el registro de NPT del paciente." });
        }

        var kardex = await _context.CensoNptKardex
            .AsNoTracking()
            .Include(x => x.Adjuntos)
            .FirstOrDefaultAsync(x => x.CensoNptRecordId == recordId, cancellationToken);

        var documento = NptKardexBuilder.Resolver(
            record,
            kardex?.KardexJson,
            kardex?.ElaboradoPor ?? PerfilQueAbreKardex(),
            ColombiaTime.Convert(DateTime.UtcNow));

        return Json(new
        {
            documento,
            estado = new
            {
                existe = kardex is not null,
                cerrado = kardex?.KardexCerradoAtUtc is not null,
                cerradoPorFarmacia = kardex?.KardexCerradoAtUtc is not null,
                // NPT no tiene planes; se mandan en cero para que el modal compartido no tenga que
                // preguntar de qué programa viene.
                planCerrado = false,
                cerradoAtUtc = kardex?.KardexCerradoAtUtc,
                enviadoAtUtc = kardex?.FarmaciaEnviadoAtUtc,
                farmaciaEstado = kardex?.FarmaciaEstado,
                okFarmacia = kardex?.FarmaciaOkKardex ?? false
            },
            adjuntos = (kardex?.Adjuntos ?? [])
                .OrderByDescending(x => x.UploadedAtUtc)
                .Select(x => new { id = x.Id, nombre = x.FileName, subidoAtUtc = x.UploadedAtUtc })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarKardexNpt(
        long recordId,
        string? kardexJson,
        CancellationToken cancellationToken)
    {
        var resultado = await ResolverKardexNptEditableAsync(recordId, cancellationToken);
        if (resultado.Error is not null)
        {
            return resultado.Error;
        }

        var kardex = resultado.Kardex!;
        kardex.KardexJson = string.IsNullOrWhiteSpace(kardexJson) ? null : kardexJson.Trim();
        kardex.ElaboradoPor = PerfilQueAbreKardex();
        kardex.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await RegistrarAuditoriaKardexNptAsync(
            "CENSO_NPT_KARDEX_GUARDADO", resultado.Record!, cancellationToken);

        return Json(new { success = true, message = "Requisición de NPT guardada." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarKardexNptAFarmacia(
        long recordId,
        string? kardexJson,
        CancellationToken cancellationToken)
    {
        var resultado = await ResolverKardexNptEditableAsync(recordId, cancellationToken);
        if (resultado.Error is not null)
        {
            return resultado.Error;
        }

        var kardex = resultado.Kardex!;
        var nowUtc = DateTime.UtcNow;

        kardex.KardexJson = string.IsNullOrWhiteSpace(kardexJson) ? kardex.KardexJson : kardexJson.Trim();
        kardex.ElaboradoPor = PerfilQueAbreKardex();
        kardex.FarmaciaEnviadoAtUtc = nowUtc;
        kardex.FarmaciaEstado = FarmaciaEstados.Nuevo;
        kardex.FarmaciaKardexVistoAtUtc = null;
        kardex.UpdatedAtUtc = nowUtc;

        await _context.SaveChangesAsync(cancellationToken);
        await RegistrarAuditoriaKardexNptAsync(
            "CENSO_NPT_KARDEX_ENVIADO_FARMACIA", resultado.Record!, cancellationToken);

        // El correo al auxiliar no debe bloquear el envío: si falla, queda en el log y la
        // requisición ya está en farmacia.
        var avisos = await _farmaciaDispatchNotificationService
            .NotifyNptRequisicionEnviadaAsync(kardex, cancellationToken);

        foreach (var aviso in avisos)
        {
            _logger.LogWarning("Notificación de requisición NPT {KardexId}: {Aviso}", kardex.Id, aviso);
        }

        return Json(new
        {
            success = true,
            avisos,
            message = "Requisición de NPT enviada a farmacia."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxAdjuntoKardexBytes + 1024 * 1024)]
    public async Task<IActionResult> SubirAdjuntoKardexNpt(
        long recordId,
        IFormFile? archivo,
        CancellationToken cancellationToken)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return BadRequest(new { message = "Selecciona un archivo." });
        }

        if (archivo.Length > MaxAdjuntoKardexBytes)
        {
            return BadRequest(new { message = "El archivo debe pesar máximo 10 MB." });
        }

        var extension = Path.GetExtension(archivo.FileName);
        if (!AdjuntoKardexExtensionesPermitidas.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Formato no permitido. Adjunta PDF, Excel, CSV o imagen." });
        }

        var resultado = await ResolverKardexNptEditableAsync(recordId, cancellationToken);
        if (resultado.Error is not null)
        {
            return resultado.Error;
        }

        using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, cancellationToken);

        var adjunto = new CensoNptKardexAdjunto
        {
            CensoNptKardexId = resultado.Kardex!.Id,
            FileName = Path.GetFileName(archivo.FileName),
            FileData = memoria.ToArray(),
            UploadedAtUtc = DateTime.UtcNow
        };

        await _context.CensoNptKardexAdjuntos.AddAsync(adjunto, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new
        {
            success = true,
            adjunto = new { id = adjunto.Id, nombre = adjunto.FileName, subidoAtUtc = adjunto.UploadedAtUtc }
        });
    }

    [HttpGet]
    public async Task<IActionResult> DescargarAdjuntoKardexNpt(long adjuntoId, CancellationToken cancellationToken)
    {
        var adjunto = await _context.CensoNptKardexAdjuntos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == adjuntoId, cancellationToken);

        if (adjunto is null)
        {
            return NotFound();
        }

        return File(adjunto.FileData, "application/octet-stream", adjunto.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarAdjuntoKardexNpt(long adjuntoId, CancellationToken cancellationToken)
    {
        var adjunto = await _context.CensoNptKardexAdjuntos
            .Include(x => x.Kardex)
            .FirstOrDefaultAsync(x => x.Id == adjuntoId, cancellationToken);

        if (adjunto is null)
        {
            return NotFound(new { message = "El adjunto no existe." });
        }

        if (adjunto.Kardex.KardexCerradoAtUtc is not null)
        {
            return BadRequest(new { message = "Requisición cerrada por farmacia: ya no admite cambios." });
        }

        _context.CensoNptKardexAdjuntos.Remove(adjunto);
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new { success = true });
    }

    private sealed class NptKardexEditableResultado
    {
        public CensoNptRecord? Record { get; init; }
        public CensoNptKardex? Kardex { get; init; }
        public IActionResult? Error { get; init; }
    }

    private Task RegistrarAuditoriaKardexNptAsync(
        string accion,
        CensoNptRecord record,
        CancellationToken cancellationToken)
    {
        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid)
            ? (Guid?)parsedUid
            : null;

        return _auditService.LogAsync(
            accion,
            "CensoNptKardex",
            $"Paciente: {record.NombrePaciente}, Doc: {record.NumeroIdentificacion}, Requisición: NPT",
            auditUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
    }
}
