using IntranetPrueba.Data;
using IntranetPrueba.Data.Entities;
using IntranetPrueba.Services.Interfaces;

namespace IntranetPrueba.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;

    public AuditService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
        string action,
        string entity,
        string? details,
        Guid? performedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var log = new AuditLog
            {
                Action = action,
                Entity = entity,
                Details = details,
                PerformedByUserId = performedByUserId,
                IpAddress = ipAddress,
                PerformedAtUtc = DateTime.UtcNow
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Evita que errores de auditoría detengan el flujo principal.
        }
    }
}
