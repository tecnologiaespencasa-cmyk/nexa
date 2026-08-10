using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Models.EspacioCorporativo;
using Nexa.Models.Security;
using Nexa.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

public partial class EspacioCorporativoController
{
    // ─────────────────────────────────────────────────────────────────────────
    // Bandeja de administracion de activos y novedades
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> Activos(
        string? busqueda,
        string? estado,
        string? tipo,
        string? responsable,
        string? estadoNovedad,
        CancellationToken cancellationToken)
    {
        var model = new EspacioActivosAdminViewModel
        {
            Busqueda = busqueda?.Trim(),
            EstadoFiltro = estado?.Trim(),
            TipoFiltro = tipo?.Trim(),
            ResponsableFiltro = responsable?.Trim(),
            EstadoNovedadFiltro = estadoNovedad?.Trim(),
            TiposActivo = EspacioCorporativoCatalogos.TiposActivo,
            EstadosActivo = EspacioCorporativoCatalogos.EstadosActivo,
            EstadosNovedad = EspacioCorporativoCatalogos.EstadosNovedad,
            PrioridadesNovedad = EspacioCorporativoCatalogos.PrioridadesNovedad,
            ClasificacionesNovedad = EspacioCorporativoCatalogos.ClasificacionesNovedad,
            Responsables = await BuildResponsablesAsync(cancellationToken)
        };

        var query = _context.EspacioActivos
            .AsNoTracking()
            .Where(x => !x.Eliminado);

        if (!string.IsNullOrWhiteSpace(model.Busqueda))
        {
            var termino = $"%{model.Busqueda}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Serial, termino)
                || EF.Functions.ILike(x.Serie, termino)
                || EF.Functions.ILike(x.Marca, termino)
                || EF.Functions.ILike(x.TipoActivo, termino)
                || (x.NombreEquipo != null && EF.Functions.ILike(x.NombreEquipo, termino))
                || (x.CodigoActivo != null && EF.Functions.ILike(x.CodigoActivo, termino))
                || (x.ResponsableNombre != null && EF.Functions.ILike(x.ResponsableNombre, termino)));
        }

        if (EspacioCorporativoCatalogos.EsEstadoActivoValido(model.EstadoFiltro))
        {
            query = query.Where(x => x.Estado == model.EstadoFiltro);
        }

        if (EspacioCorporativoCatalogos.EsTipoActivoValido(model.TipoFiltro))
        {
            query = query.Where(x => x.TipoActivo == model.TipoFiltro);
        }

        if (Guid.TryParse(model.ResponsableFiltro, out var responsableId))
        {
            query = query.Where(x => x.ResponsableUserId == responsableId);
        }

        var activos = await query
            .OrderBy(x => x.TipoActivo)
            .ThenBy(x => x.Marca)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var novedadesAbiertasPorActivo = await _context.EspacioActivoNovedades
            .AsNoTracking()
            .Where(x => x.EspacioActivoId != null
                && x.Estado != EspacioCorporativoCatalogos.EstadoNovedadResuelta
                && x.Estado != EspacioCorporativoCatalogos.EstadoNovedadRechazada)
            .GroupBy(x => x.EspacioActivoId!.Value)
            .Select(group => new { ActivoId = group.Key, Total = group.Count() })
            .ToDictionaryAsync(x => x.ActivoId, x => x.Total, cancellationToken);

