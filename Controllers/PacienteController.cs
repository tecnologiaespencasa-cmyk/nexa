using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexa.Data.Repositories.Interfaces;
using Nexa.Data.Repositories.Models;
using Nexa.Models.Security;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;

namespace Nexa.Controllers;

/// <summary>
/// Hoja de vida del paciente. Solo consulta: ninguna acción escribe en el censo ni en el portal.
///
/// El documento viaja por POST y no por la URL, para que no quede en el historial del navegador,
/// en los registros de los servidores intermedios ni en un enlace copiado. La respuesta se marca
/// como no almacenable por la misma razón: es información clínica.
/// </summary>
[Authorize(Policy = SystemPermissions.HojaVidaPaciente)]
public class PacienteController : Controller
{
    private readonly IHojaVidaPacienteService _hojaVidaPacienteService;
    private readonly IAuditService _auditService;
    private readonly INeonClinicaHeridasRepository _neonClinicaHeridasRepository;
    private readonly ISharePointDocumentService _sharePointDocumentService;
    private readonly ILogger<PacienteController> _logger;

    public PacienteController(
        IHojaVidaPacienteService hojaVidaPacienteService,
        IAuditService auditService,
        INeonClinicaHeridasRepository neonClinicaHeridasRepository,
        ISharePointDocumentService sharePointDocumentService,
        ILogger<PacienteController> logger)
    {
        _hojaVidaPacienteService = hojaVidaPacienteService;
        _auditService = auditService;
        _neonClinicaHeridasRepository = neonClinicaHeridasRepository;
        _sharePointDocumentService = sharePointDocumentService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        NoAlmacenar();
        return View(new HojaVidaPacienteViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string? documento, CancellationToken cancellationToken)
    {
        NoAlmacenar();

        var model = await _hojaVidaPacienteService.ConstruirAsync(documento, cancellationToken);

        // Toda consulta de una historia clínica deja rastro de quién la abrió, también las que no
        // encuentran al paciente: buscar documentos al azar también es un acceso que interesa ver.
        if (model.ErrorBusqueda is null)
        {
            var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? (Guid?)parsed : null;
            await _auditService.LogAsync(
                "HOJA_VIDA_PACIENTE_CONSULTADA",
                "CensoPaciente",
                $"Doc: {model.Documento}, encontrado: {(model.Encontrado ? "sí" : "no")}",
                userId,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);
        }

        return View(model);
    }

    // Proxy de las fotos de la herida, igual al del censo pero bajo el permiso de esta pantalla:
    // quien consulta la hoja de vida no necesariamente tiene acceso al censo. Solo sirve archivos
    // registrados en Neon como foto de seguimiento; cualquier otro identificador devuelve 404
    // aunque exista en SharePoint.
    [HttpGet]
    public async Task<IActionResult> FotoHerida(string? driveItemId, bool miniatura, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(driveItemId) || driveItemId.Length > 200)
        {
            return NotFound();
        }

        ClinicaHeridasFotoRow? foto;
        try
        {
            foto = await _neonClinicaHeridasRepository.GetFotoPorDriveItemIdAsync(driveItemId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hoja de vida: no fue posible validar la foto de clínica de heridas en Neon.");
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        if (foto is null)
        {
            return NotFound();
        }

        var result = await _sharePointDocumentService.GetClinicaHeridasPhotoAsync(driveItemId, miniatura, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        Response.Headers.CacheControl = "private, max-age=3600";
        return File(result.Value.Content, result.Value.ContentType);
    }

    private void NoAlmacenar()
    {
        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";
    }
}
