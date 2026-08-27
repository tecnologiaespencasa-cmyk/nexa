using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Nexa.Models.EspacioCorporativo;

namespace Nexa.Helpers;

/// <summary>
/// Convierte una plantilla y los valores capturados en el HTML del acta.
///
/// Hay dos orígenes de cuerpo:
///   • Plantillas de fábrica: HTML de confianza con marcadores {{clave}}.
///   • Plantillas del diseñador: bloques que el administrador redactó desde la
///     interfaz. Ese texto NO es de confianza, así que se codifica siempre y solo
///     después se le aplican el formato en línea y la sustitución de marcadores.
///
/// En ambos casos los valores del formulario se codifican antes de insertarse, de
/// modo que no puedan inyectar marcado en el acta.
/// </summary>
public static partial class EspacioActaRenderer
{
    private static readonly CultureInfo CulturaColombia = CultureInfo.GetCultureInfo("es-CO");
    private const string Ciudad = "Medellín";
    private const string SinDato = "<em>No registra</em>";

    [GeneratedRegex(@"\{\{([a-z0-9_]+)\}\}", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MarcadorPattern { get; }

    [GeneratedRegex(@"^https?://[^\s<>""]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UrlPattern { get; }

    [GeneratedRegex(@"\*\*(?<t>[^*\r\n]+)\*\*", RegexOptions.Compiled)]
    private static partial Regex NegritaPattern { get; }

    [GeneratedRegex(@"(?<!\*)\*(?<t>[^*\r\n]+)\*(?!\*)", RegexOptions.Compiled)]
    private static partial Regex CursivaPattern { get; }

    public sealed record DatosFirmante(string Nombre, string Documento, string Cargo);

    // ─────────────────────────────────────────────────────────────────────────
    // Entrada principal
    // ─────────────────────────────────────────────────────────────────────────

    /// <param name="resaltarVariables">
    /// Marca en el documento lo que cambia de un acta a otra. Solo se usa en la
    /// previsualización del diseñador; el acta que se firma nunca se resalta.
    /// </param>
    public static string Render(
        EspacioActaPlantilla plantilla,
        IReadOnlyDictionary<string, string?> valores,
        DatosFirmante firmante,
        DateTime fecha,
        bool resaltarVariables = false)
    {
        var tokens = ConstruirTokens(plantilla.Campos, valores, firmante, fecha, resaltarVariables);

        return plantilla.Bloques.Count > 0
            ? RenderBloques(plantilla.Bloques, plantilla.Campos, tokens, plantilla.NumerarTitulos)
            : Sustituir(plantilla.CuerpoHtml ?? string.Empty, tokens);
    }

    /// <summary>Tabla de marcadores: los del sistema más un valor ya formateado por campo.</summary>
    private static Dictionary<string, string> ConstruirTokens(
        IReadOnlyList<EspacioActaCampo> campos,
        IReadOnlyDictionary<string, string?> valores,
        DatosFirmante firmante,
        DateTime fecha,
        bool resaltarVariables = false)
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

        foreach (var campo in campos)
        {
            valores.TryGetValue(campo.Clave, out var valor);
            var formateado = FormatearValor(campo, valor);

            tokens[campo.Clave] = resaltarVariables
                ? $"""<span class="acta-var" title="{WebUtility.HtmlEncode(campo.Etiqueta)}">{formateado}</span>"""
                : formateado;
        }

        return tokens;
    }

    private static string Sustituir(string plantilla, IReadOnlyDictionary<string, string> tokens) =>
        MarcadorPattern.Replace(
            plantilla,
            match => tokens.TryGetValue(match.Groups[1].Value, out var reemplazo)
                ? reemplazo
                : string.Empty);

    // ─────────────────────────────────────────────────────────────────────────
    // Bloques del diseñador
    // ─────────────────────────────────────────────────────────────────────────

    private static string RenderBloques(
        IReadOnlyList<EspacioActaBloque> bloques,
        IReadOnlyList<EspacioActaCampo> campos,
        IReadOnlyDictionary<string, string> tokens,
        bool numerarTitulos)
    {
        var html = new StringBuilder();
        var numeroTitulo = 0;

        foreach (var bloque in bloques)
        {
            switch (bloque.Tipo)
            {
                case EspacioActaTipoBloque.Titulo:
                {
                    if (string.IsNullOrWhiteSpace(bloque.Texto))
                    {
                        break;
                    }

                    numeroTitulo++;
                    var prefijo = numerarTitulos
                        ? $"{numeroTitulo.ToString(CultureInfo.InvariantCulture)}. "
                        : string.Empty;

                    html.Append("<h2>").Append(prefijo).Append(Inline(bloque.Texto, tokens)).Append("</h2>");
                    break;
                }

                case EspacioActaTipoBloque.Parrafo:
                {
                    if (string.IsNullOrWhiteSpace(bloque.Texto))
                    {
                        break;
                    }

                    html.Append("<p>").Append(Inline(bloque.Texto, tokens).Replace("\n", "<br />")).Append("</p>");
                    break;
                }

                case EspacioActaTipoBloque.Lista:
                {
                    var items = bloque.Texto
                        .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    if (items.Length == 0)
                    {
                        break;
                    }

                    html.Append("<ul>");
                    foreach (var item in items)
                    {
                        html.Append("<li>").Append(Inline(item, tokens)).Append("</li>");
                    }

                    html.Append("</ul>");
                    break;
                }

                case EspacioActaTipoBloque.Datos:
                {
                    var lineas = bloque.Campos
                        .Select(clave => campos.FirstOrDefault(c =>
                            string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase)))
                        .Where(campo => campo is not null)
                        .ToList();

                    if (lineas.Count == 0)
                    {
                        break;
                    }

                    html.Append("<ul>");
                    foreach (var campo in lineas)
                    {
                        html.Append("<li><strong>")
                            .Append(WebUtility.HtmlEncode(campo!.Etiqueta))
                            .Append(":</strong> ")
                            .Append(tokens.GetValueOrDefault(campo.Clave, SinDato))
                            .Append("</li>");
                    }

                    html.Append("</ul>");
                    break;
                }

                case EspacioActaTipoBloque.Nota:
                {
                    if (string.IsNullOrWhiteSpace(bloque.Texto))
                    {
                        break;
                    }

                    // Estilo en línea: el acta también viaja por correo, donde no hay hoja de estilos.
                    html.Append(
                            """<p class="acta-nota" style="background:#fff3f3;border-left:3px solid #e53935;border-radius:8px;padding:.7rem .9rem;">""")
                        .Append(Inline(bloque.Texto, tokens).Replace("\n", "<br />"))
                        .Append("</p>");
                    break;
                }

                case EspacioActaTipoBloque.Separador:
                    html.Append("""<hr style="border:0;border-top:1px solid #e6e9ee;margin:1.5rem 0;" />""");
                    break;
            }
        }

        return html.ToString();
    }

    /// <summary>
    /// Texto del administrador → HTML seguro. El orden importa: primero se codifica,
    /// después se aplica el formato en línea (los marcadores no llevan asteriscos, así
    /// que sobreviven intactos) y por último se sustituyen los marcadores por valores
    /// que ya vienen codificados.
    /// </summary>
    private static string Inline(string texto, IReadOnlyDictionary<string, string> tokens)
    {
        var codificado = WebUtility.HtmlEncode(texto.Trim());
        codificado = NegritaPattern.Replace(codificado, "<strong>${t}</strong>");
        codificado = CursivaPattern.Replace(codificado, "<em>${t}</em>");
        return Sustituir(codificado, tokens);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Formato de valores
    // ─────────────────────────────────────────────────────────────────────────

    private static string FormatearValor(EspacioActaCampo campo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return campo.Tipo == EspacioActaTipoCampo.Casilla ? "No" : SinDato;
        }

        var limpio = valor.Trim();

        return campo.Tipo switch
        {
            EspacioActaTipoCampo.Enlaces => FormatearEnlaces(limpio),
            EspacioActaTipoCampo.TextoLargo => WebUtility.HtmlEncode(limpio).Replace("\n", "<br />"),
            EspacioActaTipoCampo.Numero => FormatearNumero(limpio, "N0"),
            EspacioActaTipoCampo.Decimal => FormatearNumero(limpio, "N2"),
            EspacioActaTipoCampo.Moneda => FormatearMoneda(limpio),
            EspacioActaTipoCampo.Fecha => FormatearFecha(limpio),
            EspacioActaTipoCampo.Hora => FormatearHora(limpio),
            EspacioActaTipoCampo.Casilla => EsAfirmativo(limpio) ? "Sí" : "No",
            _ => WebUtility.HtmlEncode(limpio)
        };
    }

    public static bool EsAfirmativo(string? valor) =>
        !string.IsNullOrWhiteSpace(valor)
        && (valor.Trim() is "true" or "on" or "1"
            || valor.Trim().Equals("si", StringComparison.OrdinalIgnoreCase)
            || valor.Trim().Equals("sí", StringComparison.OrdinalIgnoreCase));

    private static string FormatearNumero(string valor, string formato) =>
        decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero)
            ? numero.ToString(formato, CulturaColombia)
            : WebUtility.HtmlEncode(valor);

    private static string FormatearMoneda(string valor)
    {
        if (!decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero))
        {
            return WebUtility.HtmlEncode(valor);
        }

        // El peso colombiano se escribe sin decimales salvo que el importe los traiga.
        return numero == decimal.Truncate(numero)
            ? numero.ToString("C0", CulturaColombia)
            : numero.ToString("C2", CulturaColombia);
    }

