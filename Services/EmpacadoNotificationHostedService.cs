using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Services;

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

        // Las requisiciones de clínica de heridas siguen el mismo calendario de recordatorios.
        var requisiciones = await context.CensoClinicaHeridasKardex
            .Where(x => x.FarmaciaEstado == FarmaciaEstados.Empacado
                && x.FarmaciaEmpacadoAtUtc != null
                && x.FarmaciaEmpacadoAtUtc > cutoffEmpacado)
            .ToListAsync(cancellationToken);

        foreach (var requisicion in requisiciones)
        {
            await TryNotifyHeridasAsync(requisicion, now, context, notificationService, cancellationToken);
        }
    }

    private async Task TryNotifyHeridasAsync(
        CensoClinicaHeridasKardex requisicion,
        DateTime now,
        ApplicationDbContext context,
        IFarmaciaDispatchNotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var empacadoAt = requisicion.FarmaciaEmpacadoAtUtc!.Value;
        var guardar = false;

        // Recordatorio al auxiliar cada 24 h desde que se empacó.
        if ((now - empacadoAt) >= IntervaloRecordatorioAuxiliar)
        {
            var ultima = requisicion.FarmaciaNotifAuxiliarUltimaUtc;
            if (ultima is null || (now - ultima.Value) >= IntervaloRecordatorioAuxiliar)
            {
                var avisos = await notificationService
                    .NotifyClinicaHeridasEmpacadoPendienteAuxiliarAsync(requisicion, cancellationToken);

                foreach (var aviso in avisos)
                {
                    _logger.LogWarning("Notificacion auxiliar requisicion heridas {Id}: {Warning}", requisicion.Id, aviso);
                }

                requisicion.FarmaciaNotifAuxiliarUltimaUtc = now;
                guardar = true;
            }
        }

        // Aviso único a gerencia cuando quedan 24 h para el desempaque.
        if (requisicion.FarmaciaNotif24hRestanteUtc is null)
        {
            var horasRestantes = (VentanaEmpacado - (now - empacadoAt)).TotalHours;
            if (horasRestantes <= UmbralAlertaVencimiento.TotalHours)
            {
                var avisos = await notificationService
                    .NotifyClinicaHeridasEmpacadoPorVencerGerenciaAsync(requisicion, cancellationToken);

                foreach (var aviso in avisos)
                {
                    _logger.LogWarning("Notificacion gerencia requisicion heridas {Id}: {Warning}", requisicion.Id, aviso);
                }

                requisicion.FarmaciaNotif24hRestanteUtc = now;
                guardar = true;
            }
        }

        if (guardar)
        {
            await context.SaveChangesAsync(cancellationToken);
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

