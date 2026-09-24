using Nexa.Models.ViewModels;

namespace Nexa.Services.Interfaces;

/// <summary>
/// Arma el tabulado unificado del censo: las filas de los cinco programas, sus conteos y, cuando se
/// filtra por uno solo, sus registros completos para desplegar todas las columnas de ese censo.
/// </summary>
public interface ICensoTabuladoService
{
    /// <remarks>
    /// Las filas vienen recortadas al tope de la pantalla, salvo cuando se filtra por documento:
    /// ahi se traen todas, porque un paciente no llega al tope y verlo completo es el punto.
    /// </remarks>
    Task ConstruirAsync(
        CensoUnificadoViewModel model,
        CancellationToken cancellationToken);
}