    private static string FormatearFecha(string valor) =>
        DateTime.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha.ToString("dd 'de' MMMM 'de' yyyy", CulturaColombia)
            : WebUtility.HtmlEncode(valor);

    private static string FormatearHora(string valor) =>
        DateTime.TryParseExact(valor, ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora)
            ? hora.ToString("hh:mm tt", CulturaColombia)
            : WebUtility.HtmlEncode(valor);

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
            return SinDato;
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

    // ─────────────────────────────────────────────────────────────────────────
    // Apoyo al diseñador
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Marcadores escritos en los bloques que no corresponden a ningún campo ni a un
    /// marcador del sistema. Sirve para avisar al administrador antes de guardar.
    /// </summary>
    public static IReadOnlyList<string> MarcadoresDesconocidos(
        IReadOnlyList<EspacioActaBloque> bloques,
        IReadOnlyList<EspacioActaCampo> campos)
    {
        var conocidas = new HashSet<string>(
            campos.Select(x => x.Clave),
            StringComparer.OrdinalIgnoreCase);

        var desconocidos = new List<string>();

        foreach (var bloque in bloques)
        {
            foreach (Match match in MarcadorPattern.Matches(bloque.Texto ?? string.Empty))
            {
                var clave = match.Groups[1].Value;

                if (!conocidas.Contains(clave)
                    && !EspacioActaPlantillas.EsMarcadorDelSistema(clave)
                    && !desconocidos.Contains(clave, StringComparer.OrdinalIgnoreCase))
                {
                    desconocidos.Add(clave);
                }
            }
        }

        return desconocidos;
    }

