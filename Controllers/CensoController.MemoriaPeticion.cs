using Microsoft.EntityFrameworkCore;
using Nexa.Models.ViewModels;
using Nexa.Services.Models;

namespace Nexa.Controllers;

/// <summary>
/// Memoria de lo que se consulta más de una vez en la misma carga de la pantalla única.
///
/// La ficha arma hasta cinco formularios y cada uno pedía por su cuenta el mismo directorio de
/// auxiliares (otra base, en Neon), el mismo catálogo de medicamentos y los mismos barrios, que
/// son hasta tres consultas a Google por formulario. Medido el 2026-09-24 con un paciente de
/// agudos, crónicos y NPT: 12 llamadas a Google y 3 al directorio por cada vez que se abría.
///
/// El controlador se crea para cada petición y se descarta al terminarla, así que guardar aquí la
/// primera respuesta no cambia nada de lo que se muestra: solo evita volver a preguntar lo mismo
/// dentro de la misma carga. La siguiente consulta del paciente vuelve a leer todo fresco.
/// </summary>
public partial class CensoController
{
    private Task<IReadOnlyList<NursingAssistantDto>>? _auxiliaresDeLaPeticion;
    private readonly Dictionary<string, Task<IReadOnlyList<string>>> _directorioDeLaPeticion = new(StringComparer.Ordinal);
    private Task<IReadOnlyList<MedicamentoCatalogItemViewModel>>? _medicamentosDeLaPeticion;
    private readonly Dictionary<string, Task<IReadOnlyList<string>>> _barriosDeLaPeticion = new(StringComparer.OrdinalIgnoreCase);

    private Task<IReadOnlyList<NursingAssistantDto>> AuxiliaresDeLaPeticionAsync(CancellationToken cancellationToken) =>
        _auxiliaresDeLaPeticion ??= _userAdministrationService.GetNursingAssistantsAsync(onlyActive: true, cancellationToken);

    /// <summary>
    /// Nombres del directorio de personal para los campos de auxiliar. Además de la memoria de la
    /// petición, pasan por <see cref="Nexa.Services.DirectorioAuxiliaresCache"/>, que los conserva
    /// unos minutos entre consultas: el directorio vive en otra base (Neon) y cambia poco.
    /// </summary>
    private Task<IReadOnlyList<string>> DirectorioDeLaPeticionAsync(
        IReadOnlyCollection<string>? profesiones,
        CancellationToken cancellationToken)
    {
        var clave = profesiones is null
            ? "*"
            : string.Join('|', profesiones.OrderBy(x => x, StringComparer.Ordinal));
        if (!_directorioDeLaPeticion.TryGetValue(clave, out var consulta))
        {
            consulta = _directorioAuxiliaresCache.ObtenerAsync(
                clave,
                async ct => (await _userAdministrationService.GetOpsAssistantsAsync(onlyActive: true, profesiones, ct))
                    .Select(x => x.Name)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList()
                    // La lista se comparte entre peticiones: de solo lectura, nadie la altera.
                    .AsReadOnly(),
                cancellationToken);
            _directorioDeLaPeticion[clave] = consulta;
        }

        return consulta;
    }

    private Task<IReadOnlyList<MedicamentoCatalogItemViewModel>> MedicamentosDeLaPeticionAsync(CancellationToken cancellationToken) =>
        _medicamentosDeLaPeticion ??= ConsultarCatalogoMedicamentosAsync(cancellationToken);

    /// <summary>
    /// Los barrios de un municipio que coinciden con el término: el catálogo local más los que el
    /// censo ya tiene guardados para ese municipio. El maestro y cada programa hacen la misma
    /// búsqueda con el mismo barrio (a veces en mayúsculas, a veces no), así que es una sola.
    /// </summary>
    private Task<IReadOnlyList<string>> BuscarBarriosAsync(
        string? municipio,
        string? termino,
        CancellationToken cancellationToken)
    {
        // Terapia la llama sin mirar antes si hay municipio; el servicio ya resuelve el nulo.
        var clave = $"{municipio?.Trim()}\u001f{termino?.Trim()}";
        if (!_barriosDeLaPeticion.TryGetValue(clave, out var consulta))
        {
            consulta = BuscarBarriosConGuardadosAsync(municipio, termino, cancellationToken);
            _barriosDeLaPeticion[clave] = consulta;
        }

        return consulta;
    }

