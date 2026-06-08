using IntranetPrueba.Data.Repositories.Models;

namespace IntranetPrueba.Data.Repositories.Interfaces;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<string>> GetDistinctActionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogRow>> SearchAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? username,
        string? action,
        int take,
        CancellationToken cancellationToken = default);
}
