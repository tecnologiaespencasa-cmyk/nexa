using System.Net;
using System.Text;
using IntranetPrueba.Data.Entities;
using IntranetPrueba.Helpers;
using IntranetPrueba.Services.Interfaces;
using IntranetPrueba.Services.Models;

namespace IntranetPrueba.Services;

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
            ("Numero de novedad", novedad.Id.ToString()),
            ("Tipo de novedad", novedad.Tipo),
            ("Estado", novedad.Estado),
            ("Fecha de reporte", ColombiaTime.Convert(novedad.CreatedAtUtc).ToString("dd/MM/yyyy hh:mm tt")),
            ("Descripcion", novedad.Descripcion)
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
                ("Codigo de activo", activo.CodigoActivo),
                ("Especificaciones", activo.Especificaciones),
                ("Estado del activo", activo.Estado),
                ("Responsable asignado", activo.ResponsableNombre),
                ("Fecha de asignacion", activo.FechaAsignacionUtc.HasValue
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
                ("Observacion", "La novedad no esta asociada a un activo registrado en el inventario.")
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
        var subject = $"[Nexa] Tu novedad #{novedad.Id} cambio a {novedad.Estado}";

        var body = new StringBuilder();
        body.Append(BuildHeader("Actualizacion de tu novedad", $"Novedad #{novedad.Id} &middot; {WebUtility.HtmlEncode(equipo)}"));
        body.Append(BuildSectionTitle("Seguimiento"));
        body.Append(BuildTable(
        [
            ("Tipo de novedad", novedad.Tipo),
            ("Estado anterior", estadoAnterior),
            ("Estado actual", novedad.Estado),
            ("Clasificacion", novedad.Clasificacion),
            ("Prioridad", novedad.Prioridad),
            ("Atendida por", novedad.AtendidoPorNombre),
            ("Respuesta del area de TI", novedad.RespuestaAdmin)
        ]));
        body.Append(BuildSectionTitle("Tu reporte original"));
        body.Append(BuildTable(
        [
            ("Equipo", equipo),
            ("Descripcion", novedad.Descripcion),
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
              Este mensaje fue generado automaticamente por Nexa - Especialistas en Casa. No respondas a este correo.
            </p>
          </div>
        </div>
        """;
}
