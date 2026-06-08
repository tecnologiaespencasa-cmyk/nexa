namespace IntranetPrueba.Services.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entity,
        string? details,
        Guid? performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
