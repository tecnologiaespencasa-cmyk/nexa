using System.Security.Claims;
using IntranetPrueba.Data;
using IntranetPrueba.Data.Entities;
using IntranetPrueba.Helpers;
using IntranetPrueba.Models.EspacioCorporativo;
using IntranetPrueba.Models.Security;
using IntranetPrueba.Models.ViewModels;
using IntranetPrueba.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Controllers;

[Authorize(Policy = SystemPermissions.EspacioCorporativoAccess)]
public partial class EspacioCorporativoController : Controller
{
    private const string SuccessMessageKey = "SuccessMessage";
    private const string ErrorMessageKey = "ErrorMessage";
    private const string WarningMessageKey = "WarningMessage";

    private readonly ApplicationDbContext _context;
    private readonly IEspacioCorporativoNotificationService _notificationService;
    private readonly ICurrentUserPermissionService _currentUserPermissionService;
    private readonly IAuditService _auditService;
    private readonly ILogger<EspacioCorporativoController> _logger;

    public EspacioCorporativoController(
        ApplicationDbContext context,
        IEspacioCorporativoNotificationService notificationService,
        ICurrentUserPermissionService currentUserPermissionService,
        IAuditService auditService,
        ILogger<EspacioCorporativoController> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _currentUserPermissionService = currentUserPermissionService;
        _auditService = auditService;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pantalla principal
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var esAdministrador = await IsAdminAsync();

        var model = new EspacioCorporativoIndexViewModel
        {
            NombreUsuario = GetCurrentUserFullName(),
            EsAdministrador = esAdministrador,
            Categorias = EspacioCorporativoCatalogos.CategoriasDocumento,
            TiposDocumento = EspacioCorporativoCatalogos.TiposDocumento,
            TiposNovedad = EspacioCorporativoCatalogos.TiposNovedad
        };

        if (userId.HasValue)
        {
            model.MisActivos = await BuildMisActivosAsync(userId.Value, cancellationToken);
            model.MisNovedades = await BuildMisNovedadesAsync(userId.Value, cancellationToken);
        }

        model.NovedadesAbiertas = model.MisNovedades
            .Count(novedad => !EspacioCorporativoCatalogos.EsEstadoNovedadCerrado(novedad.Estado));

        model.Documentos = await BuildDocumentosPublicadosAsync(userId, cancellationToken);
        model.TotalDocumentos = model.Documentos.Count;
        model.ConteoPorCategoria = model.Documentos
            .GroupBy(documento => documento.Categoria)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return View(model);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Novedades reportadas por el colaborador
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportarNovedad(
        EspacioNovedadFormViewModel model,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Forbid();
        }

        if (!EspacioCorporativoCatalogos.EsTipoNovedadValido(model.Tipo))
        {
            ModelState.AddModelError(nameof(model.Tipo), "Selecciona un tipo de novedad valido.");
        }

        EspacioActivo? activo = null;
        if (model.ActivoId.HasValue)
        {
            activo = await _context.EspacioActivos
                .FirstOrDefaultAsync(
                    x => x.Id == model.ActivoId.Value && !x.Eliminado,
                    cancellationToken);

            if (activo is null)
            {
                ModelState.AddModelError(nameof(model.ActivoId), "El equipo seleccionado no existe.");
            }
            else if (activo.ResponsableUserId != userId.Value)
            {
                // Solo el responsable puede reportar novedades del equipo asignado.
                ModelState.AddModelError(nameof(model.ActivoId), "El equipo seleccionado no esta asignado a tu usuario.");
                activo = null;
            }
        }
        else if (string.IsNullOrWhiteSpace(model.EquipoReferencia))
        {
            ModelState.AddModelError(
                nameof(model.EquipoReferencia),
                "Indica a que equipo corresponde la novedad.");
        }

        if (!ModelState.IsValid)
        {
            TempData[ErrorMessageKey] = string.Join(
                " ",
                ModelState.Values
                    .SelectMany(state => state.Errors)
                    .Select(error => error.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message)));
            return RedirectToAction(nameof(Index));
        }

