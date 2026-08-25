using System.Text;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Services;

/// <summary>
/// Avisos de las requisiciones de clínica de heridas. Replican el flujo de agudos: el auxiliar
/// asignado recibe la requisición al enviarse a farmacia, el aviso de bolsa lista al despacharse, y
/// los recordatorios mientras la bolsa espera en Empacado.
/// </summary>
public partial class FarmaciaDispatchNotificationService
{
    public async Task<IReadOnlyList<string>> NotifyClinicaHeridasRequisicionEnviadaAsync(
        CensoClinicaHeridasKardex kardex,
        CancellationToken cancellationToken = default)
    {
        var completo = await CargarKardexCompletoAsync(kardex.Id, cancellationToken);
        if (completo is null)
        {
            return ["No se encontró la requisición para notificar."];
        }

        var record = completo.CensoClinicaHeridasRecord;
        if (string.IsNullOrWhiteSpace(record.AuxiliarEnfermeriaAsignado))
        {
            return ["El paciente no tiene auxiliar de enfermería asignado: no se envió la requisición."];
        }

        var assistantEmail = await GetAssignedAssistantEmailAsync(record.AuxiliarEnfermeriaAsignado, cancellationToken);
        if (string.IsNullOrWhiteSpace(assistantEmail))
        {
            return ["No se encontró correo del auxiliar asignado."];
        }

        var documento = ClinicaHeridasKardexBuilder.Resolver(
            record,
            completo.Plan,
            completo.Tipo,
            completo.KardexJson,
            completo.ElaboradoPor,
            completo.FarmaciaEnviadoAtUtc ?? completo.CreatedAtUtc);

        var atencion = ClinicaHeridasKardexTipos.Nombre(completo.Tipo);

        var attachments = new List<EmailAttachment>
        {
            new()
            {
                FileName = $"Requisicion_{SanitizeFileName(atencion)}_Plan{completo.Plan.Numero}"
                    + $"_{SanitizeFileName(record.NumeroIdentificacion)}.html",
                ContentType = "text/html",
                Content = Encoding.UTF8.GetBytes(BuildClinicaHeridasRequisicionHtml(documento, completo.Plan.Numero))
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
            Subject = $"Requisición clínica de heridas ({atencion}) - "
                + $"{record.TipoIdentificacion} {record.NumeroIdentificacion} - {record.NombrePaciente}",
            HtmlBody = $"""
                <p>Hola <strong>{HtmlEncode(record.AuxiliarEnfermeriaAsignado)}</strong>,</p>
                <p>Se envió a farmacia la requisición de insumos de <strong>{HtmlEncode(atencion)}</strong>
                   del paciente <strong>{HtmlEncode(record.NombrePaciente)}</strong>. Se adjunta la copia.</p>
                <p><strong>Documento:</strong> {HtmlEncode(record.TipoIdentificacion)} {HtmlEncode(record.NumeroIdentificacion)}</p>
                <p><strong>Plan de requisiciones:</strong> {completo.Plan.Numero}</p>
                <p><strong>Dirección:</strong> {HtmlEncode(DireccionCompleta(record))}</p>
                <p><strong>Teléfonos:</strong> {HtmlEncode(TelefonosDe(record))}</p>
                <p><strong>Tratamiento:</strong> {documento.DuracionDias} días · {HtmlEncode(documento.Frecuencia)}
                   · {documento.Aplicaciones} aplicaciones</p>
                <br/>
                <p><em>Este es un correo automático de Especialistas en Casa</em></p>
                """,
            Attachments = attachments
        }, cancellationToken);

        return result.Succeeded
            ? []
            : [$"No se pudo enviar la requisición al auxiliar: {result.ErrorMessage}"];
    }

