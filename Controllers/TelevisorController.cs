using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexa.Models.Security;
using Nexa.Services.Interfaces;

namespace Nexa.Controllers;

/// <summary>
/// Televisor: indicadores asistenciales del día para la pantalla del área (admisiones, analistas
/// asistenciales y novedades del portal). La página es autónoma —sin la barra de la intranet— y
/// se actualiza sola pidiendo <see cref="Panel"/> cada minuto, sin recargar: recargar le quitaría
/// al navegador el permiso de sonido que se le dio con un clic, y el aviso de la 1:30 p. m. no
/// sonaría.
/// </summary>
[Authorize(Policy = SystemPermissions.Televisor)]
public class TelevisorController : Controller
{
    private readonly ITelevisorCortesService _cortes;
    private readonly IAuditService _auditService;
    private readonly ILogger<TelevisorController> _logger;

    public TelevisorController(
        ITelevisorCortesService cortes,
        IAuditService auditService,
        ILogger<TelevisorController> logger)
    {
        _cortes = cortes;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? dia, string? bloque, CancellationToken cancellationToken)
    {
        var modelo = await _cortes.PantallaAsync(dia, bloque, cancellationToken);
        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Panel(DateTime? dia, string? bloque, CancellationToken cancellationToken)
    {
        try
        {
            var modelo = await _cortes.PantallaAsync(dia, bloque, cancellationToken);
            Response.Headers.CacheControl = "no-store";
            // La pantalla corrige su reloj con la hora del servidor en cada actualización.
            Response.Headers["X-Servidor-Ms"] = modelo.ServidorUtcMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return PartialView("_Tablero", modelo);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // La pantalla conserva lo último que mostró y avisa que no se pudo actualizar.
            _logger.LogError(ex, "Televisor: no se pudo armar el tablero.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost]
    public async Task<IActionResult> AjustarTurno(DateTime dia, int personaId, string turno, CancellationToken cancellationToken)
    {
        var usuario = User.Identity?.Name;
        var ok = await _cortes.AjustarTurnoAsync(dia, personaId, turno, usuario, cancellationToken);
        if (!ok)
        {
            return BadRequest(new { message = "No se pudo ajustar el turno." });
        }

        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? (Guid?)id : null;
        await _auditService.LogAsync(
            "TELEVISOR_TURNO_AJUSTADO",
            "Televisor",
            $"Persona {personaId}, día {dia:yyyy-MM-dd}: {turno}",
            userId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return Ok(new { ok = true });
    }
}
