using Nexa.Services.Interfaces;

namespace Nexa.Services;

/// <summary>
/// Toma las fotos del Televisor (ver TelevisorCortesService): cada minuto revisa si ya pasó un
/// corte y falta su foto. El índice único de televisor_cortes impide fotos dobles.
///
/// Solo corre en producción: la base es la misma para Azure y para el equipo local, y un equipo
/// local con código a medio hacer congelaría un turno con cifras equivocadas que ya no se
/// recalculan. Para activarlo en otro ambiente, Televisor:CortesHabilitados = true.
/// </summary>
public class TelevisorCortesHostedService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan EsperaInicial = TimeSpan.FromSeconds(45);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelevisorCortesHostedService> _logger;
    private readonly bool _habilitado;
    private readonly string _ambiente;

    public TelevisorCortesHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<TelevisorCortesHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _ambiente = environment.EnvironmentName;
        _habilitado = configuration.GetValue<bool?>("Televisor:CortesHabilitados") ?? !environment.IsDevelopment();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_habilitado)
        {
            _logger.LogInformation("Fotos del Televisor desactivadas en el ambiente {Ambiente}.", _ambiente);
            return;
        }

        await Task.Delay(EsperaInicial, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var cortes = scope.ServiceProvider.GetRequiredService<ITelevisorCortesService>();
                await cortes.TomarCortesPendientesAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al tomar las fotos del Televisor.");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }
}