    public async Task<IReadOnlyList<string>> NotifyClinicaHeridasDespachadoAsync(
        CensoClinicaHeridasKardex kardex,
        CancellationToken cancellationToken = default)
    {
        var completo = await CargarKardexCompletoAsync(kardex.Id, cancellationToken);
        if (completo is null)
        {
            return ["No se encontró la requisición para notificar."];
        }

        var record = completo.CensoClinicaHeridasRecord;
        var assistantEmail = await GetAssignedAssistantEmailAsync(record.AuxiliarEnfermeriaAsignado, cancellationToken);
        if (string.IsNullOrWhiteSpace(assistantEmail))
        {
            return ["No se encontró correo del auxiliar para notificar despacho."];
        }

        var atencion = ClinicaHeridasKardexTipos.Nombre(completo.Tipo);
        var result = await _emailService.SendAsync(new EmailMessage
        {
            To = [assistantEmail],
            Subject = $"Bolsa lista para reclamar (clínica de heridas · {atencion}) - "
                + $"{record.TipoIdentificacion} {record.NumeroIdentificacion} - {record.NombrePaciente}",
            HtmlBody = $"""
                <p>Hola <strong>{HtmlEncode(record.AuxiliarEnfermeriaAsignado)}</strong>,</p>
                <p>La bolsa de insumos de <strong>{HtmlEncode(atencion)}</strong> del paciente
                   <strong>{HtmlEncode(record.NombrePaciente)}</strong>
                   ({HtmlEncode(record.TipoIdentificacion)} {HtmlEncode(record.NumeroIdentificacion)})
                   está lista para ser reclamada.</p>
                <p><strong>Plan de requisiciones:</strong> {completo.Plan.Numero}</p>
                <p>Por favor acercarse a farmacia para retirar la bolsa.</p>
                <br/>
                <p><em>Este es un correo automático de Especialistas en Casa</em></p>
                """
        }, cancellationToken);

        return result.Succeeded
            ? []
            : [$"No se pudo notificar al auxiliar sobre el despacho: {result.ErrorMessage}"];
    }

    public async Task<IReadOnlyList<string>> NotifyClinicaHeridasEmpacadoPendienteAuxiliarAsync(
        CensoClinicaHeridasKardex kardex,
        CancellationToken cancellationToken = default)
    {
        var completo = await CargarKardexCompletoAsync(kardex.Id, cancellationToken);
        if (completo is null)
        {
            return ["No se encontró la requisición para notificar."];
        }

        var record = completo.CensoClinicaHeridasRecord;
        if (string.IsNullOrWhiteSpace(record.AuxiliarEnfermeriaAsignado))
        {
            return ["No hay auxiliar asignado para notificar."];
        }

        var assistantEmail = await GetAssignedAssistantEmailAsync(record.AuxiliarEnfermeriaAsignado, cancellationToken);
        if (string.IsNullOrWhiteSpace(assistantEmail))
        {
            return ["No se encontró correo del auxiliar asignado."];
        }

        var atencion = ClinicaHeridasKardexTipos.Nombre(completo.Tipo);
        var result = await _emailService.SendAsync(new EmailMessage
        {
            To = [assistantEmail],
            Subject = $"Bolsa pendiente de reclamar (clínica de heridas · {atencion}) - "
                + $"{record.TipoIdentificacion} {record.NumeroIdentificacion} - {record.NombrePaciente}",
            HtmlBody = $"""
                <p>Hola <strong>{HtmlEncode(record.AuxiliarEnfermeriaAsignado)}</strong>,</p>
                <p>Tienes una bolsa de insumos de <strong>{HtmlEncode(atencion)}</strong> pendiente de reclamar
                   en la farmacia de <strong>Especialistas en Casa</strong>.</p>
                <p><strong>Paciente:</strong> {HtmlEncode(record.NombrePaciente)}</p>
                <p><strong>Documento:</strong> {HtmlEncode(record.TipoIdentificacion)} {HtmlEncode(record.NumeroIdentificacion)}</p>
                <p><strong>Dirección:</strong> {HtmlEncode(DireccionCompleta(record))}</p>
                <p><strong>Teléfonos:</strong> {HtmlEncode(TelefonosDe(record))}</p>
                <p>Por favor acercarse a farmacia para retirar la bolsa a la brevedad posible.</p>
                <br/>
                <p><em>Este es un correo automático de Especialistas en Casa</em></p>
                """
        }, cancellationToken);

        return result.Succeeded
            ? []
            : [$"No se pudo enviar recordatorio al auxiliar: {result.ErrorMessage}"];
    }

