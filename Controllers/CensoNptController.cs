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
    private static readonly CultureInfo NptTextCulture = CultureInfo.GetCultureInfo("es-CO");
    private static readonly string[] NptAseguradorValues =
    [
        "SURA",
        "PANAMERICAN LIFE",
        "PARTICULAR"
    ];
    private static readonly string[] NptGeneroValues =
    [
        "Masculino",
        "Femenino"
    ];
    private static readonly string[] NptLlamadaBienvenidaValues =
    [
        "Efectivo",
        "No efectivo"
    ];
    private static readonly string[] NptProgramaValues =
    [
        "AGUDO",
        "CRONICO",
        "NPT",
        "CLINICA DE HERIDAS",
        "CUIDADOR/AUX DE ENFERMERIA",
        "VAC"
    ];
    private static readonly string[] NptTipoNutricionValues = ["Enteral", "Parenteral"];
    private static readonly string[] NptTipoSondaValues = ["NASOGASTRICA", "GASTROSTOMIA"];
    private static readonly string[] NptSiNoValues = ["Si", "No"];
    private static readonly Regex NptNombrePattern = new(@"^[\p{L}\s]+$", RegexOptions.Compiled);
    private static readonly string[] NptMotivoHospitalizacionValues =
    [
        "DOLOR",
        "NO MEJORIA CLINICA",
        "FALLAS EN LA ATENCION DOMICILIARIA"
    ];
    private static readonly string[] NptRemitidoPorValues =
    [
        "FAMILIAR",
        "MEDICO IPS",
        "EMI/CEM/OTROS"
    ];
    private static readonly string[] NptMotivoEgresoValues =
    [
        "CURACION",
        "FALLECE",
        "CAMBIO DE PRESTADOR",
        "CAMBIO DE ASEGURADOR",
        "ALTA VOLUNTARIA",
        "NO APLICA",
        "REINGRESO HOSPITALARIO",
        "ALTA MEDICA",
        "PROCEDIMIENTO QUIRURGICO",
        "DESMONTE",
        "CANCELAN TRAMITE DOMICILIARIO"
    ];
    private static readonly string[] NptEstadoProgramaValues = ["Activo", "Inactivo"];

    [HttpGet]
    public async Task<IActionResult> Npt(
        string? cedulaPaciente,
        long? recordId,
        CancellationToken cancellationToken)
    {
        var model = BuildDefaultNptModel();
        model.CedulaFiltro = NormalizeCedulaFilter(cedulaPaciente);

        if (recordId.HasValue)
        {
            var record = await _context.CensoNpt
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == recordId.Value, cancellationToken);
            if (record is not null)
            {
                ApplyNptRecordToModel(model, record);
                model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                    ? record.NumeroIdentificacion
                    : model.CedulaFiltro;
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.CedulaFiltro))
        {
            var record = await _context.CensoNpt
                .AsNoTracking()
                .Where(x => x.NumeroIdentificacion == model.CedulaFiltro)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (record is not null)
            {
                ApplyNptRecordToModel(model, record);
                model.CedulaFiltro = record.NumeroIdentificacion;
            }
        }

        await PopulateNptDropdownsAsync(model, cancellationToken);
        return View("Npt", model);
    }

    [HttpPost]
    public async Task<IActionResult> Npt(CensoNptViewModel model, CancellationToken cancellationToken)
    {
        NormalizeNptModel(model);
        await PopulateNptDropdownsAsync(model, cancellationToken);
        ValidateNptModel(model);

        var direccionParaGuardar = model.Direccion ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(model.Direccion))
        {
            var direccionValidation = await _addressValidationService.ValidateAddressAsync(direccionParaGuardar, cancellationToken);
            ApplyNptAddressValidationResult(model, direccionValidation, ref direccionParaGuardar);
        }
        else
        {
            ClearNptAddressModelState();
            model.DireccionEsValida = false;
            model.AsumirDireccionErrada = false;
            model.DireccionSugerida = null;
            model.DireccionMensajeValidacion = null;
            direccionParaGuardar = model.Direccion ?? string.Empty;
        }

        if (!ModelState.IsValid)
        {
            await PopulateNptLatestRecordsAsync(model, cancellationToken);
            return View("Npt", model);
        }

        CensoNptRecord record;
        var auditAction = "CENSO_NPT_CREADO";
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoNpt
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken)
                ?? new CensoNptRecord();
            ApplyNptModelToRecord(model, record, direccionParaGuardar, preserveCreatedAt: record.Id != 0);
            auditAction = record.Id == 0 ? "CENSO_NPT_CREADO" : "CENSO_NPT_ACTUALIZADO";
            if (record.Id == 0)
            {
                await _context.CensoNpt.AddAsync(record, cancellationToken);
            }
        }
        else
        {
            record = new CensoNptRecord();
            ApplyNptModelToRecord(model, record, direccionParaGuardar, preserveCreatedAt: false);
            await _context.CensoNpt.AddAsync(record, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(auditAction, "CensoNpt",
            $"Paciente: {record.NombrePaciente}, Doc: {record.NumeroIdentificacion}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = model.EditingRecordId.HasValue
            ? "Registro de NPT actualizado correctamente."
            : "Registro de NPT guardado correctamente.";
        return RedirectToAction(nameof(Npt), new { cedulaPaciente = record.NumeroIdentificacion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarNptManejo(CensoNptViewModel model, CancellationToken cancellationToken)
    {
        if (!model.FechaInicioNpt.HasValue)
        {
            ModelState.AddModelError(nameof(model.FechaInicioNpt), "Selecciona la fecha de inicio.");
        }

        if (model.FechaFinNpt.HasValue
            && model.FechaInicioNpt.HasValue
            && model.FechaFinNpt.Value.Date < model.FechaInicioNpt.Value.Date)
        {
            ModelState.AddModelError(nameof(model.FechaFinNpt), "La fecha fin no puede ser anterior a la fecha de inicio.");
        }

        if (!model.HoraConexion.HasValue)
        {
            ModelState.AddModelError(nameof(model.HoraConexion), "Ingresa la hora de conexión.");
        }

        if (!model.HoraDesconexion.HasValue)
        {
            ModelState.AddModelError(nameof(model.HoraDesconexion), "Ingresa la hora de desconexión.");
        }

        model.DiasTratamiento = CalculateNptDiasTratamiento(model.FechaInicioNpt, model.FechaFinNpt);
        ModelState.Remove(nameof(model.DiasTratamiento));

        var posted = (model.FechaInicioNpt, model.FechaFinNpt, model.DiasTratamiento, model.HoraConexion, model.HoraDesconexion);

        return GuardarNptSeccionAsync(
            model,
            restorePostedFields: m =>
                (m.FechaInicioNpt, m.FechaFinNpt, m.DiasTratamiento, m.HoraConexion, m.HoraDesconexion) = posted,
            applySectionToRecord: (record, m) =>
            {
                record.FechaInicioNpt = m.FechaInicioNpt?.Date;
                record.FechaFinNpt = m.FechaFinNpt?.Date;
                record.DiasTratamiento = m.DiasTratamiento;
                record.HoraConexion = m.HoraConexion;
                record.HoraDesconexion = m.HoraDesconexion;
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar el manejo de la NPT.",
            auditAction: "CENSO_NPT_MANEJO_ACTUALIZADO",
            successMessage: "Manejo de la NPT guardado correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarNptCargueServicios(CensoNptViewModel model, CancellationToken cancellationToken)
    {
        model.CargueLaboratorios = string.IsNullOrWhiteSpace(model.CargueLaboratorios) ? null : model.CargueLaboratorios.Trim();
        model.CargueGlucometria = string.IsNullOrWhiteSpace(model.CargueGlucometria) ? null : model.CargueGlucometria.Trim();
        model.CargueServiciosComplementarios = string.IsNullOrWhiteSpace(model.CargueServiciosComplementarios) ? null : model.CargueServiciosComplementarios.Trim();
        model.CargueSeguimientoMedico = string.IsNullOrWhiteSpace(model.CargueSeguimientoMedico) ? null : model.CargueSeguimientoMedico.Trim();

        ValidateNptSiNoOptional(model.CargueLaboratorios, nameof(model.CargueLaboratorios), "cargue laboratorios");
        ValidateNptSiNoOptional(model.CargueGlucometria, nameof(model.CargueGlucometria), "cargue de glucometría");
        ValidateNptSiNoOptional(model.CargueServiciosComplementarios, nameof(model.CargueServiciosComplementarios), "cargue de servicios complementarios");
        ValidateNptSiNoOptional(model.CargueSeguimientoMedico, nameof(model.CargueSeguimientoMedico), "cargue de seguimiento médico");

        var posted = (model.CargueLaboratorios, model.CargueGlucometria, model.CargueServiciosComplementarios, model.CargueSeguimientoMedico);

        return GuardarNptSeccionAsync(
            model,
            restorePostedFields: m =>
                (m.CargueLaboratorios, m.CargueGlucometria, m.CargueServiciosComplementarios, m.CargueSeguimientoMedico) = posted,
            applySectionToRecord: (record, m) =>
            {
                record.CargueLaboratorios = m.CargueLaboratorios;
                record.CargueGlucometria = m.CargueGlucometria;
                record.CargueServiciosComplementarios = m.CargueServiciosComplementarios;
                record.CargueSeguimientoMedico = m.CargueSeguimientoMedico;
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar el cargue de servicios.",
            auditAction: "CENSO_NPT_CARGUE_SERVICIOS_ACTUALIZADO",
            successMessage: "Cargue de servicios guardado correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarNptActivoFijo(CensoNptViewModel model, CancellationToken cancellationToken)
    {
        model.EquipoComodato = string.IsNullOrWhiteSpace(model.EquipoComodato) ? null : model.EquipoComodato.Trim();
        model.DescripcionEquipo = NormalizeOptionalNptText(model.DescripcionEquipo);
        model.NumeroPlacaEquipos = NormalizeOptionalNptText(model.NumeroPlacaEquipos);

        ValidateNptSiNoOptional(model.EquipoComodato, nameof(model.EquipoComodato), "equipo en comodato");

        if (model.FechaDevolucionEquipo.HasValue
            && model.FechaEntregaEquipo.HasValue
            && model.FechaDevolucionEquipo.Value.Date < model.FechaEntregaEquipo.Value.Date)
        {
            ModelState.AddModelError(nameof(model.FechaDevolucionEquipo), "La fecha de devolución no puede ser anterior a la fecha de entrega.");
        }

        var posted = (model.EquipoComodato, model.DescripcionEquipo, model.NumeroPlacaEquipos, model.FechaEntregaEquipo, model.FechaDevolucionEquipo);

        return GuardarNptSeccionAsync(
            model,
            restorePostedFields: m =>
                (m.EquipoComodato, m.DescripcionEquipo, m.NumeroPlacaEquipos, m.FechaEntregaEquipo, m.FechaDevolucionEquipo) = posted,
            applySectionToRecord: (record, m) =>
            {
                record.EquipoComodato = m.EquipoComodato;
                record.DescripcionEquipo = m.DescripcionEquipo;
                record.NumeroPlacaEquipos = m.NumeroPlacaEquipos;
                record.FechaEntregaEquipo = m.FechaEntregaEquipo?.Date;
                record.FechaDevolucionEquipo = m.FechaDevolucionEquipo?.Date;
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar el activo fijo.",
            auditAction: "CENSO_NPT_ACTIVO_FIJO_ACTUALIZADO",
            successMessage: "Activo fijo guardado correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarNptSeguimientoHospitalizado(CensoNptViewModel model, CancellationToken cancellationToken)
    {
        model.MotivoHospitalizacion = string.IsNullOrWhiteSpace(model.MotivoHospitalizacion) ? null : model.MotivoHospitalizacion.Trim();
        model.RemitidoPorHospitalizacion = string.IsNullOrWhiteSpace(model.RemitidoPorHospitalizacion) ? null : model.RemitidoPorHospitalizacion.Trim();
        model.IpsIntramural = NormalizeOptionalNptText(model.IpsIntramural);

        if (!string.IsNullOrWhiteSpace(model.MotivoHospitalizacion)
            && !NptMotivoHospitalizacionValues.Contains(model.MotivoHospitalizacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoHospitalizacion), "Selecciona un motivo de hospitalización válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.RemitidoPorHospitalizacion)
            && !NptRemitidoPorValues.Contains(model.RemitidoPorHospitalizacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.RemitidoPorHospitalizacion), "Selecciona un valor válido para remitido por.");
        }

        var posted = (model.FechaHospitalizacion, model.MotivoHospitalizacion, model.RemitidoPorHospitalizacion, model.IpsIntramural,
            model.FechaPrimerSeguimiento24Horas, model.FechaSegundoSeguimiento48Horas, model.FechaTercerSeguimiento72Horas,
            model.FechaCuartoSeguimientoSemana1, model.FechaQuintoSeguimientoSemana2, model.FechaSextoSeguimientoSemana3,
            model.FechaSeptimoSeguimientoSemana4, model.FechaAltaHospitalizacion);

        return GuardarNptSeccionAsync(
            model,
            restorePostedFields: m =>
            {
                (m.FechaHospitalizacion, m.MotivoHospitalizacion, m.RemitidoPorHospitalizacion, m.IpsIntramural,
                    m.FechaPrimerSeguimiento24Horas, m.FechaSegundoSeguimiento48Horas, m.FechaTercerSeguimiento72Horas,
                    m.FechaCuartoSeguimientoSemana1, m.FechaQuintoSeguimientoSemana2, m.FechaSextoSeguimientoSemana3,
                    m.FechaSeptimoSeguimientoSemana4, m.FechaAltaHospitalizacion) = posted;
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
                record.FechaAltaHospitalizacion = m.FechaAltaHospitalizacion?.Date;
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar el seguimiento hospitalizado.",
            auditAction: "CENSO_NPT_SEGUIMIENTO_HOSPITALIZADO_ACTUALIZADO",
            successMessage: "Seguimiento hospitalizado guardado correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarNptDevolucionProductos(CensoNptViewModel model, CancellationToken cancellationToken)
    {
        model.MotivoNovedadDevolucionProductos = string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos) ? null : model.MotivoNovedadDevolucionProductos.Trim();
        model.NotificacionAuxiliarDevolucionProductos = string.IsNullOrWhiteSpace(model.NotificacionAuxiliarDevolucionProductos) ? null : model.NotificacionAuxiliarDevolucionProductos.Trim();
        model.EstadoDevolucionServicioFarmaceutico = string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico) ? null : model.EstadoDevolucionServicioFarmaceutico.Trim();

        if (!string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos)
            && !MotivoNovedadDevolucionProductosValues.Contains(model.MotivoNovedadDevolucionProductos, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoNovedadDevolucionProductos), "Selecciona un motivo de la novedad válido.");
        }

        ValidateNptSiNoOptional(model.NotificacionAuxiliarDevolucionProductos, nameof(model.NotificacionAuxiliarDevolucionProductos), "notificación al auxiliar");

        if (!string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico)
            && !EstadoDevolucionServicioFarmaceuticoValues.Contains(model.EstadoDevolucionServicioFarmaceutico, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoDevolucionServicioFarmaceutico), "Selecciona un estado de devolución válido.");
        }

        var posted = (model.FechaNovedadDevolucionProductos, model.MotivoNovedadDevolucionProductos,
            model.NotificacionAuxiliarDevolucionProductos, model.FechaMaximaDevolucionProductos,
            model.EstadoDevolucionServicioFarmaceutico);

        return GuardarNptSeccionAsync(
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
            auditAction: "CENSO_NPT_DEVOLUCION_PRODUCTOS_ACTUALIZADA",
            successMessage: "Devolución de productos guardada correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarNptAltaPrograma(CensoNptViewModel model, CancellationToken cancellationToken)
    {
        model.MotivoEgreso = string.IsNullOrWhiteSpace(model.MotivoEgreso) ? null : model.MotivoEgreso.Trim();
        model.Estado = string.IsNullOrWhiteSpace(model.Estado) ? null : model.Estado.Trim();

        if (!string.IsNullOrWhiteSpace(model.MotivoEgreso)
            && !NptMotivoEgresoValues.Contains(model.MotivoEgreso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoEgreso), "Selecciona un motivo del egreso válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.Estado)
            && !NptEstadoProgramaValues.Contains(model.Estado, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Estado), "Selecciona un estado válido.");
        }

        var posted = (model.MotivoEgreso, model.FechaEgreso, model.Estado);

        return GuardarNptSeccionAsync(
            model,
            restorePostedFields: m => (m.MotivoEgreso, m.FechaEgreso, m.Estado) = posted,
            applySectionToRecord: (record, m) =>
            {
                record.MotivoEgreso = m.MotivoEgreso;
                record.FechaEgreso = m.FechaEgreso?.Date;
                record.Estado = string.IsNullOrWhiteSpace(m.Estado) ? record.Estado : m.Estado;
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar el alta del programa.",
            auditAction: "CENSO_NPT_ALTA_PROGRAMA_ACTUALIZADA",
            successMessage: "Alta del programa guardada correctamente.",
            cancellationToken);
    }

    private async Task<IActionResult> GuardarNptSeccionAsync(
        CensoNptViewModel model,
        Action<CensoNptViewModel> restorePostedFields,
        Action<CensoNptRecord, CensoNptViewModel> applySectionToRecord,
        string missingRecordMessage,
        string auditAction,
        string successMessage,
        CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);

        CensoNptRecord? record = null;
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoNpt
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);
        }

        if (record is null)
        {
            ModelState.AddModelError(string.Empty, missingRecordMessage);
        }
        else
        {
            ApplyNptRecordToModel(model, record);
            model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                ? record.NumeroIdentificacion
                : model.CedulaFiltro;
        }

        await PopulateNptDropdownsAsync(model, cancellationToken);
        restorePostedFields(model);

        if (!ModelState.IsValid)
        {
            return View("Npt", model);
        }

        var nptRecord = record!;
        applySectionToRecord(nptRecord, model);
        nptRecord.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(auditAction, "CensoNpt",
            $"Paciente: {nptRecord.NombrePaciente}, Doc: {nptRecord.NumeroIdentificacion}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = successMessage;
        return RedirectToAction(nameof(Npt), new { recordId = nptRecord.Id, cedulaPaciente = nptRecord.NumeroIdentificacion });
    }

    private void ValidateNptSiNoOptional(string? value, string fieldName, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !NptSiNoValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(fieldName, $"Selecciona una opción válida para {displayName}.");
        }
    }

    private static int? CalculateNptDiasTratamiento(DateTime? fechaInicio, DateTime? fechaFin)
    {
        if (!fechaInicio.HasValue || !fechaFin.HasValue || fechaFin.Value.Date < fechaInicio.Value.Date)
        {
            return null;
        }

        return (int)(fechaFin.Value.Date - fechaInicio.Value.Date).TotalDays + 1;
    }

    private CensoNptViewModel BuildDefaultNptModel()
    {
        var today = GetColombiaNow().Date;
        return new CensoNptViewModel
        {
            FechaIngresoPrograma = today,
            FechaNacimiento = today,
            FechaValoracion = today,
            DireccionEsValida = false
        };
    }

    private async Task PopulateNptDropdownsAsync(CensoNptViewModel model, CancellationToken cancellationToken)
    {
        model.AseguradorOptions = BuildOptions(NptAseguradorValues);
        model.TipoIdentificacionOptions = BuildOptions(TiposIdentificacion);
        model.GeneroOptions = BuildOptions(NptGeneroValues);
        model.ClasificacionZonaSuraOptions = BuildOptions(ClasificacionZonaSuraValues);
        model.MunicipioResidenciaOptions = BuildOptions(MunicipiosResidenciaValues);
        model.ZonaDireccionOptions = BuildOptions(ZonaDireccionValues);
        model.LlamadaBienvenidaOptions = BuildOptions(NptLlamadaBienvenidaValues);
        model.ProgramaPerteneceOptions = BuildOptions(NptProgramaValues);
        model.AuxiliarEnfermeriaOptions = await GetOpsAssistantOptionsAsync(cancellationToken);
        model.TipoNutricionOptions = BuildOptions(NptTipoNutricionValues);
        model.TipoSondaOptions = BuildOptions(NptTipoSondaValues);
        model.SiNoOptions = BuildOptions(NptSiNoValues);
        model.MotivoHospitalizacionOptions = BuildOptions(NptMotivoHospitalizacionValues);
        model.RemitidoPorHospitalizacionOptions = BuildOptions(NptRemitidoPorValues);
        model.MotivoNovedadDevolucionOptions = BuildOptions(MotivoNovedadDevolucionProductosValues);
        model.EstadoDevolucionOptions = BuildOptions(EstadoDevolucionServicioFarmaceuticoValues);
        model.MotivoEgresoOptions = BuildOptions(NptMotivoEgresoValues);
        model.EstadoProgramaOptions = BuildOptions(NptEstadoProgramaValues);

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
        await PopulateNptLatestRecordsAsync(model, cancellationToken);
    }

    private async Task PopulateNptLatestRecordsAsync(CensoNptViewModel model, CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);

        var query = _context.CensoNpt.AsNoTracking();
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

    private void NormalizeNptModel(CensoNptViewModel model)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);
        model.Asegurador = model.Asegurador?.Trim() ?? string.Empty;
        model.TipoIdentificacion = NormalizeNptText(model.TipoIdentificacion);
        model.NumeroIdentificacion = NormalizeIdentificationNumber(model.TipoIdentificacion, model.NumeroIdentificacion);
        model.NombrePaciente = NormalizeNptText(model.NombrePaciente);
        model.Genero = model.Genero?.Trim() ?? string.Empty;
        model.Direccion = NormalizeNptText(model.Direccion);
        model.Barrio = NormalizeNptText(model.Barrio);
        model.MunicipioResidencia = model.MunicipioResidencia?.Trim() ?? string.Empty;
        model.ZonaDireccionSegunMunicipio = model.ZonaDireccionSegunMunicipio?.Trim() ?? string.Empty;
        model.ClasificacionZonaSura = model.ClasificacionZonaSura?.Trim() ?? string.Empty;
        model.TelefonoPrincipal = NormalizePhone(model.TelefonoPrincipal);
        model.TelefonoAdicional1 = NormalizePhone(model.TelefonoAdicional1);
        model.TelefonoAdicional2 = string.IsNullOrWhiteSpace(model.TelefonoAdicional2) ? null : NormalizePhone(model.TelefonoAdicional2);
        model.LlamadaBienvenida = model.LlamadaBienvenida?.Trim();
        model.TelefonoContacto = string.IsNullOrWhiteSpace(model.TelefonoContacto) ? null : NormalizePhone(model.TelefonoContacto);
        model.Observacion = NormalizeOptionalNptText(model.Observacion);
        model.CodigoCie10 = NormalizeCie10(model.CodigoCie10);
        model.DiagnosticoDescriptivo = NormalizeOptionalNptText(model.DiagnosticoDescriptivo);
        model.ProgramaPertenece = model.ProgramaPertenece?.Trim() ?? string.Empty;
        model.AuxiliarEnfermeriaAsignado = NormalizeOptionalNptText(model.AuxiliarEnfermeriaAsignado);
        model.TipoNutricion = model.TipoNutricion?.Trim() ?? string.Empty;
        model.TipoSonda = model.TipoSonda?.Trim() ?? string.Empty;
        model.Picc = model.Picc?.Trim() ?? string.Empty;
        model.CargueLaboratorios = string.IsNullOrWhiteSpace(model.CargueLaboratorios) ? null : model.CargueLaboratorios.Trim();
        model.CargueGlucometria = string.IsNullOrWhiteSpace(model.CargueGlucometria) ? null : model.CargueGlucometria.Trim();
        model.CargueServiciosComplementarios = string.IsNullOrWhiteSpace(model.CargueServiciosComplementarios) ? null : model.CargueServiciosComplementarios.Trim();
        model.CargueSeguimientoMedico = string.IsNullOrWhiteSpace(model.CargueSeguimientoMedico) ? null : model.CargueSeguimientoMedico.Trim();
        model.EquipoComodato = string.IsNullOrWhiteSpace(model.EquipoComodato) ? null : model.EquipoComodato.Trim();
        model.DescripcionEquipo = NormalizeOptionalNptText(model.DescripcionEquipo);
        model.NumeroPlacaEquipos = NormalizeOptionalNptText(model.NumeroPlacaEquipos);
        model.MotivoHospitalizacion = string.IsNullOrWhiteSpace(model.MotivoHospitalizacion) ? null : model.MotivoHospitalizacion.Trim();
        model.RemitidoPorHospitalizacion = string.IsNullOrWhiteSpace(model.RemitidoPorHospitalizacion) ? null : model.RemitidoPorHospitalizacion.Trim();
        model.IpsIntramural = NormalizeOptionalNptText(model.IpsIntramural);
        model.MotivoNovedadDevolucionProductos = string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos) ? null : model.MotivoNovedadDevolucionProductos.Trim();
        model.NotificacionAuxiliarDevolucionProductos = string.IsNullOrWhiteSpace(model.NotificacionAuxiliarDevolucionProductos) ? null : model.NotificacionAuxiliarDevolucionProductos.Trim();
        model.EstadoDevolucionServicioFarmaceutico = string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico) ? null : model.EstadoDevolucionServicioFarmaceutico.Trim();
        model.MotivoEgreso = string.IsNullOrWhiteSpace(model.MotivoEgreso) ? null : model.MotivoEgreso.Trim();
        model.Estado = string.IsNullOrWhiteSpace(model.Estado) ? null : model.Estado.Trim();

        model.DiasTratamiento = CalculateNptDiasTratamiento(model.FechaInicioNpt, model.FechaFinNpt);
        ModelState.Remove(nameof(model.DiasTratamiento));

        model.Edad = CalculateAge(model.FechaNacimiento.Date, GetColombiaNow().Date);
        ModelState.Remove(nameof(model.Edad));
    }

    private static string NormalizeNptText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpper(NptTextCulture);
    }

    private static string? NormalizeOptionalNptText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpper(NptTextCulture);
    }

    private void ValidateNptModel(CensoNptViewModel model)
    {
        if (!NptAseguradorValues.Contains(model.Asegurador, StringComparer.OrdinalIgnoreCase))
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
            && !NptNombrePattern.IsMatch(model.NombrePaciente))
        {
            ModelState.AddModelError(nameof(model.NombrePaciente), "El nombre del paciente solo permite letras y espacios.");
        }

        if (model.FechaNacimiento.Date >= GetColombiaNow().Date)
        {
            ModelState.AddModelError(nameof(model.FechaNacimiento), "La fecha de nacimiento debe ser anterior a la fecha actual.");
        }

        if (model.FechaIngresoPrograma.Date > GetColombiaNow().Date)
        {
            ModelState.AddModelError(nameof(model.FechaIngresoPrograma), "La fecha de ingreso a programa no puede ser futura.");
        }

        if (!NptGeneroValues.Contains(model.Genero, StringComparer.OrdinalIgnoreCase))
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
            model.DiagnosticoDescriptivo = NormalizeNptText(diagnostico);
        }

        if (!NptProgramaValues.Contains(model.ProgramaPertenece, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ProgramaPertenece), "Selecciona un programa válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.LlamadaBienvenida)
            && !NptLlamadaBienvenidaValues.Contains(model.LlamadaBienvenida, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.LlamadaBienvenida), "Selecciona un estado de llamada de bienvenida válido.");
        }

        if (!NptTipoNutricionValues.Contains(model.TipoNutricion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.TipoNutricion), "Selecciona un tipo de nutrición válido.");
        }

        if (!NptTipoSondaValues.Contains(model.TipoSonda, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.TipoSonda), "Selecciona un tipo de sonda válido.");
        }

        if (!NptSiNoValues.Contains(model.Picc, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Picc), "Selecciona una opción válida para PICC.");
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

        if (model.FechaFinNpt.HasValue
            && model.FechaInicioNpt.HasValue
            && model.FechaFinNpt.Value.Date < model.FechaInicioNpt.Value.Date)
        {
            ModelState.AddModelError(nameof(model.FechaFinNpt), "La fecha fin no puede ser anterior a la fecha de inicio.");
        }

        ValidateNptSiNoOptional(model.CargueLaboratorios, nameof(model.CargueLaboratorios), "cargue laboratorios");
        ValidateNptSiNoOptional(model.CargueGlucometria, nameof(model.CargueGlucometria), "cargue de glucometría");
        ValidateNptSiNoOptional(model.CargueServiciosComplementarios, nameof(model.CargueServiciosComplementarios), "cargue de servicios complementarios");
        ValidateNptSiNoOptional(model.CargueSeguimientoMedico, nameof(model.CargueSeguimientoMedico), "cargue de seguimiento médico");
        ValidateNptSiNoOptional(model.EquipoComodato, nameof(model.EquipoComodato), "equipo en comodato");

        if (model.FechaDevolucionEquipo.HasValue
            && model.FechaEntregaEquipo.HasValue
            && model.FechaDevolucionEquipo.Value.Date < model.FechaEntregaEquipo.Value.Date)
        {
            ModelState.AddModelError(nameof(model.FechaDevolucionEquipo), "La fecha de devolución no puede ser anterior a la fecha de entrega.");
        }

        if (!string.IsNullOrWhiteSpace(model.MotivoHospitalizacion)
            && !NptMotivoHospitalizacionValues.Contains(model.MotivoHospitalizacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoHospitalizacion), "Selecciona un motivo de hospitalización válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.RemitidoPorHospitalizacion)
            && !NptRemitidoPorValues.Contains(model.RemitidoPorHospitalizacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.RemitidoPorHospitalizacion), "Selecciona un valor válido para remitido por.");
        }

        if (!string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos)
            && !MotivoNovedadDevolucionProductosValues.Contains(model.MotivoNovedadDevolucionProductos, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoNovedadDevolucionProductos), "Selecciona un motivo de la novedad válido.");
        }

        ValidateNptSiNoOptional(model.NotificacionAuxiliarDevolucionProductos, nameof(model.NotificacionAuxiliarDevolucionProductos), "notificación al auxiliar");

        if (!string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico)
            && !EstadoDevolucionServicioFarmaceuticoValues.Contains(model.EstadoDevolucionServicioFarmaceutico, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoDevolucionServicioFarmaceutico), "Selecciona un estado de devolución válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.MotivoEgreso)
            && !NptMotivoEgresoValues.Contains(model.MotivoEgreso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoEgreso), "Selecciona un motivo del egreso válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.Estado)
            && !NptEstadoProgramaValues.Contains(model.Estado, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Estado), "Selecciona un estado válido.");
        }

        ValidatePhoneValue(model.TelefonoPrincipal, nameof(model.TelefonoPrincipal), "teléfono principal");
        ValidatePhoneValue(model.TelefonoAdicional1, nameof(model.TelefonoAdicional1), "teléfono adicional 1");
        ValidatePhoneValue(model.TelefonoAdicional2, nameof(model.TelefonoAdicional2), "teléfono adicional 2");
        ValidatePhoneValue(model.TelefonoContacto, nameof(model.TelefonoContacto), "teléfono de contacto");

        ValidateNptAddressDropdowns(model);
    }

    private void ValidateNptAddressDropdowns(CensoNptViewModel model)
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

    private void ClearNptAddressModelState()
    {
        foreach (var key in new[]
        {
            nameof(CensoNptViewModel.Direccion),
            nameof(CensoNptViewModel.ClasificacionZonaSura),
            nameof(CensoNptViewModel.MunicipioResidencia),
            nameof(CensoNptViewModel.Barrio),
            nameof(CensoNptViewModel.ZonaDireccionSegunMunicipio)
        })
        {
            ModelState.Remove(key);
        }
    }

    private void ApplyNptAddressValidationResult(
        CensoNptViewModel model,
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

            ApplyNptAddressLocationDefaults(model, direccionValidation);
            return;
        }

        model.DireccionEsValida = false;
        model.DireccionSugerida = direccionValidation.SuggestedAddress;
        model.DireccionMensajeValidacion = direccionValidation.Message;
        ApplyNptAddressLocationDefaults(model, direccionValidation);

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

    private void ApplyNptAddressLocationDefaults(CensoNptViewModel model, AddressValidationResult validation)
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

    private static void ApplyNptModelToRecord(
        CensoNptViewModel model,
        CensoNptRecord record,
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
        record.Direccion = NormalizeOptionalNptText(direccionParaGuardar);
        record.DireccionValidada = model.DireccionEsValida;
        record.AsumirDireccionErrada = model.AsumirDireccionErrada;
        record.Barrio = string.IsNullOrWhiteSpace(model.Barrio) ? null : model.Barrio;
        record.MunicipioResidencia = string.IsNullOrWhiteSpace(model.MunicipioResidencia) ? null : model.MunicipioResidencia;
        record.ZonaDireccionSegunMunicipio = string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio) ? null : model.ZonaDireccionSegunMunicipio;
        record.ClasificacionZonaSura = string.IsNullOrWhiteSpace(model.ClasificacionZonaSura) ? null : model.ClasificacionZonaSura;
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
        record.TipoNutricion = model.TipoNutricion;
        record.TipoSonda = model.TipoSonda;
        record.Picc = model.Picc;
        record.FechaUltimaCuracionPicc = model.FechaUltimaCuracionPicc?.Date;
        record.FechaInicioNpt = model.FechaInicioNpt?.Date;
        record.FechaFinNpt = model.FechaFinNpt?.Date;
        record.DiasTratamiento = model.DiasTratamiento;
        record.HoraConexion = model.HoraConexion;
        record.HoraDesconexion = model.HoraDesconexion;
        record.CargueLaboratorios = model.CargueLaboratorios;
        record.CargueGlucometria = model.CargueGlucometria;
        record.CargueServiciosComplementarios = model.CargueServiciosComplementarios;
        record.CargueSeguimientoMedico = model.CargueSeguimientoMedico;
        record.EquipoComodato = model.EquipoComodato;
        record.DescripcionEquipo = model.DescripcionEquipo;
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
        record.FechaAltaHospitalizacion = model.FechaAltaHospitalizacion?.Date;
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

    private static void ApplyNptRecordToModel(
        CensoNptViewModel model,
        CensoNptRecord record)
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
        model.Barrio = record.Barrio;
        model.MunicipioResidencia = record.MunicipioResidencia;
        model.ZonaDireccionSegunMunicipio = record.ZonaDireccionSegunMunicipio;
        model.ClasificacionZonaSura = record.ClasificacionZonaSura;
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
        model.TipoNutricion = record.TipoNutricion;
        model.TipoSonda = record.TipoSonda;
        model.Picc = record.Picc;
        model.FechaUltimaCuracionPicc = record.FechaUltimaCuracionPicc?.Date;
        model.FechaInicioNpt = record.FechaInicioNpt?.Date;
        model.FechaFinNpt = record.FechaFinNpt?.Date;
        model.DiasTratamiento = record.DiasTratamiento;
        model.HoraConexion = record.HoraConexion;
        model.HoraDesconexion = record.HoraDesconexion;
        model.CargueLaboratorios = record.CargueLaboratorios;
        model.CargueGlucometria = record.CargueGlucometria;
        model.CargueServiciosComplementarios = record.CargueServiciosComplementarios;
        model.CargueSeguimientoMedico = record.CargueSeguimientoMedico;
        model.EquipoComodato = record.EquipoComodato;
        model.DescripcionEquipo = record.DescripcionEquipo;
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
        model.FechaAltaHospitalizacion = record.FechaAltaHospitalizacion?.Date;
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
