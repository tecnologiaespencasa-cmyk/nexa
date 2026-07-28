using IntranetPrueba.Data.Repositories.Interfaces;
using IntranetPrueba.Helpers;
using IntranetPrueba.Services.Interfaces;
using IntranetPrueba.Services.Models;

namespace IntranetPrueba.Services;

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
        var logs = await _auditLogRepository.SearchAsync(
            fromUtc: fromUtc,
            toUtc: toUtc,
            username: request.Username,
            action: request.Action,
            take: request.Take,
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
            }).ToList()
        };

        return ServiceResult<AuditLogSearchResultDto>.Success(result);
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
