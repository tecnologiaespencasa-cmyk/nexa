using Nexa.Services.Interfaces;
using Nexa.Services.Models;

namespace Nexa.Services;

/// <summary>
/// Consume la cola de pacientes recien guardados en el censo de clinica de
/// heridas y los empuja al puente de Supabase en cuanto llegan.
///
/// Corre fuera de la peticion HTTP: guardar el registro no espera a Supabase ni
/// falla si Supabase no responde. Antes de enviar espera un instante para
/// agrupar los guardados seguidos en un solo lote.
///
/// LOGGING: solo informacion tecnica. Nunca documento, nombre, HMAC ni secretos.
/// </summary>
public class BridgeSyncPushHostedService : BackgroundService
{
    private static readonly TimeSpan AgrupacionDelay = TimeSpan.FromSeconds(2);
    private const int MaxPorLote = 100;

    private readonly IBridgeSyncQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BridgeSyncPushHostedService> _logger;

    public BridgeSyncPushHostedService(
        IBridgeSyncQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BridgeSyncPushHostedService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _queue.Reader.WaitToReadAsync(stoppingToken))
        {
            // Pequena espera para que varios guardados seguidos viajen juntos.
            await Task.Delay(AgrupacionDelay, stoppingToken);

            var lote = new List<BridgePatient>();
            while (lote.Count < MaxPorLote && _queue.Reader.TryRead(out var paciente))
            {
                lote.Add(paciente);
            }

            if (lote.Count == 0)
            {
                continue;
            }

            try
            {
                await PushAsync(lote, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Fallo el envio inmediato de {Cantidad} paciente(s) al puente de Supabase.", lote.Count);
            }
        }
    }

    private async Task PushAsync(IReadOnlyList<BridgePatient> lote, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IClinicaHeridasBridgeSyncService>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var result = await syncService.PushPatientsAsync(lote, cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            // El paciente no se pierde: sigue en el censo y lo recupera el
            // proximo guardado o la reconciliacion periodica.
            _logger.LogError(
                "No fue posible empujar {Cantidad} paciente(s) al puente tras los reintentos: {Detalle}",
                lote.Count, result.ErrorMessage);
            await auditService.LogAsync("BRIDGE_SUPABASE_PUSH_FALLIDO", "BridgeSupabase",
                $"Pacientes en el lote: {lote.Count}. Detalle: {result.ErrorMessage}",
                null, null, cancellationToken);
            return;
        }

        var summary = result.Value;
        _logger.LogInformation(
            "Envio inmediato al puente completado. Enviados: {Enviados}. Procesados: {Procesados}. Nuevos: {Nuevos}. Actualizados: {Actualizados}. Duracion: {DurationMs} ms.",
            summary.PatientsSent, summary.PatientsProcessed, summary.Inserted, summary.Updated, summary.DurationMs);

        await auditService.LogAsync("BRIDGE_SUPABASE_PUSH_EJECUTADO", "BridgeSupabase",
            $"Enviados: {summary.PatientsSent}. Procesados: {summary.PatientsProcessed}. Nuevos: {summary.Inserted}. "
            + $"Actualizados: {summary.Updated}. Lotes con error: {summary.BatchesFailed}. Duracion: {summary.DurationMs} ms.",
            null, null, cancellationToken);
    }
}
