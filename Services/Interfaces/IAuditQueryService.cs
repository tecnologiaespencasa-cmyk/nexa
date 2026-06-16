using IntranetPrueba.Services.Models;

namespace IntranetPrueba.Services.Interfaces;

public interface IAuditQueryService
{
    Task<ServiceResult<AuditLogSearchResultDto>> SearchAsync(
        AuditLogSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<AuditTodayStatsDto> GetTodayStatsAsync(CancellationToken cancellationToken = default);
}
