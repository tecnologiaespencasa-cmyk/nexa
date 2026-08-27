using System.Globalization;
using System.Net;
using System.Text;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;

namespace Nexa.Services;

public class EspacioCorporativoNotificationService : IEspacioCorporativoNotificationService
{
    private const string LiderTecnologiaEmail = "liderdetecnologia@especialistasencasa.com";
    private const string ColorPrimario = "#e53935";
    private const string SinDato = "No registra";

    private readonly IEmailService _emailService;
    private readonly ILogger<EspacioCorporativoNotificationService> _logger;

    public EspacioCorporativoNotificationService(
        IEmailService emailService,
        ILogger<EspacioCorporativoNotificationService> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<bool> NotifyNovedadCreadaAsync(
        EspacioActivoNovedad novedad,
        EspacioActivo? activo,
        CancellationToken cancellationToken = default)
    {
        var equipo = DescribirEquipo(novedad, activo);
        var subject = $"[Nexa] Nueva novedad de equipo #{novedad.Id} - {novedad.Tipo} - {equipo}";

        var body = new StringBuilder();
        body.Append(BuildHeader("Nueva novedad reportada", $"Novedad #{novedad.Id} &middot; {WebUtility.HtmlEncode(novedad.Tipo)}"));
        body.Append(BuildSectionTitle("Datos de la novedad"));
        body.Append(BuildTable(
        [
            ("Número de novedad", novedad.Id.ToString()),
            ("Tipo de novedad", novedad.Tipo),
            ("Estado", novedad.Estado),
            ("Fecha de reporte", ColombiaTime.Convert(novedad.CreatedAtUtc).ToString("dd/MM/yyyy hh:mm tt")),
            ("Descripción", novedad.Descripcion)
        ]));

        body.Append(BuildSectionTitle("Datos del responsable"));
        body.Append(BuildTable(
        [
            ("Nombre", novedad.ReportadoPorNombre),
            ("Correo", novedad.ReportadoPorEmail)
        ]));

        body.Append(BuildSectionTitle("Datos del equipo"));
        if (activo is not null)
        {
            body.Append(BuildTable(
            [
                ("Tipo de activo", activo.TipoActivo),
                ("Nombre del equipo", activo.NombreEquipo),
                ("Marca", activo.Marca),
                ("Serie", activo.Serie),
                ("Serial", activo.Serial),
                ("Código de activo", activo.CodigoActivo),
                ("Especificaciones", activo.Especificaciones),
                ("Estado del activo", activo.Estado),
                ("Responsable asignado", activo.ResponsableNombre),
                ("Fecha de asignación", activo.FechaAsignacionUtc.HasValue
                    ? ColombiaTime.Convert(activo.FechaAsignacionUtc.Value).ToString("dd/MM/yyyy")
                    : null),
                ("Nota del activo", activo.Nota)
            ]));
        }
        else
        {
            body.Append(BuildTable(
            [
                ("Equipo reportado", novedad.EquipoReferencia),
                ("Observación", "La novedad no está asociada a un activo registrado en el inventario.")
            ]));
        }

        body.Append(BuildFooter());

        return await SendAsync(
            [LiderTecnologiaEmail],
            subject,
            body.ToString(),
            $"novedad {novedad.Id} creada",
            cancellationToken);
    }

    public async Task<bool> NotifyNovedadActualizadaAsync(
        EspacioActivoNovedad novedad,
        EspacioActivo? activo,
        string estadoAnterior,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(novedad.ReportadoPorEmail))
        {
            return false;
        }

        var equipo = DescribirEquipo(novedad, activo);
        var subject = $"[Nexa] Tu novedad #{novedad.Id} cambió a {novedad.Estado}";

        var body = new StringBuilder();
        body.Append(BuildHeader("Actualización de tu novedad", $"Novedad #{novedad.Id} &middot; {WebUtility.HtmlEncode(equipo)}"));
        body.Append(BuildSectionTitle("Seguimiento"));
        body.Append(BuildTable(
        [
            ("Tipo de novedad", novedad.Tipo),
            ("Estado anterior", estadoAnterior),
            ("Estado actual", novedad.Estado),
            ("Clasificación", novedad.Clasificacion),
            ("Prioridad", novedad.Prioridad),
            ("Atendida por", novedad.AtendidoPorNombre),
            ("Respuesta del área de TI", novedad.RespuestaAdmin)
        ]));
        body.Append(BuildSectionTitle("Tu reporte original"));
        body.Append(BuildTable(
        [
            ("Equipo", equipo),
            ("Descripción", novedad.Descripcion),
            ("Fecha de reporte", ColombiaTime.Convert(novedad.CreatedAtUtc).ToString("dd/MM/yyyy hh:mm tt"))
        ]));
        body.Append(BuildFooter());

        return await SendAsync(
            [novedad.ReportadoPorEmail],
            subject,
            body.ToString(),
            $"novedad {novedad.Id} actualizada",
            cancellationToken);
    }

