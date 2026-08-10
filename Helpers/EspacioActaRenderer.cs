using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Nexa.Models.EspacioCorporativo;

namespace Nexa.Helpers;

/// <summary>
/// Sustituye los marcadores {{clave}} de una plantilla por los valores capturados.
///
/// El cuerpo de la plantilla es HTML de confianza (vive en el código), pero los valores
/// vienen del formulario: se codifican SIEMPRE antes de insertarlos, de modo que no puedan
/// inyectar marcado en el acta.
/// </summary>
public static partial class EspacioActaRenderer
{
    private static readonly CultureInfo CulturaColombia = CultureInfo.GetCultureInfo("es-CO");
    private const string Ciudad = "Medellín";

    [GeneratedRegex(@"\{\{([a-z0-9_]+)\}\}", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MarcadorPattern { get; }

    [GeneratedRegex(@"^https?://[^\s<>""]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UrlPattern { get; }

    public sealed record DatosFirmante(string Nombre, string Documento, string Cargo);

    public static string Render(
        EspacioActaPlantilla plantilla,
        IReadOnlyDictionary<string, string?> valores,
        DatosFirmante firmante,
        DateTime fecha)
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["__ciudad"] = WebUtility.HtmlEncode(Ciudad),
            ["__fecha_dia"] = fecha.ToString("dd", CulturaColombia),
            ["__fecha_mes"] = CulturaColombia.DateTimeFormat.GetMonthName(fecha.Month),
            ["__fecha_anio"] = fecha.Year.ToString(CultureInfo.InvariantCulture),
            ["__fecha_completa"] = fecha.ToString("dd 'de' MMMM 'de' yyyy", CulturaColombia),
            ["__firmante_nombre"] = WebUtility.HtmlEncode(firmante.Nombre),
            ["__firmante_documento"] = WebUtility.HtmlEncode(firmante.Documento),
            ["__firmante_cargo"] = WebUtility.HtmlEncode(firmante.Cargo)
        };

        foreach (var campo in plantilla.Campos)
        {
            valores.TryGetValue(campo.Clave, out var valor);
            tokens[campo.Clave] = FormatearValor(campo, valor);
        }

        return MarcadorPattern.Replace(
            plantilla.CuerpoHtml,
            match => tokens.TryGetValue(match.Groups[1].Value, out var reemplazo)
                ? reemplazo
                : string.Empty);
    }

    private static string FormatearValor(EspacioActaCampo campo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return "<em>No registra</em>";
        }

        var limpio = valor.Trim();

        return campo.Tipo switch
        {
            EspacioActaTipoCampo.Enlaces => FormatearEnlaces(limpio),
            EspacioActaTipoCampo.TextoLargo => WebUtility.HtmlEncode(limpio).Replace("\n", "<br />"),
            _ => WebUtility.HtmlEncode(limpio)
        };
    }

    /// <summary>
    /// Convierte una lista de URLs en anclas. Solo se enlaza lo que sea http/https;
    /// cualquier otra cosa se imprime como texto plano codificado.
    /// </summary>
    private static string FormatearEnlaces(string valor)
    {
        var partes = valor
            .Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (partes.Count == 0)
        {
            return "<em>No registra</em>";
        }

        var builder = new StringBuilder();
        for (var i = 0; i < partes.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var parte = partes[i];
            var codificado = WebUtility.HtmlEncode(parte);

            if (UrlPattern.IsMatch(parte))
            {
                builder.Append(
                    $"""<a href="{codificado}" target="_blank" rel="noopener noreferrer">{codificado}</a>""");
            }
            else
            {
                builder.Append(codificado);
            }
        }

        return builder.ToString();
    }

    /// <summary>Enmascara una credencial para mostrarla en listados sin exponerla completa.</summary>
    public static string Enmascarar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return "—";
        }

        var limpio = valor.Trim();
        return limpio.Length <= 2
            ? new string('•', limpio.Length)
            : $"{limpio[0]}{new string('•', Math.Min(limpio.Length - 2, 8))}{limpio[^1]}";
    }
}
