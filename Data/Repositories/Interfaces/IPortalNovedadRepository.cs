using IntranetPrueba.Data.Repositories.Models;

namespace IntranetPrueba.Data.Repositories.Interfaces;

public interface IPortalNovedadRepository
{
    Task<IReadOnlyList<PortalNovedadRow>> GetNovedadesAsync(
        DateTime desde,
        DateTime hasta,
        string? categoria,
        string? auxiliar,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCategoriasAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAuxiliaresAsync(CancellationToken cancellationToken = default);
}