    public async Task<ServiceResult> EnviarCopiaActaAsync(
        EspacioActaDocumental acta,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(acta.CorreoRecibe))
        {
            return ServiceResult.Failure("El acta no tiene correo de destino.");
        }

        var documento = ConstruirDocumentoActa(acta);
        var fecha = ColombiaTime.Convert(acta.FirmadaAtUtc).ToString("dd/MM/yyyy hh:mm tt");

        var cuerpo = $"""
            <div style="font-family:'Segoe UI',Arial,sans-serif;max-width:720px;margin:0 auto;color:#2b2f36;">
              <div style="background:linear-gradient(135deg,{ColorPrimario},#a50f0f);color:#fff;padding:20px 24px;border-radius:14px 14px 0 0;">
                <div style="font-size:12px;letter-spacing:.14em;text-transform:uppercase;opacity:.85;">Especialistas en Casa &middot; Nexa</div>
                <div style="font-size:20px;font-weight:700;margin-top:6px;">{WebUtility.HtmlEncode(acta.TituloActa)}</div>
                <div style="font-size:13px;margin-top:4px;opacity:.9;">Acta N&deg; {acta.Id} &middot; {fecha}</div>
              </div>
              <div style="border:1px solid #f0d5d5;border-top:0;border-radius:0 0 14px 14px;padding:22px 24px;background:#fff;">
                <p style="margin:0 0 14px;">Hola {WebUtility.HtmlEncode(acta.NombreRecibe)},</p>
                <p style="margin:0 0 18px;">
                  Adjuntamos la copia del acta que acabas de firmar. Puedes abrir el archivo adjunto
                  para verla con el formato original y guardarla o imprimirla.
                </p>
                {documento.CuerpoParaCorreo}
                <p style="margin:22px 0 0;font-size:12px;color:#8a929c;">
                  Este mensaje fue generado automáticamente por Nexa - Especialistas en Casa. No respondas a este correo.
                </p>
              </div>
            </div>
            """;

