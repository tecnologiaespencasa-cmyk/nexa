using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Models.EspacioCorporativo;
using Nexa.Models.Security;
using Nexa.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Actas de entrega y devolución de activos con firma digital.
///
/// La firma de quien entrega (área de TI) se guarda una sola vez por usuario y se
/// reutiliza en cada acta; la de quien recibe se traza siempre en el momento.
/// Firmar es independiente de crear el activo: se puede hacer después, cuando el
/// colaborador esté presente.
/// </summary>
public partial class EspacioCorporativoController
{
    [HttpGet]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> Acta(long id, CancellationToken cancellationToken)
    {
        var activo = await _context.EspacioActivos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado, cancellationToken);

        if (activo is null)
        {
            return NotFound();
        }

        var actas = await _context.EspacioActivoActas
            .AsNoTracking()
            .Where(x => x.EspacioActivoId == id)
            .OrderByDescending(x => x.FirmadaAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Tipo,
                x.EntregaPorNombre,
                x.RecibePorNombre,
                x.RecibePorDocumento,
                x.Observaciones,
                x.FirmadaAtUtc
            })
            .ToListAsync(cancellationToken);

        var firmaGuardada = await GetFirmaGuardadaAsync(cancellationToken);
        var ultimoTipo = actas.Count > 0 ? actas[0].Tipo : null;
        var entregaVigente = string.Equals(ultimoTipo, EspacioCorporativoCatalogos.ActaEntrega, StringComparison.OrdinalIgnoreCase);

        return Json(new
        {
            ok = true,
            activo = new
            {
                id = activo.Id,
                descripcion = DescribirActivo(activo),
                serial = activo.Serial,
                codigo = activo.CodigoActivo,
                especificaciones = activo.Especificaciones,
                responsable = activo.ResponsableNombre,
                estado = activo.Estado
            },
            // Siguiente paso posible del ciclo entrega -> devolución.
            siguienteTipo = entregaVigente
                ? EspacioCorporativoCatalogos.ActaDevolucion
                : EspacioCorporativoCatalogos.ActaEntrega,
            entregaVigente,
            firmaGuardada = firmaGuardada is null
                ? null
                : new
                {
                    dataUrl = firmaGuardada.FirmaDataUrl,
                    nombre = firmaGuardada.NombreFirmante,
                    cargo = firmaGuardada.Cargo
                },
            actas = actas.Select(x => new
            {
                id = x.Id,
                tipo = x.Tipo,
                entregaPor = x.EntregaPorNombre,
                recibePor = x.RecibePorNombre,
                documento = x.RecibePorDocumento,
                observaciones = x.Observaciones,
                fecha = ColombiaTime.Convert(x.FirmadaAtUtc).ToString("dd/MM/yyyy hh:mm tt")
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> FirmarActa(
        EspacioActaFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!EspacioCorporativoCatalogos.EsTipoActaValido(model.Tipo))
        {
            return BadRequest(new { message = "Tipo de acta no válido." });
        }

        var activo = await _context.EspacioActivos
            .FirstOrDefaultAsync(x => x.Id == model.ActivoId && !x.Eliminado, cancellationToken);

        if (activo is null)
        {
            return BadRequest(new { message = "El activo no existe o fue eliminado." });
        }

        if (string.IsNullOrWhiteSpace(model.RecibePorNombre))
        {
            return BadRequest(new { message = "Indica el nombre de quien recibe." });
        }

        if (!EspacioCorporativoCatalogos.EsFirmaValida(model.FirmaRecibeDataUrl))
        {
            return BadRequest(new { message = "Falta la firma de quien recibe." });
        }

        // El estado del acta anterior manda: no se entrega dos veces seguidas
        // ni se devuelve algo que no fue entregado.
        var ultimoTipo = await _context.EspacioActivoActas
            .AsNoTracking()
            .Where(x => x.EspacioActivoId == activo.Id)
            .OrderByDescending(x => x.FirmadaAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => x.Tipo)
            .FirstOrDefaultAsync(cancellationToken);

        var entregaVigente = string.Equals(ultimoTipo, EspacioCorporativoCatalogos.ActaEntrega, StringComparison.OrdinalIgnoreCase);
        var esDevolucion = string.Equals(model.Tipo, EspacioCorporativoCatalogos.ActaDevolucion, StringComparison.OrdinalIgnoreCase);

        if (esDevolucion && !entregaVigente)
        {
            return BadRequest(new { message = "No hay una entrega firmada pendiente de devolución para este equipo." });
        }

        if (!esDevolucion && entregaVigente)
        {
            return BadRequest(new { message = "El equipo ya tiene un acta de entrega vigente. Registra primero la devolución." });
        }

        // Firma de quien entrega: la guardada tiene prioridad; si no existe, se toma
        // la que se acaba de trazar y (opcionalmente) se guarda para las próximas actas.
        var firmaGuardada = await GetFirmaGuardadaAsync(cancellationToken);
        var firmaEntrega = firmaGuardada?.FirmaDataUrl;

        if (string.IsNullOrWhiteSpace(firmaEntrega))
        {
            if (!EspacioCorporativoCatalogos.EsFirmaValida(model.FirmaEntregaDataUrl))
            {
                return BadRequest(new { message = "Aún no tienes una firma guardada. Trázala para continuar." });
            }

            firmaEntrega = model.FirmaEntregaDataUrl!.Trim();

            if (model.GuardarFirmaEntrega)
            {
                await GuardarFirmaDelUsuarioAsync(firmaEntrega, null, null, cancellationToken);
            }
        }

        var acta = new EspacioActivoActa
        {
            EspacioActivoId = activo.Id,
            Tipo = esDevolucion
                ? EspacioCorporativoCatalogos.ActaDevolucion
                : EspacioCorporativoCatalogos.ActaEntrega,
            EntregaPorUserId = GetCurrentUserId(),
            EntregaPorNombre = string.IsNullOrWhiteSpace(firmaGuardada?.NombreFirmante)
                ? GetCurrentUserFullName()
                : firmaGuardada!.NombreFirmante!,
            EntregaPorCargo = firmaGuardada?.Cargo,
            FirmaEntregaDataUrl = firmaEntrega,
            RecibePorUserId = activo.ResponsableUserId,
            RecibePorNombre = model.RecibePorNombre.Trim(),
            RecibePorDocumento = NormalizarOpcional(model.RecibePorDocumento),
            FirmaRecibeDataUrl = model.FirmaRecibeDataUrl!.Trim(),
            Observaciones = NormalizarOpcional(model.Observaciones),
            EquipoDescripcion = Resumir(DescribirActivo(activo), 300),
            Serial = activo.Serial,
            CodigoActivo = activo.CodigoActivo,
            Especificaciones = activo.Especificaciones,
            FirmadaAtUtc = DateTime.UtcNow
        };

        await _context.EspacioActivoActas.AddAsync(acta, cancellationToken);

        // La devolución libera el equipo para que vuelva al inventario disponible.
        if (esDevolucion)
        {
            activo.ResponsableUserId = null;
            activo.ResponsableNombre = null;
            activo.FechaAsignacionUtc = null;
            activo.Estado = EspacioCorporativoCatalogos.EstadoActivoDisponible;
            activo.UpdatedAtUtc = DateTime.UtcNow;
            activo.ActualizadoPorNombre = GetCurrentUserFullName();
        }

        await RegistrarMovimientoAsync(
            activo.Id,
            esDevolucion ? "Devolución firmada" : "Entrega firmada",
            $"Acta de {acta.Tipo.ToLowerInvariant()} firmada por {acta.RecibePorNombre}.",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            esDevolucion ? "ESPACIO_ACTA_DEVOLUCION" : "ESPACIO_ACTA_ENTREGA",
            $"Acta #{acta.Id} del activo {activo.Serial} firmada por {acta.RecibePorNombre}",
            cancellationToken);

        return Json(new
        {
            ok = true,
            actaId = acta.Id,
            mensaje = esDevolucion
                ? $"Devolución firmada. El equipo {activo.Serial} vuelve a estar disponible."
                : $"Entrega firmada para {acta.RecibePorNombre}.",
            urlActa = Url.Action(nameof(ActaDocumento), new { id = acta.Id })
        });
    }

    /// <summary>Acta imprimible; el navegador la convierte a PDF si se necesita archivar.</summary>
    [HttpGet]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> ActaDocumento(long id, CancellationToken cancellationToken)
    {
        var acta = await _context.EspacioActivoActas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (acta is null)
        {
            return NotFound();
        }

        var esDevolucion = string.Equals(acta.Tipo, EspacioCorporativoCatalogos.ActaDevolucion, StringComparison.OrdinalIgnoreCase);

        return View("Acta", new EspacioActaDocumentoViewModel
        {
            Id = acta.Id,
            Tipo = acta.Tipo,
            TituloActa = esDevolucion
                ? "Acta de devolución de equipo"
                : "Acta de entrega de equipo",
            EquipoDescripcion = acta.EquipoDescripcion,
            Serial = acta.Serial,
            CodigoActivo = acta.CodigoActivo,
            Especificaciones = acta.Especificaciones,
            EntregaPorNombre = acta.EntregaPorNombre,
            EntregaPorCargo = acta.EntregaPorCargo,
            FirmaEntregaDataUrl = acta.FirmaEntregaDataUrl,
            RecibePorNombre = acta.RecibePorNombre,
            RecibePorDocumento = acta.RecibePorDocumento,
            FirmaRecibeDataUrl = acta.FirmaRecibeDataUrl,
            Observaciones = acta.Observaciones,
            FechaFirma = ColombiaTime.Convert(acta.FirmadaAtUtc)
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Firma guardada del administrador
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> GuardarMiFirma(
        EspacioFirmaGuardadaViewModel model,
        CancellationToken cancellationToken)
    {
        if (!EspacioCorporativoCatalogos.EsFirmaValida(model.FirmaDataUrl))
        {
            return BadRequest(new { message = "Traza tu firma antes de guardarla." });
        }

        await GuardarFirmaDelUsuarioAsync(
            model.FirmaDataUrl!.Trim(),
            model.NombreFirmante,
            model.Cargo,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await LogAuditAsync("ESPACIO_FIRMA_GUARDADA", "Firma de entrega actualizada", cancellationToken);

        return Json(new { ok = true, mensaje = "Tu firma quedó guardada y se usará en las próximas actas." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> EliminarMiFirma(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Forbid();
        }

        var firma = await _context.EspacioFirmasUsuario
            .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

        if (firma is not null)
        {
            _context.EspacioFirmasUsuario.Remove(firma);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Json(new { ok = true, mensaje = "Firma eliminada. Se te pedirá trazarla en la próxima acta." });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<EspacioFirmaUsuario?> GetFirmaGuardadaAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return null;
        }

        return await _context.EspacioFirmasUsuario
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);
    }

    private async Task GuardarFirmaDelUsuarioAsync(
        string firmaDataUrl,
        string? nombreFirmante,
        string? cargo,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return;
        }

        var firma = await _context.EspacioFirmasUsuario
            .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

        if (firma is null)
        {
            firma = new EspacioFirmaUsuario { UserId = userId.Value };
            await _context.EspacioFirmasUsuario.AddAsync(firma, cancellationToken);
        }

        firma.FirmaDataUrl = firmaDataUrl;
        firma.NombreFirmante = NormalizarOpcional(nombreFirmante) ?? GetCurrentUserFullName();
        firma.Cargo = NormalizarOpcional(cargo) ?? firma.Cargo;
        firma.ActualizadaAtUtc = DateTime.UtcNow;
    }

    private static string DescribirActivo(EspacioActivo activo)
    {
        var nombre = string.IsNullOrWhiteSpace(activo.NombreEquipo)
            ? $"{activo.TipoActivo} {activo.Marca}".Trim()
            : activo.NombreEquipo;

        return $"{nombre} - {activo.Marca} {activo.Serie}".Trim();
    }
}
