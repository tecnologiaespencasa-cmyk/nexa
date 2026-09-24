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
    private readonly Dictionary<string, Task<IReadOnlyList<OpsAssistantDto>>> _directorioDeLaPeticion = new(StringComparer.Ordinal);
    private Task<IReadOnlyList<MedicamentoCatalogItemViewModel>>? _medicamentosDeLaPeticion;
    private readonly Dictionary<string, Task<IReadOnlyList<string>>> _barriosDeLaPeticion = new(StringComparer.OrdinalIgnoreCase);

    private Task<IReadOnlyList<NursingAssistantDto>> AuxiliaresDeLaPeticionAsync(CancellationToken cancellationToken) =>
        _auxiliaresDeLaPeticion ??= _userAdministrationService.GetNursingAssistantsAsync(onlyActive: true, cancellationToken);

    private Task<IReadOnlyList<OpsAssistantDto>> DirectorioDeLaPeticionAsync(
        IReadOnlyCollection<string>? profesiones,
        CancellationToken cancellationToken)
    {
        var clave = profesiones is null
            ? "*"
            : string.Join('|', profesiones.OrderBy(x => x, StringComparer.Ordinal));
        if (!_directorioDeLaPeticion.TryGetValue(clave, out var consulta))
        {
            consulta = _userAdministrationService.GetOpsAssistantsAsync(onlyActive: true, profesiones, cancellationToken);
            _directorioDeLaPeticion[clave] = consulta;
        }

        return consulta;
    }

    private Task<IReadOnlyList<MedicamentoCatalogItemViewModel>> MedicamentosDeLaPeticionAsync(CancellationToken cancellationToken) =>
        _medicamentosDeLaPeticion ??= ConsultarCatalogoMedicamentosAsync(cancellationToken);

    /// <summary>
    /// Los barrios de un municipio que coinciden con el barrio guardado. El maestro y cada programa
    /// hacen la misma búsqueda con el mismo barrio (a veces en mayúsculas, a veces no); Google y el
    /// catálogo estático no distinguen mayúsculas, así que es una sola búsqueda.
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
            consulta = _addressValidationService.SearchNeighborhoodsAsync(municipio!, termino!, cancellationToken);
            _barriosDeLaPeticion[clave] = consulta;
        }

        return consulta;
    }
}
