using System.Security.Claims;
using System.Text.Json;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Nexa.Controllers;

/// <summary>
/// Pantalla de control de la sincronizacion hacia la base puente de Supabase.
/// Reservada a administradores: dispara envios de datos de pacientes hacia un
/// sistema externo, aunque solo se persistan alli en forma de HMAC.
/// </summary>
[Authorize(Policy = "AdminOnly")]
public class BridgeSyncController : Controller
{
    private const string LastRunKey = "BridgeSyncLastRun";
    private const string LastRunTitleKey = "BridgeSyncLastRunTitle";

    private readonly IClinicaHeridasBridgeSyncService _bridgeSyncService;
    private readonly IAuditService _auditService;
    private readonly SupabaseBridgeOptions _options;
    private readonly ILogger<BridgeSyncController> _logger;

    public BridgeSyncController(
        IClinicaHeridasBridgeSyncService bridgeSyncService,
        IAuditService auditService,
        IOptions<SupabaseBridgeOptions> options,
        ILogger<BridgeSyncController> logger)
    {
        _bridgeSyncService = bridgeSyncService;
        _auditService = auditService;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new BridgeSyncIndexViewModel
        {
            IsConfigured = _bridgeSyncService.IsConfigured,
            ProjectHost = ExtractHost(_options.ProjectUrl),
            FunctionName = _options.FunctionName,
            HasApiSecret = !string.IsNullOrWhiteSpace(_options.ApiSecret),
            HasHmacSecret = !string.IsNullOrWhiteSpace(_options.HmacSecret),
            HashInIntranet = _options.HashInIntranet,
            BatchSize = _options.BatchSize,
            TimeoutSeconds = _options.TimeoutSeconds,
            MaxRetries = _options.MaxRetries
        };

        try
        {
            model.PatientsAvailable = await _bridgeSyncService.CountPatientsAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No fue posible contar los pacientes de clinica de heridas para el puente.");
            model.PatientsAvailableError = "No fue posible consultar el censo de clinica de heridas.";
        }

        if (TempData[LastRunKey] is string serialized)
        {
            model.LastRun = JsonSerializer.Deserialize<BridgeSyncSummary>(serialized);
            model.LastRunTitle = TempData[LastRunTitleKey] as string;
        }

        return View(model);
    }

    /// <summary>
    /// Ejecuta la sincronizacion. El limite permite la puesta en marcha
    /// escalonada: 1 paciente, luego 5, luego todos.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ejecutar(int limite, bool simulacion, CancellationToken cancellationToken)
    {
        // limite <= 0 significa "todos los pacientes".
        int? efectivo = limite > 0 ? limite : null;

        var result = await _bridgeSyncService.SyncAsync(efectivo, simulacion, cancellationToken);

        var alcance = efectivo.HasValue ? $"{efectivo.Value} paciente(s)" : "todos los pacientes";
        var modo = simulacion ? "Simulacion" : "Sincronizacion";

        if (!result.Succeeded || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "No fue posible ejecutar la sincronizacion.";
            await LogAuditAsync(
                "BRIDGE_SUPABASE_SYNC_FALLIDA",
                $"Alcance: {alcance}. Simulacion: {simulacion}. Detalle tecnico: {result.ErrorMessage}",
                cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        var summary = result.Value;
        TempData[LastRunKey] = JsonSerializer.Serialize(summary);
        TempData[LastRunTitleKey] = $"{modo} - {alcance}";

        TempData["SuccessMessage"] = simulacion
            ? $"Simulacion completada: {summary.PatientsSent} paciente(s) se enviarian de {summary.PatientsFound} encontrados. No se llamo a Supabase."
            : $"Sincronizacion completada: {summary.PatientsProcessed} paciente(s) procesados en Supabase ({summary.Inserted} nuevos, {summary.Updated} actualizados).";

        if (summary.BatchesFailed > 0)
        {
            TempData["ErrorMessage"] = $"{summary.BatchesFailed} lote(s) fallaron. Revisa el detalle tecnico.";
        }

        await LogAuditAsync(
            simulacion ? "BRIDGE_SUPABASE_SYNC_SIMULADA" : "BRIDGE_SUPABASE_SYNC_EJECUTADA",
            $"Alcance: {alcance}. Encontrados: {summary.PatientsFound}. Enviados: {summary.PatientsSent}. "
            + $"Procesados: {summary.PatientsProcessed}. Lotes ok: {summary.BatchesSent}. Lotes con error: {summary.BatchesFailed}. "
            + $"Duracion: {summary.DurationMs} ms.",
            cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Auditoria con datos tecnicos unicamente: nunca documento ni nombre.</summary>
    private Task LogAuditAsync(string action, string details, CancellationToken cancellationToken)
    {
        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? (Guid?)parsed : null;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return _auditService.LogAsync(action, "BridgeSupabase", details, userId, ip, cancellationToken);
    }

    private static string ExtractHost(string projectUrl) =>
        Uri.TryCreate(projectUrl, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
}
