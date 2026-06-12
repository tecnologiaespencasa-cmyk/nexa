using IntranetPrueba.Data;
using IntranetPrueba.Data.Entities;
using IntranetPrueba.Models.ViewModels;
using IntranetPrueba.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Services;

public class EmpacadoNotificationHostedService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan VentanaEmpacado = TimeSpan.FromHours(72);
    private static readonly TimeSpan IntervaloRecordatorioAuxiliar = TimeSpan.FromHours(24);
    private static readonly TimeSpan UmbralAlertaVencimiento = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmpacadoNotificationHostedService> _logger;

    public EmpacadoNotificationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<EmpacadoNotificationHostedService> logger)
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
                await ProcessNotificationsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al procesar notificaciones de despachos empacados.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task ProcessNotificationsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IFarmaciaDispatchNotificationService>();

        var now = DateTime.UtcNow;
        var cutoffEmpacado = now - VentanaEmpacado;

        var pedidos = await context.Censos
            .Where(x => x.FarmaciaEstado == FarmaciaEstados.Empacado
                && x.FarmaciaEmpacadoAtUtc != null
                && x.FarmaciaEmpacadoAtUtc > cutoffEmpacado)
            .ToListAsync(cancellationToken);

        foreach (var pedido in pedidos)
        {
            await TryNotifyAuxiliarAsync(pedido, now, context, notificationService, cancellationToken);
            await TryNotifyGerenciaAsync(pedido, now, context, notificationService, cancellationToken);
        }
    }

    private async Task TryNotifyAuxiliarAsync(
        CensoRecord pedido,
        DateTime now,
        ApplicationDbContext context,
        IFarmaciaDispatchNotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var empacadoAt = pedido.FarmaciaEmpacadoAtUtc!.Value;
        var tiempoDesdeEmpacado = now - empacadoAt;

        if (tiempoDesdeEmpacado < IntervaloRecordatorioAuxiliar)
        {
            return;
        }

        var ultimaNotif = pedido.FarmaciaNotifAuxiliarUltimaUtc;

        bool debeNotificar;
        if (ultimaNotif == null)
        {
            debeNotificar = true;
        }
        else
        {
            debeNotificar = (now - ultimaNotif.Value) >= IntervaloRecordatorioAuxiliar;
        }

        if (!debeNotificar)
        {
            return;
        }

        var warnings = await notificationService.NotifyEmpacadoPendienteAuxiliarAsync(pedido, cancellationToken);
        foreach (var warning in warnings)
        {
            _logger.LogWarning("Notificacion auxiliar pedido {Id}: {Warning}", pedido.Id, warning);
        }

        pedido.FarmaciaNotifAuxiliarUltimaUtc = now;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task TryNotifyGerenciaAsync(
        CensoRecord pedido,
        DateTime now,
        ApplicationDbContext context,
        IFarmaciaDispatchNotificationService notificationService,
        CancellationToken cancellationToken)
    {
        if (pedido.FarmaciaNotif24hRestanteUtc != null)
        {
            return;
        }

        var empacadoAt = pedido.FarmaciaEmpacadoAtUtc!.Value;
        var horasRestantes = (VentanaEmpacado - (now - empacadoAt)).TotalHours;

        if (horasRestantes > UmbralAlertaVencimiento.TotalHours)
        {
            return;
        }

        var warnings = await notificationService.NotifyEmpacadoPorVencerGerenciaAsync(pedido, cancellationToken);
        foreach (var warning in warnings)
        {
            _logger.LogWarning("Notificacion gerencia pedido {Id}: {Warning}", pedido.Id, warning);
        }

        pedido.FarmaciaNotif24hRestanteUtc = now;
        await context.SaveChangesAsync(cancellationToken);
    }
}

