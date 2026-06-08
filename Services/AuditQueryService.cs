using IntranetPrueba.Data.Repositories.Interfaces;
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
        var fromUtc = request.FromDate?.Date;
        DateTime? toUtc = null;
        if (request.ToDate.HasValue)
        {
            toUtc = request.ToDate.Value.Date.AddDays(1).AddTicks(-1);
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return ServiceResult<AuditLogSearchResultDto>.Failure("La fecha inicial no puede ser mayor a la fecha final.");
        }

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
}
