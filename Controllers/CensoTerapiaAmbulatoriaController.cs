using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using IntranetPrueba.Data.Entities;
using IntranetPrueba.Models.ViewModels;
using IntranetPrueba.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Controllers;

public partial class CensoController
{
    private static readonly CultureInfo TerapiaAmbulatoriaTextCulture = CultureInfo.GetCultureInfo("es-CO");
    private static readonly string[] TerapiaAmbulatoriaTipoTerapiaValues =
    [
        "Terapia fisica",
        "Terapia respiratoria",
        "Fonoaudiologia",
        "Terapia ocupacional"
    ];
    private static readonly string[] TerapiaAmbulatoriaFrecuenciaTerapiaValues =
    [
        "Diaria",
        "Tres veces por semana",
        "Dos veces por semana",
        "Una vez por semana"
    ];
    private const string TerapiaAmbulatoriaEstadoPendiente = "Pendiente confirmar datos";
    private const string TerapiaAmbulatoriaEstadoDatosConfirmados = "Datos confirmados";
    private const string TerapiaAmbulatoriaEstadoGestionCompleta = "Gestión completa";
    private static readonly string[] TerapiaAmbulatoriaEstadoPacienteValues =
    [
        "Activo",
        "Alta"
    ];
    private static readonly string[] TerapiaAmbulatoriaMotivoAltaValues =
    [
        "Fin de tratamiento",
        "Cambio de programa",
        "Agudización"
    ];
    private static readonly string[] TerapiaAmbulatoriaEstadoAltaValues =
    [
        "Activo",
        "Pre-Alta",
        "Cerrado"
    ];
    private const string TerapiaAmbulatoriaEstadoAltaCerrado = "Cerrado";
    private const string TerapiaAmbulatoriaAltaNotificationRecipient = "liderdetecnologia@especialistasencasa.com";

    [HttpGet]
    public async Task<IActionResult> TerapiaAmbulatoria(
        string? cedulaPaciente,
        string? estadoGestion,
        long? recordId,
        CancellationToken cancellationToken)
    {
        var model = BuildDefaultTerapiaAmbulatoriaModel();
        model.CedulaFiltro = NormalizeCedulaFilter(cedulaPaciente);
        model.EstadoGestionFiltro = NormalizeTerapiaAmbulatoriaEstadoGestionFiltro(estadoGestion);

        if (recordId.HasValue)
        {
            var record = await _context.CensoTerapiasAmbulatorias
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == recordId.Value, cancellationToken);
            if (record is not null)
            {
                ApplyTerapiaAmbulatoriaRecordToModel(model, record);
                model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                    ? record.NumeroIdentificacion
                    : model.CedulaFiltro;
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.CedulaFiltro))
        {
            var record = await _context.CensoTerapiasAmbulatorias
                .AsNoTracking()
                .Where(x => x.NumeroIdentificacion == model.CedulaFiltro)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (record is not null)
            {
                ApplyTerapiaAmbulatoriaRecordToModel(model, record);
                model.CedulaFiltro = record.NumeroIdentificacion;
            }
        }

