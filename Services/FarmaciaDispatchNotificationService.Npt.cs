using System.Text;
using Microsoft.EntityFrameworkCore;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Services.Models;

namespace Nexa.Services;

/// <summary>
/// Avisos de la requisición de NPT. Mismo flujo que clínica de heridas: el auxiliar asignado recibe
/// la requisición al enviarse a farmacia y el aviso de bolsa lista al despacharse.
/// </summary>
public partial class FarmaciaDispatchNotificationService
{
    public async Task<IReadOnlyList<string>> NotifyNptRequisicionEnviadaAsync(
        CensoNptKardex kardex,
        CancellationToken cancellationToken = default)
    {
        var completo = await CargarKardexNptCompletoAsync(kardex.Id, cancellationToken);
        if (completo is null)
        {
            return ["No se encontró la requisición para notificar."];
        }

        var record = completo.CensoNptRecord;
        if (string.IsNullOrWhiteSpace(record.AuxiliarEnfermeriaAsignado))
        {
            return ["El paciente no tiene auxiliar de enfermería asignado: no se envió la requisición."];
        }

        var assistantEmail = await GetAssignedAssistantEmailAsync(record.AuxiliarEnfermeriaAsignado, cancellationToken);
        if (string.IsNullOrWhiteSpace(assistantEmail))
        {
            return ["No se encontró correo del auxiliar asignado."];
        }

        var documento = NptKardexBuilder.Resolver(
            record,
            completo.KardexJson,
            completo.ElaboradoPor,
            completo.FarmaciaEnviadoAtUtc ?? completo.CreatedAtUtc);

        var attachments = new List<EmailAttachment>
        {
            new()
            {
                FileName = $"Requisicion_NPT_{SanitizeFileName(record.NumeroIdentificacion)}.html",
                ContentType = "text/html",
                Content = Encoding.UTF8.GetBytes(BuildRequisicionHtml(documento, "Atención: NPT"))
            }
        };

        foreach (var adjunto in completo.Adjuntos.OrderBy(x => x.UploadedAtUtc))
        {
            attachments.Add(new EmailAttachment
            {
                FileName = adjunto.FileName,
                ContentType = ResolverContentType(adjunto.FileName),
                Content = adjunto.FileData
            });
        }

        var result = await _emailService.SendAsync(new EmailMessage
        {
            To = [assistantEmail],
            Subject = $"Requisición NPT - {record.TipoIdentificacion} {record.NumeroIdentificacion} - {record.NombrePaciente}",
            HtmlBody = $"""
                <p>Hola <strong>{HtmlEncode(record.AuxiliarEnfermeriaAsignado)}</strong>,</p>
                <p>Se envió a farmacia la requisición de insumos de <strong>NPT</strong>
                   del paciente <strong>{HtmlEncode(record.NombrePaciente)}</strong>. Se adjunta la copia.</p>
                <p><strong>Documento:</strong> {HtmlEncode(record.TipoIdentificacion)} {HtmlEncode(record.NumeroIdentificacion)}</p>
                <p><strong>Dirección:</strong> {HtmlEncode(record.Direccion)}</p>
                <p><strong>Teléfonos:</strong> {HtmlEncode(TelefonosNptDe(record))}</p>
                <p><strong>Tratamiento:</strong> {documento.DuracionDias} días · {documento.Aplicaciones} aplicaciones</p>
                <br/>
                <p><em>Este es un correo automático de Especialistas en Casa</em></p>
                """,
            Attachments = attachments
        }, cancellationToken);

        return result.Succeeded
            ? []
            : [$"No se pudo enviar la requisición al auxiliar: {result.ErrorMessage}"];
    }

    public async Task<IReadOnlyList<string>> NotifyNptDespachadoAsync(
        CensoNptKardex kardex,
        CancellationToken cancellationToken = default)
    {
        var completo = await CargarKardexNptCompletoAsync(kardex.Id, cancellationToken);
        if (completo is null)
        {
            return ["No se encontró la requisición para notificar."];
        }

        var record = completo.CensoNptRecord;
        var assistantEmail = await GetAssignedAssistantEmailAsync(record.AuxiliarEnfermeriaAsignado, cancellationToken);
        if (string.IsNullOrWhiteSpace(assistantEmail))
        {
            return ["No se encontró correo del auxiliar para notificar despacho."];
        }

        var result = await _emailService.SendAsync(new EmailMessage
        {
            To = [assistantEmail],
            Subject = $"Bolsa lista para reclamar (NPT) - "
                + $"{record.TipoIdentificacion} {record.NumeroIdentificacion} - {record.NombrePaciente}",
            HtmlBody = $"""
                <p>Hola <strong>{HtmlEncode(record.AuxiliarEnfermeriaAsignado)}</strong>,</p>
                <p>La bolsa de insumos de <strong>NPT</strong> del paciente
                   <strong>{HtmlEncode(record.NombrePaciente)}</strong>
                   ({HtmlEncode(record.TipoIdentificacion)} {HtmlEncode(record.NumeroIdentificacion)})
                   está lista para ser reclamada.</p>
                <p>Por favor acercarse a farmacia para retirar la bolsa.</p>
                <br/>
                <p><em>Este es un correo automático de Especialistas en Casa</em></p>
                """
        }, cancellationToken);

        return result.Succeeded
            ? []
            : [$"No se pudo notificar el despacho al auxiliar: {result.ErrorMessage}"];
    }

    /// <remarks>
    /// Los adjuntos se traen aparte por lo mismo que en heridas: incluirlos en la consulta
    /// multiplicaría las filas del kardex por cada archivo.
    /// </remarks>
    private async Task<CensoNptKardex?> CargarKardexNptCompletoAsync(
        long kardexId,
        CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoNptKardex
            .AsNoTracking()
            .Include(x => x.CensoNptRecord)
            .FirstOrDefaultAsync(x => x.Id == kardexId, cancellationToken);

        if (kardex is null)
        {
            return null;
        }

        kardex.Adjuntos = await _context.CensoNptKardexAdjuntos
            .AsNoTracking()
            .Where(x => x.CensoNptKardexId == kardexId)
            .OrderBy(x => x.UploadedAtUtc)
            .ToListAsync(cancellationToken);

        return kardex;
    }

    private static string TelefonosNptDe(CensoNptRecord record) =>
        string.Join(" / ", new[] { record.TelefonoPrincipal, record.TelefonoAdicional1, record.TelefonoAdicional2 }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
}