        // Ultima acta firmada por activo: define si el equipo esta entregado o devuelto.
        var actasPorActivo = (await _context.EspacioActivoActas
                .AsNoTracking()
                .Select(x => new { x.EspacioActivoId, x.Tipo, x.FirmadaAtUtc, x.Id })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.EspacioActivoId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Total = group.Count(),
                    Ultima = group.OrderByDescending(x => x.FirmadaAtUtc).ThenByDescending(x => x.Id).First()
                });

        model.Activos = activos
            .Select(activo => new EspacioActivoAdminItemViewModel
            {
                Id = activo.Id,
                TipoActivo = activo.TipoActivo,
                NombreEquipo = activo.NombreEquipo,
                Marca = activo.Marca,
                Serie = activo.Serie,
                Serial = activo.Serial,
                Especificaciones = activo.Especificaciones,
                CodigoActivo = activo.CodigoActivo,
                ResponsableUserId = activo.ResponsableUserId,
                ResponsableNombre = activo.ResponsableNombre,
                Estado = activo.Estado,
                Nota = activo.Nota,
                FechaAsignacion = ColombiaTime.Convert(activo.FechaAsignacionUtc),
                FechaCreacion = ColombiaTime.Convert(activo.CreatedAtUtc),
                FechaActualizacion = ColombiaTime.Convert(activo.UpdatedAtUtc),
                NovedadesAbiertas = novedadesAbiertasPorActivo.TryGetValue(activo.Id, out var total) ? total : 0,
                UltimaActaTipo = actasPorActivo.TryGetValue(activo.Id, out var acta) ? acta.Ultima.Tipo : null,
                UltimaActaFecha = actasPorActivo.TryGetValue(activo.Id, out var actaFecha)
                    ? ColombiaTime.Convert(actaFecha.Ultima.FirmadaAtUtc)
                    : null,
                TotalActas = actasPorActivo.TryGetValue(activo.Id, out var actaTotal) ? actaTotal.Total : 0
            })
            .ToList();

        model.Novedades = await BuildNovedadesAdminAsync(model.EstadoNovedadFiltro, cancellationToken);
        model.Metricas = await BuildMetricasAsync(cancellationToken);

        var miFirma = await GetFirmaGuardadaAsync(cancellationToken);
        model.TieneFirmaGuardada = miFirma is not null;
        model.MiFirmaDataUrl = miFirma?.FirmaDataUrl;
        model.MiFirmaNombre = miFirma?.NombreFirmante ?? GetCurrentUserFullName();
        model.MiFirmaCargo = miFirma?.Cargo;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> GuardarActivo(
        EspacioActivoFormViewModel model,
        CancellationToken cancellationToken)
    {
        NormalizarActivo(model);
        ValidarActivo(model);

        if (!ModelState.IsValid)
        {
            TempData[ErrorMessageKey] = JoinModelStateErrors();
            return RedirectToAction(nameof(Activos));
        }

        var responsable = model.ResponsableUserId.HasValue
            ? await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == model.ResponsableUserId.Value && x.IsActive, cancellationToken)
            : null;

        if (model.ResponsableUserId.HasValue && responsable is null)
        {
            TempData[ErrorMessageKey] = "El responsable seleccionado no existe o está inactivo.";
            return RedirectToAction(nameof(Activos));
        }

        var esNuevo = !model.Id.HasValue;
        EspacioActivo activo;

        if (esNuevo)
        {
            activo = new EspacioActivo { CreatedAtUtc = DateTime.UtcNow, CreadoPorNombre = GetCurrentUserFullName() };
            await _context.EspacioActivos.AddAsync(activo, cancellationToken);
        }
        else
        {
            var existente = await _context.EspacioActivos
                .FirstOrDefaultAsync(x => x.Id == model.Id!.Value && !x.Eliminado, cancellationToken);

            if (existente is null)
            {
                TempData[ErrorMessageKey] = "El activo no existe o fue eliminado.";
                return RedirectToAction(nameof(Activos));
            }

            activo = existente;
        }

        var responsableAnteriorId = activo.ResponsableUserId;
        var responsableAnteriorNombre = activo.ResponsableNombre;
        var estadoAnterior = activo.Estado;

        activo.TipoActivo = model.TipoActivo;
        activo.NombreEquipo = model.NombreEquipo;
        activo.Marca = model.Marca;
        activo.Serie = model.Serie;
        activo.Serial = model.Serial;
        activo.Especificaciones = model.Especificaciones;
        activo.CodigoActivo = model.CodigoActivo;
        activo.Nota = model.Nota;
        activo.ResponsableUserId = responsable?.Id;
        activo.ResponsableNombre = responsable?.FullName;
        activo.ActualizadoPorNombre = GetCurrentUserFullName();

        if (!esNuevo)
        {
            activo.UpdatedAtUtc = DateTime.UtcNow;
        }

        if (responsable is not null && responsableAnteriorId != responsable.Id)
        {
            activo.FechaAsignacionUtc = DateTime.UtcNow;
        }
        else if (responsable is null)
        {
            activo.FechaAsignacionUtc = null;
        }

        activo.Estado = ResolverEstadoActivo(model.Estado, responsable is not null, estadoAnterior, esNuevo);

        await _context.SaveChangesAsync(cancellationToken);

        var movimientos = new List<(string Tipo, string Detalle)>();
        if (esNuevo)
        {
            movimientos.Add(("Creación", $"Activo creado ({activo.TipoActivo} {activo.Marca}, serial {activo.Serial})."));
        }
        else
        {
            movimientos.Add(("Actualización", $"Datos del activo actualizados (serial {activo.Serial})."));
        }

        if (responsableAnteriorId != activo.ResponsableUserId)
        {
            movimientos.Add(activo.ResponsableUserId.HasValue
                ? ("Asignación", $"Asignado a {activo.ResponsableNombre}." +
                    (responsableAnteriorId.HasValue ? $" Responsable anterior: {responsableAnteriorNombre}." : string.Empty))
                : ("Devolución", $"Activo liberado. Responsable anterior: {responsableAnteriorNombre}."));
        }

        if (!esNuevo && !string.Equals(estadoAnterior, activo.Estado, StringComparison.OrdinalIgnoreCase))
        {
            movimientos.Add(("Cambio de estado", $"Estado: {estadoAnterior} -> {activo.Estado}."));
        }

        foreach (var (tipoMovimiento, detalle) in movimientos)
        {
            await RegistrarMovimientoAsync(activo.Id, tipoMovimiento, detalle, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            esNuevo ? "ESPACIO_ACTIVO_CREADO" : "ESPACIO_ACTIVO_ACTUALIZADO",
            $"Activo #{activo.Id} serial {activo.Serial} responsable {activo.ResponsableNombre ?? "sin asignar"}",
            cancellationToken);

        TempData[SuccessMessageKey] = esNuevo
            ? "Activo creado correctamente."
            : "Activo actualizado correctamente.";

        return RedirectToAction(nameof(Activos));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> EliminarActivo(long id, CancellationToken cancellationToken)
    {
        var activo = await _context.EspacioActivos
            .FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado, cancellationToken);

        if (activo is null)
        {
            TempData[ErrorMessageKey] = "El activo no existe o ya fue eliminado.";
            return RedirectToAction(nameof(Activos));
        }

        activo.Eliminado = true;
        activo.EliminadoAtUtc = DateTime.UtcNow;
        activo.UpdatedAtUtc = DateTime.UtcNow;
        activo.ActualizadoPorNombre = GetCurrentUserFullName();

        await RegistrarMovimientoAsync(
            activo.Id,
            "Eliminación",
            $"Activo retirado del inventario (serial {activo.Serial}).",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "ESPACIO_ACTIVO_ELIMINADO",
            $"Activo #{activo.Id} serial {activo.Serial}",
            cancellationToken);

        TempData[SuccessMessageKey] = "Activo eliminado del inventario.";
        return RedirectToAction(nameof(Activos));
    }

    /// <summary>
    /// Guarda la clasificacion de una novedad y, opcionalmente, ejecuta una transicion
    /// del flujo (Reportada -> En proceso -> Resuelta/Rechazada). El estado nunca se
    /// asigna de forma libre: solo se aceptan transiciones validas desde el estado actual.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> GestionarNovedad(
        EspacioNovedadGestionViewModel model,
        CancellationToken cancellationToken)
    {
        var filtroActual = Request.Form["estadoNovedadFiltro"].ToString();

        var novedad = await _context.EspacioActivoNovedades
            .Include(x => x.EspacioActivo)
            .FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);

        if (novedad is null)
        {
            TempData[ErrorMessageKey] = "La novedad no existe.";
            return RedirectToAction(nameof(Activos), new { estadoNovedad = filtroActual });
        }

        var estadoAnterior = novedad.Estado;
        var hayTransicion = !string.IsNullOrWhiteSpace(model.Destino);

        if (hayTransicion && !EspacioCorporativoCatalogos.EsTransicionValida(estadoAnterior, model.Destino))
        {
            TempData[ErrorMessageKey] =
                $"No es posible pasar la novedad #{novedad.Id} de '{estadoAnterior}' a '{model.Destino}'.";
            return RedirectToAction(nameof(Activos), new { estadoNovedad = filtroActual });
        }

        novedad.Clasificacion = EspacioCorporativoCatalogos.EsClasificacionValida(model.Clasificacion)
            ? model.Clasificacion!.Trim()
            : EspacioCorporativoCatalogos.ClasificacionSinClasificar;
        novedad.Prioridad = EspacioCorporativoCatalogos.EsPrioridadValida(model.Prioridad)
            ? model.Prioridad!.Trim()
            : null;
        novedad.RespuestaAdmin = string.IsNullOrWhiteSpace(model.RespuestaAdmin)
            ? null
            : model.RespuestaAdmin.Trim();
        novedad.AtendidoPorNombre = GetCurrentUserFullName();
        novedad.UpdatedAtUtc = DateTime.UtcNow;

        if (hayTransicion)
        {
            novedad.Estado = model.Destino!.Trim();
            novedad.ResueltoAtUtc = EspacioCorporativoCatalogos.EsEstadoNovedadCerrado(novedad.Estado)
                ? DateTime.UtcNow
                : null;

            if (novedad.EspacioActivoId.HasValue)
            {
                await RegistrarMovimientoAsync(
                    novedad.EspacioActivoId.Value,
                    "Gestión novedad",
                    $"Novedad #{novedad.Id}: {estadoAnterior} -> {novedad.Estado}. Clasificación: {novedad.Clasificacion}.",
                    cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (hayTransicion)
        {
            await _notificationService.NotifyNovedadActualizadaAsync(
                novedad,
                novedad.EspacioActivo,
                estadoAnterior,
                cancellationToken);

            await LogAuditAsync(
                "ESPACIO_NOVEDAD_TRANSICION",
                $"Novedad #{novedad.Id}: {estadoAnterior} -> {novedad.Estado}",
                cancellationToken);

            TempData[SuccessMessageKey] = $"Novedad #{novedad.Id}: {estadoAnterior} → {novedad.Estado}.";
        }
        else
        {
            await LogAuditAsync(
                "ESPACIO_NOVEDAD_CLASIFICADA",
                $"Novedad #{novedad.Id} clasificada como {novedad.Clasificacion}",
                cancellationToken);

            TempData[SuccessMessageKey] = $"Novedad #{novedad.Id} actualizada.";
        }

        return RedirectToAction(nameof(Activos), new { estadoNovedad = filtroActual });
    }

    [HttpGet]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> HistorialActivo(long id, CancellationToken cancellationToken)
    {
        var existe = await _context.EspacioActivos.AnyAsync(x => x.Id == id, cancellationToken);
        if (!existe)
        {
            return NotFound();
        }

        var movimientos = await _context.EspacioActivoMovimientos
            .AsNoTracking()
            .Where(x => x.EspacioActivoId == id)
            .OrderByDescending(x => x.RegistradoAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(60)
            .ToListAsync(cancellationToken);

        return Json(new
        {
            ok = true,
            movimientos = movimientos.Select(movimiento => new
            {
                tipo = movimiento.Tipo,
                detalle = movimiento.Detalle,
                usuario = movimiento.RegistradoPorNombre,
                fecha = ColombiaTime.Convert(movimiento.RegistradoAtUtc).ToString("dd/MM/yyyy hh:mm tt")
            })
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers de activos
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<EspacioNovedadAdminItemViewModel>> BuildNovedadesAdminAsync(
        string? estadoFiltro,
        CancellationToken cancellationToken)
    {
        var query = _context.EspacioActivoNovedades
            .AsNoTracking()
            .Include(x => x.EspacioActivo)
            .AsQueryable();

        if (EspacioCorporativoCatalogos.EsEstadoNovedadValido(estadoFiltro))
        {
            query = query.Where(x => x.Estado == estadoFiltro);
        }

        var novedades = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(200)
            .ToListAsync(cancellationToken);

        return novedades
            .Select(novedad => new EspacioNovedadAdminItemViewModel
            {
                Id = novedad.Id,
                ActivoId = novedad.EspacioActivoId,
                EquipoDescripcion = DescribirEquipo(novedad),
                Serial = novedad.EspacioActivo?.Serial,
                CodigoActivo = novedad.EspacioActivo?.CodigoActivo,
                ReportadoPorNombre = novedad.ReportadoPorNombre,
                ReportadoPorEmail = novedad.ReportadoPorEmail,
                Tipo = novedad.Tipo,
                Descripcion = novedad.Descripcion,
                Estado = novedad.Estado,
                Prioridad = novedad.Prioridad,
                Clasificacion = novedad.Clasificacion,
                RespuestaAdmin = novedad.RespuestaAdmin,
                AtendidoPorNombre = novedad.AtendidoPorNombre,
                FechaReporte = ColombiaTime.Convert(novedad.CreatedAtUtc),
                FechaResolucion = ColombiaTime.Convert(novedad.ResueltoAtUtc)
            })
            .ToList();
    }

    private async Task<EspacioActivosMetricasViewModel> BuildMetricasAsync(CancellationToken cancellationToken)
    {
        var porEstado = await _context.EspacioActivos
            .AsNoTracking()
            .Where(x => !x.Eliminado)
            .GroupBy(x => x.Estado)
            .Select(group => new { Estado = group.Key, Total = group.Count() })
            .ToListAsync(cancellationToken);

        var novedadesAbiertas = await _context.EspacioActivoNovedades
            .AsNoTracking()
            .CountAsync(
                x => x.Estado != EspacioCorporativoCatalogos.EstadoNovedadResuelta
                    && x.Estado != EspacioCorporativoCatalogos.EstadoNovedadRechazada,
                cancellationToken);

        var sinClasificar = await _context.EspacioActivoNovedades
            .AsNoTracking()
            .CountAsync(
                x => x.Clasificacion == null || x.Clasificacion == EspacioCorporativoCatalogos.ClasificacionSinClasificar,
                cancellationToken);

        int ContarEstado(string estado) => porEstado
            .Where(item => string.Equals(item.Estado, estado, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Total);

        return new EspacioActivosMetricasViewModel
        {
            Total = porEstado.Sum(item => item.Total),
            Asignados = ContarEstado(EspacioCorporativoCatalogos.EstadoActivoAsignado),
            Disponibles = ContarEstado(EspacioCorporativoCatalogos.EstadoActivoDisponible),
            EnMantenimiento = ContarEstado(EspacioCorporativoCatalogos.EstadoActivoMantenimiento)
                + ContarEstado("En reparación"),
            DadosDeBaja = ContarEstado(EspacioCorporativoCatalogos.EstadoActivoDadoBaja),
            NovedadesAbiertas = novedadesAbiertas,
            NovedadesSinClasificar = sinClasificar
        };
    }

    private async Task<IReadOnlyList<EspacioUsuarioOpcionViewModel>> BuildResponsablesAsync(CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new EspacioUsuarioOpcionViewModel
            {
                Id = x.Id,
                NombreCompleto = x.FullName,
                Usuario = x.Username,
                Email = x.Email
            })
            .ToListAsync(cancellationToken);
    }

    private static void NormalizarActivo(EspacioActivoFormViewModel model)
    {
        model.TipoActivo = model.TipoActivo?.Trim() ?? string.Empty;
        model.NombreEquipo = NormalizarOpcional(model.NombreEquipo);
        model.Marca = model.Marca?.Trim() ?? string.Empty;
        model.Serie = model.Serie?.Trim() ?? string.Empty;
        model.Serial = model.Serial?.Trim() ?? string.Empty;
        model.Especificaciones = NormalizarOpcional(model.Especificaciones);
        model.CodigoActivo = NormalizarOpcional(model.CodigoActivo);
        model.Nota = NormalizarOpcional(model.Nota);
        model.Estado = NormalizarOpcional(model.Estado);
    }

    private void ValidarActivo(EspacioActivoFormViewModel model)
    {
        if (!EspacioCorporativoCatalogos.EsTipoActivoValido(model.TipoActivo))
        {
            ModelState.AddModelError(nameof(model.TipoActivo), "Selecciona un tipo de activo válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.Estado) && !EspacioCorporativoCatalogos.EsEstadoActivoValido(model.Estado))
        {
            ModelState.AddModelError(nameof(model.Estado), "Selecciona un estado válido.");
        }
    }

    private static string ResolverEstadoActivo(
        string? estadoSolicitado,
        bool tieneResponsable,
        string estadoAnterior,
        bool esNuevo)
    {
        if (EspacioCorporativoCatalogos.EsEstadoActivoValido(estadoSolicitado))
        {
            return estadoSolicitado!.Trim();
        }

        if (!esNuevo && !string.IsNullOrWhiteSpace(estadoAnterior))
        {
            return estadoAnterior;
        }

        return tieneResponsable
            ? EspacioCorporativoCatalogos.EstadoActivoAsignado
            : EspacioCorporativoCatalogos.EstadoActivoDisponible;
    }

    private static string? NormalizarOpcional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private string JoinModelStateErrors() => string.Join(
        " ",
        ModelState.Values
            .SelectMany(state => state.Errors)
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message)));
}