    public async Task<IReadOnlyList<string>> NotifyClinicaHeridasEmpacadoPorVencerGerenciaAsync(
        CensoClinicaHeridasKardex kardex,
        CancellationToken cancellationToken = default)
    {
        var completo = await CargarKardexCompletoAsync(kardex.Id, cancellationToken);
        if (completo is null)
        {
            return ["No se encontró la requisición para notificar."];
        }

        var record = completo.CensoClinicaHeridasRecord;
        var atencion = ClinicaHeridasKardexTipos.Nombre(completo.Tipo);

        var result = await _emailService.SendAsync(new EmailMessage
        {
            To = [GerenciaEmail],
            Subject = $"AVISO: Despacho por vencer en 24h (clínica de heridas · {atencion}) - "
                + $"{record.TipoIdentificacion} {record.NumeroIdentificacion} - {record.NombrePaciente}",
            HtmlBody = $"""
                <p>Estimada Gerencia,</p>
                <p>El despacho de clínica de heridas del siguiente paciente tiene <strong>24 horas restantes</strong>
                   para ser reclamado antes de ser desempacado.</p>
                <p><strong>Atención:</strong> {HtmlEncode(atencion)} (plan {completo.Plan.Numero})</p>
                <p><strong>Paciente:</strong> {HtmlEncode(record.NombrePaciente)}</p>
                <p><strong>Documento:</strong> {HtmlEncode(record.TipoIdentificacion)} {HtmlEncode(record.NumeroIdentificacion)}</p>
                <p><strong>Auxiliar asignado:</strong> {HtmlEncode(record.AuxiliarEnfermeriaAsignado ?? "No asignado")}</p>
                <p><strong>Dirección:</strong> {HtmlEncode(DireccionCompleta(record))}</p>
                <p><strong>Teléfonos:</strong> {HtmlEncode(TelefonosDe(record))}</p>
                <p>Por favor tomar las acciones necesarias para garantizar el retiro oportuno de la bolsa.</p>
                <br/>
                <p><em>Este es un correo automático de Especialistas en Casa</em></p>
                """
        }, cancellationToken);

        return result.Succeeded
            ? []
            : [$"No se pudo enviar alerta de vencimiento a gerencia: {result.ErrorMessage}"];
    }

    /// <summary>
    /// Recarga el kardex con su paciente, su plan y sus adjuntos: los disparadores solo tienen la
    /// entidad que acaban de guardar, sin las relaciones.
    /// </summary>
    /// <remarks>
    /// Los adjuntos se traen en una segunda consulta a propósito: combinar dos referencias con una
    /// colección en un solo Include sobre FirstOrDefault genera un SQL con alias que PostgreSQL
    /// rechaza, y además multiplicaría las filas del kardex por cada adjunto.
    /// </remarks>
    private async Task<CensoClinicaHeridasKardex?> CargarKardexCompletoAsync(
        long kardexId,
        CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoClinicaHeridasKardex
            .AsNoTracking()
            .Include(x => x.CensoClinicaHeridasRecord)
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == kardexId, cancellationToken);

        if (kardex is null)
        {
            return null;
        }

        kardex.Adjuntos = await _context.CensoClinicaHeridasKardexAdjuntos
            .AsNoTracking()
            .Where(x => x.CensoClinicaHeridasKardexId == kardexId)
            .OrderBy(x => x.UploadedAtUtc)
            .ToListAsync(cancellationToken);

