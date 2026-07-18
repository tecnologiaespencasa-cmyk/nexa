using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using IntranetPrueba.Data.Entities;
using IntranetPrueba.Models.ViewModels;
using IntranetPrueba.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Controllers;

public partial class CensoController
{
    private static readonly CultureInfo ClinicaHeridasTextCulture = CultureInfo.GetCultureInfo("es-CO");
    private static readonly string[] ClinicaHeridasAseguradorValues =
    [
        "EPS SURA",
        "PANAMERICAN LIFE",
        "PARTICULAR"
    ];
    private static readonly string[] ClinicaHeridasGeneroValues =
    [
        "Masculino",
        "Femenino"
    ];
    private static readonly string[] ClinicaHeridasLlamadaBienvenidaValues =
    [
        "Efectivo",
        "No efectivo"
    ];
    private static readonly string[] ClinicaHeridasProgramaValues =
    [
        "Agudo",
        "Cronico",
        "NPT"
    ];
    private static readonly string[] ClinicaHeridasSiNoValues = ["Si", "No"];
    private static readonly string[] ClinicaHeridasUbicacionHeridaValues =
    [
        "CABEZA CUELLO",
        "PECHO",
        "TRONCO",
        "ABDOMEN",
        "MIEMBRO SUPERIOR",
        "MIEMBRO INFERIOR",
        "MANOS",
        "PIES",
        "GLUTEO",
        "CADERA",
        "SACRA"
    ];
    private static readonly string[] ClinicaHeridasFrecuenciaVisitasValues = ["1", "2", "3", "4", "5", "6", "7"];
    private static readonly Regex ClinicaHeridasNombrePattern = new(@"^[\p{L}\s]+$", RegexOptions.Compiled);
    private static readonly string[] ClinicaHeridasMotivoHospitalizacionValues =
    [
        "Dolor",
        "No mejoria clinica",
        "Fallas en la atención domiciliaria"
    ];
    private static readonly string[] ClinicaHeridasRemitidoPorValues =
    [
        "Familiar",
        "Medico IPS",
        "EMI/CEM/OTROS"
    ];
    private static readonly string[] ClinicaHeridasMotivoEgresoValues =
    [
        "CURACION",
        "FALLECE",
        "NO CUMPLE CRITERIOS",
        "AMBITO DE ATENCION NO CORRESPONDE",
        "CAMBIO DE PRESTADOR CAMBIO DE ASEGURADOR",
        "ALTA VOLUNTARIA",
        "NO APLICA",
        "ALTA MEDICA AMBULATORIA",
        "REINGRESO HOSPITALARIO",
        "ALTA MEDICA",
        "SIN COBERTURA",
        "DESMONTE",
        "CANCELAN TRAMITE DOMICILIARIO",
        "NO PERTINENCIA/LESION PALIATIVA/EDUCACION"
    ];
    private static readonly string[] ClinicaHeridasEstadoProgramaValues = ["Activo", "Inactivo"];

    [HttpGet]
    public async Task<IActionResult> ClinicaHeridas(
        string? cedulaPaciente,
        long? recordId,
        CancellationToken cancellationToken)
    {
        var model = BuildDefaultClinicaHeridasModel();
        model.CedulaFiltro = NormalizeCedulaFilter(cedulaPaciente);

        if (recordId.HasValue)
        {
            var record = await _context.CensoClinicaHeridas
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == recordId.Value, cancellationToken);
            if (record is not null)
            {
                ApplyClinicaHeridasRecordToModel(model, record);
                model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                    ? record.NumeroIdentificacion
                    : model.CedulaFiltro;
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.CedulaFiltro))
        {
            var record = await _context.CensoClinicaHeridas
                .AsNoTracking()
                .Where(x => x.NumeroIdentificacion == model.CedulaFiltro)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (record is not null)
            {
                ApplyClinicaHeridasRecordToModel(model, record);
                model.CedulaFiltro = record.NumeroIdentificacion;
            }
        }

