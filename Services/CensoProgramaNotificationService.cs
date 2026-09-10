using System.Net;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;

namespace Nexa.Services;

/// <summary>
/// Aviso por correo a programas especiales cuando un paciente entra a clínica de heridas,
/// crónicos o NPT. Agudos y terapia ambulatoria no se notifican: ese equipo no los atiende.
///
/// El correo habla el mismo lenguaje de color que la pantalla del censo —cada programa tiene su
/// matiz y se repite aquí en la franja superior y en la insignia—, de modo que quien lo recibe
/// reconoce el programa antes de leer una sola palabra. Lo demás es deliberadamente sobrio: una
/// tabla de datos y un pie.
///
/// Está armado con tablas y estilos en línea porque es lo único que se comporta igual en todos
/// los clientes de correo; en Outlook no hay flexbox ni grid.
/// </summary>
public sealed class CensoProgramaNotificationService(
    IEmailService emailService,
    ILogger<CensoProgramaNotificationService> logger) : ICensoProgramaNotificationService
{
    private const string DestinatarioPorDefecto = "programasespeciales@especialistasencasa.com";

    /// <summary>Los tres programas que atiende programas especiales, con su matiz del censo.</summary>
    private static readonly Dictionary<string, string> MatizPorPrograma = new(StringComparer.Ordinal)
    {
        [CensoProgramas.ClinicaHeridas] = "#0f9d76",
        [CensoProgramas.Cronicos] = "#6b4de6",
        [CensoProgramas.Npt] = "#d97400"
    };

    public async Task<string> NotificarProgramaAgregadoAsync(
        CensoPaciente paciente,
        string programa,
        string agregadoPor,
        CancellationToken cancellationToken)
    {
        if (!MatizPorPrograma.TryGetValue(programa, out var matiz))
        {
            return string.Empty;
        }

        var destino = Environment.GetEnvironmentVariable("PROGRAMAS_ESPECIALES_EMAIL")?.Trim();
        if (string.IsNullOrWhiteSpace(destino))
        {
            destino = DestinatarioPorDefecto;
        }

        var nombrePrograma = CensoProgramas.Nombre(programa);
        var mensaje = new EmailMessage
        {
            To = [destino],
            Subject = $"{nombrePrograma}: {paciente.NombrePaciente}",
            HtmlBody = ConstruirCuerpo(paciente, nombrePrograma, matiz, agregadoPor)
        };

        try
        {
            var resultado = await emailService.SendAsync(mensaje, cancellationToken);
            if (!resultado.Succeeded)
            {
                logger.LogWarning(
                    "No se pudo avisar a programas especiales del programa {Programa} del paciente {Documento}: {Error}",
                    programa, paciente.NumeroIdentificacion, resultado.ErrorMessage);
                return "El programa quedó agregado, pero no se pudo enviar el aviso a programas especiales.";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error avisando a programas especiales del programa {Programa} del paciente {Documento}.",
                programa, paciente.NumeroIdentificacion);
            return "El programa quedó agregado, pero no se pudo enviar el aviso a programas especiales.";
        }

        return string.Empty;
    }

    private static string ConstruirCuerpo(
        CensoPaciente paciente,
        string nombrePrograma,
        string matiz,
        string agregadoPor)
    {
        var nombre = WebUtility.HtmlEncode(paciente.NombrePaciente ?? string.Empty);
        var documento = WebUtility.HtmlEncode(
            $"{paciente.TipoIdentificacion} {paciente.NumeroIdentificacion}".Trim());
        var programa = WebUtility.HtmlEncode(nombrePrograma);
        var usuario = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(agregadoPor) ? "Sistema" : agregadoPor);
        var cuando = WebUtility.HtmlEncode(
            ColombiaTime.Convert(DateTime.UtcNow).ToString("dd/MM/yyyy hh:mm tt"));

        var edad = paciente.Edad > 0 ? $"{paciente.Edad} años" : "—";
        var diagnostico = string.IsNullOrWhiteSpace(paciente.DiagnosticoDescriptivo)
            ? "Sin diagnóstico registrado"
            : WebUtility.HtmlEncode(paciente.DiagnosticoDescriptivo);
        var municipio = string.IsNullOrWhiteSpace(paciente.MunicipioResidencia)
            ? "—"
            : WebUtility.HtmlEncode(paciente.MunicipioResidencia);

        return $"""
            <div style="background:#f3f5f8;padding:24px 12px;font-family:'Segoe UI',Arial,sans-serif;">
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="max-width:600px;margin:0 auto;">
                <tr>
                  <td style="background:#ffffff;border-radius:14px 14px 0 0;border-top:5px solid {matiz};padding:26px 30px 4px;">
                    <p style="margin:0;font-size:11px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;color:#8a929c;">
                      Nuevo programa asignado
                    </p>
                    <p style="margin:10px 0 0;font-size:22px;font-weight:800;line-height:1.2;color:#24272e;">
                      {nombre}
                    </p>
                    <p style="margin:12px 0 0;">
                      <span style="display:inline-block;background:{matiz};color:#ffffff;border-radius:999px;padding:5px 14px;font-size:12px;font-weight:700;">
                        {programa}
                      </span>
                    </p>
                  </td>
                </tr>
                <tr>
                  <td style="background:#ffffff;padding:22px 30px 6px;">
                    <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="font-size:14px;color:#24272e;">
                      {Fila("Documento", documento)}
                      {Fila("Edad", edad)}
                      {Fila("Municipio", municipio)}
                      {Fila("Diagnóstico", diagnostico)}
                      {Fila("Lo agregó", usuario)}
                      {Fila("Fecha", cuando)}
                    </table>
                  </td>
                </tr>
                <tr>
                  <td style="background:#ffffff;border-radius:0 0 14px 14px;padding:18px 30px 26px;">
                    <p style="margin:0;font-size:13px;line-height:1.6;color:#5b626b;">
                      El paciente ya aparece en el censo con este programa. Su ficha se abre buscando
                      el documento en Nexa.
                    </p>
                    <p style="margin:22px 0 0;border-top:1px solid #eceff3;padding-top:12px;font-size:11px;color:#8a929c;">
                      Aviso automático de Nexa &middot; Especialistas en Casa
                    </p>
                  </td>
                </tr>
              </table>
            </div>
            """;
    }

    private static string Fila(string etiqueta, string valor) => $"""
        <tr>
          <td style="padding:7px 16px 7px 0;color:#8a929c;font-size:12px;white-space:nowrap;vertical-align:top;">{etiqueta}</td>
          <td style="padding:7px 0;font-weight:600;">{valor}</td>
        </tr>
        """;
}