    /// <summary>
    /// Hasta 2026-11 los barrios que faltaban en el catálogo local se le preguntaban a Google (tres
    /// consultas por lista). El 31% de los pacientes tiene guardado un barrio que no está en el
    /// catálogo (Doce de Octubre, Aures II, Ciudad Niquía…): salieron de esas sugerencias. Para no
    /// perderlas sin pagarle a Google, se ofrecen los barrios ya guardados en el maestro.
    /// </summary>
    private async Task<IReadOnlyList<string>> BuscarBarriosConGuardadosAsync(
        string? municipio,
        string? termino,
        CancellationToken cancellationToken)
    {
        var catalogo = await _addressValidationService.SearchNeighborhoodsAsync(municipio!, termino!, cancellationToken);
        var canonico = ToCanonicalMunicipality(municipio);
        if (string.IsNullOrWhiteSpace(canonico)
            || string.Equals(canonico, MunicipioNoParametrizado, StringComparison.OrdinalIgnoreCase))
        {
            return catalogo;
        }

        var guardados = (await BarriosGuardadosAsync(cancellationToken))
            .Where(x => string.Equals(ToCanonicalMunicipality(x.Municipio), canonico, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Barrio)
            .ToList();

        // Mismo criterio que el catálogo: con término vacío o de una letra va la lista completa;
        // si el término no coincide con nada, también (el navegador filtra mientras se escribe).
        var claveTermino = ClaveBarrio(termino);
        if (claveTermino.Length > 1)
        {
            var coinciden = guardados.Where(x => ClaveBarrio(x).Contains(claveTermino, StringComparison.Ordinal)).ToList();
            if (coinciden.Count > 0)
            {
                guardados = coinciden;
            }
        }

        var vistos = new HashSet<string>(catalogo.Select(ClaveBarrio), StringComparer.Ordinal);
        var resultado = catalogo.ToList();
        foreach (var barrio in guardados)
        {
            if (vistos.Add(ClaveBarrio(barrio)))
            {
                resultado.Add(barrio);
            }
        }

        return resultado.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private Task<List<(string Municipio, string Barrio)>>? _barriosGuardadosDeLaPeticion;

    private Task<List<(string Municipio, string Barrio)>> BarriosGuardadosAsync(CancellationToken cancellationToken) =>
        _barriosGuardadosDeLaPeticion ??= ConsultarBarriosGuardadosAsync(cancellationToken);

    private async Task<List<(string Municipio, string Barrio)>> ConsultarBarriosGuardadosAsync(CancellationToken cancellationToken)
    {
        var filas = await _context.CensoPacientes
            .AsNoTracking()
            .Where(x => x.MunicipioResidencia != null && x.Barrio != null)
            .Select(x => new { x.MunicipioResidencia, x.Barrio })
            .Distinct()
            .ToListAsync(cancellationToken);

        return filas
            // "DOCE  DE OCTUBRE" (doble espacio) se ofrece como "DOCE DE OCTUBRE".
            .Select(x => (
                Municipio: x.MunicipioResidencia!.Trim(),
                Barrio: System.Text.RegularExpressions.Regex.Replace(x.Barrio!.Trim(), @"\s+", " ").ToUpperInvariant()))
            .Where(x => x.Barrio.Length >= 3
                        && !string.Equals(x.Barrio, MunicipioNoParametrizado, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>Barrio sin tildes, espacios ni guiones, para comparar "Campo Valdés 1" con "CAMPO VALDES 1".</summary>
    private static string ClaveBarrio(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var descompuesto = valor.Trim().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (char.IsLetterOrDigit(c)
                && System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToUpperInvariant(c));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// La validación de dirección que ya quedó guardada en el maestro, si la dirección enviada es la
    /// misma. Al guardar el maestro o un programa se volvía a consultar a Google la misma dirección
    /// cada vez, aunque nadie la hubiera tocado; ahora solo se consulta si cambió.
    /// Devuelve null cuando hay que validarla (cambió, no hay maestro, o nunca se validó ni asumió).
    /// </summary>
    private async Task<(bool Validada, bool Asumida)?> DireccionYaValidadaEnMaestroAsync(
        string? documento,
        string? direccion,
        bool asumirDireccionErrada,
        CancellationToken cancellationToken)
    {
        var doc = NormalizeCedulaFilter(documento);
        if (string.IsNullOrWhiteSpace(doc) || string.IsNullOrWhiteSpace(direccion))
        {
            return null;
        }

        var guardada = await _context.CensoPacientes
            .AsNoTracking()
            .Where(x => x.NumeroIdentificacion == doc)
            .Select(x => new { x.Direccion, x.DireccionValidada, x.AsumirDireccionErrada })
            .FirstOrDefaultAsync(cancellationToken);

        return guardada is not null
               && AreEquivalentAddresses(guardada.Direccion, direccion)
               && (guardada.DireccionValidada || (guardada.AsumirDireccionErrada && asumirDireccionErrada))
            ? (guardada.DireccionValidada, guardada.AsumirDireccionErrada && !guardada.DireccionValidada)
            : null;
    }
}
