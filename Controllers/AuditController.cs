using Nexa.Models.Security;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexa.Controllers;

[Authorize(Policy = SystemPermissions.AuditRead)]
public class AuditController : Controller
{
    private const int AuditPageSize = 100;
    private readonly IAuditQueryService _auditQueryService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(
        IAuditQueryService auditQueryService,
        ILogger<AuditController> logger)
    {
        _auditQueryService = auditQueryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AuditFilterViewModel filter, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var model = new AuditIndexViewModel
        {
            Filter = filter,
            EarliestAllowedDate = AuditRetentionPolicy.GetEarliestAllowedDate(nowUtc),
            LatestAllowedDate = AuditRetentionPolicy.GetLatestAllowedDate(nowUtc)
        };

        try
        {
            var stats = await _auditQueryService.GetTodayStatsAsync(cancellationToken);
            var serviceResult = await _auditQueryService.SearchAsync(
                new AuditLogSearchRequest
                {
                    FromDate = filter.FromDate,
                    ToDate = filter.ToDate,
                    Username = filter.Username,
                    Action = filter.Action,
                    Category = filter.Category,
                    Page = filter.Page,
                    PageSize = AuditPageSize
                },
                cancellationToken);

            if (!serviceResult.Succeeded || serviceResult.Value is null)
            {
                ModelState.AddModelError(string.Empty, serviceResult.ErrorMessage ?? "No se pudo consultar la bitácora.");
                return View(model);
            }

            model.AvailableActions = serviceResult.Value.AvailableActions.ToList();
            model.CategoryCounts = serviceResult.Value.CategoryCounts;
            model.TotalCount = serviceResult.Value.TotalCount;
            model.CurrentPage = serviceResult.Value.CurrentPage;
            model.PageSize = serviceResult.Value.PageSize;
            model.TotalPages = serviceResult.Value.TotalPages;
            model.Filter.Page = serviceResult.Value.CurrentPage;
            model.Stats = new AuditStatsViewModel
            {
                TodayTotal = stats.TotalEvents,
                TodayUniqueUsers = stats.UniqueUsers,
                TodayFailedLogins = stats.FailedLogins,
                TodayOperational = stats.OperationalEvents
            };
            model.Logs = serviceResult.Value.Logs.Select(log => new AuditLogItemViewModel
            {
                Id = log.Id,
                PerformedAtUtc = log.PerformedAtUtc,
                Action = log.Action,
                Entity = log.Entity,
                Details = log.Details,
                IpAddress = log.IpAddress,
                Username = log.Username,
                FullName = log.FullName
            }).ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "No fue posible cargar el centro de auditoría.");
            ModelState.AddModelError(string.Empty, "No fue posible cargar la auditoría. Intenta nuevamente.");
        }

        return View(model);
    }
}
