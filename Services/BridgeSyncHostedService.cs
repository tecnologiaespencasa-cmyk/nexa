using Nexa.Services.Interfaces;
using Nexa.Services.Models;
using Microsoft.Extensions.Options;

namespace Nexa.Services;

/// <summary>
/// Sincroniza periodicamente los pacientes del censo de clinica de heridas con
/// la base puente de Supabase. Todo el proceso ocurre en el backend: no hay
/// pantalla ni endpoint que lo dispare.
///
/// Se controla por configuracion (seccion SupabaseBridge):
///   Enabled            false por defecto; mientras siga en false no se envia nada.
///   MaxPatientsPerRun  0 = todos. Para la puesta en marcha escalonada: 1, luego 5, luego 0.
///   DryRun             true cuenta y arma los lotes sin llamar a Supabase.
///   IntervalHours      horas entre ejecuciones.
///   InitialDelaySeconds espera tras arrancar la aplicacion.
///
/// LOGGING: solo informacion tecnica. Nunca documento, nombre, HMAC ni secretos.
/// </summary>
public class BridgeSyncHostedService : BackgroundService
{
    private static readonly TimeSpan DisabledCheckInterval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SupabaseBridgeOptions> _options;
    private readonly ILogger<BridgeSyncHostedService> _logger;

    public BridgeSyncHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SupabaseBridgeOptions> options,
        ILogger<BridgeSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initialDelay = TimeSpan.FromSeconds(Math.Clamp(_options.CurrentValue.InitialDelaySeconds, 0, 3600));
        if (initialDelay > TimeSpan.Zero)
        {
            await Task.Delay(initialDelay, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;

            if (!options.Enabled)
            {
                // Se relee la configuracion en cada vuelta: basta con reiniciar
                // la aplicacion (o recargar appsettings) para activarlo.
                await Task.Delay(DisabledCheckInterval, stoppingToken);
                continue;
            }

            try
            {
                await RunAsync(options, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Fallo la sincronizacion del puente de clinica de heridas.");
            }

            var interval = TimeSpan.FromHours(Math.Clamp(options.IntervalHours, 0.25, 168));
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RunAsync(SupabaseBridgeOptions options, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IClinicaHeridasBridgeSyncService>();

        if (!options.DryRun && !syncService.IsConfigured)
        {
            _logger.LogWarning(
                "El puente de Supabase esta habilitado pero le falta configuracion (SupabaseBridge:ProjectUrl y SupabaseBridge:ApiSecret). No se sincroniza.");
            return;
        }

        int? limit = options.MaxPatientsPerRun > 0 ? options.MaxPatientsPerRun : null;
        var result = await syncService.SyncAsync(limit, options.DryRun, cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            _logger.LogError("Sincronizacion del puente sin exito: {Detalle}", result.ErrorMessage);
            await LogAuditAsync(scope, "BRIDGE_SUPABASE_SYNC_FALLIDA",
                $"Limite: {limit?.ToString() ?? "todos"}. Simulacion: {options.DryRun}. Detalle: {result.ErrorMessage}",
                cancellationToken);
            return;
        }

        var summary = result.Value;
        await LogAuditAsync(scope,
            options.DryRun ? "BRIDGE_SUPABASE_SYNC_SIMULADA" : "BRIDGE_SUPABASE_SYNC_EJECUTADA",
            $"Limite: {limit?.ToString() ?? "todos"}. Encontrados: {summary.PatientsFound}. Enviados: {summary.PatientsSent}. "
            + $"Procesados: {summary.PatientsProcessed}. Nuevos: {summary.Inserted}. Actualizados: {summary.Updated}. "
            + $"Lotes ok: {summary.BatchesSent}. Lotes con error: {summary.BatchesFailed}. Duracion: {summary.DurationMs} ms.",
            cancellationToken);
    }

    /// <summary>Auditoria del proceso automatico: sin usuario y sin datos personales.</summary>
    private static Task LogAuditAsync(
        AsyncServiceScope scope,
        string action,
        string details,
        CancellationToken cancellationToken)
    {
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        return auditService.LogAsync(action, "BridgeSupabase", details, null, null, cancellationToken);
    }
}
