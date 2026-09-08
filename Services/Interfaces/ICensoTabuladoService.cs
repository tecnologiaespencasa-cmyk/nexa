using Nexa.Models.ViewModels;

namespace Nexa.Services.Interfaces;

/// <summary>
/// Arma el tabulado unificado del censo: las filas de los cinco programas, sus conteos y, cuando se
/// filtra por uno solo, sus registros completos para desplegar todas las columnas de ese censo.
/// </summary>
public interface ICensoTabuladoService
{
    /// <param name="sinRecorte">
    /// true para traer todas las filas, sin el tope que existe para que la tabla en pantalla sea
    /// legible. Lo usa el exportable: un Excel recortado a 100 filas no sirve para nada.
    /// </param>
    Task ConstruirAsync(
        CensoUnificadoViewModel model,
        bool sinRecorte,
        CancellationToken cancellationToken);
}
