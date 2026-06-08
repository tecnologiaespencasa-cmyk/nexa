using IntranetPrueba.Models.Security;
using IntranetPrueba.Models.ViewModels;
using IntranetPrueba.Services.Interfaces;
using IntranetPrueba.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntranetPrueba.Controllers;

[Authorize(Policy = SystemPermissions.AuditRead)]
public class AuditController : Controller
{
    private readonly IAuditQueryService _auditQueryService;

    public AuditController(IAuditQueryService auditQueryService)
    {
        _auditQueryService = auditQueryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AuditFilterViewModel filter, CancellationToken cancellationToken)
    {
        var serviceResult = await _auditQueryService.SearchAsync(
            new AuditLogSearchRequest
            {
                FromDate = filter.FromDate,
                ToDate = filter.ToDate,
                Username = filter.Username,
                Action = filter.Action,
                Take = 500
            },
            cancellationToken);

        if (!serviceResult.Succeeded || serviceResult.Value is null)
        {
            ModelState.AddModelError(string.Empty, serviceResult.ErrorMessage ?? "No se pudo consultar la bitacora.");
            return View(new AuditIndexViewModel { Filter = filter });
        }

        var model = new AuditIndexViewModel
        {
            Filter = filter,
            AvailableActions = serviceResult.Value.AvailableActions.ToList(),
            Logs = serviceResult.Value.Logs.Select(log => new AuditLogItemViewModel
            {
                Id = log.Id,
                PerformedAtUtc = log.PerformedAtUtc,
                Action = log.Action,
                Entity = log.Entity,
                Details = log.Details,
                IpAddress = log.IpAddress,
                Username = log.Username,
                FullName = log.FullName
            }).ToList()
        };

        return View(model);
    }
}
