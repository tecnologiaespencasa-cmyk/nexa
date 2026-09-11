using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Models.ViewModels;

namespace Nexa.Controllers;

/// <summary>
/// Lado farmacia de la requisición del censo de NPT: ver el documento, sus adjuntos y darle el OK,
/// que la cierra para que el censo ya no la pueda editar.
///
/// Es el mismo ciclo de clínica de heridas —recepcionado, facturado, empacado, firma y despacho—
/// pero en su propio carril: son dos programas distintos y no conviene que se mezclen en la
/// bandeja. A diferencia de heridas no hay planes ni tipos: la atención tiene una requisición.
/// </summary>
public partial class FarmaciaController
{
    [HttpGet]
    public async Task<IActionResult> DocumentoNpt(long id, string? documento = null, CancellationToken cancellationToken = default)
    {
        var kardex = await _context.CensoNptKardex
            .Include(x => x.CensoNptRecord)
            .Include(x => x.Adjuntos)
            .FirstOrDefaultAsync(x => x.Id == id && x.FarmaciaEnviadoAtUtc != null, cancellationToken);

        if (kardex is null)
        {
            return NotFound();
        }

        kardex.FarmaciaKardexVistoAtUtc ??= DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        // Se reutiliza la vista de heridas: el documento tiene la misma forma y farmacia no debe
        // aprender dos pantallas para leer lo mismo. El rótulo es el que dice de qué programa es.
        var model = new FarmaciaClinicaHeridasDocumentViewModel
        {
            Id = kardex.Id,
            Tipo = NptKardexBuilder.TipoNombre,
            TipoNombre = NptKardexBuilder.TipoNombre,
            EsNpt = true,
            Documento = NptKardexBuilder.Resolver(
                kardex.CensoNptRecord,
                kardex.KardexJson,
                kardex.ElaboradoPor,
                kardex.FarmaciaEnviadoAtUtc ?? kardex.CreatedAtUtc),
            Cerrado = kardex.KardexCerradoAtUtc is not null,
            CerradoAtUtc = kardex.KardexCerradoAtUtc,
            EnviadoAtUtc = kardex.FarmaciaEnviadoAtUtc,
            FarmaciaEstado = kardex.FarmaciaEstado,
            Adjuntos = kardex.Adjuntos
                .OrderByDescending(x => x.UploadedAtUtc)
                .Select(x => new FarmaciaClinicaHeridasAdjuntoViewModel
                {
                    Id = x.Id,
                    Nombre = x.FileName,
                    SubidoAtUtc = x.UploadedAtUtc
                })
                .ToList()
        };

        ViewData["DocumentoFiltro"] = documento?.Trim();
        return View("DocumentoClinicaHeridas", model);
    }

    [HttpGet]
    public async Task<IActionResult> DescargarAdjuntoNpt(long adjuntoId, CancellationToken cancellationToken)
    {
        var adjunto = await _context.CensoNptKardexAdjuntos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == adjuntoId, cancellationToken);

        if (adjunto is null)
        {
            return NotFound();
        }

