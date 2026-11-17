using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Barrio y zona de una dirección según las direcciones vecinas que el censo ya tiene validadas.
///
/// Desde 2026-11-17 las direcciones las interpreta la IA (OpenAI) en lugar de Google Maps, y la IA
/// no tiene mapa: sus barrios y comunas de Medellín fallaban casi siempre. En cambio, en el Valle de
/// Aburrá la nomenclatura es una cuadrícula (la Calle 32 con la Carrera 80 está al lado de la Calle
/// 33 con la Carrera 81), y el censo tiene miles de direcciones que Google validó con su barrio.
/// Probado ese día con 2.988 de ellas: ubicando cada una por sus vecinas, la zona salió bien en el
/// 89% y el barrio en el 62%. Es una sugerencia que la persona confirma, no una verdad.
/// </summary>
public partial class CensoController
{
    private sealed record PuntoCuadricula(
        string Municipio,
        string Tipo,
        double Via,
        double Placa,
        double Calle,
        double Carrera,
        string Barrio,
        string? Zona);

    /// <param name="Barrio">El barrio que se sugiere solo (null si las vecinas no se ponen de acuerdo).</param>
    /// <param name="Opciones">Barrios de las vecinas, del más votado al menos, para elegir con un clic.</param>
    private sealed record UbicacionVecina(string? Barrio, string? Zona, int Vecinas, IReadOnlyList<string> Opciones);

    private static readonly TimeSpan VigenciaCuadricula = TimeSpan.FromHours(6);
    private static readonly SemaphoreSlim CandadoCuadricula = new(1, 1);
    private static IReadOnlyList<PuntoCuadricula>? _cuadricula;
    private static DateTimeOffset _cuadriculaVence;

    // Vía y placa: "CALLE 32EE # 80-39", "CR 43A 1 SUR 100", "TV 39B No 74-12". La letra no puede
    // ser SUR/ESTE/BIS/NO para no tragarse el sufijo ni el separador.
    private const string NumeroVia = @"(?<{0}>\d{{1,3}})(?:\s*(?!SUR\b|ESTE\b|BIS\b|NO\b|N\b|NRO\b)(?<{0}l>[A-Z]{{1,3}})\b)?(?:\s*BIS\b)?(?:\s*(?<{0}s>SUR|ESTE)\b)?";

    private static readonly Regex PatronCuadricula = new(
        @"\b(?<tipo>CALLE|CLL|CL|CARRERA|CRA|KRA|KR|CR|TRANSVERSAL|TV|TR|DIAGONAL|DG|CIRCULAR|CQ|CIR)\.?\s*"
        + string.Format(CultureInfo.InvariantCulture, NumeroVia, "via")
        + @"\s*(?:#|NO\.?|N°|NRO\.?|N\b)?\s*"
        + string.Format(CultureInfo.InvariantCulture, NumeroVia, "pl")
        + @"\s*[-\s]\s*\d{1,4}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private async Task<UbicacionVecina?> UbicarPorVecinasAsync(
        string? municipioCanonico,
        string? direccion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(municipioCanonico) || string.IsNullOrWhiteSpace(direccion))
        {
            return null;
        }

        var punto = LeerPuntoCuadricula(direccion);
        if (punto is null)
        {
            return null;
        }

        var municipio = NormalizeKey(municipioCanonico);
        var delMunicipio = (await CuadriculaAsync(cancellationToken)).Where(x => x.Municipio == municipio).ToList();

        // 1. La misma vía (misma calle, carrera o transversal): lo más fiable, y la única forma de
        //    ubicar transversales, diagonales y circulares, que no siguen la cuadrícula.
        var mismaVia = delMunicipio
            .Where(x => x.Tipo == punto.Value.Tipo && Math.Abs(x.Via - punto.Value.Via) < 0.001)
            .Select(x => (Distancia: Math.Abs(x.Placa - punto.Value.Placa), Punto: x))
            .Where(x => x.Distancia <= 6)
            .OrderBy(x => x.Distancia)
            .Take(7)
            .ToList();
        // Los barrios del catálogo son nombres reales; los guardados a veces son texto libre, así
        // que como opción solo cuentan si están en el catálogo o los repiten dos vecinas.
        var catalogo = (await _addressValidationService.SearchNeighborhoodsAsync(municipioCanonico, "a", cancellationToken))
            .Select(ClaveBarrio)
            .ToHashSet(StringComparer.Ordinal);

        if (mismaVia.Count >= 2 || (mismaVia.Count == 1 && mismaVia[0].Distancia <= 1))
        {
            return Votar(mismaVia, catalogo, cercanas: true);
        }

        // 2. La cuadrícula de calles y carreras, solo para esas dos.
        if (punto.Value.Tipo is not ("CL" or "CR"))
        {
            return null;
        }

        var porCuadricula = delMunicipio
            .Where(x => x.Tipo is "CL" or "CR")
            .Select(x => (Distancia: Math.Sqrt(Math.Pow(x.Calle - punto.Value.Calle, 2) + Math.Pow(x.Carrera - punto.Value.Carrera, 2)), Punto: x))
            .Where(x => x.Distancia <= 8)
            .OrderBy(x => x.Distancia)
            .Take(7)
            .ToList();
        var cercanas = porCuadricula.Where(x => x.Distancia <= 4).ToList();
        if (cercanas.Count >= 2)
        {
            return Votar(cercanas, catalogo, cercanas: true);
        }

