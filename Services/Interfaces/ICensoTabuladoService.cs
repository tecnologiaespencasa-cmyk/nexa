using Nexa.Models.ViewModels;

namespace Nexa.Services.Interfaces;

/// <summary>
/// Arma el tabulado unificado del censo: las filas de los cinco programas, sus conteos y, cuando se
/// filtra por uno solo, sus registros completos para desplegar todas las columnas de ese censo.
/// </summary>
public interface ICensoTabuladoService
{
    Task ConstruirAsync(CensoUnificadoViewModel model, CancellationToken cancellationToken);
}