        await PopulateClinicaHeridasDropdownsAsync(model, cancellationToken);
        return View("ClinicaHeridas", model);
    }

    [HttpPost]
    public async Task<IActionResult> ClinicaHeridas(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        NormalizeClinicaHeridasModel(model);
        await PopulateClinicaHeridasDropdownsAsync(model, cancellationToken);
        ValidateClinicaHeridasModel(model);

        var direccionParaGuardar = model.Direccion ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(model.Direccion))
        {
            var direccionValidation = await _addressValidationService.ValidateAddressAsync(direccionParaGuardar, cancellationToken);
            ApplyClinicaHeridasAddressValidationResult(model, direccionValidation, ref direccionParaGuardar);
        }
        else
        {
            ClearClinicaHeridasAddressModelState();
            model.DireccionEsValida = false;
            model.AsumirDireccionErrada = false;
            model.DireccionSugerida = null;
            model.DireccionMensajeValidacion = null;
            direccionParaGuardar = model.Direccion ?? string.Empty;
        }

        if (!ModelState.IsValid)
        {
            await PopulateClinicaHeridasLatestRecordsAsync(model, cancellationToken);
            return View("ClinicaHeridas", model);
        }

        CensoClinicaHeridasRecord record;
        var auditAction = "CENSO_CLINICA_HERIDAS_CREADO";
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoClinicaHeridas
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken)
                ?? new CensoClinicaHeridasRecord();
            ApplyClinicaHeridasModelToRecord(model, record, direccionParaGuardar, preserveCreatedAt: record.Id != 0);
            auditAction = record.Id == 0
                ? "CENSO_CLINICA_HERIDAS_CREADO"
                : "CENSO_CLINICA_HERIDAS_ACTUALIZADO";
            if (record.Id == 0)
            {
                await _context.CensoClinicaHeridas.AddAsync(record, cancellationToken);
            }
        }
        else
        {
            record = new CensoClinicaHeridasRecord();
            ApplyClinicaHeridasModelToRecord(model, record, direccionParaGuardar, preserveCreatedAt: false);
            await _context.CensoClinicaHeridas.AddAsync(record, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(auditAction, "CensoClinicaHeridas",
            $"Paciente: {record.NombrePaciente}, Doc: {record.NumeroIdentificacion}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = model.EditingRecordId.HasValue
            ? "Registro de clínica de heridas actualizado correctamente."
            : "Registro de clínica de heridas guardado correctamente.";
        return RedirectToAction(nameof(ClinicaHeridas), new { cedulaPaciente = record.NumeroIdentificacion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarClinicaHeridasManejoHerida(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);
        model.DescripcionHerida = NormalizeOptionalClinicaHeridasText(model.DescripcionHerida);
        model.UbicacionHerida = model.UbicacionHerida?.Trim();
        ValidateClinicaHeridasManejoHeridaModel(model);

        var postedDescripcionHerida = model.DescripcionHerida;
        var postedUbicacionHerida = model.UbicacionHerida;
        var postedFrecuenciaVisitas = model.FrecuenciaVisitasSemana;

        CensoClinicaHeridasRecord? record = null;
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoClinicaHeridas
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);
        }

        if (record is null)
        {
            ModelState.AddModelError(string.Empty, "Primero guarda los datos básicos del paciente para registrar el manejo de la herida.");
        }
        else
        {
            ApplyClinicaHeridasRecordToModel(model, record);
            model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                ? record.NumeroIdentificacion
                : model.CedulaFiltro;
        }

        await PopulateClinicaHeridasDropdownsAsync(model, cancellationToken);
        model.DescripcionHerida = postedDescripcionHerida;
        model.UbicacionHerida = postedUbicacionHerida;
        model.FrecuenciaVisitasSemana = postedFrecuenciaVisitas;

        if (!ModelState.IsValid)
        {
            return View("ClinicaHeridas", model);
        }

        var heridasRecord = record!;
        heridasRecord.DescripcionHerida = model.DescripcionHerida;
        heridasRecord.UbicacionHerida = model.UbicacionHerida;
        heridasRecord.FrecuenciaVisitasSemana = model.FrecuenciaVisitasSemana;
        heridasRecord.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync("CENSO_CLINICA_HERIDAS_MANEJO_HERIDA_ACTUALIZADO", "CensoClinicaHeridas",
            $"Paciente: {heridasRecord.NombrePaciente}, Doc: {heridasRecord.NumeroIdentificacion}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = "Manejo de la herida guardado correctamente.";
        return RedirectToAction(nameof(ClinicaHeridas), new { recordId = heridasRecord.Id, cedulaPaciente = heridasRecord.NumeroIdentificacion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarClinicaHeridasActivoFijo(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);
        model.EquipoComodato = model.EquipoComodato?.Trim();
        model.NumeroPlacaEquipos = NormalizeOptionalClinicaHeridasText(model.NumeroPlacaEquipos);

        var postedEquipoComodato = model.EquipoComodato;
        var postedNumeroPlaca = model.NumeroPlacaEquipos;
        var postedFechaEntrega = model.FechaEntregaEquipo;
        var postedFechaDevolucion = model.FechaDevolucionEquipo;

        CensoClinicaHeridasRecord? record = null;
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoClinicaHeridas
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);
        }

        if (record is null)
        {
            ModelState.AddModelError(string.Empty, "Primero guarda los datos básicos del paciente para registrar el activo fijo.");
        }
        else
        {
            if (!string.Equals(record.Vac, "Si", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "La sección Activo fijo solo se habilita cuando el campo VAC de los datos básicos está guardado en Si.");
            }

            ApplyClinicaHeridasRecordToModel(model, record);
            model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                ? record.NumeroIdentificacion
                : model.CedulaFiltro;
        }

        ValidateClinicaHeridasActivoFijoModel(postedEquipoComodato, postedNumeroPlaca, postedFechaEntrega, postedFechaDevolucion);

        await PopulateClinicaHeridasDropdownsAsync(model, cancellationToken);
        model.EquipoComodato = postedEquipoComodato;
        model.NumeroPlacaEquipos = postedNumeroPlaca;
        model.FechaEntregaEquipo = postedFechaEntrega;
        model.FechaDevolucionEquipo = postedFechaDevolucion;

        if (!ModelState.IsValid)
        {
            return View("ClinicaHeridas", model);
        }

        var heridasRecord = record!;
        heridasRecord.EquipoComodato = model.EquipoComodato;
        heridasRecord.NumeroPlacaEquipos = model.NumeroPlacaEquipos;
        heridasRecord.FechaEntregaEquipo = model.FechaEntregaEquipo?.Date;
        heridasRecord.FechaDevolucionEquipo = model.FechaDevolucionEquipo?.Date;
        heridasRecord.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync("CENSO_CLINICA_HERIDAS_ACTIVO_FIJO_ACTUALIZADO", "CensoClinicaHeridas",
            $"Paciente: {heridasRecord.NombrePaciente}, Doc: {heridasRecord.NumeroIdentificacion}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = "Activo fijo guardado correctamente.";
        return RedirectToAction(nameof(ClinicaHeridas), new { recordId = heridasRecord.Id, cedulaPaciente = heridasRecord.NumeroIdentificacion });
    }

    private void ValidateClinicaHeridasActivoFijoModel(
        string? equipoComodato,
        string? numeroPlaca,
        DateTime? fechaEntrega,
        DateTime? fechaDevolucion)
    {
        if (!ClinicaHeridasSiNoValues.Contains(equipoComodato ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(CensoClinicaHeridasViewModel.EquipoComodato), "Selecciona si el equipo está en comodato.");
        }

        if (string.IsNullOrWhiteSpace(numeroPlaca))
        {
            ModelState.AddModelError(nameof(CensoClinicaHeridasViewModel.NumeroPlacaEquipos), "Ingresa el número de placa de los equipos asignados.");
        }

        if (!fechaEntrega.HasValue)
        {
            ModelState.AddModelError(nameof(CensoClinicaHeridasViewModel.FechaEntregaEquipo), "Selecciona la fecha de entrega del equipo.");
        }

        if (fechaDevolucion.HasValue
            && fechaEntrega.HasValue
            && fechaDevolucion.Value.Date < fechaEntrega.Value.Date)
        {
            ModelState.AddModelError(nameof(CensoClinicaHeridasViewModel.FechaDevolucionEquipo), "La fecha de devolución no puede ser anterior a la fecha de entrega.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarClinicaHeridasSeguimientoHospitalizado(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.MotivoHospitalizacion = string.IsNullOrWhiteSpace(model.MotivoHospitalizacion) ? null : model.MotivoHospitalizacion.Trim();
        model.RemitidoPorHospitalizacion = string.IsNullOrWhiteSpace(model.RemitidoPorHospitalizacion) ? null : model.RemitidoPorHospitalizacion.Trim();
        model.IpsIntramural = NormalizeOptionalClinicaHeridasText(model.IpsIntramural);

        if (!string.IsNullOrWhiteSpace(model.MotivoHospitalizacion)
            && !ClinicaHeridasMotivoHospitalizacionValues.Contains(model.MotivoHospitalizacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoHospitalizacion), "Selecciona un motivo de hospitalización válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.RemitidoPorHospitalizacion)
            && !ClinicaHeridasRemitidoPorValues.Contains(model.RemitidoPorHospitalizacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.RemitidoPorHospitalizacion), "Selecciona un valor válido para remitido por.");
        }

        var posted = (model.FechaHospitalizacion, model.MotivoHospitalizacion, model.RemitidoPorHospitalizacion, model.IpsIntramural,
            model.FechaPrimerSeguimiento24Horas, model.FechaSegundoSeguimiento48Horas, model.FechaTercerSeguimiento72Horas,
            model.FechaCuartoSeguimientoSemana1, model.FechaQuintoSeguimientoSemana2, model.FechaSextoSeguimientoSemana3,
            model.FechaSeptimoSeguimientoSemana4);

        return GuardarClinicaHeridasSeccionAsync(
            model,
            restorePostedFields: m =>
            {
                (m.FechaHospitalizacion, m.MotivoHospitalizacion, m.RemitidoPorHospitalizacion, m.IpsIntramural,
                    m.FechaPrimerSeguimiento24Horas, m.FechaSegundoSeguimiento48Horas, m.FechaTercerSeguimiento72Horas,
                    m.FechaCuartoSeguimientoSemana1, m.FechaQuintoSeguimientoSemana2, m.FechaSextoSeguimientoSemana3,
                    m.FechaSeptimoSeguimientoSemana4) = posted;
            },
            applySectionToRecord: (record, m) =>
            {
                record.FechaHospitalizacion = m.FechaHospitalizacion?.Date;
                record.MotivoHospitalizacion = m.MotivoHospitalizacion;
                record.RemitidoPorHospitalizacion = m.RemitidoPorHospitalizacion;
                record.IpsIntramural = m.IpsIntramural;
                record.FechaPrimerSeguimiento24Horas = m.FechaPrimerSeguimiento24Horas?.Date;
                record.FechaSegundoSeguimiento48Horas = m.FechaSegundoSeguimiento48Horas?.Date;
                record.FechaTercerSeguimiento72Horas = m.FechaTercerSeguimiento72Horas?.Date;
                record.FechaCuartoSeguimientoSemana1 = m.FechaCuartoSeguimientoSemana1?.Date;
                record.FechaQuintoSeguimientoSemana2 = m.FechaQuintoSeguimientoSemana2?.Date;
                record.FechaSextoSeguimientoSemana3 = m.FechaSextoSeguimientoSemana3?.Date;
                record.FechaSeptimoSeguimientoSemana4 = m.FechaSeptimoSeguimientoSemana4?.Date;
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar el seguimiento hospitalizado.",
            auditAction: "CENSO_CLINICA_HERIDAS_SEGUIMIENTO_HOSPITALIZADO_ACTUALIZADO",
            successMessage: "Seguimiento hospitalizado guardado correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarClinicaHeridasDevolucionProductos(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.MotivoNovedadDevolucionProductos = string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos) ? null : model.MotivoNovedadDevolucionProductos.Trim();
        model.NotificacionAuxiliarDevolucionProductos = string.IsNullOrWhiteSpace(model.NotificacionAuxiliarDevolucionProductos) ? null : model.NotificacionAuxiliarDevolucionProductos.Trim();
        model.EstadoDevolucionServicioFarmaceutico = string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico) ? null : model.EstadoDevolucionServicioFarmaceutico.Trim();

        if (!string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos)
            && !MotivoNovedadDevolucionProductosValues.Contains(model.MotivoNovedadDevolucionProductos, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoNovedadDevolucionProductos), "Selecciona un motivo de la novedad válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.NotificacionAuxiliarDevolucionProductos)
            && !ClinicaHeridasSiNoValues.Contains(model.NotificacionAuxiliarDevolucionProductos, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.NotificacionAuxiliarDevolucionProductos), "Selecciona una opción válida para notificación al auxiliar.");
        }

        if (!string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico)
            && !EstadoDevolucionServicioFarmaceuticoValues.Contains(model.EstadoDevolucionServicioFarmaceutico, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoDevolucionServicioFarmaceutico), "Selecciona un estado de la devolución válido.");
        }

        var posted = (model.FechaNovedadDevolucionProductos, model.MotivoNovedadDevolucionProductos,
            model.NotificacionAuxiliarDevolucionProductos, model.FechaMaximaDevolucionProductos,
            model.EstadoDevolucionServicioFarmaceutico);

        return GuardarClinicaHeridasSeccionAsync(
            model,
            restorePostedFields: m =>
            {
                (m.FechaNovedadDevolucionProductos, m.MotivoNovedadDevolucionProductos,
                    m.NotificacionAuxiliarDevolucionProductos, m.FechaMaximaDevolucionProductos,
                    m.EstadoDevolucionServicioFarmaceutico) = posted;
            },
            applySectionToRecord: (record, m) =>
            {
                record.FechaNovedadDevolucionProductos = m.FechaNovedadDevolucionProductos?.Date;
                record.MotivoNovedadDevolucionProductos = m.MotivoNovedadDevolucionProductos;
                record.NotificacionAuxiliarDevolucionProductos = m.NotificacionAuxiliarDevolucionProductos;
                record.FechaMaximaDevolucionProductos = m.FechaMaximaDevolucionProductos?.Date;
                record.EstadoDevolucionServicioFarmaceutico = m.EstadoDevolucionServicioFarmaceutico;
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar la devolución de productos.",
            auditAction: "CENSO_CLINICA_HERIDAS_DEVOLUCION_PRODUCTOS_ACTUALIZADA",
            successMessage: "Devolución de productos guardada correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarClinicaHeridasAltaPrograma(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.MotivoEgreso = string.IsNullOrWhiteSpace(model.MotivoEgreso) ? null : model.MotivoEgreso.Trim();
        model.Estado = string.IsNullOrWhiteSpace(model.Estado) ? null : model.Estado.Trim();

        if (!string.IsNullOrWhiteSpace(model.MotivoEgreso)
            && !ClinicaHeridasMotivoEgresoValues.Contains(model.MotivoEgreso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoEgreso), "Selecciona un motivo del egreso válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.Estado)
            && !ClinicaHeridasEstadoProgramaValues.Contains(model.Estado, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Estado), "Selecciona un estado válido.");
        }

        var posted = (model.MotivoEgreso, model.FechaEgreso, model.Estado);

        return GuardarClinicaHeridasSeccionAsync(
            model,
            restorePostedFields: m => (m.MotivoEgreso, m.FechaEgreso, m.Estado) = posted,
            applySectionToRecord: (record, m) =>
            {
                record.MotivoEgreso = m.MotivoEgreso;
                record.FechaEgreso = m.FechaEgreso?.Date;
                record.Estado = string.IsNullOrWhiteSpace(m.Estado) ? record.Estado : m.Estado;
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar el alta del programa.",
            auditAction: "CENSO_CLINICA_HERIDAS_ALTA_PROGRAMA_ACTUALIZADA",
            successMessage: "Alta del programa guardada correctamente.",
            cancellationToken);
    }

    private async Task<IActionResult> GuardarClinicaHeridasSeccionAsync(
        CensoClinicaHeridasViewModel model,
        Action<CensoClinicaHeridasViewModel> restorePostedFields,
        Action<CensoClinicaHeridasRecord, CensoClinicaHeridasViewModel> applySectionToRecord,
        string missingRecordMessage,
        string auditAction,
        string successMessage,
        CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);

        CensoClinicaHeridasRecord? record = null;
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoClinicaHeridas
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);
        }

        if (record is null)
        {
            ModelState.AddModelError(string.Empty, missingRecordMessage);
        }
        else
        {
            ApplyClinicaHeridasRecordToModel(model, record);
            model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                ? record.NumeroIdentificacion
                : model.CedulaFiltro;
        }

        await PopulateClinicaHeridasDropdownsAsync(model, cancellationToken);
        restorePostedFields(model);

        if (!ModelState.IsValid)
        {
            return View("ClinicaHeridas", model);
        }

        var heridasRecord = record!;
        applySectionToRecord(heridasRecord, model);
        heridasRecord.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(auditAction, "CensoClinicaHeridas",
            $"Paciente: {heridasRecord.NombrePaciente}, Doc: {heridasRecord.NumeroIdentificacion}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = successMessage;
        return RedirectToAction(nameof(ClinicaHeridas), new { recordId = heridasRecord.Id, cedulaPaciente = heridasRecord.NumeroIdentificacion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirClinicaHeridasAdjuntos(
        CensoClinicaHeridasViewModel model,
        List<IFormFile> heridaAdjuntos,
        CancellationToken cancellationToken)
    {
        CensoClinicaHeridasRecord? record = null;
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoClinicaHeridas
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);
        }

        if (record is null)
        {
            ModelState.AddModelError(string.Empty, "Primero guarda los datos básicos del paciente para adjuntar fotos de la herida.");
            await PopulateClinicaHeridasDropdownsAsync(model, cancellationToken);
            return View("ClinicaHeridas", model);
        }

        var result = await _sharePointDocumentService.UploadClinicaHeridasDocumentsAsync(
            record.NombrePaciente,
            record.TipoIdentificacion,
            record.NumeroIdentificacion,
            heridaAdjuntos,
            cancellationToken);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Fotos de la herida guardadas en SharePoint correctamente.";
        }
        else
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "No fue posible guardar las fotos en SharePoint.";
        }

        return RedirectToAction(nameof(ClinicaHeridas), new { recordId = record.Id, cedulaPaciente = record.NumeroIdentificacion });
    }

    private void ValidateClinicaHeridasManejoHeridaModel(CensoClinicaHeridasViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.DescripcionHerida))
        {
            ModelState.AddModelError(nameof(model.DescripcionHerida), "Ingresa la descripción de la herida.");
        }

        if (!ClinicaHeridasUbicacionHeridaValues.Contains(model.UbicacionHerida ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.UbicacionHerida), "Selecciona una ubicación de la herida válida.");
        }

        if (!model.FrecuenciaVisitasSemana.HasValue
            || model.FrecuenciaVisitasSemana.Value < 1
            || model.FrecuenciaVisitasSemana.Value > 7)
        {
            ModelState.AddModelError(nameof(model.FrecuenciaVisitasSemana), "Selecciona la frecuencia de visitas a la semana (1 a 7).");
        }
    }

    private async Task PopulateClinicaHeridasAdjuntosAsync(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        if (!model.EditingRecordId.HasValue)
        {
            model.AdjuntosHerida = [];
            model.AdjuntosHeridaError = null;
            return;
        }

        var record = await _context.CensoClinicaHeridas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);

        if (record is null)
        {
            model.AdjuntosHerida = [];
            model.AdjuntosHeridaError = null;
            return;
        }

        var result = await _sharePointDocumentService.ListClinicaHeridasDocumentsAsync(
            record.NombrePaciente,
            record.TipoIdentificacion,
            record.NumeroIdentificacion,
            cancellationToken);

        if (!result.Succeeded)
        {
            model.AdjuntosHerida = [];
            model.AdjuntosHeridaError = result.ErrorMessage;
            return;
        }

        model.AdjuntosHerida = result.Value?
            .Select(item => new CensoClinicaHeridasAdjuntoViewModel
            {
                Name = item.Name,
                WebUrl = item.WebUrl,
                Size = item.Size,
                LastModifiedAt = item.LastModifiedAt
            })
            .ToList() ?? [];
        model.AdjuntosHeridaError = null;
    }

    private CensoClinicaHeridasViewModel BuildDefaultClinicaHeridasModel()
    {
        var today = GetColombiaNow().Date;
        return new CensoClinicaHeridasViewModel
        {
            FechaIngresoPrograma = today,
            FechaNacimiento = today,
            FechaValoracion = today,
            DireccionEsValida = false
        };
    }

    private async Task PopulateClinicaHeridasDropdownsAsync(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.AseguradorOptions = BuildOptions(ClinicaHeridasAseguradorValues);
        model.TipoIdentificacionOptions = BuildOptions(TiposIdentificacion);
        model.GeneroOptions = BuildOptions(ClinicaHeridasGeneroValues);
        model.ClasificacionZonaSuraOptions = BuildOptions(ClasificacionZonaSuraValues);
        model.MunicipioResidenciaOptions = BuildOptions(MunicipiosResidenciaValues);
        model.ZonaDireccionOptions = BuildOptions(ZonaDireccionValues);
        model.LlamadaBienvenidaOptions = BuildOptions(ClinicaHeridasLlamadaBienvenidaValues);
        model.ProgramaPerteneceOptions = BuildOptions(ClinicaHeridasProgramaValues);
        model.AuxiliarEnfermeriaOptions = await GetOpsAssistantOptionsAsync(cancellationToken);
        model.SiNoOptions = BuildOptions(ClinicaHeridasSiNoValues);
        model.UbicacionHeridaOptions = BuildOptions(ClinicaHeridasUbicacionHeridaValues);
        model.FrecuenciaVisitasOptions = BuildOptions(ClinicaHeridasFrecuenciaVisitasValues);
        model.MotivoHospitalizacionOptions = BuildOptions(ClinicaHeridasMotivoHospitalizacionValues);
        model.RemitidoPorHospitalizacionOptions = BuildOptions(ClinicaHeridasRemitidoPorValues);
        model.MotivoNovedadDevolucionOptions = BuildOptions(MotivoNovedadDevolucionProductosValues);
        model.EstadoDevolucionOptions = BuildOptions(EstadoDevolucionServicioFarmaceuticoValues);
        model.MotivoEgresoOptions = BuildOptions(ClinicaHeridasMotivoEgresoValues);
        model.EstadoProgramaOptions = BuildOptions(ClinicaHeridasEstadoProgramaValues);

        model.MunicipioResidencia = ToCanonicalMunicipality(model.MunicipioResidencia);

        if (!string.IsNullOrWhiteSpace(model.MunicipioResidencia)
            && string.IsNullOrWhiteSpace(model.ClasificacionZonaSura))
        {
            model.ClasificacionZonaSura = InferClasificacionZonaSura(model.MunicipioResidencia);
        }

        if (!string.IsNullOrWhiteSpace(model.MunicipioResidencia)
            && string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio))
        {
            model.ZonaDireccionSegunMunicipio = InferZonaDireccionSegunMunicipio(model.MunicipioResidencia, model.Barrio, direccion: model.Direccion);
        }

        if (!string.IsNullOrWhiteSpace(model.CodigoCie10))
        {
            model.CodigoCie10 = NormalizeCie10(model.CodigoCie10);
            if (string.IsNullOrWhiteSpace(model.DiagnosticoDescriptivo)
                && _cie10Catalog.TryGetValue(model.CodigoCie10, out var diagnostico))
            {
                model.DiagnosticoDescriptivo = diagnostico;
            }
        }

        IReadOnlyList<string> barrioOptions = string.IsNullOrWhiteSpace(model.MunicipioResidencia)
            ? []
            : await _addressValidationService.SearchNeighborhoodsAsync(
                model.MunicipioResidencia,
                string.IsNullOrWhiteSpace(model.Barrio) ? "a" : model.Barrio,
                cancellationToken);

        if (barrioOptions.Count == 0)
        {
            barrioOptions = ["NO PARAMETRIZADO"];
        }

        if (!string.IsNullOrWhiteSpace(model.Barrio)
            && !barrioOptions.Contains(model.Barrio, StringComparer.OrdinalIgnoreCase))
        {
            barrioOptions = barrioOptions
                .Concat([model.Barrio])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        model.BarrioOptions = barrioOptions;
        await PopulateClinicaHeridasLatestRecordsAsync(model, cancellationToken);
        await PopulateClinicaHeridasAdjuntosAsync(model, cancellationToken);
    }

    private async Task PopulateClinicaHeridasLatestRecordsAsync(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);

        var query = _context.CensoClinicaHeridas.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(model.CedulaFiltro))
        {
            query = query.Where(x => x.NumeroIdentificacion == model.CedulaFiltro);
        }

        model.UltimosRegistros = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    private void NormalizeClinicaHeridasModel(CensoClinicaHeridasViewModel model)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);
        model.Asegurador = model.Asegurador?.Trim() ?? string.Empty;
        model.TipoIdentificacion = NormalizeClinicaHeridasText(model.TipoIdentificacion);
        model.NumeroIdentificacion = NormalizeIdentificationNumber(model.TipoIdentificacion, model.NumeroIdentificacion);
        model.NombrePaciente = NormalizeClinicaHeridasText(model.NombrePaciente);
        model.Genero = model.Genero?.Trim() ?? string.Empty;
        model.Direccion = NormalizeClinicaHeridasText(model.Direccion);
        model.DetalleDireccion = NormalizeOptionalClinicaHeridasText(model.DetalleDireccion);
        model.ClasificacionZonaSura = model.ClasificacionZonaSura?.Trim() ?? string.Empty;
        model.MunicipioResidencia = model.MunicipioResidencia?.Trim() ?? string.Empty;
        model.Barrio = NormalizeClinicaHeridasText(model.Barrio);
        model.ZonaDireccionSegunMunicipio = model.ZonaDireccionSegunMunicipio?.Trim() ?? string.Empty;
        model.TelefonoPrincipal = NormalizePhone(model.TelefonoPrincipal);
        model.TelefonoAdicional1 = NormalizePhone(model.TelefonoAdicional1);
        model.TelefonoAdicional2 = string.IsNullOrWhiteSpace(model.TelefonoAdicional2) ? null : NormalizePhone(model.TelefonoAdicional2);
        model.LlamadaBienvenida = model.LlamadaBienvenida?.Trim();
        model.TelefonoContacto = string.IsNullOrWhiteSpace(model.TelefonoContacto) ? null : NormalizePhone(model.TelefonoContacto);
        model.Observacion = NormalizeOptionalClinicaHeridasText(model.Observacion);
        model.CodigoCie10 = NormalizeCie10(model.CodigoCie10);
        model.DiagnosticoDescriptivo = NormalizeOptionalClinicaHeridasText(model.DiagnosticoDescriptivo);
        model.ProgramaPertenece = model.ProgramaPertenece?.Trim() ?? string.Empty;
        model.AuxiliarEnfermeriaAsignado = NormalizeOptionalClinicaHeridasText(model.AuxiliarEnfermeriaAsignado);
        model.Picc = model.Picc?.Trim() ?? string.Empty;
        model.Vac = model.Vac?.Trim() ?? string.Empty;
        model.DescripcionHerida = NormalizeOptionalClinicaHeridasText(model.DescripcionHerida);
        model.UbicacionHerida = string.IsNullOrWhiteSpace(model.UbicacionHerida) ? null : model.UbicacionHerida.Trim();
        model.EquipoComodato = string.IsNullOrWhiteSpace(model.EquipoComodato) ? null : model.EquipoComodato.Trim();
        model.NumeroPlacaEquipos = NormalizeOptionalClinicaHeridasText(model.NumeroPlacaEquipos);
        model.MotivoHospitalizacion = string.IsNullOrWhiteSpace(model.MotivoHospitalizacion) ? null : model.MotivoHospitalizacion.Trim();
        model.RemitidoPorHospitalizacion = string.IsNullOrWhiteSpace(model.RemitidoPorHospitalizacion) ? null : model.RemitidoPorHospitalizacion.Trim();
        model.IpsIntramural = NormalizeOptionalClinicaHeridasText(model.IpsIntramural);
        model.MotivoNovedadDevolucionProductos = string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos) ? null : model.MotivoNovedadDevolucionProductos.Trim();
        model.NotificacionAuxiliarDevolucionProductos = string.IsNullOrWhiteSpace(model.NotificacionAuxiliarDevolucionProductos) ? null : model.NotificacionAuxiliarDevolucionProductos.Trim();
        model.EstadoDevolucionServicioFarmaceutico = string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico) ? null : model.EstadoDevolucionServicioFarmaceutico.Trim();
        model.MotivoEgreso = string.IsNullOrWhiteSpace(model.MotivoEgreso) ? null : model.MotivoEgreso.Trim();
        model.Estado = string.IsNullOrWhiteSpace(model.Estado) ? null : model.Estado.Trim();

        if (!string.Equals(model.Vac, "Si", StringComparison.OrdinalIgnoreCase))
        {
            model.EquipoComodato = null;
            model.NumeroPlacaEquipos = null;
            model.FechaEntregaEquipo = null;
            model.FechaDevolucionEquipo = null;
        }

        model.Edad = CalculateAge(model.FechaNacimiento.Date, GetColombiaNow().Date);
        ModelState.Remove(nameof(model.Edad));
    }

    private static string NormalizeClinicaHeridasText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpper(ClinicaHeridasTextCulture);
    }

    private static string? NormalizeOptionalClinicaHeridasText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpper(ClinicaHeridasTextCulture);
    }

    private void ValidateClinicaHeridasModel(CensoClinicaHeridasViewModel model)
    {
        if (!ClinicaHeridasAseguradorValues.Contains(model.Asegurador, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Asegurador), "Selecciona un asegurador válido.");
        }

        if (!TiposIdentificacion.Contains(model.TipoIdentificacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.TipoIdentificacion), "Selecciona un tipo de identificación válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.NumeroIdentificacion))
        {
            if (AllowsAlphaNumericIdentification(model.TipoIdentificacion))
            {
                if (!AlphaNumericIdentificationPattern.IsMatch(model.NumeroIdentificacion))
                {
                    ModelState.AddModelError(nameof(model.NumeroIdentificacion), "El número de documento solo permite letras y dígitos para PA o CE.");
                }
            }
            else if (!NumericIdentificationPattern.IsMatch(model.NumeroIdentificacion))
            {
                ModelState.AddModelError(nameof(model.NumeroIdentificacion), "El número de documento solo permite dígitos.");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.NombrePaciente)
            && !ClinicaHeridasNombrePattern.IsMatch(model.NombrePaciente))
        {
            ModelState.AddModelError(nameof(model.NombrePaciente), "El nombre del paciente solo permite letras y espacios.");
        }

        if (model.FechaNacimiento.Date >= GetColombiaNow().Date)
        {
            ModelState.AddModelError(nameof(model.FechaNacimiento), "La fecha de nacimiento debe ser anterior a la fecha actual.");
        }

        if (model.FechaIngresoPrograma.Date > GetColombiaNow().Date)
        {
            ModelState.AddModelError(nameof(model.FechaIngresoPrograma), "La fecha de ingreso al programa no puede ser futura.");
        }

        if (!ClinicaHeridasGeneroValues.Contains(model.Genero, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Genero), "Selecciona un género válido.");
        }

        if (!Cie10Pattern.IsMatch(model.CodigoCie10)
            || !_cie10Catalog.TryGetValue(model.CodigoCie10, out var diagnostico))
        {
            model.DiagnosticoDescriptivo = string.Empty;
            ModelState.AddModelError(nameof(model.CodigoCie10), "El código CIE10 ingresado no existe en el catálogo parametrizado.");
        }
        else
        {
            model.DiagnosticoDescriptivo = NormalizeClinicaHeridasText(diagnostico);
        }

        if (!ClinicaHeridasProgramaValues.Contains(model.ProgramaPertenece, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ProgramaPertenece), "Selecciona un programa válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.LlamadaBienvenida)
            && !ClinicaHeridasLlamadaBienvenidaValues.Contains(model.LlamadaBienvenida, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.LlamadaBienvenida), "Selecciona un estado de llamada de bienvenida válido.");
        }

        if (!ClinicaHeridasSiNoValues.Contains(model.Picc, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Picc), "Selecciona una opción válida para PICC.");
        }

        if (!ClinicaHeridasSiNoValues.Contains(model.Vac, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Vac), "Selecciona una opción válida para VAC.");
        }

        if (!string.IsNullOrWhiteSpace(model.AuxiliarEnfermeriaAsignado))
        {
            if (!model.AuxiliarEnfermeriaOptions.Any())
            {
                ModelState.AddModelError(nameof(model.AuxiliarEnfermeriaAsignado), "No hay auxiliares OPS activos para asignar.");
            }
            else
            {
                var canonicalAuxiliar = model.AuxiliarEnfermeriaOptions
                    .Select(x => x.Value)
                    .FirstOrDefault(x => string.Equals(x, model.AuxiliarEnfermeriaAsignado, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(canonicalAuxiliar))
                {
                    ModelState.AddModelError(nameof(model.AuxiliarEnfermeriaAsignado), "Selecciona un auxiliar OPS válido.");
                }
                else
                {
                    model.AuxiliarEnfermeriaAsignado = canonicalAuxiliar;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(model.UbicacionHerida)
            && !ClinicaHeridasUbicacionHeridaValues.Contains(model.UbicacionHerida, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.UbicacionHerida), "Selecciona una ubicación de la herida válida.");
        }

        if (!string.IsNullOrWhiteSpace(model.EquipoComodato)
            && !ClinicaHeridasSiNoValues.Contains(model.EquipoComodato, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EquipoComodato), "Selecciona una opción válida para equipo en comodato.");
        }

        if (model.FechaDevolucionEquipo.HasValue
            && model.FechaEntregaEquipo.HasValue
            && model.FechaDevolucionEquipo.Value.Date < model.FechaEntregaEquipo.Value.Date)
        {
            ModelState.AddModelError(nameof(model.FechaDevolucionEquipo), "La fecha de devolución no puede ser anterior a la fecha de entrega.");
        }

        if (!string.IsNullOrWhiteSpace(model.MotivoHospitalizacion)
            && !ClinicaHeridasMotivoHospitalizacionValues.Contains(model.MotivoHospitalizacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoHospitalizacion), "Selecciona un motivo de hospitalización válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.RemitidoPorHospitalizacion)
            && !ClinicaHeridasRemitidoPorValues.Contains(model.RemitidoPorHospitalizacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.RemitidoPorHospitalizacion), "Selecciona un valor válido para remitido por.");
        }

        if (!string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos)
            && !MotivoNovedadDevolucionProductosValues.Contains(model.MotivoNovedadDevolucionProductos, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoNovedadDevolucionProductos), "Selecciona un motivo de la novedad válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.NotificacionAuxiliarDevolucionProductos)
            && !ClinicaHeridasSiNoValues.Contains(model.NotificacionAuxiliarDevolucionProductos, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.NotificacionAuxiliarDevolucionProductos), "Selecciona una opción válida para notificación al auxiliar.");
        }

        if (!string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico)
            && !EstadoDevolucionServicioFarmaceuticoValues.Contains(model.EstadoDevolucionServicioFarmaceutico, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoDevolucionServicioFarmaceutico), "Selecciona un estado de la devolución válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.MotivoEgreso)
            && !ClinicaHeridasMotivoEgresoValues.Contains(model.MotivoEgreso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoEgreso), "Selecciona un motivo del egreso válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.Estado)
            && !ClinicaHeridasEstadoProgramaValues.Contains(model.Estado, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Estado), "Selecciona un estado válido.");
        }

        ValidatePhoneValue(model.TelefonoPrincipal, nameof(model.TelefonoPrincipal), "teléfono principal");
        ValidatePhoneValue(model.TelefonoAdicional1, nameof(model.TelefonoAdicional1), "teléfono adicional 1");
        ValidatePhoneValue(model.TelefonoAdicional2, nameof(model.TelefonoAdicional2), "teléfono adicional 2");
        ValidatePhoneValue(model.TelefonoContacto, nameof(model.TelefonoContacto), "teléfono de contacto");

        ValidateClinicaHeridasAddressDropdowns(model);
    }

    private void ValidateClinicaHeridasAddressDropdowns(CensoClinicaHeridasViewModel model)
    {
        model.MunicipioResidencia = ToCanonicalMunicipality(model.MunicipioResidencia) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(model.MunicipioResidencia)
            && !MunicipiosResidenciaValues.Contains(model.MunicipioResidencia, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MunicipioResidencia), "Selecciona un municipio válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.ClasificacionZonaSura)
            && !ClasificacionZonaSuraValues.Contains(model.ClasificacionZonaSura, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ClasificacionZonaSura), "Selecciona una clasificación zona Sura válida.");
        }

        if (!string.IsNullOrWhiteSpace(model.MunicipioResidencia))
        {
            var zonaInferida = InferZonaDireccionSegunMunicipio(model.MunicipioResidencia, model.Barrio, direccion: model.Direccion);
            if (!string.Equals(zonaInferida, "No Parametrizado", StringComparison.OrdinalIgnoreCase))
            {
                model.ZonaDireccionSegunMunicipio = zonaInferida;
            }
        }

        if (!string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio)
            && !ZonaDireccionValues.Contains(model.ZonaDireccionSegunMunicipio, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ZonaDireccionSegunMunicipio), "Selecciona una zona de dirección válida.");
        }
    }

    private void ClearClinicaHeridasAddressModelState()
    {
        foreach (var key in new[]
        {
            nameof(CensoClinicaHeridasViewModel.Direccion),
            nameof(CensoClinicaHeridasViewModel.ClasificacionZonaSura),
            nameof(CensoClinicaHeridasViewModel.MunicipioResidencia),
            nameof(CensoClinicaHeridasViewModel.Barrio),
            nameof(CensoClinicaHeridasViewModel.ZonaDireccionSegunMunicipio)
        })
        {
            ModelState.Remove(key);
        }
    }

    private void ApplyClinicaHeridasAddressValidationResult(
        CensoClinicaHeridasViewModel model,
        AddressValidationResult direccionValidation,
        ref string direccionParaGuardar)
    {
        if (direccionValidation.Outcome == AddressValidationOutcome.Valid)
        {
            model.DireccionEsValida = true;
            model.AsumirDireccionErrada = false;
            model.DireccionSugerida = direccionValidation.FormattedAddress;
            model.DireccionMensajeValidacion = direccionValidation.Message;

            if (!string.IsNullOrWhiteSpace(direccionValidation.FormattedAddress))
            {
                direccionParaGuardar = direccionValidation.FormattedAddress;
                model.Direccion = direccionParaGuardar;
            }

            ApplyClinicaHeridasAddressLocationDefaults(model, direccionValidation);
            return;
        }

        model.DireccionEsValida = false;
        model.DireccionSugerida = direccionValidation.SuggestedAddress;
        model.DireccionMensajeValidacion = direccionValidation.Message;
        ApplyClinicaHeridasAddressLocationDefaults(model, direccionValidation);

        if (model.AsumirDireccionErrada)
        {
            return;
        }

        var mensaje = direccionValidation.Message;
        if (!string.IsNullOrWhiteSpace(direccionValidation.SuggestedAddress))
        {
            mensaje += $" Sugerencia: {direccionValidation.SuggestedAddress}.";
        }

        mensaje += " Corrige la dirección o marca 'Asumir dirección errada y continuar'.";
        ModelState.AddModelError(nameof(model.Direccion), mensaje);
    }

    private void ApplyClinicaHeridasAddressLocationDefaults(CensoClinicaHeridasViewModel model, AddressValidationResult validation)
    {
        var canonicalMunicipio = ToCanonicalMunicipality(validation.Municipality);
        if (!string.IsNullOrWhiteSpace(canonicalMunicipio))
        {
            model.MunicipioResidencia = canonicalMunicipio;
            model.ClasificacionZonaSura = InferClasificacionZonaSura(canonicalMunicipio);
        }

        if (string.IsNullOrWhiteSpace(model.Barrio) && !string.IsNullOrWhiteSpace(validation.Neighborhood))
        {
            model.Barrio = validation.Neighborhood.Trim();
        }

        if (!string.IsNullOrWhiteSpace(canonicalMunicipio))
        {
            var zonaInferida = InferZonaDireccionSegunMunicipio(
                canonicalMunicipio,
                model.Barrio,
                validation.District,
                validation.FormattedAddress);

            if (string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio)
                || string.Equals(model.ZonaDireccionSegunMunicipio, "No Parametrizado", StringComparison.OrdinalIgnoreCase))
            {
                model.ZonaDireccionSegunMunicipio = zonaInferida;
            }
        }
    }

    private static void ApplyClinicaHeridasModelToRecord(
        CensoClinicaHeridasViewModel model,
        CensoClinicaHeridasRecord record,
        string direccionParaGuardar,
        bool preserveCreatedAt)
    {
        record.Asegurador = model.Asegurador;
        record.FechaIngresoPrograma = model.FechaIngresoPrograma.Date;
        record.TipoIdentificacion = model.TipoIdentificacion;
        record.NumeroIdentificacion = model.NumeroIdentificacion;
        record.NombrePaciente = model.NombrePaciente;
        record.FechaNacimiento = model.FechaNacimiento.Date;
        record.Edad = model.Edad;
        record.Genero = model.Genero;
        record.Direccion = NormalizeOptionalClinicaHeridasText(direccionParaGuardar);
        record.DireccionValidada = model.DireccionEsValida;
        record.AsumirDireccionErrada = model.AsumirDireccionErrada;
        record.DetalleDireccion = model.DetalleDireccion;
        record.ClasificacionZonaSura = string.IsNullOrWhiteSpace(model.ClasificacionZonaSura) ? null : model.ClasificacionZonaSura;
        record.MunicipioResidencia = string.IsNullOrWhiteSpace(model.MunicipioResidencia) ? null : model.MunicipioResidencia;
        record.Barrio = string.IsNullOrWhiteSpace(model.Barrio) ? null : model.Barrio;
        record.ZonaDireccionSegunMunicipio = string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio) ? null : model.ZonaDireccionSegunMunicipio;
        record.TelefonoPrincipal = model.TelefonoPrincipal;
        record.TelefonoAdicional1 = model.TelefonoAdicional1;
        record.TelefonoAdicional2 = model.TelefonoAdicional2;
        record.LlamadaBienvenida = string.IsNullOrWhiteSpace(model.LlamadaBienvenida) ? null : model.LlamadaBienvenida;
        record.TelefonoContacto = model.TelefonoContacto;
        record.Observacion = model.Observacion;
        record.CodigoCie10 = model.CodigoCie10;
        record.DiagnosticoDescriptivo = model.DiagnosticoDescriptivo ?? string.Empty;
        record.FechaValoracion = model.FechaValoracion.Date;
        record.ProgramaPertenece = model.ProgramaPertenece;
        record.AuxiliarEnfermeriaAsignado = model.AuxiliarEnfermeriaAsignado;
        record.Picc = model.Picc;
        record.Vac = model.Vac;
        record.DescripcionHerida = model.DescripcionHerida;
        record.UbicacionHerida = model.UbicacionHerida;
        record.FrecuenciaVisitasSemana = model.FrecuenciaVisitasSemana;
        record.EquipoComodato = model.EquipoComodato;
        record.NumeroPlacaEquipos = model.NumeroPlacaEquipos;
        record.FechaEntregaEquipo = model.FechaEntregaEquipo?.Date;
        record.FechaDevolucionEquipo = model.FechaDevolucionEquipo?.Date;
        record.FechaHospitalizacion = model.FechaHospitalizacion?.Date;
        record.MotivoHospitalizacion = model.MotivoHospitalizacion;
        record.RemitidoPorHospitalizacion = model.RemitidoPorHospitalizacion;
        record.IpsIntramural = model.IpsIntramural;
        record.FechaPrimerSeguimiento24Horas = model.FechaPrimerSeguimiento24Horas?.Date;
        record.FechaSegundoSeguimiento48Horas = model.FechaSegundoSeguimiento48Horas?.Date;
        record.FechaTercerSeguimiento72Horas = model.FechaTercerSeguimiento72Horas?.Date;
        record.FechaCuartoSeguimientoSemana1 = model.FechaCuartoSeguimientoSemana1?.Date;
        record.FechaQuintoSeguimientoSemana2 = model.FechaQuintoSeguimientoSemana2?.Date;
        record.FechaSextoSeguimientoSemana3 = model.FechaSextoSeguimientoSemana3?.Date;
        record.FechaSeptimoSeguimientoSemana4 = model.FechaSeptimoSeguimientoSemana4?.Date;
        record.FechaNovedadDevolucionProductos = model.FechaNovedadDevolucionProductos?.Date;
        record.MotivoNovedadDevolucionProductos = model.MotivoNovedadDevolucionProductos;
        record.NotificacionAuxiliarDevolucionProductos = model.NotificacionAuxiliarDevolucionProductos;
        record.FechaMaximaDevolucionProductos = model.FechaMaximaDevolucionProductos?.Date;
        record.EstadoDevolucionServicioFarmaceutico = model.EstadoDevolucionServicioFarmaceutico;
        record.MotivoEgreso = model.MotivoEgreso;
        record.FechaEgreso = model.FechaEgreso?.Date;
        record.Estado = string.IsNullOrWhiteSpace(model.Estado)
            ? (preserveCreatedAt ? record.Estado : "Activo")
            : model.Estado;

        if (preserveCreatedAt)
        {
            record.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            record.CreatedAtUtc = DateTime.UtcNow;
        }
    }

    private static void ApplyClinicaHeridasRecordToModel(
        CensoClinicaHeridasViewModel model,
        CensoClinicaHeridasRecord record)
    {
        model.EditingRecordId = record.Id;
        model.Asegurador = record.Asegurador;
        model.FechaIngresoPrograma = record.FechaIngresoPrograma.Date;
        model.TipoIdentificacion = record.TipoIdentificacion;
        model.NumeroIdentificacion = record.NumeroIdentificacion;
        model.NombrePaciente = record.NombrePaciente;
        model.FechaNacimiento = record.FechaNacimiento.Date;
        model.Edad = record.Edad;
        model.Genero = record.Genero;
        model.Direccion = record.Direccion;
        model.DireccionEsValida = record.DireccionValidada;
        model.AsumirDireccionErrada = record.AsumirDireccionErrada;
        model.DetalleDireccion = record.DetalleDireccion;
        model.ClasificacionZonaSura = record.ClasificacionZonaSura;
        model.MunicipioResidencia = record.MunicipioResidencia;
        model.Barrio = record.Barrio;
        model.ZonaDireccionSegunMunicipio = record.ZonaDireccionSegunMunicipio;
        model.TelefonoPrincipal = record.TelefonoPrincipal;
        model.TelefonoAdicional1 = record.TelefonoAdicional1;
        model.TelefonoAdicional2 = record.TelefonoAdicional2;
        model.LlamadaBienvenida = record.LlamadaBienvenida;
        model.TelefonoContacto = record.TelefonoContacto;
        model.Observacion = record.Observacion;
        model.CodigoCie10 = record.CodigoCie10;
        model.DiagnosticoDescriptivo = record.DiagnosticoDescriptivo;
        model.FechaValoracion = record.FechaValoracion.Date;
        model.ProgramaPertenece = record.ProgramaPertenece;
        model.AuxiliarEnfermeriaAsignado = record.AuxiliarEnfermeriaAsignado;
        model.Picc = record.Picc;
        model.Vac = record.Vac;
        model.DescripcionHerida = record.DescripcionHerida;
        model.UbicacionHerida = record.UbicacionHerida;
        model.FrecuenciaVisitasSemana = record.FrecuenciaVisitasSemana;
        model.EquipoComodato = record.EquipoComodato;
        model.NumeroPlacaEquipos = record.NumeroPlacaEquipos;
        model.FechaEntregaEquipo = record.FechaEntregaEquipo?.Date;
        model.FechaDevolucionEquipo = record.FechaDevolucionEquipo?.Date;
        model.FechaHospitalizacion = record.FechaHospitalizacion?.Date;
        model.MotivoHospitalizacion = record.MotivoHospitalizacion;
        model.RemitidoPorHospitalizacion = record.RemitidoPorHospitalizacion;
        model.IpsIntramural = record.IpsIntramural;
        model.FechaPrimerSeguimiento24Horas = record.FechaPrimerSeguimiento24Horas?.Date;
        model.FechaSegundoSeguimiento48Horas = record.FechaSegundoSeguimiento48Horas?.Date;
        model.FechaTercerSeguimiento72Horas = record.FechaTercerSeguimiento72Horas?.Date;
        model.FechaCuartoSeguimientoSemana1 = record.FechaCuartoSeguimientoSemana1?.Date;
        model.FechaQuintoSeguimientoSemana2 = record.FechaQuintoSeguimientoSemana2?.Date;
        model.FechaSextoSeguimientoSemana3 = record.FechaSextoSeguimientoSemana3?.Date;
        model.FechaSeptimoSeguimientoSemana4 = record.FechaSeptimoSeguimientoSemana4?.Date;
        model.FechaNovedadDevolucionProductos = record.FechaNovedadDevolucionProductos?.Date;
        model.MotivoNovedadDevolucionProductos = record.MotivoNovedadDevolucionProductos;
        model.NotificacionAuxiliarDevolucionProductos = record.NotificacionAuxiliarDevolucionProductos;
        model.FechaMaximaDevolucionProductos = record.FechaMaximaDevolucionProductos?.Date;
        model.EstadoDevolucionServicioFarmaceutico = record.EstadoDevolucionServicioFarmaceutico;
        model.MotivoEgreso = record.MotivoEgreso;
        model.FechaEgreso = record.FechaEgreso?.Date;
        model.Estado = record.Estado;
    }
}