        try
        {
            return await _emailService.SendAsync(
                new EmailMessage
                {
                    To = [acta.CorreoRecibe],
                    Subject = $"[Especialistas en Casa] {acta.TituloActa} - Acta N° {acta.Id}",
                    HtmlBody = cuerpo,
                    Attachments =
                    [
                        new EmailAttachment
                        {
                            FileName = $"Acta-{acta.Id}.html",
                            ContentType = "text/html",
                            Content = Encoding.UTF8.GetBytes(documento.HtmlCompleto)
                        }
                    ]
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando la copia del acta {ActaId}.", acta.Id);
            return ServiceResult.Failure("No fue posible enviar el correo con la copia del acta.");
        }
    }

    private sealed record DocumentoActa(string HtmlCompleto, string CuerpoParaCorreo);

    // Cadena sin interpolar: en un raw string interpolado las llaves de CSS habría que
    // escaparlas, así que el estilo vive aparte y se inserta como un único marcador.
    private const string EstilosActaAdjunta = """
        body{background:#f3f5f8;font-family:'Segoe UI',Arial,sans-serif;margin:0;padding:2rem 1rem;color:#24272e;}
        .hoja{background:#fff;border-radius:14px;box-shadow:0 18px 40px rgba(20,25,35,.1);margin:0 auto;max-width:820px;padding:2.4rem 2.6rem;}
        h1{border-bottom:3px solid #e53935;color:#a50f0f;font-size:1.25rem;padding-bottom:.6rem;}
        h2{color:#a50f0f;font-size:.8rem;letter-spacing:.08em;text-transform:uppercase;margin-top:1.6rem;}
        a{color:#1a5fa8;}
        @media print{body{background:#fff;padding:0;}.hoja{box-shadow:none;max-width:none;padding:0;}}
        """;

    /// <summary>
    /// Arma el acta como HTML autocontenido. Se envía dos veces: incrustada en el cuerpo del
    /// correo y como adjunto, porque varios clientes bloquean las imágenes en data URI.
    /// </summary>
    private static DocumentoActa ConstruirDocumentoActa(EspacioActaDocumental acta)
    {
        var fecha = ColombiaTime.Convert(acta.FirmadaAtUtc).ToString("dd/MM/yyyy hh:mm tt");
        var firmas = ConstruirFirmasActa(acta);

        var cuerpo = $"""
            <div style="font-size:14px;line-height:1.6;color:#24272e;">
              <h1 style="font-size:17px;font-weight:800;margin:0 0 16px;">{WebUtility.HtmlEncode(acta.TituloActa)}</h1>
              {acta.CuerpoHtml}
              {firmas}
            </div>
            """;

        var completo = $"""
            <!DOCTYPE html>
            <html lang="es"><head><meta charset="utf-8" />
            <title>{WebUtility.HtmlEncode(acta.TituloActa)} - Acta {acta.Id}</title>
            <style>{EstilosActaAdjunta}</style></head>
            <body><div class="hoja">
              <h1>{WebUtility.HtmlEncode(acta.TituloActa)}</h1>
              <p style="color:#7b8490;font-size:.82rem;margin-top:-.4rem;">Acta N&deg; {acta.Id} &middot; {fecha}</p>
              {acta.CuerpoHtml}
              {firmas}
              <p style="border-top:1px solid #eceff3;color:#8a929c;font-size:.74rem;margin-top:2rem;padding-top:.8rem;text-align:center;">
                Documento generado electrónicamente por Nexa &middot; Especialistas en Casa.
              </p>
            </div></body></html>
            """;

        return new DocumentoActa(completo, cuerpo);
    }

    /// <summary>
    /// Bloque de firmas del acta. Se arma como tabla porque es lo único que se comporta
    /// igual en todos los clientes de correo, y reparte el ancho entre las firmas que
    /// traiga el acta: dos en las de siempre, más si la plantilla las declaró.
    /// </summary>
    private static string ConstruirFirmasActa(EspacioActaDocumental acta)
    {
        var firmas = EspacioActaFirmas.Leer(acta);
        var ancho = (100d / Math.Max(firmas.Count, 1)).ToString("0.##", CultureInfo.InvariantCulture);

        var celdas = new StringBuilder();

        foreach (var firma in firmas)
        {
            celdas.Append(
                $"""
                 <td style="width:{ancho}%;text-align:center;vertical-align:bottom;padding:0 12px;">
                   <img src="{firma.DataUrl}" alt="Firma" style="height:82px;object-fit:contain;max-width:100%;" />
                   <div style="border-top:1.5px solid #24272e;margin-top:2px;padding-top:6px;font-size:14px;font-weight:700;">
                     {WebUtility.HtmlEncode(firma.Nombre)}
                   </div>
                   {(string.IsNullOrWhiteSpace(firma.Cargo)
                       ? string.Empty
                       : $"""<div style="font-size:12px;color:#7b8490;">{WebUtility.HtmlEncode(firma.Cargo)}</div>""")}
                   {(string.IsNullOrWhiteSpace(firma.Documento)
                       ? string.Empty
                       : $"""<div style="font-size:12px;color:#7b8490;">C.C. {WebUtility.HtmlEncode(firma.Documento)}</div>""")}
                   <div style="font-size:12px;color:#7b8490;">{WebUtility.HtmlEncode(firma.Rotulo)}</div>
                 </td>
                 """);
        }

        return $"""
            <table style="width:100%;margin-top:34px;border-collapse:collapse;">
              <tr>{celdas}</tr>
            </table>
            """;
    }

    private async Task<bool> SendAsync(
        IReadOnlyList<string> to,
        string subject,
        string htmlBody,
        string contexto,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _emailService.SendAsync(
                new EmailMessage
                {
                    To = to,
                    Subject = subject,
                    HtmlBody = htmlBody
                },
                cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "No se pudo enviar el correo de {Contexto} del espacio corporativo: {Error}",
                    contexto,
                    result.ErrorMessage);
            }

            return result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado enviando el correo de {Contexto} del espacio corporativo.", contexto);
            return false;
        }
    }

    private static string DescribirEquipo(EspacioActivoNovedad novedad, EspacioActivo? activo)
    {
        if (activo is null)
        {
            return string.IsNullOrWhiteSpace(novedad.EquipoReferencia)
                ? "Equipo no registrado"
                : novedad.EquipoReferencia;
        }

        var nombre = string.IsNullOrWhiteSpace(activo.NombreEquipo)
            ? $"{activo.TipoActivo} {activo.Marca}".Trim()
            : activo.NombreEquipo;

        return $"{nombre} (Serial {activo.Serial})";
    }

    private static string BuildHeader(string titulo, string subtitulo) => $"""
        <div style="font-family:'Segoe UI',Arial,sans-serif;max-width:680px;margin:0 auto;color:#2b2f36;">
          <div style="background:linear-gradient(135deg,{ColorPrimario},#a50f0f);color:#ffffff;padding:22px 26px;border-radius:14px 14px 0 0;">
            <div style="font-size:12px;letter-spacing:.14em;text-transform:uppercase;opacity:.85;">Mi espacio corporativo &middot; Nexa</div>
            <div style="font-size:22px;font-weight:700;margin-top:6px;">{WebUtility.HtmlEncode(titulo)}</div>
            <div style="font-size:14px;margin-top:4px;opacity:.9;">{subtitulo}</div>
          </div>
          <div style="border:1px solid #f0d5d5;border-top:0;border-radius:0 0 14px 14px;padding:22px 26px;background:#ffffff;">
        """;

    private static string BuildSectionTitle(string titulo) => $"""
        <div style="font-size:13px;font-weight:700;text-transform:uppercase;letter-spacing:.08em;color:{ColorPrimario};margin:18px 0 8px;">{WebUtility.HtmlEncode(titulo)}</div>
        """;

    private static string BuildTable(IReadOnlyList<(string Label, string? Value)> rows)
    {
        var builder = new StringBuilder();
        builder.Append("""<table style="width:100%;border-collapse:collapse;font-size:14px;">""");

        foreach (var (label, value) in rows)
        {
            var displayValue = string.IsNullOrWhiteSpace(value) ? SinDato : value.Trim();
            builder.Append($"""
                <tr>
                  <td style="padding:7px 10px;background:#fff6f6;border:1px solid #f4e0e0;width:38%;font-weight:600;color:#7c1f1f;">{WebUtility.HtmlEncode(label)}</td>
                  <td style="padding:7px 10px;border:1px solid #f4e0e0;">{WebUtility.HtmlEncode(displayValue).Replace("\n", "<br />")}</td>
                </tr>
                """);
        }

        builder.Append("</table>");
        return builder.ToString();
    }

    private static string BuildFooter() => """
            <p style="margin:22px 0 0;font-size:12px;color:#8a929c;">
              Este mensaje fue generado automáticamente por Nexa - Especialistas en Casa. No respondas a este correo.
            </p>
          </div>
        </div>
        """;
}
