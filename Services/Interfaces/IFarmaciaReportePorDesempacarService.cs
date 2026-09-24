using Nexa.Services.Models;

namespace Nexa.Services.Interfaces;

/// <summary>
/// Reporte diario de farmacia para dirección asistencial: los despachos que pasaron a Por
/// desempacar en el día y siguen así al corte de las 11:00 p. m.
/// </summary>
public interface IFarmaciaReportePorDesempacarService
{
    /// <summary>
    /// Envía el reporte del corte más reciente si todavía no salió y no pasó la ventana de
    /// recuperación. Lo llama el servicio en segundo plano cada pocos minutos.
    /// </summary>
    Task EnviarSiCorrespondeAsync(DateTime ahoraUtc, CancellationToken cancellationToken = default);

    /// <summary>Arma el reporte de un corte, solo leyendo la base.</summary>
    Task<FarmaciaReportePorDesempacar> ConstruirAsync(
        FarmaciaCorteReporte corte,
        DateTime ahoraUtc,
        CancellationToken cancellationToken = default);

    /// <summary>El correo del reporte: resumen en el cuerpo y el listado en un Excel adjunto.</summary>
    EmailMessage ConstruirCorreo(
        FarmaciaReportePorDesempacar reporte,
        IReadOnlyList<string> destinatarios,
        bool esPrueba = false);
}