        return kardex;
    }

    private static string TelefonosDe(CensoClinicaHeridasRecord record) =>
        string.Join(" / ", new[] { record.TelefonoPrincipal, record.TelefonoAdicional1, record.TelefonoAdicional2 }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string DireccionCompleta(CensoClinicaHeridasRecord record) =>
        string.Join(" ", new[] { record.Direccion, record.DetalleDireccion }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string ResolverContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".csv" => "text/csv",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" or ".xlsm" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };

    /// <summary>
    /// Copia de la requisición en HTML, con la misma estructura tabulada del documento en pantalla y
    /// el rojo corporativo en los encabezados.
    /// </summary>
    private static string BuildClinicaHeridasRequisicionHtml(ClinicaHeridasKardexDocumento documento, int numeroPlan)
    {
        var encabezados = ClinicaHeridasKardexBuilder.NormalizarEncabezados(
            documento.Encabezados,
            documento.Aplicaciones);

        var columnas = string.Concat(encabezados.Select(x => $"<th>{HtmlEncode(x)}</th>"));

        var filas = string.Join(Environment.NewLine, documento.Insumos.Select(insumo =>
        {
            var cantidades = string.Concat(Enumerable.Range(0, documento.Aplicaciones)
                .Select(i => $"<td class=\"n\">{(i < insumo.Cantidades.Count ? insumo.Cantidades[i] : 0)}</td>"));

            return $"<tr><td class=\"n\">{insumo.Item}</td><td>{HtmlEncode(insumo.Descripcion)}</td>"
                + $"{cantidades}<td class=\"n t\">{insumo.Total}</td></tr>";
        }));

        var observaciones = string.IsNullOrWhiteSpace(documento.Observaciones)
            ? string.Empty
            : $"<h2>Observaciones</h2><p class=\"obs\">{HtmlEncode(documento.Observaciones)}</p>";

        return $$"""
            <!doctype html><html lang="es"><head><meta charset="utf-8" />
            <style>
            body{font-family:Arial,Helvetica,sans-serif;color:#2b2b2b}
            h1{font-size:16px;text-align:center;margin-bottom:4px}
            h2{background:#d93111;color:#fff;font-size:12px;padding:4px 8px;margin:14px 0 0;text-transform:uppercase}
            .sub{text-align:center;font-size:12px;color:#555;margin-top:0}
            table{border-collapse:collapse;width:100%;margin-top:0}
            th,td{border:1px solid #d6d6d6;padding:4px 6px;font-size:11px;vertical-align:top}
            th{background:#f2f2f2;text-align:left}
            td.n{text-align:center}
            td.t{background:#f5f5f5;font-weight:bold}
            .obs{border:1px solid #d6d6d6;padding:6px;font-size:11px;margin:0}
            </style>
            </head><body>
            <h1>{{HtmlEncode(documento.Titulo)}}</h1>
            <p class="sub">Atención: {{HtmlEncode(documento.TipoNombre)}} · Plan {{numeroPlan}}</p>

            <h2>Datos del paciente</h2>
            <table>
            <tr><th>Paciente</th><td>{{HtmlEncode(documento.Paciente)}}</td><th>Documento</th><td>{{HtmlEncode(documento.Documento)}}</td></tr>
            <tr><th>Asegurador</th><td>{{HtmlEncode(documento.Asegurador)}}</td><th>Edad</th><td>{{HtmlEncode(documento.Edad)}}</td></tr>
            <tr><th>Dirección</th><td>{{HtmlEncode(documento.Direccion)}}</td><th>Teléfonos</th><td>{{HtmlEncode(documento.Telefonos)}}</td></tr>
            <tr><th>CIE10</th><td>{{HtmlEncode(documento.CodigoCie10)}}</td><th>Diagnóstico</th><td>{{HtmlEncode(documento.Diagnostico)}}</td></tr>
            </table>

            <h2>Tratamiento</h2>
            <table>
            <tr><th>Duración (días)</th><td>{{documento.DuracionDias}}</td><th>Frecuencia</th><td>{{HtmlEncode(documento.Frecuencia)}}</td></tr>
            <tr><th>Aplicaciones</th><td>{{documento.Aplicaciones}}</td><th>Fecha de solicitud</th><td>{{HtmlEncode(documento.FechaSolicitud)}}</td></tr>
            <tr><th>Auxiliar asignado</th><td>{{HtmlEncode(documento.AuxiliarAsignado)}}</td><th>Elaborado por</th><td>{{HtmlEncode(documento.ElaboradoPor)}}</td></tr>
            </table>

            <h2>Detalle de la requisición</h2>
            <table>
            <thead><tr><th>Item</th><th>Descripción del insumo</th>{{columnas}}<th>Total</th></tr></thead>
            <tbody>{{filas}}</tbody>
            </table>
            {{observaciones}}
            </body></html>
            """;
    }
}