        return File(adjunto.FileData, "application/octet-stream", adjunto.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetOkKardexNpt(long id, CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoNptKardex
            .Include(x => x.CensoNptRecord)
            .FirstOrDefaultAsync(
                x => x.Id == id && x.FarmaciaEnviadoAtUtc != null && x.FarmaciaEstado == FarmaciaEstados.Nuevo,
                cancellationToken);

        if (kardex is null)
        {
            return NotFound();
        }

        kardex.FarmaciaOkKardex = true;
        kardex.FarmaciaEstado = FarmaciaEstados.Recepcionado;
        kardex.KardexCerradoAtUtc = DateTime.UtcNow;
        kardex.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await RegistrarAuditoriaNptAsync("FARMACIA_OK_KARDEX_NPT", kardex, cancellationToken);

        TempData["SuccessMessage"] = "Requisición aprobada. Quedó cerrada en el censo de NPT.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEntregaParcialNpt(
        [FromBody] FarmaciaEntregaParcialInputModel model,
        CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoNptKardex.FirstOrDefaultAsync(
            x => x.Id == model.Id && x.FarmaciaEnviadoAtUtc != null && x.FarmaciaEstado == FarmaciaEstados.Recepcionado,
            cancellationToken);

        if (kardex is null)
        {
            return NotFound(new { message = "Pedido no encontrado o no esta en estado Recepcionado." });
        }

        if (model.EsEntregaParcial && (model.CantidadEntregas is null or < 2))
        {
            return BadRequest(new { message = "La cantidad de entregas debe ser al menos 2." });
        }

        kardex.FarmaciaEsEntregaParcial = model.EsEntregaParcial;
        kardex.FarmaciaCantidadEntregas = model.EsEntregaParcial ? model.CantidadEntregas : null;
        kardex.FarmaciaEntregaActual = 1;
        kardex.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new { message = "Configuracion de entrega guardada." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AvanzarEntregaNpt(long id, CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoNptKardex.FirstOrDefaultAsync(
            x => x.Id == id && x.FarmaciaEnviadoAtUtc != null
                && (x.FarmaciaEstado == FarmaciaEstados.Recepcionado
                    || x.FarmaciaEstado == FarmaciaEstados.Facturado
                    || x.FarmaciaEstado == FarmaciaEstados.Empacado
                    || (x.FarmaciaEstado == FarmaciaEstados.Despachado && x.FarmaciaEsEntregaParcial == true)),
            cancellationToken);

        if (kardex is null)
        {
            return NotFound(new { message = "Pedido no encontrado o no tiene entrega parcial activa." });
        }

        if (kardex.FarmaciaEsEntregaParcial != true || !kardex.FarmaciaCantidadEntregas.HasValue)
        {
            return BadRequest(new { message = "El pedido no tiene entrega parcial configurada." });
        }

        if (kardex.FarmaciaEntregaActual >= kardex.FarmaciaCantidadEntregas.Value)
        {
            return BadRequest(new { message = "Ya se alcanzo la ultima entrega." });
        }

        kardex.FarmaciaEntregaActual++;
        kardex.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new
        {
            message = $"Avanzado a entrega {kardex.FarmaciaEntregaActual} de {kardex.FarmaciaCantidadEntregas}.",
            entregaActual = kardex.FarmaciaEntregaActual,
            cantidadEntregas = kardex.FarmaciaCantidadEntregas
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetFacturadoNpt(long id, CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoNptKardex
            .Include(x => x.CensoNptRecord)
            .FirstOrDefaultAsync(
                x => x.Id == id && x.FarmaciaEnviadoAtUtc != null && x.FarmaciaEstado == FarmaciaEstados.Recepcionado,
                cancellationToken);

        if (kardex is null)
        {
            return NotFound(new { message = "Pedido no encontrado o no esta en estado Recepcionado." });
        }

        kardex.FarmaciaFacturado = true;
        kardex.FarmaciaEstado = FarmaciaEstados.Facturado;
        kardex.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await RegistrarAuditoriaNptAsync("FARMACIA_NPT_FACTURADO", kardex, cancellationToken);
        return Json(new { message = "Pedido marcado como Facturado." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEmpacadoNpt(long id, CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoNptKardex
            .Include(x => x.CensoNptRecord)
            .FirstOrDefaultAsync(
                x => x.Id == id && x.FarmaciaEnviadoAtUtc != null && x.FarmaciaEstado == FarmaciaEstados.Facturado,
                cancellationToken);

        if (kardex is null)
        {
            return NotFound(new { message = "Pedido no encontrado o no esta en estado Facturado." });
        }

        kardex.FarmaciaEstado = FarmaciaEstados.Empacado;
        kardex.FarmaciaEmpacadoAtUtc = DateTime.UtcNow;
        kardex.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await RegistrarAuditoriaNptAsync("FARMACIA_NPT_EMPACADO", kardex, cancellationToken);
        return Json(new { message = "Pedido en estado Empacado. Tiene 72 horas para firmar." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetBolsaDesempacadaNpt(long id, CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoNptKardex.FirstOrDefaultAsync(
            x => x.Id == id && x.FarmaciaEnviadoAtUtc != null && x.FarmaciaEstado == FarmaciaEstados.PorDesempacar,
            cancellationToken);

        if (kardex is null)
        {
            return NotFound(new { message = "Pedido no encontrado o no esta en estado Por Desempacar." });
        }

        kardex.FarmaciaBolsaDesempacada = true;
        kardex.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new { message = "Bolsa marcada como desempacada." });
    }

    [HttpGet]
    public async Task<IActionResult> FirmaNpt(long id, CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoNptKardex
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.FarmaciaEnviadoAtUtc != null, cancellationToken);

        if (kardex is null)
        {
            return NotFound(new { message = "No se encontro el despacho de farmacia." });
        }

        if (kardex.FarmaciaEstado != FarmaciaEstados.Empacado && kardex.FarmaciaEstado != FarmaciaEstados.PorDesempacar)
        {
            return BadRequest(new { message = "La firma solo esta disponible en estado Empacado o Por Desempacar." });
        }

        if (kardex.FarmaciaEstado == FarmaciaEstados.PorDesempacar && kardex.FarmaciaBolsaDesempacada)
        {
            return BadRequest(new { message = "La bolsa ya fue marcada como desempacada." });
        }

        var firma = BuildNptSignatureModel(kardex);
        return Json(new
        {
            id = firma.PedidoId,
            nombreRecibe = firma.NombreRecibe,
            firmaEntregaDataUrl = firma.FirmaEntregaDataUrl,
            firmaRecibeDataUrl = firma.FirmaRecibeDataUrl,
            fechaHoraRecepcion = ColombiaTime.Convert(firma.FechaHoraRecepcionUtc)?.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            fechaHoraRecepcionTexto = firma.FechaHoraRecepcionTexto,
            estaCompleta = firma.EstaCompleta
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarFirmaNpt(FarmaciaSignatureInputModel model, CancellationToken cancellationToken)
    {
        if (model.Id <= 0)
        {
            return BadRequest(new { message = "No se encontro el despacho para guardar la firma." });
        }

        var nombreRecibe = model.NombreRecibe?.Trim();
        if (string.IsNullOrWhiteSpace(nombreRecibe))
        {
            return BadRequest(new { message = "Ingresa el nombre de quien recibe." });
        }

        if (!IsValidSignatureDataUrl(model.FirmaEntregaDataUrl))
        {
            return BadRequest(new { message = "La firma de quien entrega es obligatoria." });
        }

        if (!IsValidSignatureDataUrl(model.FirmaRecibeDataUrl))
        {
            return BadRequest(new { message = "La firma de quien recibe es obligatoria." });
        }

        if (model.FechaHoraRecepcion == default)
        {
            return BadRequest(new { message = "Ingresa la fecha y hora de recepcion." });
        }

        var kardex = await _context.CensoNptKardex
            .Include(x => x.CensoNptRecord)
            .FirstOrDefaultAsync(x => x.Id == model.Id && x.FarmaciaEnviadoAtUtc != null, cancellationToken);

        if (kardex is null)
        {
            return NotFound(new { message = "No se encontro el despacho de farmacia." });
        }

        if (kardex.FarmaciaEstado != FarmaciaEstados.Empacado && kardex.FarmaciaEstado != FarmaciaEstados.PorDesempacar)
        {
            return BadRequest(new { message = "La firma solo esta disponible en estado Empacado o Por Desempacar." });
        }

        if (kardex.FarmaciaEstado == FarmaciaEstados.PorDesempacar && kardex.FarmaciaBolsaDesempacada)
        {
            return BadRequest(new { message = "La bolsa ya fue marcada como desempacada." });
        }

        kardex.FarmaciaNombreRecibe = nombreRecibe;
        kardex.FarmaciaFirmaEntregaDataUrl = model.FirmaEntregaDataUrl.Trim();
        kardex.FarmaciaFirmaRecibeDataUrl = model.FirmaRecibeDataUrl.Trim();
        kardex.FarmaciaFechaHoraRecepcionUtc = DateTime.SpecifyKind(model.FechaHoraRecepcion, DateTimeKind.Local).ToUniversalTime();
        kardex.FarmaciaFirmaActualizadaAtUtc = DateTime.UtcNow;
        kardex.FarmaciaEstado = FarmaciaEstados.Despachado;
        kardex.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await RegistrarAuditoriaNptAsync(
            "FARMACIA_NPT_DESPACHADO",
            kardex,
            cancellationToken,
            $", Recibe: {kardex.FarmaciaNombreRecibe}");

        // Se espera el aviso en vez de lanzarlo en segundo plano: el servicio y su DbContext viven
        // en el scope de esta petición y al terminarla quedarían liberados a mitad de la consulta.
        var avisos = await _notificationService.NotifyNptDespachadoAsync(kardex, cancellationToken);

        return Json(new
        {
            message = "Firmas guardadas. Paciente pasado a Despachado.",
            avisos,
            estaCompleta = true,
            nombreRecibe = kardex.FarmaciaNombreRecibe,
            fechaHoraRecepcionTexto = ColombiaTime.Convert(kardex.FarmaciaFechaHoraRecepcionUtc)?.ToString("dd/MM/yyyy HH:mm")
        });
    }

    private static FarmaciaSignatureViewModel BuildNptSignatureModel(CensoNptKardex kardex)
    {
        return new FarmaciaSignatureViewModel
        {
            PedidoId = kardex.Id,
            NombreRecibe = kardex.FarmaciaNombreRecibe,
            FirmaEntregaDataUrl = kardex.FarmaciaFirmaEntregaDataUrl,
            FirmaRecibeDataUrl = kardex.FarmaciaFirmaRecibeDataUrl,
            FechaHoraRecepcionUtc = kardex.FarmaciaFechaHoraRecepcionUtc,
            ActualizadaAtUtc = kardex.FarmaciaFirmaActualizadaAtUtc
        };
    }

    private Task RegistrarAuditoriaNptAsync(
        string accion,
        CensoNptKardex kardex,
        CancellationToken cancellationToken,
        string extra = "")
    {
        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid)
            ? (Guid?)parsedUid
            : null;

        return _auditService.LogAsync(
            accion,
            "CensoNptKardex",
            $"Paciente: {kardex.CensoNptRecord.NombrePaciente}, "
                + $"Doc: {kardex.CensoNptRecord.NumeroIdentificacion}, Requisición: NPT{extra}",
            auditUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
    }
}
