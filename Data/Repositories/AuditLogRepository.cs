using IntranetPrueba.Data.Repositories.Interfaces;
using IntranetPrueba.Data.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Data.Repositories;

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
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs
            .AsNoTracking()
            .Include(log => log.PerformedByUser)
            .AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(log => log.PerformedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(log => log.PerformedAtUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var actionFilter = action.Trim().ToUpperInvariant();
            query = query.Where(log => log.Action.ToUpper() == actionFilter);
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            var usernameFilter = username.Trim().ToUpperInvariant();
            query = query.Where(log =>
                log.PerformedByUser != null
                && log.PerformedByUser.Username.ToUpper().Contains(usernameFilter));
        }

        return await query
            .OrderByDescending(log => log.PerformedAtUtc)
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

}