    /// <summary>Valores de muestra para previsualizar una plantilla sin diligenciarla.</summary>
    public static Dictionary<string, string?> ValoresDeMuestra(IReadOnlyList<EspacioActaCampo> campos)
    {
        var muestra = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var campo in campos)
        {
            muestra[campo.Clave] = campo.Tipo switch
            {
                EspacioActaTipoCampo.Seleccion => campo.Opciones.FirstOrDefault()?.Valor ?? "Opción",
                EspacioActaTipoCampo.Numero => "12",
                EspacioActaTipoCampo.Decimal => "12.5",
                EspacioActaTipoCampo.Moneda => "1250000",
                EspacioActaTipoCampo.Fecha => DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EspacioActaTipoCampo.Hora => "14:30",
                EspacioActaTipoCampo.Casilla => "Si",
                EspacioActaTipoCampo.Correo => "colaborador@especialistasencasa.com",
                EspacioActaTipoCampo.Telefono => "604 444 4444",
                EspacioActaTipoCampo.Documento => "1 020 304 050",
                EspacioActaTipoCampo.Enlaces => "https://especialistasencasa.com",
                EspacioActaTipoCampo.Credencial => "Clave123*",
                EspacioActaTipoCampo.TextoLargo => $"Ejemplo de {campo.Etiqueta.ToLowerInvariant()}.",
                _ => campo.Etiqueta
            };
        }

        return muestra;
    }
}
