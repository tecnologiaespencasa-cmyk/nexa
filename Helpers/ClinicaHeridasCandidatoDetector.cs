using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nexa.Helpers;

/// <summary>
/// Busca en el texto de una remisión las palabras que delatan a un posible paciente de clínica de
/// heridas: "clínica de heridas", "curaciones" y "celulitis".
///
/// La trampa está en el formato de remisión de SURA: trae impresos los rótulos "CURACIONES" y
/// "Clínica de heridas: Anotar si requiere valoración…", estén o no diligenciados. Si se buscara en
/// todo el texto, cualquier remisión de SURA saldría candidata. Por eso:
///   - en una hoja de cálculo solo se mira lo que la IPS escribió (la celda de respuesta, no la
///     del rótulo), y
///   - en un PDF o un texto pegado se borran antes los rótulos conocidos del formato.
/// </summary>
public static class ClinicaHeridasCandidatoDetector
{
    private const int MargenFragmento = 80;
    private const RegexOptions Opciones = RegexOptions.CultureInvariant | RegexOptions.Multiline;

    private static readonly (string PalabraClave, Regex Patron)[] PalabrasClave =
    [
        ("Clínica de heridas", new Regex(@"\bCLINICA\W+DE\W+HERIDAS?\b", Opciones)),
        ("Curaciones", new Regex(@"\bCURACION(?:ES)?\b", Opciones)),
        ("Celulitis", new Regex(@"\bCELULITIS\b", Opciones))
    ];

    /// <summary>Rótulos impresos del formato de remisión que no dicen nada del paciente.</summary>
    private static readonly Regex[] RotulosDelFormato =
    [
        new(@"CLINICA\W+DE\W+HERIDAS\W+ANOTAR\W+SI\W+REQUIERE\W+VALORACION\W+Y\W+MANEJO\W+POR\W+CLINICA\W+DE\W+HERIDAS(?:\W+Y\W+DEFINIR\W+PLAN\W+DE\W+MANEJO)?", Opciones),
        new(@"FRECUENCIA\W+DE\W+LA\W+CURACION", Opciones),
        new(@"FECHA\W+DE\W+LA\W+ULTIMA\W+CURACION", Opciones),
        // El encabezado de sección: solo en su línea, o pegado al rótulo que lo sigue (un PDF puede
        // juntar ambos en la misma línea).
        new(@"^[ \t]*CURACIONES[ \t\r]*$", Opciones),
        new(@"\bCURACIONES\W+(?=CLINICA\W+DE\W+HERIDAS\W+ANOTAR\b)", Opciones)
    ];

    /// <param name="texto">El texto que se le pasó a la extracción.</param>
    /// <param name="soloRespuestas">
    /// true para el texto de una hoja de cálculo, donde cada fila llega como "rótulo | respuesta".
    /// </param>
    public static IReadOnlyList<CoincidenciaClinicaHeridas> Detectar(string? texto, bool soloRespuestas)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        var buscable = soloRespuestas ? DejarSoloRespuestas(texto) : texto;
        var (normalizado, indiceOriginal) = Normalizar(buscable);

        // Los rótulos se tapan con espacios para no mover las posiciones que llevan al original.
        var caracteres = normalizado.ToCharArray();
        foreach (var rotulo in RotulosDelFormato)
        {
            foreach (Match coincidencia in rotulo.Matches(normalizado))
            {
                for (var i = coincidencia.Index; i < coincidencia.Index + coincidencia.Length; i++)
                {
                    if (caracteres[i] != '\n')
                    {
                        caracteres[i] = ' ';
                    }
                }
            }
        }

        var limpio = new string(caracteres);
        var resultado = new List<CoincidenciaClinicaHeridas>();
        foreach (var (palabraClave, patron) in PalabrasClave)
        {
            var coincidencia = patron.Match(limpio);
            if (!coincidencia.Success)
            {
                continue;
            }

            var inicio = indiceOriginal[coincidencia.Index];
            var fin = indiceOriginal[coincidencia.Index + coincidencia.Length - 1] + 1;
            resultado.Add(ConstruirCoincidencia(palabraClave, buscable, inicio, fin));
        }

        return resultado;
    }

    /// <summary>
    /// Deja de cada fila solo lo que va después del primer " | " —la respuesta— y tapa con
    /// espacios el rótulo y las filas que no tienen respuesta. Conserva la longitud del texto.
    /// </summary>
    private static string DejarSoloRespuestas(string texto)
    {
        var builder = new StringBuilder(texto.Length);
        foreach (var linea in texto.Split('\n'))
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            var separador = linea.IndexOf(" | ", StringComparison.Ordinal);
            var desde = separador < 0 ? linea.Length : separador + 3;
            builder.Append(' ', desde);
            builder.Append(linea, desde, linea.Length - desde);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Mayúsculas y sin tildes, recordando para cada carácter normalizado de qué posición del
    /// original salió: así el fragmento que se muestra conserva las tildes del documento.
    /// </summary>
    private static (string Normalizado, List<int> IndiceOriginal) Normalizar(string texto)
    {
        var builder = new StringBuilder(texto.Length);
        var indices = new List<int>(texto.Length);
        for (var i = 0; i < texto.Length; i++)
        {
            foreach (var c in texto[i].ToString().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                builder.Append(char.ToUpperInvariant(c));
                indices.Add(i);
            }
        }

        return (builder.ToString(), indices);
    }

    private static CoincidenciaClinicaHeridas ConstruirCoincidencia(string palabraClave, string texto, int inicio, int fin)
    {
        var desde = Math.Max(0, inicio - MargenFragmento);
        var hasta = Math.Min(texto.Length, fin + MargenFragmento);
        var antes = Compactar(texto[desde..inicio]);
        var despues = Compactar(texto[fin..hasta]);

        return new CoincidenciaClinicaHeridas(
            palabraClave,
            (desde > 0 ? "…" : string.Empty) + antes,
            Compactar(texto[inicio..fin]),
            despues + (hasta < texto.Length ? "…" : string.Empty));
    }

    private static string Compactar(string valor)
        => Regex.Replace(valor.Replace('|', ' '), @"\s+", " ");
}

/// <param name="PalabraClave">La palabra clave, escrita como se le muestra al usuario.</param>
/// <param name="Antes">Texto del documento justo antes de la palabra.</param>
/// <param name="Encontrado">La palabra tal como aparece en el documento.</param>
/// <param name="Despues">Texto del documento justo después.</param>
public sealed record CoincidenciaClinicaHeridas(string PalabraClave, string Antes, string Encontrado, string Despues);