        var novedad = new EspacioActivoNovedad
        {
            EspacioActivoId = activo?.Id,
            EquipoReferencia = activo is null ? model.EquipoReferencia?.Trim() : null,
            ReportadoPorUserId = userId.Value,
            ReportadoPorNombre = GetCurrentUserFullName(),
            ReportadoPorEmail = await GetCurrentUserEmailAsync(userId.Value, cancellationToken),
            Tipo = model.Tipo.Trim(),
            Descripcion = model.Descripcion.Trim(),
            Estado = EspacioCorporativoCatalogos.EstadoNovedadReportada,
            Clasificacion = EspacioCorporativoCatalogos.ClasificacionSinClasificar,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _context.EspacioActivoNovedades.AddAsync(novedad, cancellationToken);

        if (activo is not null)
        {
            await RegistrarMovimientoAsync(
                activo.Id,
                "Novedad",
                $"{novedad.Tipo}: {Resumir(novedad.Descripcion, 300)}",
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var notificado = await _notificationService.NotifyNovedadCreadaAsync(novedad, activo, cancellationToken);
        if (notificado)
        {
            novedad.NotificacionEnviada = true;
            await _context.SaveChangesAsync(cancellationToken);
            TempData[SuccessMessageKey] = $"Novedad #{novedad.Id} registrada. El area de TI fue notificada por correo.";
        }
        else
        {
            TempData[SuccessMessageKey] = $"Novedad #{novedad.Id} registrada correctamente.";
            TempData[WarningMessageKey] = "No fue posible enviar el correo de notificacion; el area de TI vera la novedad en la bandeja de activos.";
        }

        await LogAuditAsync(
            "ESPACIO_NOVEDAD_CREADA",
            $"Novedad #{novedad.Id} ({novedad.Tipo}) sobre {(activo is null ? novedad.EquipoReferencia : activo.Serial)}",
            cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Favoritos de documentacion
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlternarFavorito(long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Forbid();
        }

        var existeDocumento = await _context.EspacioDocumentos
            .AnyAsync(x => x.Id == id && !x.Eliminado && x.Publicado, cancellationToken);

        if (!existeDocumento)
        {
            return NotFound();
        }

        var favorito = await _context.EspacioDocumentoFavoritos
            .FirstOrDefaultAsync(
                x => x.EspacioDocumentoId == id && x.UserId == userId.Value,
                cancellationToken);

        bool esFavorito;
        if (favorito is null)
        {
            await _context.EspacioDocumentoFavoritos.AddAsync(
                new EspacioDocumentoFavorito
                {
                    EspacioDocumentoId = id,
                    UserId = userId.Value,
                    CreatedAtUtc = DateTime.UtcNow
                },
                cancellationToken);
            esFavorito = true;
        }
        else
        {
            _context.EspacioDocumentoFavoritos.Remove(favorito);
            esFavorito = false;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, esFavorito });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Descarga / apertura de documentos
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Documento(long id, CancellationToken cancellationToken)
    {
        var esAdministrador = await IsAdminAsync();

        var documento = await _context.EspacioDocumentos
            .FirstOrDefaultAsync(
                x => x.Id == id && !x.Eliminado && (x.Publicado || esAdministrador),
                cancellationToken);

        if (documento is null)
        {
            return NotFound();
        }

        if (!string.Equals(documento.TipoContenido, EspacioCorporativoCatalogos.TipoContenidoArchivo, StringComparison.OrdinalIgnoreCase)
            || documento.ArchivoContenido is null
            || documento.ArchivoContenido.Length == 0)
        {
            return NotFound();
        }

        documento.Descargas += 1;
        await _context.SaveChangesAsync(cancellationToken);

        var contentType = string.IsNullOrWhiteSpace(documento.ArchivoContentType)
            ? "application/octet-stream"
            : documento.ArchivoContentType;

        var fileName = string.IsNullOrWhiteSpace(documento.ArchivoNombre)
            ? $"documento-{documento.Id}"
            : documento.ArchivoNombre;

        // Siempre "inline": la accion del colaborador es consultar el documento, no descargarlo.
        // Los formatos que el navegador no sabe pintar (Office, ZIP) los resuelve el propio
        // navegador; nosotros no forzamos la descarga.
        Response.Headers.ContentDisposition = $"inline; filename=\"{SanitizarNombreArchivo(fileName)}\"";
        return File(documento.ArchivoContenido, contentType);
    }

    [HttpGet]
    public async Task<IActionResult> DocumentoTexto(long id, CancellationToken cancellationToken)
    {
        var esAdministrador = await IsAdminAsync();

        var documento = await _context.EspacioDocumentos
            .AsNoTracking()
            .Where(x => x.Id == id && !x.Eliminado && (x.Publicado || esAdministrador))
            .Select(x => new
            {
                x.Id,
                x.Titulo,
                x.Descripcion,
                x.Categoria,
                x.TipoDocumento,
                x.TipoContenido,
                x.Version,
                x.CodigoDocumento,
                x.ContenidoTexto,
                x.CreadoPorNombre,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (documento is null)
        {
            return NotFound();
        }

        return Json(new
        {
            ok = true,
            id = documento.Id,
            titulo = documento.Titulo,
            descripcion = documento.Descripcion,
            categoria = documento.Categoria,
            tipoDocumento = documento.TipoDocumento,
            version = documento.Version,
            codigo = documento.CodigoDocumento,
            contenido = documento.ContenidoTexto,
            autor = documento.CreadoPorNombre,
            publicado = ColombiaTime.Convert(documento.CreatedAtUtc).ToString("dd/MM/yyyy"),
            actualizado = documento.UpdatedAtUtc.HasValue
                ? ColombiaTime.Convert(documento.UpdatedAtUtc.Value).ToString("dd/MM/yyyy")
                : null
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Consultas compartidas
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<EspacioActivoAsignadoViewModel>> BuildMisActivosAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var activos = await _context.EspacioActivos
            .AsNoTracking()
            .Where(x => x.ResponsableUserId == userId && !x.Eliminado)
            .OrderBy(x => x.TipoActivo)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (activos.Count == 0)
        {
            return [];
        }

        var activoIds = activos.Select(x => x.Id).ToList();
        var novedadesAbiertas = await _context.EspacioActivoNovedades
            .AsNoTracking()
            .Where(x => x.EspacioActivoId != null
                && activoIds.Contains(x.EspacioActivoId.Value)
                && x.Estado != EspacioCorporativoCatalogos.EstadoNovedadResuelta
                && x.Estado != EspacioCorporativoCatalogos.EstadoNovedadRechazada)
            .GroupBy(x => x.EspacioActivoId!.Value)
            .Select(group => new { ActivoId = group.Key, Total = group.Count() })
            .ToDictionaryAsync(x => x.ActivoId, x => x.Total, cancellationToken);

        return activos
            .Select(activo => new EspacioActivoAsignadoViewModel
            {
                Id = activo.Id,
                TipoActivo = activo.TipoActivo,
                NombreEquipo = activo.NombreEquipo,
                Marca = activo.Marca,
                Serie = activo.Serie,
                Serial = activo.Serial,
                CodigoActivo = activo.CodigoActivo,
                Especificaciones = activo.Especificaciones,
                Estado = activo.Estado,
                Nota = activo.Nota,
                FechaAsignacion = ColombiaTime.Convert(activo.FechaAsignacionUtc),
                NovedadesAbiertas = novedadesAbiertas.TryGetValue(activo.Id, out var total) ? total : 0
            })
            .ToList();
    }

    private async Task<IReadOnlyList<EspacioNovedadResumenViewModel>> BuildMisNovedadesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var novedades = await _context.EspacioActivoNovedades
            .AsNoTracking()
            .Include(x => x.EspacioActivo)
            .Where(x => x.ReportadoPorUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        return novedades
            .Select(novedad => new EspacioNovedadResumenViewModel
            {
                Id = novedad.Id,
                Tipo = novedad.Tipo,
                Estado = novedad.Estado,
                Prioridad = novedad.Prioridad,
                Clasificacion = novedad.Clasificacion,
                Descripcion = novedad.Descripcion,
                EquipoDescripcion = DescribirEquipo(novedad),
                RespuestaAdmin = novedad.RespuestaAdmin,
                FechaReporte = ColombiaTime.Convert(novedad.CreatedAtUtc),
                FechaResolucion = ColombiaTime.Convert(novedad.ResueltoAtUtc)
            })
            .ToList();
    }

    private async Task<IReadOnlyList<EspacioDocumentoTarjetaViewModel>> BuildDocumentosPublicadosAsync(
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var documentos = await _context.EspacioDocumentos
            .AsNoTracking()
            .Where(x => !x.Eliminado && x.Publicado)
            .OrderByDescending(x => x.Destacado)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Titulo,
                x.Descripcion,
                x.Categoria,
                x.TipoDocumento,
                x.TipoContenido,
                x.Version,
                x.CodigoDocumento,
                x.Etiquetas,
                x.ArchivoNombre,
                x.ArchivoTamanoBytes,
                x.EnlaceUrl,
                x.Destacado,
                x.Descargas,
                x.CreadoPorNombre,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (documentos.Count == 0)
        {
            return [];
        }

        var favoritos = userId.HasValue
            ? (await _context.EspacioDocumentoFavoritos
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value)
                .Select(x => x.EspacioDocumentoId)
                .ToListAsync(cancellationToken))
                .ToHashSet()
            : [];

        return documentos
            .Select(documento => new EspacioDocumentoTarjetaViewModel
            {
                Id = documento.Id,
                Titulo = documento.Titulo,
                Descripcion = documento.Descripcion,
                Categoria = documento.Categoria,
                TipoDocumento = documento.TipoDocumento,
                TipoContenido = documento.TipoContenido,
                Version = documento.Version,
                CodigoDocumento = documento.CodigoDocumento,
                Etiquetas = documento.Etiquetas,
                ArchivoNombre = documento.ArchivoNombre,
                ExtensionArchivo = ObtenerExtension(documento.ArchivoNombre),
                TamanoLegible = FormatearTamano(documento.ArchivoTamanoBytes),
                EnlaceUrl = documento.EnlaceUrl,
                Destacado = documento.Destacado,
                EsFavorito = favoritos.Contains(documento.Id),
                Descargas = documento.Descargas,
                CreadoPorNombre = documento.CreadoPorNombre,
                FechaPublicacion = ColombiaTime.Convert(documento.CreatedAtUtc),
                FechaActualizacion = ColombiaTime.Convert(documento.UpdatedAtUtc),
                TextoBusqueda = string.Join(
                        ' ',
                        new[]
                        {
                            documento.Titulo,
                            documento.Descripcion,
                            documento.Categoria,
                            documento.TipoDocumento,
                            documento.Etiquetas,
                            documento.CodigoDocumento,
                            documento.ArchivoNombre
                        }.Where(value => !string.IsNullOrWhiteSpace(value)))
                    .ToLowerInvariant()
            })
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private Task<bool> IsAdminAsync() =>
        _currentUserPermissionService.HasPermissionAsync(User, SystemPermissions.EspacioCorporativoAdmin);

    private Guid? GetCurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    private string GetCurrentUserFullName() =>
        User.FindFirstValue("full_name")
        ?? User.Identity?.Name
        ?? "Usuario";

    private async Task<string?> GetCurrentUserEmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        var claimEmail = User.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrWhiteSpace(claimEmail))
        {
            return claimEmail;
        }

        return await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.Email)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task RegistrarMovimientoAsync(
        long activoId,
        string tipo,
        string detalle,
        CancellationToken cancellationToken)
    {
        await _context.EspacioActivoMovimientos.AddAsync(
            new EspacioActivoMovimiento
            {
                EspacioActivoId = activoId,
                Tipo = tipo,
                Detalle = Resumir(detalle, 600),
                RegistradoPorNombre = GetCurrentUserFullName(),
                RegistradoAtUtc = DateTime.UtcNow
            },
            cancellationToken);
    }

    private async Task LogAuditAsync(string action, string details, CancellationToken cancellationToken)
    {
        try
        {
            await _auditService.LogAsync(
                action,
                "EspacioCorporativo",
                Resumir(details, 2000),
                GetCurrentUserId(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar la auditoria de {Action}.", action);
        }
    }

    private static string DescribirEquipo(EspacioActivoNovedad novedad)
    {
        if (novedad.EspacioActivo is null)
        {
            return string.IsNullOrWhiteSpace(novedad.EquipoReferencia)
                ? "Equipo no registrado"
                : novedad.EquipoReferencia;
        }

        var nombre = string.IsNullOrWhiteSpace(novedad.EspacioActivo.NombreEquipo)
            ? $"{novedad.EspacioActivo.TipoActivo} {novedad.EspacioActivo.Marca}".Trim()
            : novedad.EspacioActivo.NombreEquipo;

        return $"{nombre} - Serial {novedad.EspacioActivo.Serial}";
    }

    private static string Resumir(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : string.Concat(normalized.AsSpan(0, maxLength - 3), "...");
    }

    private static string? ObtenerExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(extension)
            ? null
            : extension.TrimStart('.').ToUpperInvariant();
    }

    private static string? FormatearTamano(long? bytes)
    {
        if (!bytes.HasValue || bytes.Value <= 0)
        {
            return null;
        }

        string[] unidades = ["B", "KB", "MB", "GB"];
        double tamano = bytes.Value;
        var indice = 0;

        while (tamano >= 1024 && indice < unidades.Length - 1)
        {
            tamano /= 1024;
            indice++;
        }

        return $"{tamano:0.#} {unidades[indice]}";
    }

    private static string SanitizarNombreArchivo(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars().Concat(['"', '\\', '\r', '\n']).ToArray();
        var sanitized = new string(fileName.Where(character => !invalidChars.Contains(character)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "documento" : sanitized;
    }
}
