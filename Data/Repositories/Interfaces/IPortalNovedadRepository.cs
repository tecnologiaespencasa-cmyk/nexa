using Nexa.Data.Repositories.Models;

namespace Nexa.Data.Repositories.Interfaces;

public interface IPortalNovedadRepository
{
    /// <summary>
    /// Novedades creadas en [<paramref name="desdeUtc"/>, <paramref name="hastaUtcExclusivo"/>).
    /// El portal guarda "createdAt" en UTC (Prisma): quien pide un día de Colombia debe convertir sus
    /// límites a UTC antes de llamar, o las novedades de la noche caen en el día siguiente.
    /// </summary>
    Task<IReadOnlyList<PortalNovedadRow>> GetNovedadesAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        string? categoria,
        string? auxiliar,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Novedades sin resolver en este momento, sin importar cuándo se crearon, y la fecha de creación
    /// (UTC) de la más antigua.
    /// </summary>
    Task<(int Pendientes, DateTime? MasAntiguaUtc)> GetPendientesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumen del día para el Televisor, por área responsable ("responsableGestion"). Los límites
    /// llegan en UTC. Como el portal no guarda la hora en que se resolvió cada novedad, una novedad
    /// resuelta cuenta como resuelta en su última actualización ("updatedAt"), igual que en Reportes;
    /// con el mismo criterio se reconstruye lo que estaba pendiente en un momento pasado.
    /// </summary>
    Task<PortalNovedadesResumen> GetResumenDiaAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        DateTime ochoAmUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCategoriasAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAuxiliaresAsync(CancellationToken cancellationToken = default);
}
