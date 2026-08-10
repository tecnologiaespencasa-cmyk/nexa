using Nexa.Data.Repositories.Interfaces;
using Nexa.Helpers;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;

namespace Nexa.Services;

public class AuditQueryService : IAuditQueryService
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditQueryService(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<ServiceResult<AuditLogSearchResultDto>> SearchAsync(
        AuditLogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var today = AuditRetentionPolicy.GetLatestAllowedDate(DateTime.UtcNow);
        var earliestAllowedDate = AuditRetentionPolicy.GetEarliestAllowedDate(DateTime.UtcNow);
        var fromDate = request.FromDate?.Date;
        var toDate = request.ToDate?.Date;

        if ((fromDate.HasValue && fromDate.Value < earliestAllowedDate)
            || (toDate.HasValue && toDate.Value < earliestAllowedDate))
        {
            return ServiceResult<AuditLogSearchResultDto>.Failure(
                $"Solo se pueden consultar registros de los últimos {AuditRetentionPolicy.RetentionDays} días.");
        }

        if ((fromDate.HasValue && fromDate.Value > today)
            || (toDate.HasValue && toDate.Value > today))
        {
            return ServiceResult<AuditLogSearchResultDto>.Failure("No se permiten fechas posteriores a la fecha actual.");
        }

        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            return ServiceResult<AuditLogSearchResultDto>.Failure("La fecha inicial no puede ser mayor a la fecha final.");
        }

        DateTime? fromUtc = fromDate.HasValue
            ? ColombiaTime.ConvertToUtc(fromDate.Value)
            : null;
        DateTime? toUtc = toDate.HasValue
            ? ColombiaTime.ConvertToUtc(toDate.Value.AddDays(1))
            : null;

        var actions = await _auditLogRepository.GetDistinctActionsAsync(cancellationToken);
        var categoryCountsByAction = await _auditLogRepository.GetActionCountsAsync(
            fromUtc,
            toUtc,
            request.Username,
            request.Action,
            cancellationToken);
        var totalCount = await _auditLogRepository.CountAsync(
            fromUtc,
            toUtc,
            request.Username,
            request.Action,
            request.Category,
            cancellationToken);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var currentPage = Math.Clamp(request.Page, 1, totalPages);
        var logs = await _auditLogRepository.SearchAsync(
            fromUtc: fromUtc,
            toUtc: toUtc,
            username: request.Username,
            action: request.Action,
            category: request.Category,
            skip: (currentPage - 1) * pageSize,
            take: pageSize,
            cancellationToken: cancellationToken);

        var result = new AuditLogSearchResultDto
        {
            AvailableActions = actions,
            Logs = logs.Select(log => new AuditLogListItemDto
            {
                Id = log.Id,
                PerformedAtUtc = log.PerformedAtUtc,
                Action = log.Action,
                Entity = log.Entity,
                Details = log.Details,
                IpAddress = log.IpAddress,
                Username = log.Username,
                FullName = log.FullName
            }).ToList(),
            CategoryCounts = categoryCountsByAction
                .GroupBy(item => GetCategory(item.Key))
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Value)),
            TotalCount = totalCount,
            CurrentPage = currentPage,
            PageSize = pageSize,
            TotalPages = totalPages
        };

        return ServiceResult<AuditLogSearchResultDto>.Success(result);
    }

    private static string GetCategory(string action)
    {
        var normalizedAction = action.ToUpperInvariant();
        if (normalizedAction is "LOGIN_SUCCESS" or "LOGIN_FAILED" or "LOGOUT" or "ACCESS_DENIED")
        {
            return "auth";
        }

        if (normalizedAction.StartsWith("USER_")
            || normalizedAction.StartsWith("NURSING_")
            || normalizedAction == "BOOTSTRAP_ADMIN_PASSWORD_RESET")
        {
            return "usuarios";
        }

        if (normalizedAction.StartsWith("CENSO_"))
        {
            return "censo";
        }

        return normalizedAction.StartsWith("FARMACIA_") ? "farmacia" : "otros";
    }

    public async Task<AuditTodayStatsDto> GetTodayStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await _auditLogRepository.GetTodayStatsAsync(cancellationToken);
        return new AuditTodayStatsDto
        {
            TotalEvents = stats.TotalEvents,
            UniqueUsers = stats.UniqueUsers,
            FailedLogins = stats.FailedLogins,
            OperationalEvents = stats.OperationalEvents
        };
    }
}
