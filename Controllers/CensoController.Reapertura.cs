using System.Globalization;
using System.Net;
using System.Security.Claims;
using IntranetPrueba.Data.Entities;
using IntranetPrueba.Models.Security;
using IntranetPrueba.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Controllers;

public partial class CensoController
{
    private const string GerenciaReaperturaEmailFallback = "gerencia@especialistasencasa.com";

    [HttpPost]
    public async Task<IActionResult> SolicitarReaperturaKardex(
        long id,
        string? motivo,
        long? prorrogaVersionId,
        bool documentoProrroga = false,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0) return BadRequest(new { message = "ID de registro invalido." });

        var motivoNormalizado = (motivo ?? string.Empty).Trim();
        if (!ReaperturaKardexMotivos.Todos.Contains(motivoNormalizado))
        {
            return BadRequest(new { message = "Selecciona un motivo de reapertura valido." });
        }

        var record = await _context.Censos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null) return NotFound(new { message = "Registro no encontrado." });

        string tipoDocumento;
        long? versionId = null;
        string documentoDescripcion;

        if (prorrogaVersionId.HasValue)
        {
            var prorroga = await _context.CensoProrrogas.FirstOrDefaultAsync(
                x => x.Id == prorrogaVersionId.Value && x.CensoRecordId == id, cancellationToken);
            if (prorroga is null) return NotFound(new { message = "Prorroga no encontrada." });
            if (!prorroga.CerradaAtUtc.HasValue)
            {
                return BadRequest(new { message = "Esta prorroga no esta cerrada; no requiere reapertura." });
            }

            tipoDocumento = ReaperturaKardexTipoDocumento.ProrrogaVersion;
            versionId = prorroga.Id;
            documentoDescripcion = $"prorroga #{prorroga.Numero}";
        }
        else if (documentoProrroga)
        {
            if (!await IsBaseProrrogaClosedAsync(record, clearInvalidMarker: true, cancellationToken))
            {
                return BadRequest(new { message = "Esta prorroga no esta cerrada; no requiere reapertura." });
            }

            tipoDocumento = ReaperturaKardexTipoDocumento.ProrrogaBase;
            documentoDescripcion = "prorroga";
        }
        else
        {
            if (!record.KardexCerradoAtUtc.HasValue)
            {
                return BadRequest(new { message = "El kardex no esta cerrado; no requiere reapertura." });
            }

            tipoDocumento = ReaperturaKardexTipoDocumento.KardexPrincipal;
            documentoDescripcion = "kardex";
        }

        var yaPendiente = await _context.CensoKardexReaperturas.AnyAsync(
            r => r.CensoRecordId == id
                && r.Estado == ReaperturaKardexEstado.Pendiente
                && r.TipoDocumento == tipoDocumento
                && r.ProrrogaVersionId == versionId,
            cancellationToken);
        if (yaPendiente)
        {
            return BadRequest(new { message = "Ya existe una solicitud de reapertura pendiente para este documento." });
        }

        var currentUserId = GetCurrentUserIdOrEmpty();
        var solicitante = await ResolveUserDisplayNameAsync(currentUserId, cancellationToken);

        var solicitud = new CensoKardexReaperturaSolicitud
        {
            CensoRecordId = id,
            ProrrogaVersionId = versionId,
            TipoDocumento = tipoDocumento,
            Motivo = motivoNormalizado,
            Estado = ReaperturaKardexEstado.Pendiente,
            SolicitadoPorUserId = currentUserId,
            SolicitadoPorNombre = solicitante,
            SolicitadoAtUtc = DateTime.UtcNow
        };
        _context.CensoKardexReaperturas.Add(solicitud);
        await _context.SaveChangesAsync(cancellationToken);

        var emailWarning = await SendReaperturaSolicitudEmailAsync(record, solicitud, documentoDescripcion, cancellationToken);

        await _auditService.LogAsync("CENSO_REAPERTURA_SOLICITADA", "Censo",
            $"Paciente: {record.NombrePaciente}, Doc: {record.NumeroIdentificacion}, Motivo: {motivoNormalizado}, Tipo: {tipoDocumento}",
            currentUserId == Guid.Empty ? null : currentUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Json(new
        {
            message = string.IsNullOrEmpty(emailWarning)
                ? "Solicitud de reapertura enviada. Un supervisor debe aprobarla."
                : $"Solicitud de reapertura registrada. {emailWarning}",
            solicitud = MapReaperturaDto(solicitud)
        });
    }

    [HttpPost]
    [Authorize(Policy = SystemPermissions.Aprobacion)]
    public async Task<IActionResult> AprobarReaperturaKardex(long solicitudId, CancellationToken cancellationToken = default)
    {
        var solicitud = await _context.CensoKardexReaperturas.FirstOrDefaultAsync(
            r => r.Id == solicitudId && r.Estado == ReaperturaKardexEstado.Pendiente, cancellationToken);
        if (solicitud is null) return NotFound(new { message = "Solicitud de reapertura no encontrada o ya gestionada." });

        var record = await _context.Censos.FirstOrDefaultAsync(x => x.Id == solicitud.CensoRecordId, cancellationToken);
        if (record is null) return NotFound(new { message = "Registro no encontrado." });

        switch (solicitud.TipoDocumento)
        {
            case ReaperturaKardexTipoDocumento.ProrrogaVersion:
                if (solicitud.ProrrogaVersionId.HasValue)
                {
                    var prorroga = await _context.CensoProrrogas.FirstOrDefaultAsync(
                        x => x.Id == solicitud.ProrrogaVersionId.Value, cancellationToken);
                    if (prorroga is not null)
                    {
                        prorroga.CerradaAtUtc = null;
                        prorroga.CerradaPorFarmaciaId = null;
                        prorroga.UpdatedAtUtc = DateTime.UtcNow;
                    }
                }
                break;
            case ReaperturaKardexTipoDocumento.ProrrogaBase:
                record.ProrrogaCerradaAtUtc = null;
                record.ProrrogaCerradaPorFarmaciaId = null;
                break;
            default:
                record.KardexCerradoAtUtc = null;
                record.KardexCerradoPorFarmaciaId = null;
                break;
        }

        var currentUserId = GetCurrentUserIdOrEmpty();
        solicitud.Estado = ReaperturaKardexEstado.Aprobada;
        solicitud.ResueltoPorUserId = currentUserId == Guid.Empty ? null : currentUserId;
        solicitud.ResueltoPorNombre = await ResolveUserDisplayNameAsync(currentUserId, cancellationToken);
        solicitud.ResueltoAtUtc = DateTime.UtcNow;

        // Marca persistente en el censo: el paciente tuvo reapertura de kardex (última reapertura gana).
        record.TuvoReaperturaKardex = true;
        record.ReaperturaSolicitadaPor = solicitud.SolicitadoPorNombre;
        record.ReaperturaAprobadaPor = solicitud.ResueltoPorNombre;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("CENSO_REAPERTURA_APROBADA", "Censo",
            $"Paciente: {record.NombrePaciente}, Doc: {record.NumeroIdentificacion}, Tipo: {solicitud.TipoDocumento}",
            currentUserId == Guid.Empty ? null : currentUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Json(new
        {
            message = "Reapertura aprobada. El documento quedo habilitado para edicion.",
            recordId = record.Id
        });
    }

    [HttpPost]
    [Authorize(Policy = SystemPermissions.Aprobacion)]
    public async Task<IActionResult> RechazarReaperturaKardex(
        long solicitudId,
        string? observacion,
        CancellationToken cancellationToken = default)
    {
        var solicitud = await _context.CensoKardexReaperturas.FirstOrDefaultAsync(
            r => r.Id == solicitudId && r.Estado == ReaperturaKardexEstado.Pendiente, cancellationToken);
        if (solicitud is null) return NotFound(new { message = "Solicitud de reapertura no encontrada o ya gestionada." });

        var record = await _context.Censos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == solicitud.CensoRecordId, cancellationToken);

        var currentUserId = GetCurrentUserIdOrEmpty();
        solicitud.Estado = ReaperturaKardexEstado.Rechazada;
        solicitud.ResueltoPorUserId = currentUserId == Guid.Empty ? null : currentUserId;
        solicitud.ResueltoPorNombre = await ResolveUserDisplayNameAsync(currentUserId, cancellationToken);
        solicitud.ResueltoAtUtc = DateTime.UtcNow;
        var obs = observacion?.Trim();
        solicitud.ObservacionResolucion = string.IsNullOrWhiteSpace(obs) ? null : obs;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("CENSO_REAPERTURA_RECHAZADA", "Censo",
            $"Paciente: {record?.NombrePaciente}, Doc: {record?.NumeroIdentificacion}, Tipo: {solicitud.TipoDocumento}",
            currentUserId == Guid.Empty ? null : currentUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Json(new { message = "Solicitud de reapertura rechazada." });
    }

    private Guid GetCurrentUserIdOrEmpty()
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;
    }

    private async Task<string> ResolveUserDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId != Guid.Empty)
        {
            var fullName = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(fullName)) return fullName!;
        }

        return User.FindFirstValue(ClaimTypes.Name) ?? "Usuario";
    }

    private static object? MapReaperturaDto(CensoKardexReaperturaSolicitud? solicitud)
    {
        if (solicitud is null) return null;
        return new
        {
            id = solicitud.Id,
            estado = solicitud.Estado,
            motivo = solicitud.Motivo,
            tipoDocumento = solicitud.TipoDocumento,
            solicitante = solicitud.SolicitadoPorNombre,
            solicitadaAt = solicitud.SolicitadoAtUtc
        };
    }

    private async Task<string> SendReaperturaSolicitudEmailAsync(
        CensoRecord record,
        CensoKardexReaperturaSolicitud solicitud,
        string documentoDescripcion,
        CancellationToken cancellationToken)
    {
        var destino = Environment.GetEnvironmentVariable("REAPERTURA_GERENCIA_EMAIL")?.Trim();
        if (string.IsNullOrWhiteSpace(destino)) destino = GerenciaReaperturaEmailFallback;

        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(solicitud.SolicitadoAtUtc, ColombiaTimeZone);
        var paciente = WebUtility.HtmlEncode(record.NombrePaciente ?? string.Empty);
        var cedula = WebUtility.HtmlEncode($"{record.TipoIdentificacion} {record.NumeroIdentificacion}".Trim());
        var usuario = WebUtility.HtmlEncode(solicitud.SolicitadoPorNombre);
        var motivo = WebUtility.HtmlEncode(solicitud.Motivo);
        var docDesc = WebUtility.HtmlEncode(documentoDescripcion);
        var fechaTexto = WebUtility.HtmlEncode(fechaLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));

        var html = $@"<div style=""font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937;"">
  <h2 style=""color:#b91c1c;margin-bottom:4px;"">Solicitud de reapertura de kardex</h2>
  <p>Se ha solicitado la reapertura del <strong>{docDesc}</strong> del siguiente paciente:</p>
  <table style=""border-collapse:collapse;"">
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Paciente:</strong></td><td>{paciente}</td></tr>
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Documento:</strong></td><td>{cedula}</td></tr>
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Solicitado por:</strong></td><td>{usuario}</td></tr>
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Motivo:</strong></td><td>{motivo}</td></tr>
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Fecha solicitud:</strong></td><td>{fechaTexto}</td></tr>
  </table>
  <p style=""margin-top:16px;"">Por favor gestione la <strong>aprobacion o rechazo</strong> de esta reapertura en la intranet <strong>Nexa</strong>.</p>
</div>";

        var message = new EmailMessage
        {
            To = new[] { destino },
            Subject = $"Solicitud de reapertura de kardex - {record.NombrePaciente}",
            HtmlBody = html
        };

        try
        {
            var result = await _emailService.SendAsync(message, cancellationToken);
            if (!result.Succeeded)
            {
                _logger.LogWarning("No se pudo enviar el correo de reapertura: {Error}", result.ErrorMessage);
                return "No fue posible enviar el correo a gerencia, pero la solicitud quedo registrada.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando el correo de reapertura de kardex.");
            return "No fue posible enviar el correo a gerencia, pero la solicitud quedo registrada.";
        }

        return string.Empty;
    }
}
