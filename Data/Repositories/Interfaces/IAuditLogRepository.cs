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
        string? category,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? username,
        string? action,
        string? category,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> GetActionCountsAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? username,
        string? action,
        CancellationToken cancellationToken = default);

    Task<AuditTodayStats> GetTodayStatsAsync(CancellationToken cancellationToken = default);
}
