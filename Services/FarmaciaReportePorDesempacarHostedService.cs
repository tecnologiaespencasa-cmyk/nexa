using Nexa.Services.Interfaces;

namespace Nexa.Services;

/// <summary>
/// Dispara el reporte diario de despachos por desempacar (ver FarmaciaReportePorDesempacarService).
/// Revisa cada pocos minutos si ya pasó el corte de las 11:00 p. m. y el reporte del día no ha
/// salido; el registro en farmacia_reporte_diario_envios es lo que evita enviarlo dos veces.
///
/// Solo corre en producción: la base es la misma para Azure y para el equipo local, y un equipo
/// local encendido a las 11:00 p. m. mandaría el correo con código a medio hacer. Para activarlo
/// en otro ambiente, FarmaciaReporteDiario:Habilitado = true.
/// </summary>
public class FarmaciaReportePorDesempacarHostedService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FarmaciaReportePorDesempacarHostedService> _logger;
    private readonly bool _habilitado;
    private readonly string _ambiente;

    public FarmaciaReportePorDesempacarHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<FarmaciaReportePorDesempacarHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _ambiente = environment.EnvironmentName;
        _habilitado = configuration.GetValue<bool?>("FarmaciaReporteDiario:Habilitado") ?? !environment.IsDevelopment();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_habilitado)
        {
            _logger.LogInformation(
                "Reporte diario de despachos por desempacar desactivado en el ambiente {Ambiente}.", _ambiente);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var reporte = scope.ServiceProvider.GetRequiredService<IFarmaciaReportePorDesempacarService>();
                await reporte.EnviarSiCorrespondeAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al revisar el reporte diario de despachos por desempacar.");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }
}
