using Nexa.Data.Repositories.Interfaces;
using Nexa.Data.Entities;
using Nexa.Data.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Data.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly ApplicationDbContext _context;

    public AuditLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<string>> GetDistinctActionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .AsNoTracking()
            .Select(log => log.Action)
            .Distinct()
            .OrderBy(action => action)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogRow>> SearchAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? username,
        string? action,
        string? category,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await CreateFilteredQuery(fromUtc, toUtc, username, action, category)
            .OrderByDescending(log => log.PerformedAtUtc)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Clamp(take, 1, 1000))
            .Select(log => new AuditLogRow
            {
                Id = log.Id,
                PerformedAtUtc = log.PerformedAtUtc,
                Action = log.Action,
                Entity = log.Entity,
                Details = log.Details,
                IpAddress = log.IpAddress,
                PerformedByUserId = log.PerformedByUserId,
                Username = log.PerformedByUser != null ? log.PerformedByUser.Username : null,
                FullName = log.PerformedByUser != null ? log.PerformedByUser.FullName : null
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? username,
        string? action,
        string? category,
        CancellationToken cancellationToken = default) =>
        CreateFilteredQuery(fromUtc, toUtc, username, action, category).CountAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, int>> GetActionCountsAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? username,
        string? action,
        CancellationToken cancellationToken = default)
    {
        var counts = await CreateFilteredQuery(fromUtc, toUtc, username, action, category: null)
            .GroupBy(log => log.Action)
            .Select(group => new { Action = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(item => item.Action, item => item.Count, StringComparer.OrdinalIgnoreCase);
    }

    private IQueryable<AuditLog> CreateFilteredQuery(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? username,
        string? action,
        string? category)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(log => log.PerformedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(log => log.PerformedAtUtc < toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var actionFilter = action.Trim().ToUpperInvariant();
            query = query.Where(log => log.Action.ToUpper() == actionFilter);
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            var userFilter = username.Trim().ToUpperInvariant();
            query = query.Where(log =>
                log.PerformedByUser != null
                && (log.PerformedByUser.Username.ToUpper().Contains(userFilter)
                    || log.PerformedByUser.FullName.ToUpper().Contains(userFilter)));
        }

        return ApplyCategory(query, category);
    }

    private static IQueryable<AuditLog> ApplyCategory(IQueryable<AuditLog> query, string? category)
    {
        return category?.Trim().ToLowerInvariant() switch
        {
            "auth" => query.Where(log => log.Action.ToUpper() == "LOGIN_SUCCESS"
                || log.Action.ToUpper() == "LOGIN_FAILED"
                || log.Action.ToUpper() == "LOGOUT"
                || log.Action.ToUpper() == "ACCESS_DENIED"),
            "usuarios" => query.Where(log => log.Action.ToUpper().StartsWith("USER_")
                || log.Action.ToUpper().StartsWith("NURSING_")
                || log.Action.ToUpper() == "BOOTSTRAP_ADMIN_PASSWORD_RESET"),
            "censo" => query.Where(log => log.Action.ToUpper().StartsWith("CENSO_")),
            "farmacia" => query.Where(log => log.Action.ToUpper().StartsWith("FARMACIA_")),
            "otros" => query.Where(log =>
                log.Action.ToUpper() != "LOGIN_SUCCESS"
                && log.Action.ToUpper() != "LOGIN_FAILED"
                && log.Action.ToUpper() != "LOGOUT"
                && log.Action.ToUpper() != "ACCESS_DENIED"
                && !log.Action.ToUpper().StartsWith("USER_")
                && !log.Action.ToUpper().StartsWith("NURSING_")
                && log.Action.ToUpper() != "BOOTSTRAP_ADMIN_PASSWORD_RESET"
                && !log.Action.ToUpper().StartsWith("CENSO_")
                && !log.Action.ToUpper().StartsWith("FARMACIA_")),
            _ => query
        };
    }

    public async Task<AuditTodayStats> GetTodayStatsAsync(CancellationToken cancellationToken = default)
    {
        var colombiaToday = Helpers.ColombiaTime.Convert(DateTime.UtcNow).Date;
        var todayStart = Helpers.ColombiaTime.ConvertToUtc(colombiaToday);
        var todayEnd = Helpers.ColombiaTime.ConvertToUtc(colombiaToday.AddDays(1));

        var logs = await _context.AuditLogs
            .AsNoTracking()
            .Where(l => l.PerformedAtUtc >= todayStart && l.PerformedAtUtc < todayEnd)
            .Select(l => new { l.Action, l.PerformedByUserId })
            .ToListAsync(cancellationToken);

        return new AuditTodayStats
        {
            TotalEvents = logs.Count,
            UniqueUsers = logs
                .Where(l => l.PerformedByUserId != null)
                .Select(l => l.PerformedByUserId)
                .Distinct()
                .Count(),
            FailedLogins = logs.Count(l => l.Action == "LOGIN_FAILED"),
            OperationalEvents = logs.Count(l => l.Action.StartsWith("CENSO_") || l.Action.StartsWith("FARMACIA_"))
        };
    }

}
