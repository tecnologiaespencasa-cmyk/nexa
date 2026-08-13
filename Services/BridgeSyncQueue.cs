using System.Threading.Channels;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;
using Microsoft.Extensions.Options;

namespace Nexa.Services;

/// <summary>
/// Implementacion en memoria de <see cref="IBridgeSyncQueue"/>.
///
/// Es acotada a proposito: si el consumidor se atasca (Supabase caido), se
/// descartan los elementos mas antiguos en vez de crecer sin limite. Perder un
/// encolado no pierde el paciente: sigue en el censo y la reconciliacion
/// periodica (SupabaseBridge:Enabled) o el proximo guardado lo vuelven a enviar.
/// </summary>
public class BridgeSyncQueue : IBridgeSyncQueue
{
    private const int Capacity = 500;

    private readonly Channel<BridgePatient> _channel = Channel.CreateBounded<BridgePatient>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    private readonly IOptionsMonitor<SupabaseBridgeOptions> _options;
    private readonly ILogger<BridgeSyncQueue> _logger;

    public BridgeSyncQueue(
        IOptionsMonitor<SupabaseBridgeOptions> options,
        ILogger<BridgeSyncQueue> logger)
    {
        _options = options;
        _logger = logger;
    }

    public ChannelReader<BridgePatient> Reader => _channel.Reader;

    public bool Enqueue(BridgePatient patient)
    {
        var options = _options.CurrentValue;
        if (!options.PushOnSave)
        {
            return false;
        }

        if (!options.IsConfigured)
        {
            _logger.LogWarning(
                "Se guardo un registro de clinica de heridas pero el puente de Supabase no esta configurado; no se encolo el envio.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(patient.Document) || string.IsNullOrWhiteSpace(patient.Name))
        {
            return false;
        }

        return _channel.Writer.TryWrite(patient);
    }
}
