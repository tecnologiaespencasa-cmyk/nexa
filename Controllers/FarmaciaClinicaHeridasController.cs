using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Lado farmacia de las requisiciones de clínica de heridas: ver el documento, sus adjuntos y darle
/// el OK, que cierra el kardex para que el censo ya no lo pueda editar.
/// </summary>
public partial class FarmaciaController
{
    private static readonly JsonSerializerOptions HeridasKardexJsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<IActionResult> DocumentoClinicaHeridas(long id, string? documento = null, CancellationToken cancellationToken = default)
    {
        var kardex = await _context.CensoClinicaHeridasKardex
            .Include(x => x.CensoClinicaHeridasRecord)
            .Include(x => x.Plan)
            .Include(x => x.Adjuntos)
            .FirstOrDefaultAsync(x => x.Id == id && x.FarmaciaEnviadoAtUtc != null, cancellationToken);

        if (kardex is null)
        {
            return NotFound();
        }

        kardex.FarmaciaKardexVistoAtUtc ??= DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var model = new FarmaciaClinicaHeridasDocumentViewModel
        {
            Id = kardex.Id,
            Tipo = kardex.Tipo,
            TipoNombre = $"{ClinicaHeridasKardexTipos.Nombre(kardex.Tipo)} · Plan {kardex.Plan.Numero}",
            Documento = ResolverDocumento(kardex),
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

    /// <summary>
    /// Reconstruye el documento a partir del JSON guardado. Si nunca se editó (o quedó ilegible),
    /// se regenera desde el censo para que farmacia siempre vea algo coherente.
    /// </summary>
    private static ClinicaHeridasKardexDocumento ResolverDocumento(CensoClinicaHeridasKardex kardex)
    {
        if (!string.IsNullOrWhiteSpace(kardex.KardexJson))
        {
            try
            {
                var guardado = JsonSerializer.Deserialize<ClinicaHeridasKardexDocumento>(
                    kardex.KardexJson,
                    HeridasKardexJsonOptions);

                if (guardado is not null)
                {
                    guardado.Tipo = kardex.Tipo;
                    guardado.TipoNombre = ClinicaHeridasKardexTipos.Nombre(kardex.Tipo);
                    guardado.Titulo = ClinicaHeridasKardexBuilder.Titulo;
                    guardado.Encabezados = ClinicaHeridasKardexBuilder.NormalizarEncabezados(
                        guardado.Encabezados,
                        guardado.Aplicaciones);
                    return guardado;
                }
            }
            catch (JsonException)
            {
                // Se cae al generado.
            }
        }

        // Farmacia siempre ve el plan tal como se envió: si nunca se editó, se regenera con los
        // apósitos y el tratamiento que ese plan tenía, no con los que el censo tenga hoy.
        var origen = kardex.CensoClinicaHeridasRecord;
        origen.ApositoMedicamento1 = kardex.Plan.ApositoMedicamento1;
        origen.ApositoMedicamento2 = kardex.Plan.ApositoMedicamento2;
        origen.ApositoMedicamento3 = kardex.Plan.ApositoMedicamento3;
        origen.ApositoMedicamento4 = kardex.Plan.ApositoMedicamento4;
        origen.DuracionTratamientoDias = kardex.Plan.DuracionTratamientoDias;
        origen.FrecuenciaVisita = kardex.Plan.FrecuenciaVisita;

        return ClinicaHeridasKardexBuilder.Generar(
            origen,
            kardex.Tipo,
            kardex.ElaboradoPor,
            kardex.FarmaciaEnviadoAtUtc ?? kardex.CreatedAtUtc);
    }

    [HttpGet]
    public async Task<IActionResult> DescargarAdjuntoClinicaHeridas(long adjuntoId, CancellationToken cancellationToken)
    {
        var adjunto = await _context.CensoClinicaHeridasKardexAdjuntos
            .AsNoTracking()
            .Include(x => x.Kardex)
            .FirstOrDefaultAsync(x => x.Id == adjuntoId && x.Kardex.FarmaciaEnviadoAtUtc != null, cancellationToken);

        if (adjunto is null)
        {
            return NotFound();
        }

        return File(adjunto.FileData, "application/octet-stream", adjunto.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetOkKardexClinicaHeridas(long id, CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoClinicaHeridasKardex
            .Include(x => x.CensoClinicaHeridasRecord)
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

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid)
            ? (Guid?)parsedUid
            : null;

        await _auditService.LogAsync(
            "FARMACIA_OK_KARDEX_CLINICA_HERIDAS",
            "CensoClinicaHeridasKardex",
            $"Paciente: {kardex.CensoClinicaHeridasRecord.NombrePaciente}, "
                + $"Doc: {kardex.CensoClinicaHeridasRecord.NumeroIdentificacion}, "
                + $"Kardex: {ClinicaHeridasKardexTipos.Nombre(kardex.Tipo)}",
            auditUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        TempData["SuccessMessage"] = "Kardex aprobado. La requisición quedó cerrada en el censo.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEntregaParcialClinicaHeridas(
        [FromBody] FarmaciaEntregaParcialInputModel model,
        CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoClinicaHeridasKardex.FirstOrDefaultAsync(
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
    public async Task<IActionResult> AvanzarEntregaClinicaHeridas(long id, CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoClinicaHeridasKardex.FirstOrDefaultAsync(
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
    public async Task<IActionResult> SetFacturadoClinicaHeridas(long id, CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoClinicaHeridasKardex
            .Include(x => x.CensoClinicaHeridasRecord)
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

        await RegistrarAuditoriaHeridasAsync("FARMACIA_CLINICA_HERIDAS_FACTURADO", kardex, cancellationToken);
        return Json(new { message = "Pedido marcado como Facturado." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEmpacadoClinicaHeridas(long id, CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoClinicaHeridasKardex
            .Include(x => x.CensoClinicaHeridasRecord)
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

        await RegistrarAuditoriaHeridasAsync("FARMACIA_CLINICA_HERIDAS_EMPACADO", kardex, cancellationToken);
        return Json(new { message = "Pedido en estado Empacado. Tiene 72 horas para firmar." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetBolsaDesempacadaClinicaHeridas(long id, CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoClinicaHeridasKardex.FirstOrDefaultAsync(
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
    public async Task<IActionResult> FirmaClinicaHeridas(long id, CancellationToken cancellationToken)
    {
        var kardex = await _context.CensoClinicaHeridasKardex
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

        var firma = BuildClinicaHeridasSignatureModel(kardex);
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
    public async Task<IActionResult> GuardarFirmaClinicaHeridas(FarmaciaSignatureInputModel model, CancellationToken cancellationToken)
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

        var kardex = await _context.CensoClinicaHeridasKardex
            .Include(x => x.CensoClinicaHeridasRecord)
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
        await RegistrarAuditoriaHeridasAsync(
            "FARMACIA_CLINICA_HERIDAS_DESPACHADO",
            kardex,
            cancellationToken,
            $", Recibe: {kardex.FarmaciaNombreRecibe}");

        // El aviso se espera en vez de lanzarse en segundo plano: el servicio y su DbContext viven en
        // el scope de esta petición, y al terminarla quedarían liberados a mitad de la consulta.
        var avisos = await _notificationService.NotifyClinicaHeridasDespachadoAsync(kardex, cancellationToken);

        return Json(new
        {
            message = "Firmas guardadas. Paciente pasado a Despachado.",
            avisos,
            estaCompleta = true,
            nombreRecibe = kardex.FarmaciaNombreRecibe,
            fechaHoraRecepcionTexto = ColombiaTime.Convert(kardex.FarmaciaFechaHoraRecepcionUtc)?.ToString("dd/MM/yyyy HH:mm")
        });
    }

    private static FarmaciaSignatureViewModel BuildClinicaHeridasSignatureModel(CensoClinicaHeridasKardex kardex)
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

    private Task RegistrarAuditoriaHeridasAsync(
        string accion,
        CensoClinicaHeridasKardex kardex,
        CancellationToken cancellationToken,
        string extra = "")
    {
        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            ? (Guid?)parsed
            : null;

        return _auditService.LogAsync(
            accion,
            "CensoClinicaHeridasKardex",
            $"Paciente: {kardex.CensoClinicaHeridasRecord.NombrePaciente}, "
                + $"Doc: {kardex.CensoClinicaHeridasRecord.NumeroIdentificacion}, "
                + $"Kardex: {ClinicaHeridasKardexTipos.Nombre(kardex.Tipo)}{extra}",
            userId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
    }
}
