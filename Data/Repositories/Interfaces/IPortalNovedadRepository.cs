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

    Task<IReadOnlyList<string>> GetCategoriasAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAuxiliaresAsync(CancellationToken cancellationToken = default);
}