        await PopulateTerapiaAmbulatoriaDropdownsAsync(model, cancellationToken);
        return View("TerapiaAmbulatoria", model);
    }

    [HttpPost]
    public async Task<IActionResult> TerapiaAmbulatoria(CensoTerapiaAmbulatoriaViewModel model, CancellationToken cancellationToken)
    {
        NormalizeTerapiaAmbulatoriaModel(model);
        await PopulateTerapiaAmbulatoriaDropdownsAsync(model, cancellationToken);
        ValidateTerapiaAmbulatoriaModel(model);

        var direccionParaGuardar = model.Direccion ?? string.Empty;
        if (ShouldValidateTerapiaAddress(model))
        {
            var direccionValidation = await _addressValidationService.ValidateAddressAsync(direccionParaGuardar, cancellationToken);
            ApplyTerapiaAddressValidationResult(model, direccionValidation, ref direccionParaGuardar);
        }
        else
        {
            ClearTerapiaAddressModelState();
            model.DireccionEsValida = false;
            model.AsumirDireccionErrada = false;
            model.DireccionSugerida = null;
            model.DireccionMensajeValidacion = null;
            direccionParaGuardar = model.Direccion ?? string.Empty;
        }

        model.EstadoGestion = CalculateTerapiaAmbulatoriaEstadoGestion(model);
        ModelState.Remove(nameof(model.EstadoGestion));

        if (!ModelState.IsValid)
        {
            await PopulateTerapiaAmbulatoriaLatestRecordsAsync(model, cancellationToken);
            return View("TerapiaAmbulatoria", model);
        }

        CensoTerapiaAmbulatoriaRecord record;
        var auditAction = "CENSO_TERAPIA_AMBULATORIA_CREADO";
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoTerapiasAmbulatorias
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken)
                ?? MapToTerapiaAmbulatoriaRecord(model, direccionParaGuardar);
            ApplyTerapiaAmbulatoriaModelToRecord(model, record, direccionParaGuardar, preserveCreatedAt: record.Id != 0);
            auditAction = record.Id == 0
                ? "CENSO_TERAPIA_AMBULATORIA_CREADO"
                : "CENSO_TERAPIA_AMBULATORIA_ACTUALIZADO";
            if (record.Id == 0)
            {
                await _context.CensoTerapiasAmbulatorias.AddAsync(record, cancellationToken);
            }
        }
        else
        {
            record = MapToTerapiaAmbulatoriaRecord(model, direccionParaGuardar);
            await _context.CensoTerapiasAmbulatorias.AddAsync(record, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(auditAction, "CensoTerapiaAmbulatoria",
            $"Paciente: {record.NombrePaciente}, Doc: {record.NumeroIdentificacion}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = model.EditingRecordId.HasValue
            ? "Registro de terapia ambulatoria actualizado correctamente."
            : "Registro de terapia ambulatoria guardado correctamente.";
        return RedirectToAction(nameof(TerapiaAmbulatoria), new { cedulaPaciente = record.NumeroIdentificacion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarTerapiaAmbulatoriaProrroga(CensoTerapiaAmbulatoriaViewModel model, CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);
        model.ProrrogaCodigoAutorizacion = NormalizeOptionalTerapiaAmbulatoriaText(model.ProrrogaCodigoAutorizacion);
        model.ProrrogaCantidad = NormalizeOptionalTerapiaAmbulatoriaText(model.ProrrogaCantidad);
        model.ProrrogaTiposTerapiaSeleccionados = (model.ProrrogaTiposTerapiaSeleccionados ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        ValidateTerapiaAmbulatoriaProrrogaModel(model);
        var postedTiposTerapiaSeleccionados = model.ProrrogaTiposTerapiaSeleccionados;
        var postedFechaSolicitud = model.ProrrogaFechaSolicitud;
        var postedFechaSolicitudAsegurador = model.ProrrogaFechaSolicitudAsegurador;
        var postedFechaEntregaAutorizacion = model.ProrrogaFechaEntregaAutorizacion;
        var postedCodigoAutorizacion = model.ProrrogaCodigoAutorizacion;
        var postedFrecuencia = model.ProrrogaFrecuencia;
        var postedCantidad = model.ProrrogaCantidad;

        CensoTerapiaAmbulatoriaRecord? record = null;
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoTerapiasAmbulatorias
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);
        }

        if (record is null)
        {
            ModelState.AddModelError(string.Empty, "Primero guarda o abre un paciente para registrar la prórroga.");
        }
        else
        {
            ApplyTerapiaAmbulatoriaRecordToModel(model, record);
            model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                ? record.NumeroIdentificacion
                : model.CedulaFiltro;
        }

        await PopulateTerapiaAmbulatoriaDropdownsAsync(model, cancellationToken);
        model.ProrrogaTiposTerapiaSeleccionados = postedTiposTerapiaSeleccionados;
        model.ProrrogaFechaSolicitud = postedFechaSolicitud;
        model.ProrrogaFechaSolicitudAsegurador = postedFechaSolicitudAsegurador;
        model.ProrrogaFechaEntregaAutorizacion = postedFechaEntregaAutorizacion;
        model.ProrrogaCodigoAutorizacion = postedCodigoAutorizacion;
        model.ProrrogaFrecuencia = postedFrecuencia;
        model.ProrrogaCantidad = postedCantidad;

        if (!ModelState.IsValid)
        {
            return View("TerapiaAmbulatoria", model);
        }

        var terapiaRecord = record!;
        var prorroga = await _context.CensoTerapiaAmbulatoriaProrrogas
            .FirstOrDefaultAsync(x => x.CensoTerapiaAmbulatoriaRecordId == terapiaRecord.Id, cancellationToken);
        var isNewProrroga = prorroga is null;
        prorroga ??= new CensoTerapiaAmbulatoriaProrroga
        {
            CensoTerapiaAmbulatoriaRecordId = terapiaRecord.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        prorroga.TipoTerapia = string.Join(", ", model.ProrrogaTiposTerapiaSeleccionados);
        prorroga.FechaSolicitudProrroga = model.ProrrogaFechaSolicitud!.Value.Date;
        prorroga.FechaSolicitudAsegurador = model.ProrrogaFechaSolicitudAsegurador!.Value.Date;
        prorroga.FechaEntregaAutorizacion = model.ProrrogaFechaEntregaAutorizacion!.Value.Date;
        prorroga.CodigoAutorizacion = model.ProrrogaCodigoAutorizacion!.Trim();
        prorroga.Frecuencia = model.ProrrogaFrecuencia!.Value;
        prorroga.Cantidad = model.ProrrogaCantidad!.Trim();

        if (isNewProrroga)
        {
            await _context.CensoTerapiaAmbulatoriaProrrogas.AddAsync(prorroga, cancellationToken);
        }
        else
        {
            prorroga.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var auditAction = isNewProrroga
            ? "CENSO_TERAPIA_AMBULATORIA_PRORROGA_CREADA"
            : "CENSO_TERAPIA_AMBULATORIA_PRORROGA_ACTUALIZADA";
        await _auditService.LogAsync(auditAction, "CensoTerapiaAmbulatoriaProrroga",
            $"Paciente: {terapiaRecord.NombrePaciente}, Doc: {terapiaRecord.NumeroIdentificacion}, Prorroga: {prorroga.Id}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = isNewProrroga
            ? "Prórroga de terapia ambulatoria guardada correctamente."
            : "Prórroga de terapia ambulatoria actualizada correctamente.";
        return RedirectToAction(nameof(TerapiaAmbulatoria), new { recordId = terapiaRecord.Id, cedulaPaciente = terapiaRecord.NumeroIdentificacion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarTerapiaAmbulatoriaGestionAlta(CensoTerapiaAmbulatoriaViewModel model, CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);
        model.MotivoAlta = model.MotivoAlta?.Trim() ?? string.Empty;
        model.EstadoAlta = model.EstadoAlta?.Trim() ?? string.Empty;
        ValidateTerapiaAmbulatoriaGestionAltaModel(model);

        var postedFechaAlta = model.FechaAlta;
        var postedMotivoAlta = model.MotivoAlta;
        var postedEstadoAlta = model.EstadoAlta;

        CensoTerapiaAmbulatoriaRecord? record = null;
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoTerapiasAmbulatorias
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);
        }

        if (record is null)
        {
            ModelState.AddModelError(string.Empty, "Primero guarda o abre un paciente para gestionar el alta.");
        }
        else
        {
            ApplyTerapiaAmbulatoriaRecordToModel(model, record);
            model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                ? record.NumeroIdentificacion
                : model.CedulaFiltro;
        }

        await PopulateTerapiaAmbulatoriaDropdownsAsync(model, cancellationToken);
        model.FechaAlta = postedFechaAlta;
        model.MotivoAlta = postedMotivoAlta;
        model.EstadoAlta = postedEstadoAlta;

        if (!ModelState.IsValid)
        {
            return View("TerapiaAmbulatoria", model);
        }

        var terapiaRecord = record!;
        var shouldNotifyAlta = IsTerapiaAltaCerrada(model.EstadoAlta)
            && !terapiaRecord.AltaNotificacionEnviadaAtUtc.HasValue;

        terapiaRecord.FechaAlta = model.FechaAlta!.Value.Date;
        terapiaRecord.MotivoAlta = model.MotivoAlta;
        terapiaRecord.EstadoAlta = model.EstadoAlta;
        terapiaRecord.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync("CENSO_TERAPIA_AMBULATORIA_GESTION_ALTA_ACTUALIZADA", "CensoTerapiaAmbulatoria",
            $"Paciente: {terapiaRecord.NombrePaciente}, Doc: {terapiaRecord.NumeroIdentificacion}, Estado alta: {terapiaRecord.EstadoAlta}",
            auditUserId, auditIp, cancellationToken);

        var notificationWarning = string.Empty;
        if (shouldNotifyAlta)
        {
            var email = BuildTerapiaAmbulatoriaAltaEmail(terapiaRecord);
            var emailResult = await _emailService.SendAsync(email, cancellationToken);
            if (emailResult.Succeeded)
            {
                terapiaRecord.AltaNotificacionEnviadaAtUtc = DateTime.UtcNow;
                terapiaRecord.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                notificationWarning = emailResult.ErrorMessage ?? "No fue posible enviar la notificación de alta.";
            }
        }

        TempData["SuccessMessage"] = "Gestión alta de terapia ambulatoria actualizada correctamente.";
        if (!string.IsNullOrWhiteSpace(notificationWarning))
        {
            TempData["ErrorMessage"] = notificationWarning;
        }

        return RedirectToAction(nameof(TerapiaAmbulatoria), new { recordId = terapiaRecord.Id, cedulaPaciente = terapiaRecord.NumeroIdentificacion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirTerapiaAmbulatoriaAdjuntos(
        CensoTerapiaAmbulatoriaViewModel model,
        List<IFormFile> terapiaAdjuntos,
        CancellationToken cancellationToken)
    {
        CensoTerapiaAmbulatoriaRecord? record = null;
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoTerapiasAmbulatorias
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);
        }

        if (record is null)
        {
            ModelState.AddModelError(string.Empty, "Primero guarda o abre un paciente para adjuntar documentos.");
            await PopulateTerapiaAmbulatoriaDropdownsAsync(model, cancellationToken);
            return View("TerapiaAmbulatoria", model);
        }

        var result = await _sharePointDocumentService.UploadTerapiaAmbulatoriaDocumentsAsync(
            record.NombrePaciente,
            record.NumeroIdentificacion,
            terapiaAdjuntos,
            cancellationToken);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Documentos adjuntos guardados en SharePoint correctamente.";
        }
        else
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "No fue posible guardar los adjuntos en SharePoint.";
        }

        return RedirectToAction(nameof(TerapiaAmbulatoria), new { recordId = record.Id, cedulaPaciente = record.NumeroIdentificacion });
    }


    [HttpGet]
    public async Task<IActionResult> ExportarTerapiasAmbulatoriasExcel(string? cedulaPaciente, string? estadoGestion, CancellationToken cancellationToken)
    {
        var cedulaFiltro = NormalizeCedulaFilter(cedulaPaciente);
        var estadoGestionFiltro = NormalizeTerapiaAmbulatoriaEstadoGestionFiltro(estadoGestion);
        var query = ApplyTerapiaAmbulatoriaHistoryFilters(
            _context.CensoTerapiasAmbulatorias
                .AsNoTracking()
                .Include(x => x.Prorrogas),
            cedulaFiltro,
            estadoGestionFiltro);

        var records = await query
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var content = BuildTerapiaAmbulatoriaExcelXml(records);
        var bytes = Encoding.UTF8.GetBytes(content);
        var fileName = string.IsNullOrWhiteSpace(cedulaFiltro)
            ? $"censo_terapias_ambulatorias_{DateTime.Now:yyyyMMdd_HHmmss}.xls"
            : $"censo_terapias_ambulatorias_{cedulaFiltro}_{DateTime.Now:yyyyMMdd_HHmmss}.xls";
        return File(bytes, "application/vnd.ms-excel", fileName);
    }


    private CensoTerapiaAmbulatoriaViewModel BuildDefaultTerapiaAmbulatoriaModel()
    {
        var today = GetColombiaNow().Date;
        return new CensoTerapiaAmbulatoriaViewModel
        {
            FechaNacimiento = today,
            FechaInicio = today,
            DireccionEsValida = false,
            MunicipioResidencia = MunicipioNoParametrizado,
            ClasificacionZonaSura = InferClasificacionZonaSura(MunicipioNoParametrizado),
            ZonaDireccionSegunMunicipio = InferZonaDireccionSegunMunicipio(MunicipioNoParametrizado),
            Area = AreaValues[0],
            EstadoGestion = TerapiaAmbulatoriaEstadoPendiente,
            EstadoPaciente = TerapiaAmbulatoriaEstadoPacienteValues[0],
            EstadoAlta = TerapiaAmbulatoriaEstadoAltaValues[0],
            FechaIngreso = today
        };
    }

    private async Task PopulateTerapiaAmbulatoriaDropdownsAsync(CensoTerapiaAmbulatoriaViewModel model, CancellationToken cancellationToken)
    {
        model.TipoIdentificacionOptions = BuildOptions(TiposIdentificacion);
        model.ClasificacionZonaSuraOptions = BuildOptions(ClasificacionZonaSuraValues);
        model.MunicipioResidenciaOptions = BuildOptions(MunicipiosResidenciaValues);
        model.ZonaDireccionOptions = BuildOptions(ZonaDireccionValues);
        model.AreaOptions = BuildOptions(AreaValues);
        model.FisioterapeutaOptions = await GetOpsAssistantOptionsAsync(cancellationToken);
        model.EstadoPacienteOptions = BuildOptions(TerapiaAmbulatoriaEstadoPacienteValues);
        model.FrecuenciaTerapiaOptions = BuildOptions(TerapiaAmbulatoriaFrecuenciaTerapiaValues);
        model.TipoTerapiaOptions = BuildOptions(TerapiaAmbulatoriaTipoTerapiaValues);
        model.MotivoAltaOptions = BuildOptions(TerapiaAmbulatoriaMotivoAltaValues);
        model.EstadoAltaOptions = BuildOptions(TerapiaAmbulatoriaEstadoAltaValues);

        model.MunicipioResidencia = ToCanonicalMunicipality(model.MunicipioResidencia) ?? MunicipioNoParametrizado;

        if (string.IsNullOrWhiteSpace(model.ClasificacionZonaSura))
        {
            model.ClasificacionZonaSura = InferClasificacionZonaSura(model.MunicipioResidencia);
        }

        if (string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio))
        {
            model.ZonaDireccionSegunMunicipio = InferZonaDireccionSegunMunicipio(model.MunicipioResidencia, model.Barrio, direccion: model.Direccion);
        }

        if (string.IsNullOrWhiteSpace(model.Area))
        {
            model.Area = AreaValues[0];
        }

        if (string.IsNullOrWhiteSpace(model.EstadoAlta))
        {
            model.EstadoAlta = TerapiaAmbulatoriaEstadoAltaValues[0];
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

        var barrioOptions = await _addressValidationService.SearchNeighborhoodsAsync(
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
        await PopulateTerapiaAmbulatoriaLatestRecordsAsync(model, cancellationToken);
        await PopulateTerapiaAmbulatoriaProrrogasAsync(model, cancellationToken);
        await PopulateTerapiaAmbulatoriaAdjuntosAsync(model, cancellationToken);
    }

    private async Task PopulateTerapiaAmbulatoriaLatestRecordsAsync(CensoTerapiaAmbulatoriaViewModel model, CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);
        model.EstadoGestionFiltro = NormalizeTerapiaAmbulatoriaEstadoGestionFiltro(model.EstadoGestionFiltro);

        var baseQuery = ApplyTerapiaAmbulatoriaHistoryFilters(
            _context.CensoTerapiasAmbulatorias
                .AsNoTracking(),
            model.CedulaFiltro,
            estadoGestionFiltro: null);

        model.PendientesConfirmarDatosCount = await baseQuery
            .CountAsync(x => x.EstadoGestion == TerapiaAmbulatoriaEstadoPendiente, cancellationToken);
        model.DatosConfirmadosCount = await baseQuery
            .CountAsync(x => x.EstadoGestion == TerapiaAmbulatoriaEstadoDatosConfirmados, cancellationToken);
        model.GestionCompletaCount = await baseQuery
            .CountAsync(x => x.EstadoGestion == TerapiaAmbulatoriaEstadoGestionCompleta, cancellationToken);

        var query = ApplyTerapiaAmbulatoriaHistoryFilters(
            _context.CensoTerapiasAmbulatorias
                .AsNoTracking()
                .Include(x => x.Prorrogas),
            model.CedulaFiltro,
            model.EstadoGestionFiltro);

        model.UltimosRegistros = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    private static string? NormalizeTerapiaAmbulatoriaEstadoGestionFiltro(string? estadoGestion)
    {
        if (string.IsNullOrWhiteSpace(estadoGestion))
        {
            return null;
        }

        var normalized = estadoGestion.Trim();
        return GetTerapiaAmbulatoriaEstadosGestion().FirstOrDefault(x =>
            string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> GetTerapiaAmbulatoriaEstadosGestion()
    {
        return
        [
            TerapiaAmbulatoriaEstadoPendiente,
            TerapiaAmbulatoriaEstadoDatosConfirmados,
            TerapiaAmbulatoriaEstadoGestionCompleta
        ];
    }

    private static IQueryable<CensoTerapiaAmbulatoriaRecord> ApplyTerapiaAmbulatoriaHistoryFilters(
        IQueryable<CensoTerapiaAmbulatoriaRecord> query,
        string? cedulaFiltro,
        string? estadoGestionFiltro)
    {
        var normalizedCedula = NormalizeCedulaFilter(cedulaFiltro);
        if (!string.IsNullOrWhiteSpace(normalizedCedula))
        {
            query = query.Where(x => x.NumeroIdentificacion == normalizedCedula);
        }

        var normalizedEstadoGestion = NormalizeTerapiaAmbulatoriaEstadoGestionFiltro(estadoGestionFiltro);
        if (!string.IsNullOrWhiteSpace(normalizedEstadoGestion))
        {
            query = query.Where(x => x.EstadoGestion == normalizedEstadoGestion);
        }

        return query;
    }

    private async Task PopulateTerapiaAmbulatoriaProrrogasAsync(CensoTerapiaAmbulatoriaViewModel model, CancellationToken cancellationToken)
    {
        if (!model.EditingRecordId.HasValue)
        {
            model.ProrrogasTerapia = [];
            return;
        }

        model.ProrrogasTerapia = await _context.CensoTerapiaAmbulatoriaProrrogas
            .AsNoTracking()
            .Where(x => x.CensoTerapiaAmbulatoriaRecordId == model.EditingRecordId.Value)
            .OrderByDescending(x => x.FechaSolicitudProrroga)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var prorroga = model.ProrrogasTerapia.FirstOrDefault();
        if (prorroga is not null)
        {
            model.ProrrogaTiposTerapiaSeleccionados = prorroga.TipoTerapia
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            model.ProrrogaFechaSolicitud = prorroga.FechaSolicitudProrroga.Date;
            model.ProrrogaFechaSolicitudAsegurador = prorroga.FechaSolicitudAsegurador.Date;
            model.ProrrogaFechaEntregaAutorizacion = prorroga.FechaEntregaAutorizacion.Date;
            model.ProrrogaCodigoAutorizacion = prorroga.CodigoAutorizacion;
            model.ProrrogaFrecuencia = prorroga.Frecuencia;
            model.ProrrogaCantidad = prorroga.Cantidad;
        }
    }
    private void ValidateTerapiaAmbulatoriaGestionAltaModel(CensoTerapiaAmbulatoriaViewModel model)
    {
        if (!model.EditingRecordId.HasValue)
        {
            ModelState.AddModelError(string.Empty, "Primero guarda o abre un paciente para gestionar el alta.");
        }

        if (!model.FechaAlta.HasValue)
        {
            ModelState.AddModelError(nameof(model.FechaAlta), "Selecciona la fecha de alta.");
        }

        if (!TerapiaAmbulatoriaMotivoAltaValues.Contains(model.MotivoAlta, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoAlta), "Selecciona un motivo de alta válido.");
        }

        if (!TerapiaAmbulatoriaEstadoAltaValues.Contains(model.EstadoAlta, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoAlta), "Selecciona un estado del alta válido.");
        }
    }

    private async Task PopulateTerapiaAmbulatoriaAdjuntosAsync(CensoTerapiaAmbulatoriaViewModel model, CancellationToken cancellationToken)
    {
        if (!model.EditingRecordId.HasValue)
        {
            model.AdjuntosTerapia = [];
            model.AdjuntosTerapiaError = null;
            return;
        }

        var record = await _context.CensoTerapiasAmbulatorias
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);

        if (record is null)
        {
            model.AdjuntosTerapia = [];
            model.AdjuntosTerapiaError = null;
            return;
        }

        var result = await _sharePointDocumentService.ListTerapiaAmbulatoriaDocumentsAsync(
            record.NombrePaciente,
            record.NumeroIdentificacion,
            cancellationToken);

        if (!result.Succeeded)
        {
            model.AdjuntosTerapia = [];
            model.AdjuntosTerapiaError = result.ErrorMessage;
            return;
        }

        model.AdjuntosTerapia = result.Value?
            .Select(item => new CensoTerapiaAdjuntoViewModel
            {
                Name = item.Name,
                WebUrl = item.WebUrl,
                Size = item.Size,
                LastModifiedAt = item.LastModifiedAt
            })
            .ToList() ?? [];
        model.AdjuntosTerapiaError = null;
    }

    private void NormalizeTerapiaAmbulatoriaModel(CensoTerapiaAmbulatoriaViewModel model)
    {
        model.NombrePaciente = NormalizeTerapiaAmbulatoriaText(model.NombrePaciente);
        model.TipoIdentificacion = NormalizeTerapiaAmbulatoriaText(model.TipoIdentificacion);
        model.NumeroIdentificacion = NormalizeIdentificationNumber(model.TipoIdentificacion, model.NumeroIdentificacion);
        model.CorreoElectronico = NormalizeTerapiaAmbulatoriaText(model.CorreoElectronico);
        model.FrecuenciaTerapia = model.FrecuenciaTerapia?.Trim() ?? string.Empty;
        model.CodigoCie10 = NormalizeCie10(model.CodigoCie10);
        model.DiagnosticoDescriptivo = NormalizeOptionalTerapiaAmbulatoriaText(model.DiagnosticoDescriptivo);
        model.NumeroAutorizacion = NormalizeTerapiaAmbulatoriaText(model.NumeroAutorizacion);
        model.Direccion = NormalizeTerapiaAmbulatoriaText(model.Direccion);
        model.DetalleDireccion = NormalizeOptionalTerapiaAmbulatoriaText(model.DetalleDireccion);
        model.ClasificacionZonaSura = model.ClasificacionZonaSura?.Trim() ?? string.Empty;
        model.MunicipioResidencia = model.MunicipioResidencia?.Trim() ?? string.Empty;
        model.Barrio = NormalizeTerapiaAmbulatoriaText(model.Barrio);
        model.ZonaDireccionSegunMunicipio = model.ZonaDireccionSegunMunicipio?.Trim() ?? string.Empty;
        model.Area = model.Area?.Trim() ?? string.Empty;
        model.IpsQueRemite = NormalizeTerapiaAmbulatoriaText(model.IpsQueRemite);
        model.TelefonoPrincipal = NormalizePhone(model.TelefonoPrincipal);
        model.TelefonoAdicional1 = string.IsNullOrWhiteSpace(model.TelefonoAdicional1) ? null : NormalizePhone(model.TelefonoAdicional1);
        model.TelefonoAdicional2 = string.IsNullOrWhiteSpace(model.TelefonoAdicional2) ? null : NormalizePhone(model.TelefonoAdicional2);
        model.Fisioterapeuta = NormalizeTerapiaAmbulatoriaText(model.Fisioterapeuta);
        model.EstadoGestion = model.EstadoGestion?.Trim() ?? TerapiaAmbulatoriaEstadoPendiente;
        model.EstadoGestionFiltro = NormalizeTerapiaAmbulatoriaEstadoGestionFiltro(model.EstadoGestionFiltro);
        model.EstadoPaciente = model.EstadoPaciente?.Trim() ?? string.Empty;
        model.MotivoAlta = model.MotivoAlta?.Trim() ?? string.Empty;
        model.EstadoAlta = model.EstadoAlta?.Trim() ?? TerapiaAmbulatoriaEstadoAltaValues[0];
        model.TiposTerapiaSeleccionados = (model.TiposTerapiaSeleccionados ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        model.SegundoTratamientoTiposTerapiaSeleccionados ??= [];
        model.TercerTratamientoTiposTerapiaSeleccionados ??= [];
        NormalizeTerapiaAmbulatoriaOptionalTreatment(
            model.TieneSegundoTratamiento,
            model.SegundoTratamientoTiposTerapiaSeleccionados,
            value => model.SegundoTratamientoFrecuenciaTerapia = value,
            model.SegundoTratamientoFrecuenciaTerapia);
        NormalizeTerapiaAmbulatoriaOptionalTreatment(
            model.TieneTercerTratamiento,
            model.TercerTratamientoTiposTerapiaSeleccionados,
            value => model.TercerTratamientoFrecuenciaTerapia = value,
            model.TercerTratamientoFrecuenciaTerapia);
        if (!model.TieneSegundoTratamiento)
        {
            model.SegundoTratamientoTiposTerapiaSeleccionados = [];
            model.SegundoTratamientoCantidad = null;
            model.SegundoTratamientoFrecuenciaTerapia = null;
            model.TieneTercerTratamiento = false;
        }

        if (!model.TieneTercerTratamiento)
        {
            model.TercerTratamientoTiposTerapiaSeleccionados = [];
            model.TercerTratamientoCantidad = null;
            model.TercerTratamientoFrecuenciaTerapia = null;
        }

        model.Edad = CalculateAge(model.FechaNacimiento.Date, GetColombiaNow().Date);
        model.FechaFin = CalculateTerapiaAmbulatoriaFechaFin(model);
        ModelState.Remove(nameof(model.FechaFin));
    }

    private static void NormalizeTerapiaAmbulatoriaOptionalTreatment(
        bool isEnabled,
        List<string> selectedTypes,
        Action<string?> setFrequency,
        string? frequency)
    {
        if (!isEnabled)
        {
            return;
        }

        var normalizedTypes = selectedTypes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        selectedTypes.Clear();
        selectedTypes.AddRange(normalizedTypes);
        setFrequency(string.IsNullOrWhiteSpace(frequency) ? null : frequency.Trim());
    }

    private static string NormalizeTerapiaAmbulatoriaText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpper(TerapiaAmbulatoriaTextCulture);
    }

    private static string? NormalizeOptionalTerapiaAmbulatoriaText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpper(TerapiaAmbulatoriaTextCulture);
    }

    private void ValidateTerapiaAmbulatoriaModel(CensoTerapiaAmbulatoriaViewModel model)
    {
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
                    ModelState.AddModelError(nameof(model.NumeroIdentificacion), "El número de identificación solo permite letras y dígitos para PA o CE.");
                }
            }
            else if (!NumericIdentificationPattern.IsMatch(model.NumeroIdentificacion))
            {
                ModelState.AddModelError(nameof(model.NumeroIdentificacion), "El número de identificación solo permite dígitos.");
            }
        }

        if (model.FechaNacimiento.Date >= GetColombiaNow().Date)
        {
            ModelState.AddModelError(nameof(model.FechaNacimiento), "La fecha de nacimiento debe ser anterior a la fecha actual.");
        }

        if (model.FechaIngreso.Date > GetColombiaNow().Date)
        {
            ModelState.AddModelError(nameof(model.FechaIngreso), "La fecha de ingreso no puede ser futura.");
        }

        if (!Cie10Pattern.IsMatch(model.CodigoCie10)
            || !_cie10Catalog.TryGetValue(model.CodigoCie10, out var diagnostico))
        {
            model.DiagnosticoDescriptivo = string.Empty;
            ModelState.AddModelError(nameof(model.CodigoCie10), "El código CIE10 ingresado no existe en el catálogo parametrizado.");
        }
        else
        {
            model.DiagnosticoDescriptivo = NormalizeTerapiaAmbulatoriaText(diagnostico);
        }

        var allowedTiposTerapia = TerapiaAmbulatoriaTipoTerapiaValues.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedFrecuenciasTerapia = TerapiaAmbulatoriaFrecuenciaTerapiaValues.ToHashSet(StringComparer.OrdinalIgnoreCase);
        ValidateTerapiaAmbulatoriaTreatment(
            true,
            model.Cantidad,
            model.FrecuenciaTerapia,
            model.TiposTerapiaSeleccionados,
            nameof(model.Cantidad),
            nameof(model.FrecuenciaTerapia),
            nameof(model.TiposTerapiaSeleccionados),
            allowedTiposTerapia,
            allowedFrecuenciasTerapia,
            "primer tratamiento");

        ValidateTerapiaAmbulatoriaTreatment(
            model.TieneSegundoTratamiento,
            model.SegundoTratamientoCantidad,
            model.SegundoTratamientoFrecuenciaTerapia,
            model.SegundoTratamientoTiposTerapiaSeleccionados,
            nameof(model.SegundoTratamientoCantidad),
            nameof(model.SegundoTratamientoFrecuenciaTerapia),
            nameof(model.SegundoTratamientoTiposTerapiaSeleccionados),
            allowedTiposTerapia,
            allowedFrecuenciasTerapia,
            "segundo tratamiento");

        if (model.TieneTercerTratamiento && !model.TieneSegundoTratamiento)
        {
            ModelState.AddModelError(nameof(model.TieneTercerTratamiento), "Activa primero el segundo tratamiento para agregar un tercero.");
        }

        ValidateTerapiaAmbulatoriaTreatment(
            model.TieneTercerTratamiento,
            model.TercerTratamientoCantidad,
            model.TercerTratamientoFrecuenciaTerapia,
            model.TercerTratamientoTiposTerapiaSeleccionados,
            nameof(model.TercerTratamientoCantidad),
            nameof(model.TercerTratamientoFrecuenciaTerapia),
            nameof(model.TercerTratamientoTiposTerapiaSeleccionados),
            allowedTiposTerapia,
            allowedFrecuenciasTerapia,
            "tercer tratamiento");

        if (!TerapiaAmbulatoriaEstadoPacienteValues.Contains(model.EstadoPaciente, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoPaciente), "Selecciona un estado del paciente válido.");
        }

        if (string.Equals(model.EstadoPaciente, "Alta", StringComparison.OrdinalIgnoreCase)
            && !model.FechaFin.HasValue)
        {
            ModelState.AddModelError(nameof(model.FechaFin), "La fecha fin es obligatoria cuando el estado del paciente es Alta.");
        }

        if (model.FechaFin.HasValue && model.FechaFin.Value.Date < model.FechaInicio.Date)
        {
            ModelState.AddModelError(nameof(model.FechaFin), "La fecha fin no puede ser anterior a la fecha de inicio.");
        }

        if (!string.IsNullOrWhiteSpace(model.Fisioterapeuta))
        {
            if (!model.FisioterapeutaOptions.Any())
            {
                ModelState.AddModelError(nameof(model.Fisioterapeuta), "No hay auxiliares OPS activos para asignar.");
            }
            else
            {
                var canonicalFisioterapeuta = model.FisioterapeutaOptions
                    .Select(x => x.Value)
                    .FirstOrDefault(x => string.Equals(x, model.Fisioterapeuta, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(canonicalFisioterapeuta))
                {
                    ModelState.AddModelError(nameof(model.Fisioterapeuta), "Selecciona un auxiliar OPS válido.");
                }
                else
                {
                    model.Fisioterapeuta = canonicalFisioterapeuta;
                }
            }
        }

        ValidatePhoneValue(model.TelefonoPrincipal, nameof(model.TelefonoPrincipal), "teléfono principal");
        ValidatePhoneValue(model.TelefonoAdicional1, nameof(model.TelefonoAdicional1), "teléfono adicional 1");
        ValidatePhoneValue(model.TelefonoAdicional2, nameof(model.TelefonoAdicional2), "teléfono adicional 2");
        if (string.IsNullOrWhiteSpace(model.TelefonoAdicional1))
        {
            ModelState.AddModelError(nameof(model.TelefonoAdicional1), "El teléfono adicional 1 es obligatorio.");
        }

        ValidateTerapiaAddressDropdowns(model);
    }

    private void ValidateTerapiaAmbulatoriaTreatment(
        bool isRequired,
        int? cantidad,
        string? frecuenciaTerapia,
        IReadOnlyCollection<string> tiposTerapia,
        string cantidadKey,
        string frecuenciaKey,
        string tiposKey,
        HashSet<string> allowedTiposTerapia,
        HashSet<string> allowedFrecuenciasTerapia,
        string label)
    {
        if (!isRequired)
        {
            return;
        }

        if (!cantidad.HasValue || cantidad.Value < 1)
        {
            ModelState.AddModelError(cantidadKey, $"Ingresa una cantidad válida para el {label}.");
        }

        if (string.IsNullOrWhiteSpace(frecuenciaTerapia))
        {
            ModelState.AddModelError(frecuenciaKey, $"Selecciona la frecuencia de terapia para el {label}.");
        }
        else if (!allowedFrecuenciasTerapia.Contains(frecuenciaTerapia))
        {
            ModelState.AddModelError(frecuenciaKey, $"Selecciona una frecuencia de terapia válida para el {label}.");
        }

        if (tiposTerapia.Count == 0)
        {
            ModelState.AddModelError(tiposKey, $"Selecciona al menos un tipo de terapia para el {label}.");
        }
        else if (tiposTerapia.Any(x => !allowedTiposTerapia.Contains(x)))
        {
            ModelState.AddModelError(tiposKey, $"Selecciona tipos de terapia válidos para el {label}.");
        }
    }

    private void ValidateTerapiaAmbulatoriaProrrogaModel(CensoTerapiaAmbulatoriaViewModel model)
    {
        if (!model.EditingRecordId.HasValue)
        {
            ModelState.AddModelError(string.Empty, "Primero guarda o abre un paciente para registrar la prórroga.");
        }

        var allowedTiposTerapia = TerapiaAmbulatoriaTipoTerapiaValues.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (model.ProrrogaTiposTerapiaSeleccionados.Count == 0)
        {
            ModelState.AddModelError(nameof(model.ProrrogaTiposTerapiaSeleccionados), "Selecciona al menos un tipo de terapia para la prórroga.");
        }
        else if (model.ProrrogaTiposTerapiaSeleccionados.Any(x => !allowedTiposTerapia.Contains(x)))
        {
            ModelState.AddModelError(nameof(model.ProrrogaTiposTerapiaSeleccionados), "Selecciona tipos de terapia válidos para la prórroga.");
        }

        if (!model.ProrrogaFechaSolicitud.HasValue)
        {
            ModelState.AddModelError(nameof(model.ProrrogaFechaSolicitud), "Selecciona la fecha de solicitud de prórroga.");
        }

        if (!model.ProrrogaFechaSolicitudAsegurador.HasValue)
        {
            ModelState.AddModelError(nameof(model.ProrrogaFechaSolicitudAsegurador), "Selecciona la fecha de solicitud del asegurador.");
        }

        if (!model.ProrrogaFechaEntregaAutorizacion.HasValue)
        {
            ModelState.AddModelError(nameof(model.ProrrogaFechaEntregaAutorizacion), "Selecciona la fecha de entrega de autorización.");
        }

        if (string.IsNullOrWhiteSpace(model.ProrrogaCodigoAutorizacion))
        {
            ModelState.AddModelError(nameof(model.ProrrogaCodigoAutorizacion), "Ingresa el código de autorización.");
        }

        if (!model.ProrrogaFrecuencia.HasValue || model.ProrrogaFrecuencia.Value < 1)
        {
            ModelState.AddModelError(nameof(model.ProrrogaFrecuencia), "Ingresa una frecuencia válida.");
        }

        if (string.IsNullOrWhiteSpace(model.ProrrogaCantidad))
        {
            ModelState.AddModelError(nameof(model.ProrrogaCantidad), "Ingresa la cantidad.");
        }
    }

    private void ValidateTerapiaAddressDropdowns(CensoTerapiaAmbulatoriaViewModel model)
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

        if (!string.IsNullOrWhiteSpace(model.Area)
            && !AreaValues.Contains(model.Area, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Area), "Selecciona un area valida.");
        }

    }

    private static bool ShouldValidateTerapiaAddress(CensoTerapiaAmbulatoriaViewModel model)
    {
        return !string.IsNullOrWhiteSpace(model.Direccion);
    }

    private static bool IsTerapiaSinDireccion(CensoTerapiaAmbulatoriaViewModel model)
    {
        return string.Equals(model.EstadoGestion, "Sin direccion", StringComparison.OrdinalIgnoreCase);
    }

    private static string CalculateTerapiaAmbulatoriaEstadoGestion(CensoTerapiaAmbulatoriaViewModel model)
    {
        if (!HasTerapiaAmbulatoriaDatosConfirmados(model))
        {
            return TerapiaAmbulatoriaEstadoPendiente;
        }

        if (!model.GestionEnSistema || string.IsNullOrWhiteSpace(model.Fisioterapeuta))
        {
            return TerapiaAmbulatoriaEstadoDatosConfirmados;
        }

        return TerapiaAmbulatoriaEstadoGestionCompleta;
    }

    private static bool HasTerapiaAmbulatoriaDatosConfirmados(CensoTerapiaAmbulatoriaViewModel model)
    {
        return !string.IsNullOrWhiteSpace(model.NombrePaciente)
            && !string.IsNullOrWhiteSpace(model.TipoIdentificacion)
            && !string.IsNullOrWhiteSpace(model.NumeroIdentificacion)
            && !string.IsNullOrWhiteSpace(model.CorreoElectronico)
            && model.Cantidad.HasValue
            && model.Cantidad.Value > 0
            && !string.IsNullOrWhiteSpace(model.FrecuenciaTerapia)
            && model.TiposTerapiaSeleccionados.Count > 0
            && IsTerapiaAmbulatoriaOptionalTreatmentComplete(
                model.TieneSegundoTratamiento,
                model.SegundoTratamientoCantidad,
                model.SegundoTratamientoFrecuenciaTerapia,
                model.SegundoTratamientoTiposTerapiaSeleccionados)
            && IsTerapiaAmbulatoriaOptionalTreatmentComplete(
                model.TieneTercerTratamiento,
                model.TercerTratamientoCantidad,
                model.TercerTratamientoFrecuenciaTerapia,
                model.TercerTratamientoTiposTerapiaSeleccionados)
            && !string.IsNullOrWhiteSpace(model.CodigoCie10)
            && !string.IsNullOrWhiteSpace(model.DiagnosticoDescriptivo)
            && !string.IsNullOrWhiteSpace(model.NumeroAutorizacion)
            && !string.IsNullOrWhiteSpace(model.IpsQueRemite)
            && !string.IsNullOrWhiteSpace(model.TelefonoPrincipal)
            && !string.IsNullOrWhiteSpace(model.TelefonoAdicional1)
            && !string.IsNullOrWhiteSpace(model.EstadoPaciente)
            && model.FechaFin.HasValue;
    }

    private static bool IsTerapiaAmbulatoriaOptionalTreatmentComplete(
        bool isEnabled,
        int? cantidad,
        string? frecuenciaTerapia,
        IReadOnlyCollection<string> tiposTerapia)
    {
        return !isEnabled
            || (cantidad.HasValue
                && cantidad.Value > 0
                && !string.IsNullOrWhiteSpace(frecuenciaTerapia)
                && tiposTerapia.Count > 0);
    }

    private void ClearTerapiaAddressModelState()
    {
        foreach (var key in new[]
        {
            nameof(CensoTerapiaAmbulatoriaViewModel.Direccion),
            nameof(CensoTerapiaAmbulatoriaViewModel.ClasificacionZonaSura),
            nameof(CensoTerapiaAmbulatoriaViewModel.MunicipioResidencia),
            nameof(CensoTerapiaAmbulatoriaViewModel.Barrio),
            nameof(CensoTerapiaAmbulatoriaViewModel.ZonaDireccionSegunMunicipio),
            nameof(CensoTerapiaAmbulatoriaViewModel.Area)
        })
        {
            ModelState.Remove(key);
        }
    }

    private void ApplyTerapiaAddressDefaultsForMissingAddress(CensoTerapiaAmbulatoriaViewModel model)
    {
        model.Direccion = string.IsNullOrWhiteSpace(model.Direccion) ? "SIN DIRECCION" : model.Direccion;
        model.ClasificacionZonaSura = string.IsNullOrWhiteSpace(model.ClasificacionZonaSura) ? InferClasificacionZonaSura(MunicipioNoParametrizado) : model.ClasificacionZonaSura;
        model.MunicipioResidencia = string.IsNullOrWhiteSpace(model.MunicipioResidencia) ? MunicipioNoParametrizado : model.MunicipioResidencia;
        model.Barrio = string.IsNullOrWhiteSpace(model.Barrio) ? "NO PARAMETRIZADO" : model.Barrio;
        model.ZonaDireccionSegunMunicipio = string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio) ? InferZonaDireccionSegunMunicipio(MunicipioNoParametrizado) : model.ZonaDireccionSegunMunicipio;
        model.Area = string.IsNullOrWhiteSpace(model.Area) ? AreaValues[0] : model.Area;
        model.DireccionEsValida = false;
        model.AsumirDireccionErrada = true;
        model.DireccionMensajeValidacion = "Registro guardado con estado Sin direccion.";
    }

    private void ApplyTerapiaAddressValidationResult(
        CensoTerapiaAmbulatoriaViewModel model,
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

            ApplyTerapiaAddressLocationDefaults(model, direccionValidation);
            return;
        }

        model.DireccionEsValida = false;
        model.DireccionSugerida = direccionValidation.SuggestedAddress;
        model.DireccionMensajeValidacion = direccionValidation.Message;
        ApplyTerapiaAddressLocationDefaults(model, direccionValidation);

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

    private void ApplyTerapiaAddressLocationDefaults(CensoTerapiaAmbulatoriaViewModel model, AddressValidationResult validation)
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

    private CensoTerapiaAmbulatoriaRecord MapToTerapiaAmbulatoriaRecord(CensoTerapiaAmbulatoriaViewModel model, string direccionParaGuardar)
    {
        var record = new CensoTerapiaAmbulatoriaRecord();
        ApplyTerapiaAmbulatoriaModelToRecord(model, record, direccionParaGuardar, preserveCreatedAt: false);
        return record;
    }

    private static void ApplyTerapiaAmbulatoriaModelToRecord(
        CensoTerapiaAmbulatoriaViewModel model,
        CensoTerapiaAmbulatoriaRecord record,
        string direccionParaGuardar,
        bool preserveCreatedAt)
    {
        record.NombrePaciente = model.NombrePaciente;
        record.TipoIdentificacion = model.TipoIdentificacion;
        record.NumeroIdentificacion = model.NumeroIdentificacion;
        record.FechaNacimiento = model.FechaNacimiento.Date;
        record.Edad = model.Edad;
        record.CorreoElectronico = model.CorreoElectronico;
        record.Cantidad = model.Cantidad!.Value;
        record.FrecuenciaTerapia = model.FrecuenciaTerapia;
        record.TipoTerapia = string.Join(", ", model.TiposTerapiaSeleccionados);
        record.TieneSegundoTratamiento = model.TieneSegundoTratamiento;
        record.SegundoTratamientoCantidad = model.TieneSegundoTratamiento ? model.SegundoTratamientoCantidad : null;
        record.SegundoTratamientoFrecuenciaTerapia = model.TieneSegundoTratamiento ? model.SegundoTratamientoFrecuenciaTerapia : null;
        record.SegundoTratamientoTipoTerapia = model.TieneSegundoTratamiento
            ? string.Join(", ", model.SegundoTratamientoTiposTerapiaSeleccionados)
            : null;
        record.TieneTercerTratamiento = model.TieneSegundoTratamiento && model.TieneTercerTratamiento;
        record.TercerTratamientoCantidad = record.TieneTercerTratamiento ? model.TercerTratamientoCantidad : null;
        record.TercerTratamientoFrecuenciaTerapia = record.TieneTercerTratamiento ? model.TercerTratamientoFrecuenciaTerapia : null;
        record.TercerTratamientoTipoTerapia = record.TieneTercerTratamiento
            ? string.Join(", ", model.TercerTratamientoTiposTerapiaSeleccionados)
            : null;
        record.CodigoCie10 = model.CodigoCie10;
        record.DiagnosticoDescriptivo = model.DiagnosticoDescriptivo ?? string.Empty;
        record.NumeroAutorizacion = model.NumeroAutorizacion ?? string.Empty;
        record.Direccion = NormalizeOptionalTerapiaAmbulatoriaText(direccionParaGuardar);
        record.DireccionValidada = model.DireccionEsValida;
        record.AsumirDireccionErrada = model.AsumirDireccionErrada;
        record.DetalleDireccion = model.DetalleDireccion;
        record.ClasificacionZonaSura = model.ClasificacionZonaSura;
        record.MunicipioResidencia = model.MunicipioResidencia;
        record.Barrio = model.Barrio;
        record.ZonaDireccionSegunMunicipio = model.ZonaDireccionSegunMunicipio;
        record.Area = model.Area;
        record.IpsQueRemite = model.IpsQueRemite;
        record.TelefonoPrincipal = model.TelefonoPrincipal;
        record.TelefonoAdicional1 = model.TelefonoAdicional1;
        record.TelefonoAdicional2 = model.TelefonoAdicional2;
        record.Fisioterapeuta = model.Fisioterapeuta ?? string.Empty;
        record.GestionEnSistema = model.GestionEnSistema;
        record.EstadoGestion = model.EstadoGestion;
        record.EstadoPaciente = model.EstadoPaciente;
        record.FechaIngreso = model.FechaIngreso.Date;
        record.FechaInicio = model.FechaInicio.Date;
        record.FechaFin = model.FechaFin?.Date;
        record.FechaAlta = model.FechaAlta?.Date;
        record.MotivoAlta = string.IsNullOrWhiteSpace(model.MotivoAlta) ? null : model.MotivoAlta;
        record.EstadoAlta = string.IsNullOrWhiteSpace(model.EstadoAlta)
            ? TerapiaAmbulatoriaEstadoAltaValues[0]
            : model.EstadoAlta;

        if (preserveCreatedAt)
        {
            record.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            record.CreatedAtUtc = DateTime.UtcNow;
        }
    }

    private static void ApplyTerapiaAmbulatoriaRecordToModel(
        CensoTerapiaAmbulatoriaViewModel model,
        CensoTerapiaAmbulatoriaRecord record)
    {
        model.EditingRecordId = record.Id;
        model.NombrePaciente = record.NombrePaciente;
        model.TipoIdentificacion = record.TipoIdentificacion;
        model.NumeroIdentificacion = record.NumeroIdentificacion;
        model.FechaNacimiento = record.FechaNacimiento.Date;
        model.Edad = record.Edad;
        model.CorreoElectronico = record.CorreoElectronico;
        model.Cantidad = record.Cantidad;
        model.FrecuenciaTerapia = record.FrecuenciaTerapia;
        model.TiposTerapiaSeleccionados = record.TipoTerapia
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        model.TieneSegundoTratamiento = record.TieneSegundoTratamiento;
        model.SegundoTratamientoCantidad = record.SegundoTratamientoCantidad;
        model.SegundoTratamientoFrecuenciaTerapia = record.SegundoTratamientoFrecuenciaTerapia;
        model.SegundoTratamientoTiposTerapiaSeleccionados = (record.SegundoTratamientoTipoTerapia ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        model.TieneTercerTratamiento = record.TieneTercerTratamiento;
        model.TercerTratamientoCantidad = record.TercerTratamientoCantidad;
        model.TercerTratamientoFrecuenciaTerapia = record.TercerTratamientoFrecuenciaTerapia;
        model.TercerTratamientoTiposTerapiaSeleccionados = (record.TercerTratamientoTipoTerapia ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        model.CodigoCie10 = record.CodigoCie10;
        model.DiagnosticoDescriptivo = record.DiagnosticoDescriptivo;
        model.NumeroAutorizacion = record.NumeroAutorizacion;
        model.Direccion = record.Direccion;
        model.DireccionEsValida = record.DireccionValidada;
        model.AsumirDireccionErrada = record.AsumirDireccionErrada;
        model.DetalleDireccion = record.DetalleDireccion;
        model.ClasificacionZonaSura = record.ClasificacionZonaSura;
        model.MunicipioResidencia = record.MunicipioResidencia;
        model.Barrio = record.Barrio;
        model.ZonaDireccionSegunMunicipio = record.ZonaDireccionSegunMunicipio;
        model.Area = record.Area;
        model.IpsQueRemite = record.IpsQueRemite;
        model.TelefonoPrincipal = record.TelefonoPrincipal;
        model.TelefonoAdicional1 = record.TelefonoAdicional1;
        model.TelefonoAdicional2 = record.TelefonoAdicional2;
        model.Fisioterapeuta = record.Fisioterapeuta;
        model.GestionEnSistema = record.GestionEnSistema;
        model.EstadoGestion = record.EstadoGestion;
        model.EstadoPaciente = record.EstadoPaciente;
        model.FechaIngreso = record.FechaIngreso.Date;
        model.FechaInicio = record.FechaInicio.Date;
        model.FechaFin = record.FechaFin?.Date;
        model.FechaAlta = record.FechaAlta?.Date;
        model.MotivoAlta = record.MotivoAlta ?? string.Empty;
        model.EstadoAlta = string.IsNullOrWhiteSpace(record.EstadoAlta)
            ? TerapiaAmbulatoriaEstadoAltaValues[0]
            : record.EstadoAlta;
    }

    private static bool IsTerapiaAltaCerrada(string? estadoAlta)
    {
        return string.Equals(estadoAlta?.Trim(), TerapiaAmbulatoriaEstadoAltaCerrado, StringComparison.OrdinalIgnoreCase);
    }

    private static EmailMessage BuildTerapiaAmbulatoriaAltaEmail(CensoTerapiaAmbulatoriaRecord record)
    {
        var tipoDocumento = record.TipoIdentificacion;
        var documento = record.NumeroIdentificacion;
        var subject = $"NUEVA ALTA DE TERAPIA AMBULATORIA - {tipoDocumento} - {documento}";
        var nombrePaciente = WebUtility.HtmlEncode(record.NombrePaciente);
        var tipoDocumentoHtml = WebUtility.HtmlEncode(tipoDocumento);
        var documentoHtml = WebUtility.HtmlEncode(documento);
        var numeroAutorizacion = WebUtility.HtmlEncode(record.NumeroAutorizacion);

        var body = $"""
            <p>Cordial Saludo,</p>
            <p>Se notifica que el paciente {nombrePaciente} con documento {tipoDocumentoHtml} {documentoHtml} cuenta con un alta completa para su gestión desde facturación. El numero de autorización es: {numeroAutorizacion}.</p>
            <p>¡Que tengas un feliz dia!</p>
            """;

        return new EmailMessage
        {
            To = [TerapiaAmbulatoriaAltaNotificationRecipient],
            Subject = subject,
            HtmlBody = body
        };
    }


    private static DateTime? CalculateTerapiaAmbulatoriaFechaFin(DateTime fechaInicio, int? cantidad, string? frecuenciaTerapia)
    {
        if (!cantidad.HasValue || cantidad.Value < 1 || string.IsNullOrWhiteSpace(frecuenciaTerapia))
        {
            return null;
        }

        var terapiasPorSemana = frecuenciaTerapia.Trim().ToUpperInvariant() switch
        {
            "DIARIA" => 5,
            "TRES VECES POR SEMANA" => 3,
            "DOS VECES POR SEMANA" => 2,
            "UNA VEZ POR SEMANA" => 1,
            _ => 0
        };

        if (terapiasPorSemana < 1)
        {
            return null;
        }

        var semanas = (int)Math.Ceiling(cantidad.Value / (decimal)terapiasPorSemana);
        return fechaInicio.Date.AddDays(semanas * 7);
    }

    private static DateTime? CalculateTerapiaAmbulatoriaFechaFin(CensoTerapiaAmbulatoriaViewModel model)
    {
        var fechas = new List<DateTime?>();
        fechas.Add(CalculateTerapiaAmbulatoriaFechaFin(model.FechaInicio, model.Cantidad, model.FrecuenciaTerapia));

        if (model.TieneSegundoTratamiento)
        {
            fechas.Add(CalculateTerapiaAmbulatoriaFechaFin(
                model.FechaInicio,
                model.SegundoTratamientoCantidad,
                model.SegundoTratamientoFrecuenciaTerapia));
        }

        if (model.TieneSegundoTratamiento && model.TieneTercerTratamiento)
        {
            fechas.Add(CalculateTerapiaAmbulatoriaFechaFin(
                model.FechaInicio,
                model.TercerTratamientoCantidad,
                model.TercerTratamientoFrecuenciaTerapia));
        }

        var validFechas = fechas
            .Where(x => x.HasValue)
            .Select(x => x!.Value.Date)
            .OrderByDescending(x => x)
            .ToList();

        return validFechas.Count == 0 ? null : validFechas[0];
    }


    private static string BuildTerapiaAmbulatoriaExcelXml(IReadOnlyList<CensoTerapiaAmbulatoriaRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
        sb.AppendLine(" <Worksheet ss:Name=\"Terapias Ambulatorias\">");
        sb.AppendLine("  <Table>");

        sb.AppendLine("   <Row>");
        AppendHeaderCell(sb, "Id");
        AppendHeaderCell(sb, "NombrePaciente");
        AppendHeaderCell(sb, "TipoIdentificacion");
        AppendHeaderCell(sb, "NumeroIdentificacion");
        AppendHeaderCell(sb, "FechaNacimiento");
        AppendHeaderCell(sb, "Edad");
        AppendHeaderCell(sb, "CorreoElectronico");
        AppendHeaderCell(sb, "Cantidad");
        AppendHeaderCell(sb, "FrecuenciaTerapia");
        AppendHeaderCell(sb, "TipoTerapia");
        AppendHeaderCell(sb, "TieneSegundoTratamiento");
        AppendHeaderCell(sb, "SegundoTratamientoCantidad");
        AppendHeaderCell(sb, "SegundoTratamientoFrecuenciaTerapia");
        AppendHeaderCell(sb, "SegundoTratamientoTipoTerapia");
        AppendHeaderCell(sb, "TieneTercerTratamiento");
        AppendHeaderCell(sb, "TercerTratamientoCantidad");
        AppendHeaderCell(sb, "TercerTratamientoFrecuenciaTerapia");
        AppendHeaderCell(sb, "TercerTratamientoTipoTerapia");
        AppendHeaderCell(sb, "CodigoCie10");
        AppendHeaderCell(sb, "DiagnosticoDescriptivo");
        AppendHeaderCell(sb, "NumeroAutorizacion");
        AppendHeaderCell(sb, "Direccion");
        AppendHeaderCell(sb, "DireccionValidada");
        AppendHeaderCell(sb, "AsumirDireccionErrada");
        AppendHeaderCell(sb, "DetalleDireccion");
        AppendHeaderCell(sb, "ClasificacionZonaSura");
        AppendHeaderCell(sb, "MunicipioResidencia");
        AppendHeaderCell(sb, "Barrio");
        AppendHeaderCell(sb, "ZonaDireccionSegunMunicipio");
        AppendHeaderCell(sb, "Area");
        AppendHeaderCell(sb, "IpsQueRemite");
        AppendHeaderCell(sb, "TelefonoPrincipal");
        AppendHeaderCell(sb, "TelefonoAdicional1");
        AppendHeaderCell(sb, "TelefonoAdicional2");
        AppendHeaderCell(sb, "Fisioterapeuta");
        AppendHeaderCell(sb, "GestionEnSistema");
        AppendHeaderCell(sb, "EstadoGestion");
        AppendHeaderCell(sb, "EstadoPaciente");
        AppendHeaderCell(sb, "FechaIngreso");
        AppendHeaderCell(sb, "FechaInicio");
        AppendHeaderCell(sb, "FechaFin");
        AppendHeaderCell(sb, "FechaAlta");
        AppendHeaderCell(sb, "MotivoAlta");
        AppendHeaderCell(sb, "EstadoAlta");
        AppendHeaderCell(sb, "AltaNotificacionEnviadaAtUtc");
        AppendHeaderCell(sb, "CreatedAtUtc");
        AppendHeaderCell(sb, "UpdatedAtUtc");
        AppendHeaderCell(sb, "Prorroga_Id");
        AppendHeaderCell(sb, "Prorroga_TipoTerapia");
        AppendHeaderCell(sb, "Prorroga_FechaSolicitudProrroga");
        AppendHeaderCell(sb, "Prorroga_FechaSolicitudAsegurador");
        AppendHeaderCell(sb, "Prorroga_FechaEntregaAutorizacion");
        AppendHeaderCell(sb, "Prorroga_CodigoAutorizacion");
        AppendHeaderCell(sb, "Prorroga_Frecuencia");
        AppendHeaderCell(sb, "Prorroga_Cantidad");
        AppendHeaderCell(sb, "Prorroga_CreatedAtUtc");
        sb.AppendLine("   </Row>");

        foreach (var item in records)
        {
            var prorrogas = item.Prorrogas.Count > 0
                ? item.Prorrogas.OrderBy(x => x.Id).Cast<CensoTerapiaAmbulatoriaProrroga?>()
                : [null];

            foreach (var prorroga in prorrogas)
            {
                sb.AppendLine("   <Row>");
                AppendDataCell(sb, item.Id.ToString(CultureInfo.InvariantCulture));
                AppendDataCell(sb, item.NombrePaciente);
                AppendDataCell(sb, item.TipoIdentificacion);
                AppendDataCell(sb, item.NumeroIdentificacion);
                AppendDataCell(sb, item.FechaNacimiento.ToString("yyyy-MM-dd"));
                AppendDataCell(sb, item.Edad.ToString(CultureInfo.InvariantCulture));
                AppendDataCell(sb, item.CorreoElectronico);
                AppendDataCell(sb, item.Cantidad.ToString(CultureInfo.InvariantCulture));
                AppendDataCell(sb, item.FrecuenciaTerapia);
                AppendDataCell(sb, item.TipoTerapia);
                AppendDataCell(sb, item.TieneSegundoTratamiento ? "Sí" : "No");
                AppendDataCell(sb, item.SegundoTratamientoCantidad?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                AppendDataCell(sb, item.SegundoTratamientoFrecuenciaTerapia ?? string.Empty);
                AppendDataCell(sb, item.SegundoTratamientoTipoTerapia ?? string.Empty);
                AppendDataCell(sb, item.TieneTercerTratamiento ? "Sí" : "No");
                AppendDataCell(sb, item.TercerTratamientoCantidad?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                AppendDataCell(sb, item.TercerTratamientoFrecuenciaTerapia ?? string.Empty);
                AppendDataCell(sb, item.TercerTratamientoTipoTerapia ?? string.Empty);
                AppendDataCell(sb, item.CodigoCie10);
                AppendDataCell(sb, item.DiagnosticoDescriptivo);
                AppendDataCell(sb, item.NumeroAutorizacion);
                AppendDataCell(sb, item.Direccion ?? string.Empty);
                AppendDataCell(sb, item.DireccionValidada ? "Sí" : "No");
                AppendDataCell(sb, item.AsumirDireccionErrada ? "Sí" : "No");
                AppendDataCell(sb, item.DetalleDireccion ?? string.Empty);
                AppendDataCell(sb, item.ClasificacionZonaSura ?? string.Empty);
                AppendDataCell(sb, item.MunicipioResidencia ?? string.Empty);
                AppendDataCell(sb, item.Barrio ?? string.Empty);
                AppendDataCell(sb, item.ZonaDireccionSegunMunicipio ?? string.Empty);
                AppendDataCell(sb, item.Area ?? string.Empty);
                AppendDataCell(sb, item.IpsQueRemite);
                AppendDataCell(sb, item.TelefonoPrincipal);
                AppendDataCell(sb, item.TelefonoAdicional1 ?? string.Empty);
                AppendDataCell(sb, item.TelefonoAdicional2 ?? string.Empty);
                AppendDataCell(sb, item.Fisioterapeuta);
                AppendDataCell(sb, item.GestionEnSistema ? "Sí" : "No");
                AppendDataCell(sb, item.EstadoGestion);
                AppendDataCell(sb, item.EstadoPaciente);
                AppendDataCell(sb, item.FechaIngreso.ToString("yyyy-MM-dd"));
                AppendDataCell(sb, item.FechaInicio.ToString("yyyy-MM-dd"));
                AppendDataCell(sb, FormatNullableDate(item.FechaFin));
                AppendDataCell(sb, FormatNullableDate(item.FechaAlta));
                AppendDataCell(sb, item.MotivoAlta ?? string.Empty);
                AppendDataCell(sb, item.EstadoAlta);
                AppendDataCell(sb, item.AltaNotificacionEnviadaAtUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                AppendDataCell(sb, item.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"));
                AppendDataCell(sb, item.UpdatedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                AppendDataCell(sb, prorroga?.Id.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                AppendDataCell(sb, prorroga?.TipoTerapia ?? string.Empty);
                AppendDataCell(sb, prorroga?.FechaSolicitudProrroga.ToString("yyyy-MM-dd") ?? string.Empty);
                AppendDataCell(sb, prorroga?.FechaSolicitudAsegurador.ToString("yyyy-MM-dd") ?? string.Empty);
                AppendDataCell(sb, prorroga?.FechaEntregaAutorizacion.ToString("yyyy-MM-dd") ?? string.Empty);
                AppendDataCell(sb, prorroga?.CodigoAutorizacion ?? string.Empty);
                AppendDataCell(sb, prorroga?.Frecuencia.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                AppendDataCell(sb, prorroga?.Cantidad ?? string.Empty);
                AppendDataCell(sb, prorroga?.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                sb.AppendLine("   </Row>");
            }
        }

        sb.AppendLine("  </Table>");
        sb.AppendLine(" </Worksheet>");
        sb.AppendLine("</Workbook>");
        return sb.ToString();
    }


}