        // Más lejos (hasta 8 cuadras) no se sugiere nada solo: solo se ofrecen sus barrios para elegir.
        return porCuadricula.Count >= 2 ? Votar(porCuadricula, catalogo, cercanas: false) : null;
    }

    private static UbicacionVecina? Votar(
        List<(double Distancia, PuntoCuadricula Punto)> vecinas,
        IReadOnlySet<string> catalogo,
        bool cercanas)
    {
        // Gana el barrio que más se repite; en empate, el de la vecina más cercana (ya vienen en orden).
        // Solo se sugiere si lo repiten al menos dos vecinas, o si la única vecina está casi en la
        // misma placa: el barrio guardado a veces es texto libre ("CALVARIO ENTRADA CRISTOREY.").
        var masVotado = vecinas
            .GroupBy(x => x.Punto.Barrio)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Min(x => x.Distancia))
            .First();
        var barrio = cercanas && (masVotado.Count() >= 2 || (vecinas.Count == 1 && vecinas[0].Distancia <= 1))
            ? masVotado.Key
            : null;
        var zonas = vecinas
            .Where(x => x.Punto.Zona is not null)
            .GroupBy(x => x.Punto.Zona)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Min(x => x.Distancia))
            .ToList();
        // De lejos, la zona solo se sugiere si todas las vecinas coinciden.
        var zona = cercanas || zonas.Count == 1 ? zonas.FirstOrDefault()?.Key : null;
        var opciones = vecinas
            .GroupBy(x => x.Punto.Barrio)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Min(x => x.Distancia))
            .Where(g => g.Count() >= 2 || catalogo.Contains(ClaveBarrio(g.Key)))
            .Select(g => g.Key)
            .Take(4)
            .ToList();
        return barrio is null && zona is null && opciones.Count == 0
            ? null
            : new UbicacionVecina(barrio, zona, vecinas.Count, opciones);
    }

    private async Task<IReadOnlyList<PuntoCuadricula>> CuadriculaAsync(CancellationToken cancellationToken)
    {
        if (_cuadricula is not null && _cuadriculaVence > DateTimeOffset.UtcNow)
        {
            return _cuadricula;
        }

        await CandadoCuadricula.WaitAsync(cancellationToken);
        try
        {
            if (_cuadricula is not null && _cuadriculaVence > DateTimeOffset.UtcNow)
            {
                return _cuadricula;
            }

            var filas = await _context.CensoPacientes
                .AsNoTracking()
                .Where(x => x.DireccionValidada && x.Direccion != null && x.Barrio != null && x.MunicipioResidencia != null)
                .Select(x => new { x.Direccion, x.Barrio, x.MunicipioResidencia, x.ZonaDireccionSegunMunicipio })
                .ToListAsync(cancellationToken);

            var puntos = new List<PuntoCuadricula>();
            foreach (var fila in filas)
            {
                var barrio = Regex.Replace(fila.Barrio!.Trim(), @"\s+", " ").ToUpperInvariant();
                if (barrio.Length < 3 || string.Equals(barrio, MunicipioNoParametrizado, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var punto = LeerPuntoCuadricula(fila.Direccion!);
                if (punto is null)
                {
                    continue;
                }

                var zona = string.IsNullOrWhiteSpace(fila.ZonaDireccionSegunMunicipio)
                           || NormalizeKey(fila.ZonaDireccionSegunMunicipio) == "NOPARAMETRIZADO"
                    ? null
                    : fila.ZonaDireccionSegunMunicipio.Trim();
                puntos.Add(new PuntoCuadricula(
                    NormalizeKey(fila.MunicipioResidencia!),
                    punto.Value.Tipo, punto.Value.Via, punto.Value.Placa, punto.Value.Calle, punto.Value.Carrera,
                    barrio, zona));
            }

            _cuadricula = puntos;
            _cuadriculaVence = DateTimeOffset.UtcNow.Add(VigenciaCuadricula);
            return puntos;
        }
        finally
        {
            CandadoCuadricula.Release();
        }
    }

    /// <summary>Tipo de vía (CL, CR, TV, DG, CQ), número de vía, placa y su cruce calle/carrera.</summary>
    private static (string Tipo, double Via, double Placa, double Calle, double Carrera)? LeerPuntoCuadricula(string direccion)
    {
        var texto = QuitarTildes(direccion).ToUpperInvariant().Replace("N°", "#", StringComparison.Ordinal);
        var m = PatronCuadricula.Match(texto);
        if (!m.Success)
        {
            return null;
        }

        static double Valor(Match m, string grupo)
        {
            var numero = double.Parse(m.Groups[grupo].Value, CultureInfo.InvariantCulture);
            var letra = m.Groups[grupo + "l"].Value;
            if (letra.Length > 0)
            {
                numero += (letra[0] - 'A' + 1) / 30.0;
            }

            return m.Groups[grupo + "s"].Value == "SUR" ? -numero : numero;
        }

        var tipo = m.Groups["tipo"].Value switch
        {
            "CALLE" or "CLL" or "CL" => "CL",
            "CARRERA" or "CRA" or "KRA" or "KR" or "CR" => "CR",
            "TRANSVERSAL" or "TV" or "TR" => "TV",
            "DIAGONAL" or "DG" => "DG",
            _ => "CQ"
        };
        var via = Valor(m, "via");
        var placa = Valor(m, "pl");
        // En una calle la placa dice la carrera, y al revés.
        return tipo == "CR" ? (tipo, via, placa, placa, via) : (tipo, via, placa, via, placa);
    }

    private static string QuitarTildes(string valor)
    {
        var descompuesto = valor.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
