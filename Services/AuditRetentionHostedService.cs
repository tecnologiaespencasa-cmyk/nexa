using IntranetPrueba.Data;
using IntranetPrueba.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Services;

public class AuditRetentionHostedService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditRetentionHostedService> _logger;

    public AuditRetentionHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditRetentionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeleteExpiredAuditLogsAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "No fue posible depurar los registros de auditoría vencidos.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task DeleteExpiredAuditLogsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cutoffUtc = AuditRetentionPolicy.GetRetentionCutoffUtc(DateTime.UtcNow);

        var deleted = await context.AuditLogs
            .Where(log => log.PerformedAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Se eliminaron {DeletedAuditLogs} registros de auditoría anteriores a {AuditRetentionCutoffUtc}.",
                deleted,
                cutoffUtc);
        }
    }
}
