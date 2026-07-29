using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using IntranetPrueba.Data.Entities;
using IntranetPrueba.Models.Security;
using IntranetPrueba.Models.ViewModels;
using IntranetPrueba.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Controllers;

public partial class CensoController
{
    private static readonly CultureInfo CronicoTextCulture = CultureInfo.GetCultureInfo("es-CO");
    private static readonly string[] CronicoFuenteIngresoValues = ["Asegurador", "Remisión interna"];
    private static readonly string[] CronicoGeneroValues = ["Masculino", "Femenino", "Indeterminado"];
    private static readonly string[] CronicoClasificacionCasoValues = ["REHABILITABLE", "PERMANENTE", "PALIATIVO"];
    private static readonly string[] CronicoEstadoPacienteValues = ["CRONICO ESTABLE", "CRONICO AGUDIZADO"];
    // El estado del paciente se administra automáticamente (ya no es un campo editable):
    // por defecto "CRONICO ESTABLE"; pasa a "Inactivo" cuando egresa del programa crónico.
    private const string CronicoEstadoActivo = "CRONICO ESTABLE";
    private const string CronicoEstadoInactivo = "Inactivo";
    private static readonly string[] CronicoBarthelAuditadoValues = ["Si", "No", "Sin dato"];
    private static readonly string[] CronicoCalificacionBarthelValues =
        Enumerable.Range(0, 21).Select(i => $"{i * 5}").ToArray();
    private static readonly string[] CronicoKarnofskyValues =
        Enumerable.Range(0, 11).Select(i => $"{i * 10}").ToArray();
    private static readonly string[] CronicoFastValues = ["1", "2", "3", "4", "5", "6", "7"];
    private static readonly string[] CronicoRankinValues = ["1", "2", "3", "4", "5", "6"];
    private static readonly string[] CronicoDisneaMmrcValues = ["1", "2", "3", "4"];
    private static readonly string[] CronicoNyhaValues = ["I", "II", "III", "IV"];
    private static readonly string[] CronicoSiNoValues = ["Si", "No"];
    private static readonly string[] CronicoEstadoClinicaHeridasValues = ["Activo", "Inactivo"];
    private static readonly string[] CronicoCalibreSondaVesicalValues = ["12FR", "14FR", "16FR", "18FR", "20FR", "22FR"];
    private static readonly string[] CronicoTallaValues = ["S", "M", "L", "XL"];
    private static readonly string[] CronicoEstadoMipresValues = ["APROBADO", "NO APROBADO", "NO GESTIONADO", "NO APLICA"];
    private static readonly string[] CronicoMotivoEgresoValues =
    [
        "RECUPERACIÓN",
        "CONTINUA EN PROGRAMA",
        "FALLECE EN DOMICILIO",
        "FALLECE INTRAMURAL",
        "TRASLADO IPS ASEGURADOR",
        "TRASLADO IPS PACIENTE",
        "ALTA MEDICA",
        "SIN CRITERIO DE ESTANCIA",
        "FALLECE",
        "ALTA VOLUNTARIA"
    ];

    // Catálogo cerrado de diagnósticos crónicos: "Diagnóstico crónico CIE10" solo admite estos
    // códigos; "Grupo de patología crónica" se autocompleta desde el valor asociado.
    private static readonly IReadOnlyDictionary<string, string> CronicoDiagnosticoCatalog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["M029"] = "ENFERMEDADES OSTEOARTICULARES",
        ["U071"] = "ENFERMEDADES RESPIRATORIAS",
        ["F03X"] = "ENFERMEDADES NEUROLÓGICAS",
        ["E135"] = "ENFERMEDADES CARDIOVASCULARES",
        ["G710"] = "ENFERMEDAD METABÓLICA",
        ["R521"] = "ENFERMEDADES OSTEOARTICULARES",
        ["I743"] = "ENFERMEDADES CARDIOVASCULARES",
        ["I742"] = "ENFERMEDADES CARDIOVASCULARES",
        ["G934"] = "ENFERMEDADES NEUROLÓGICAS",
        ["C819"] = "ENFERMEDADES NEOPLÁSICAS",
        ["B209"] = "ENFERMEDADES INFECCIOSAS",
        ["J449"] = "ENFERMEDADES RESPIRATORIAS",
        ["I771"] = "ENFERMEDAD CARDIOVASCULAR",
        ["G912"] = "ENFERMEDADES NEUROLÓGICAS",
        ["D760"] = "ENFERMEDADES NEOPLÁSICAS",
        ["I500"] = "ENFERMEDADES CARDIOVASCULARES",
        ["N180"] = "ENFERMEDADES RENALES",
        ["C959"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C859"] = "ENFERMEDADES NEOPLÁSICAS",
        ["G039"] = "ENFERMEDADES NEUROLÓGICAS",
        ["C900"] = "ENFERMEDAD NEOPLÁSICA",
        ["I272"] = "ENFERMEDADES RESPIRATORIAS",
        ["K746"] = "ENFERMEDADES HEPÁTICAS",
        ["M866"] = "ENFERMEDADES OSTEOARTICULARES",
        ["R522"] = "ENFERMEDADES NEOPLÁSICAS",
        ["G809"] = "ENFERMEDADES NEUROLÓGICAS",
        ["M159"] = "ENFERMEDADES OSTEOARTICULARES",
        ["G619"] = "ENFERMEDADES NEUROLÓGICAS",
        ["F729"] = "ENFERMEDADES NEUROLÓGICAS",
        ["G09X"] = "ENFERMEDADES NEUROLÓGICAS",
        ["T911"] = "ENFERMEDADES NEUROLÓGICAS",
        ["T095"] = "ENFERMEDADES OSTEOARTICULARES",
        ["T922"] = "ENFERMEDADES OSTEOARTICULARES",
        ["T921"] = "ENFERMEDADES OSTEOARTICULARES",
        ["I691"] = "ENFERMEDADES NEUROLÓGICAS",
        ["I690"] = "ENFERMEDADES NEUROLÓGICAS",
        ["I692"] = "ENFERMEDADES NEUROLÓGICAS",
        ["I693"] = "ENFERMEDADES NEUROLÓGICAS",
        ["T932"] = "ENFERMEDADES OSTEOARTICULARES",
        ["T913"] = "ENFERMEDADES NEUROLÓGICAS",
        ["T905"] = "ENFERMEDADES NEUROLÓGICAS",
        ["T940"] = "ENFERMEDADES OSTEOARTICULARES",
        ["M510"] = "ENFERMEDADES NEUROLÓGICAS",
        ["D489"] = "ENFERMEDAD NEOPLÁSICA",
        ["C73X"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C329"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C509"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C449"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C61X"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C679"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C249"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C402"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C710"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C189"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C539"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C541"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C159"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C169"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C260"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C699"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C56X"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C609"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C20X"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C64X"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C795"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C349"] = "ENFERMEDADES NEOPLÁSICAS",
        ["C023"] = "ENFERMEDADES NEOPLÁSICAS",
        ["L899"] = "ENFERMEDADES DE LA PIEL",
        ["C229"] = "ENFERMEDADES NEOPLÁSICAS",
        ["I830"] = "ENFERMEDADES CARDIOVASCULARES",
        ["Q793"] = "ENFERMEDADES NEOPLÁSICAS",
        ["A46X"] = "ENFERMEDADES NEOPLÁSICAS",
    };

    [HttpGet]
    public async Task<IActionResult> ProgramaCronicos(
        string? cedulaPaciente,
        long? recordId,
        CancellationToken cancellationToken)
    {
        var model = BuildDefaultCronicoModel();
        model.CedulaFiltro = NormalizeCedulaFilter(cedulaPaciente);

        if (recordId.HasValue)
        {
            var record = await _context.CensoCronicos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == recordId.Value, cancellationToken);
            if (record is not null)
            {
                ApplyCronicoRecordToModel(model, record);
                model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                    ? record.NumeroIdentificacion
                    : model.CedulaFiltro;
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.CedulaFiltro))
        {
            var record = await _context.CensoCronicos
                .AsNoTracking()
                .Where(x => x.NumeroIdentificacion == model.CedulaFiltro)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (record is not null)
            {
                ApplyCronicoRecordToModel(model, record);
                model.CedulaFiltro = record.NumeroIdentificacion;
            }
        }

        await PopulateCronicoDropdownsAsync(model, cancellationToken);
        return View("ProgramaCronicos", model);
    }

    [HttpPost]
    public async Task<IActionResult> ProgramaCronicos(CensoCronicoViewModel model, CancellationToken cancellationToken)
    {
        NormalizeCronicoModel(model);
        await PopulateCronicoDropdownsAsync(model, cancellationToken);
        ValidateCronicoModel(model);

        var direccionParaGuardar = model.Direccion ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(model.Direccion))
        {
            var direccionValidation = await _addressValidationService.ValidateAddressAsync(direccionParaGuardar, cancellationToken);
            ApplyCronicoAddressValidationResult(model, direccionValidation, ref direccionParaGuardar);
        }
        else
        {
            ClearCronicoAddressModelState();
            model.DireccionEsValida = false;
            model.AsumirDireccionErrada = false;
            model.DireccionSugerida = null;
            model.DireccionMensajeValidacion = null;
            direccionParaGuardar = model.Direccion ?? string.Empty;
        }

        if (!ModelState.IsValid)
        {
            await PopulateCronicoLatestRecordsAsync(model, cancellationToken);
            return View("ProgramaCronicos", model);
        }

        CensoCronicoRecord record;
        var auditAction = "CENSO_CRONICO_CREADO";
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoCronicos
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken)
                ?? new CensoCronicoRecord();
            var isNew = record.Id == 0;
            ApplyDatosBasicosToRecord(model, record, direccionParaGuardar);
            ApplyGestionCasoToRecord(record, model);
            ApplyServiciosToRecord(record, model);
            ApplyHospitalizacionToRecord(record, model);
            record.UpdatedAtUtc = DateTime.UtcNow;
            auditAction = isNew ? "CENSO_CRONICO_CREADO" : "CENSO_CRONICO_ACTUALIZADO";
            if (isNew)
            {
                record.CreatedAtUtc = DateTime.UtcNow;
                await _context.CensoCronicos.AddAsync(record, cancellationToken);
            }
        }
        else
        {
            record = new CensoCronicoRecord { CreatedAtUtc = DateTime.UtcNow };
            ApplyDatosBasicosToRecord(model, record, direccionParaGuardar);
            ApplyGestionCasoToRecord(record, model);
            ApplyServiciosToRecord(record, model);
            ApplyHospitalizacionToRecord(record, model);
            await _context.CensoCronicos.AddAsync(record, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(auditAction, "CensoCronico",
            $"Doc: {record.NumeroIdentificacion}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = model.EditingRecordId.HasValue
            ? "Registro de programa crónicos actualizado correctamente."
            : "Registro de programa crónicos guardado correctamente.";
        return RedirectToAction(nameof(ProgramaCronicos), new { cedulaPaciente = record.NumeroIdentificacion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarCronicoGestionCaso(CensoCronicoViewModel model, CancellationToken cancellationToken)
    {
        NormalizeCronicoGestionCasoFields(model);
        NormalizeCronicoServiciosFields(model);

        return GuardarCronicoSeccionAsync(
            model,
            validateSection: m =>
            {
                ValidateCronicoGestionCaso(m);
                ValidateCronicoServicios(m);
            },
            applySectionToRecord: (record, m) =>
            {
                ApplyGestionCasoToRecord(record, m);
                ApplyServiciosToRecord(record, m);
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar la gestión del caso.",
            auditAction: "CENSO_CRONICO_GESTION_CASO_ACTUALIZADA",
            successMessage: "Gestión del caso guardada correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarCronicoHospitalizacion(CensoCronicoViewModel model, CancellationToken cancellationToken)
    {
        NormalizeCronicoHospitalizacionFields(model);

        return GuardarCronicoSeccionAsync(
            model,
            validateSection: ValidateCronicoHospitalizacion,
            applySectionToRecord: ApplyHospitalizacionToRecord,
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar la hospitalización y seguimiento.",
            auditAction: "CENSO_CRONICO_HOSPITALIZACION_ACTUALIZADA",
            successMessage: "Hospitalización y seguimiento guardados correctamente.",
            cancellationToken);
    }

    private async Task<IActionResult> GuardarCronicoSeccionAsync(
        CensoCronicoViewModel model,
        Action<CensoCronicoViewModel> validateSection,
        Action<CensoCronicoRecord, CensoCronicoViewModel> applySectionToRecord,
        string missingRecordMessage,
        string auditAction,
        string successMessage,
        CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);

        CensoCronicoRecord? record = null;
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoCronicos
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);
        }

        if (record is null)
        {
            ModelState.AddModelError(string.Empty, missingRecordMessage);
        }

        // Los datos básicos no se editan desde esta sección; se conservan los almacenados.
        foreach (var key in new[]
        {
            nameof(CensoCronicoViewModel.FuenteIngreso),
            nameof(CensoCronicoViewModel.FechaIngreso),
            nameof(CensoCronicoViewModel.TipoIdentificacion),
            nameof(CensoCronicoViewModel.NumeroIdentificacion),
            nameof(CensoCronicoViewModel.Genero)
        })
        {
            ModelState.Remove(key);
        }

        await PopulateCronicoDropdownsAsync(model, cancellationToken);
        validateSection(model);

        if (!ModelState.IsValid || record is null)
        {
            return View("ProgramaCronicos", model);
        }

        applySectionToRecord(record, model);
        record.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(auditAction, "CensoCronico",
            $"Doc: {record.NumeroIdentificacion}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = successMessage;
        return RedirectToAction(nameof(ProgramaCronicos), new { recordId = record.Id, cedulaPaciente = record.NumeroIdentificacion });
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerCronicoAgudizaciones(long id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest(new { message = "ID de registro inválido." });

        var agudizaciones = await _context.CensoCronicoAgudizaciones
            .AsNoTracking()
            .Where(x => x.CensoCronicoRecordId == id)
            .OrderBy(x => x.Numero)
            .Select(x => new
            {
                id = x.Id,
                numero = x.Numero,
                agudizacionJson = x.AgudizacionJson,
                kardexJson = x.KardexEdicionJson,
                requisicionJson = x.RequisicionFarmaciaJson,
                cerrada = x.KardexCerradoAtUtc != null,
                cerradaAtUtc = x.KardexCerradoAtUtc,
                enviadaFarmacia = x.FarmaciaEnviadoAtUtc != null,
                enviadoAtUtc = x.FarmaciaEnviadoAtUtc,
                farmaciaEstado = x.FarmaciaEstado,
                farmaciaOkKardex = x.FarmaciaOkKardex,
                tuvoReapertura = x.TuvoReaperturaKardex,
                reaperturaSolicitud = x.Reaperturas
                    .Where(r => r.Estado == ReaperturaKardexEstado.Pendiente)
                    .OrderByDescending(r => r.Id)
                    .Select(r => new
                    {
                        id = r.Id,
                        estado = r.Estado,
                        motivo = r.Motivo,
                        solicitante = r.SolicitadoPorNombre,
                        solicitadaAt = r.SolicitadoAtUtc
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Json(new { agudizaciones });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarCronicoAgudizacion(
        long id,
        string? agudizacionJson,
        long? agudizacionVersionId,
        CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest(new { message = "ID de registro inválido." });

        var record = await _context.CensoCronicos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null) return NotFound(new { message = "Registro no encontrado." });

        if (string.IsNullOrWhiteSpace(agudizacionJson))
        {
            return BadRequest(new { message = "Ingresa los datos de la agudización." });
        }

        var jsonTrimmed = agudizacionJson.Trim();

        CensoCronicoAgudizacion agudizacion;
        if (agudizacionVersionId.HasValue)
        {
            var existing = await _context.CensoCronicoAgudizaciones.FirstOrDefaultAsync(
                x => x.Id == agudizacionVersionId.Value && x.CensoCronicoRecordId == id,
                cancellationToken);
            if (existing is null) return NotFound(new { message = "Agudización no encontrada." });
            if (existing.KardexCerradoAtUtc.HasValue)
            {
                return BadRequest(new { message = "El kardex de esta agudización fue aprobado por farmacia y está cerrado. Solicita la reapertura para poder modificarla." });
            }

            agudizacion = existing;
            agudizacion.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            var maxNumero = await _context.CensoCronicoAgudizaciones
                .Where(x => x.CensoCronicoRecordId == id)
                .Select(x => (int?)x.Numero)
                .MaxAsync(cancellationToken) ?? 0;

            agudizacion = new CensoCronicoAgudizacion
            {
                CensoCronicoRecordId = id,
                Numero = maxNumero + 1,
                CreatedAtUtc = DateTime.UtcNow
            };
            await _context.CensoCronicoAgudizaciones.AddAsync(agudizacion, cancellationToken);
        }

        agudizacion.AgudizacionJson = jsonTrimmed;
        record.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(
            agudizacionVersionId.HasValue ? "CENSO_CRONICO_AGUDIZACION_ACTUALIZADA" : "CENSO_CRONICO_AGUDIZACION_CREADA",
            "CensoCronicoAgudizacion",
            $"Doc: {record.NumeroIdentificacion}, Agudización: #{agudizacion.Numero}",
            auditUserId, auditIp, cancellationToken);

        return Json(new
        {
            message = $"Agudización #{agudizacion.Numero} guardada correctamente.",
            agudizacionId = agudizacion.Id,
            numero = agudizacion.Numero
        });
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerCronicoHospitalizaciones(long id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest(new { message = "ID de registro inválido." });

        var hospitalizaciones = await _context.CensoCronicoHospitalizaciones
            .AsNoTracking()
            .Where(x => x.CensoCronicoRecordId == id)
            .OrderBy(x => x.Numero)
            .Select(x => new
            {
                id = x.Id,
                numero = x.Numero,
                hospitalizacionJson = x.HospitalizacionJson
            })
            .ToListAsync(cancellationToken);

        return Json(new { hospitalizaciones });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarCronicoHospitalizacionRegistro(
        long id,
        string? hospitalizacionJson,
        long? hospitalizacionVersionId,
        CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest(new { message = "ID de registro inválido." });

        var record = await _context.CensoCronicos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null) return NotFound(new { message = "Registro no encontrado." });

        if (string.IsNullOrWhiteSpace(hospitalizacionJson))
        {
            return BadRequest(new { message = "Ingresa los datos de la hospitalización." });
        }

        var jsonTrimmed = hospitalizacionJson.Trim();

        CensoCronicoHospitalizacion hospitalizacion;
        if (hospitalizacionVersionId.HasValue)
        {
            var existing = await _context.CensoCronicoHospitalizaciones.FirstOrDefaultAsync(
                x => x.Id == hospitalizacionVersionId.Value && x.CensoCronicoRecordId == id,
                cancellationToken);
            if (existing is null) return NotFound(new { message = "Hospitalización no encontrada." });
            hospitalizacion = existing;
            hospitalizacion.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            var maxNumero = await _context.CensoCronicoHospitalizaciones
                .Where(x => x.CensoCronicoRecordId == id)
                .Select(x => (int?)x.Numero)
                .MaxAsync(cancellationToken) ?? 0;

            hospitalizacion = new CensoCronicoHospitalizacion
            {
                CensoCronicoRecordId = id,
                Numero = maxNumero + 1,
                CreatedAtUtc = DateTime.UtcNow
            };
            await _context.CensoCronicoHospitalizaciones.AddAsync(hospitalizacion, cancellationToken);
        }

        hospitalizacion.HospitalizacionJson = jsonTrimmed;
        record.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(
            hospitalizacionVersionId.HasValue ? "CENSO_CRONICO_HOSPITALIZACION_ACTUALIZADA" : "CENSO_CRONICO_HOSPITALIZACION_CREADA",
            "CensoCronicoHospitalizacion",
            $"Doc: {record.NumeroIdentificacion}, Hospitalización: #{hospitalizacion.Numero}",
            auditUserId, auditIp, cancellationToken);

        return Json(new
        {
            message = $"Hospitalización #{hospitalizacion.Numero} guardada correctamente.",
            hospitalizacionId = hospitalizacion.Id,
            numero = hospitalizacion.Numero
        });
    }

    // Autocompletado de "Grupo de patología crónica" a partir del catálogo cerrado
    // (CronicoDiagnosticoCatalog), NO del catálogo CIE10 general de la aplicación.
    [HttpGet]
    public IActionResult BuscarGrupoPatologiaCronica(string codigo)
    {
        var normalizedCode = NormalizeCie10(codigo);
        var found = CronicoDiagnosticoCatalog.TryGetValue(normalizedCode, out var grupo);
        return Json(new
        {
            found,
            codigo = normalizedCode,
            grupo = found ? grupo : string.Empty
        });
    }

    // Exportable a Excel del censo de Programa Crónicos. Incluye todos los campos del
    // registro (no los JSON de agudizaciones/hospitalizaciones, pensado para público no
    // técnico) más "Días de estancia" calculado en vivo (hoy - fecha de ingreso).
    [HttpGet]
    public async Task<IActionResult> ExportarCronicosExcel(string? cedulaPaciente, CancellationToken cancellationToken)
    {
        var cedulaFiltro = NormalizeCedulaFilter(cedulaPaciente);
        var query = _context.CensoCronicos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(cedulaFiltro))
        {
            query = query.Where(x => x.NumeroIdentificacion == cedulaFiltro);
        }

        var records = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var content = BuildCronicoExcelXml(records, GetColombiaNow().Date);
        var bytes = Encoding.UTF8.GetBytes(content);
        var fileName = $"censo_cronicos_{DateTime.Now:yyyyMMdd_HHmmss}.xls";
        return File(bytes, "application/vnd.ms-excel", fileName);
    }

    private static string BuildCronicoExcelXml(IReadOnlyList<CensoCronicoRecord> records, DateTime hoy)
    {
        static string F(DateTime? d) => d.HasValue ? d.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty;
        static string N(int? n) => n?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        static string SiNo(bool b) => b ? "Sí" : "No";
        string FTs(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ColombiaTimeZone)
            .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
        sb.AppendLine(" <Styles>");
        sb.AppendLine("  <Style ss:ID=\"Header\"><Font ss:Bold=\"1\"/></Style>");
        sb.AppendLine(" </Styles>");
        sb.AppendLine(" <Worksheet ss:Name=\"Programa Cronicos\">");
        sb.AppendLine("  <Table>");

        var columnas = new (string Header, Func<CensoCronicoRecord, string> Value)[]
        {
            ("Id", r => r.Id.ToString(CultureInfo.InvariantCulture)),
            ("Fuente de ingreso", r => r.FuenteIngreso),
            ("Fecha de ingreso", r => F(r.FechaIngreso)),
            ("Días de estancia", r => Math.Max(0, (hoy - r.FechaIngreso.Date).Days).ToString(CultureInfo.InvariantCulture)),
            ("Tipo de identificación", r => r.TipoIdentificacion),
            ("Número de identificación", r => r.NumeroIdentificacion),
            ("Nombre del paciente", r => r.NombrePaciente),
            ("Fecha de nacimiento", r => F(r.FechaNacimiento)),
            ("Edad", r => r.Edad.ToString(CultureInfo.InvariantCulture)),
            ("Correo electrónico", r => r.CorreoElectronico ?? string.Empty),
            ("Género", r => r.Genero),
            ("Dirección", r => r.Direccion ?? string.Empty),
            ("Detalle de la dirección", r => r.DetalleDireccion ?? string.Empty),
            ("Dirección validada", r => SiNo(r.DireccionValidada)),
            ("Asumir dirección errada", r => SiNo(r.AsumirDireccionErrada)),
            ("Clasificación zona Sura", r => r.ClasificacionZonaSura ?? string.Empty),
            ("Municipio de residencia", r => r.MunicipioResidencia ?? string.Empty),
            ("Barrio", r => r.Barrio ?? string.Empty),
            ("Zona de dirección según municipio", r => r.ZonaDireccionSegunMunicipio ?? string.Empty),
            ("Área", r => r.Area ?? string.Empty),
            ("Clasificación del caso", r => r.ClasificacionCaso ?? string.Empty),
            ("Estado del paciente", r => r.EstadoPaciente ?? string.Empty),
            ("Diagnóstico crónico CIE10", r => r.DiagnosticoCronicoCie10 ?? string.Empty),
            ("Grupo de patología crónica", r => r.GrupoPatologiaCronica ?? string.Empty),
            ("Diagnóstico crónico complementario", r => r.DiagnosticoCronicoComplementario ?? string.Empty),
            ("Grupo de patología crónica complementario", r => r.GrupoPatologiaCronicaComplementario ?? string.Empty),
            ("Barthel auditado", r => r.BarthelAuditado ?? string.Empty),
            ("Fecha de auditoría", r => F(r.FechaAuditoria)),
            ("Calificación Barthel", r => r.CalificacionBarthel ?? string.Empty),
            ("Karnofsky", r => r.Karnofsky ?? string.Empty),
            ("Fast", r => r.Fast ?? string.Empty),
            ("Rankin", r => r.Rankin ?? string.Empty),
            ("Disnea Mmrc", r => r.DisneaMmrc ?? string.Empty),
            ("Nyha", r => r.Nyha ?? string.Empty),
            ("Braden", r => r.Braden ?? string.Empty),
            ("Riesgo de caída", r => r.RiesgoCaida ?? string.Empty),
            ("Riesgo de lesión de piel", r => r.RiesgoLesionPiel ?? string.Empty),
            ("Clínica de heridas", r => r.ClinicaHeridas ?? string.Empty),
            ("Estado en clínica de heridas", r => r.EstadoClinicaHeridas ?? string.Empty),
            ("Programa de nutrición (NE/NPT)", r => r.ProgramaNutricion ?? string.Empty),
            ("Fecha de inicio nutrición", r => F(r.FechaInicioNutricion)),
            ("Auxiliar asignado nutrición", r => r.AuxiliarAsignadoNutricion ?? string.Empty),
            ("Fecha fin nutrición", r => F(r.FechaFinNutricion)),
            ("Educación y plan de cuidados / enfermería", r => r.EducacionPlanCuidados ?? string.Empty),
            ("Terapia física", r => r.TerapiaFisica ?? string.Empty),
            ("Terapia respiratoria", r => r.TerapiaRespiratoria ?? string.Empty),
            ("Terapia ocupacional", r => r.TerapiaOcupacional ?? string.Empty),
            ("Fonoaudiología", r => r.Fonoaudiologia ?? string.Empty),
            ("Nutrición", r => r.Nutricion ?? string.Empty),
            ("Psicología", r => r.Psicologia ?? string.Empty),
            ("Traqueostomía", r => r.Traqueostomia ?? string.Empty),
            ("Sonda nasogástrica", r => r.SondaNasogastrica ?? string.Empty),
            ("Calibre de la sonda nasogástrica", r => r.CalibreSondaNasogastrica ?? string.Empty),
            ("Frecuencia de cambio de sonda nasogástrica", r => r.FrecuenciaCambioSondaNasogastrica ?? string.Empty),
            ("Fecha de último cambio (sonda nasogástrica)", r => F(r.FechaUltimoCambioSondaNasogastrica)),
            ("Sonda gastrostomía", r => r.SondaGastrostomia ?? string.Empty),
            ("Colostomía", r => r.Colostomia ?? string.Empty),
            ("Sonda cistostomía", r => r.SondaCistostomia ?? string.Empty),
            ("Catéter PICC", r => r.CateterPicc ?? string.Empty),
            ("Sonda vesical", r => r.SondaVesical ?? string.Empty),
            ("Calibre de sonda", r => r.CalibreSondaVesical ?? string.Empty),
            ("Frecuencia de cambio (días)", r => r.FrecuenciaCambioSondaVesical ?? string.Empty),
            ("Fecha de último cambio (sonda vesical)", r => F(r.FechaUltimoCambioSondaVesical)),
            ("Fecha de próximo cambio (sonda vesical)", r => F(r.FechaProximoCambioSondaVesical)),
            ("Observación del cambio de sonda", r => r.ObservacionCambioSonda ?? string.Empty),
            ("Fórmula de control", r => r.FormulaControl ?? string.Empty),
            ("Mipres pañales", r => r.MipresPanales ?? string.Empty),
            ("Talla", r => r.TallaPanales ?? string.Empty),
            ("Fecha última prescripción (pañales)", r => F(r.FechaUltimaPrescripcionPanales)),
            ("Tiempo de prescripción pañales (meses)", r => N(r.TiempoPrescripcionPanalesMeses)),
            ("Estado Mipres pañales", r => r.EstadoMipresPanales ?? string.Empty),
            ("Mipres nutrición", r => r.MipresNutricion ?? string.Empty),
            ("Fecha última prescripción (nutrición)", r => F(r.FechaUltimaPrescripcionNutricion)),
            ("Tiempo de prescripción nutrición (meses)", r => N(r.TiempoPrescripcionNutricionMeses)),
            ("Estado Mipres nutrición", r => r.EstadoMipresNutricion ?? string.Empty),
            ("Egresa programa crónico", r => r.EgresaProgramaCronico ?? string.Empty),
            ("Motivo egreso", r => r.MotivoEgreso ?? string.Empty),
            ("Fecha de egreso", r => F(r.FechaEgreso)),
            ("Fecha de creación", r => FTs(r.CreatedAtUtc)),
            ("Última actualización", r => r.UpdatedAtUtc.HasValue ? FTs(r.UpdatedAtUtc.Value) : string.Empty),
        };

        sb.AppendLine("   <Row>");
        foreach (var col in columnas)
        {
            AppendHeaderCell(sb, col.Header);
        }
        sb.AppendLine("   </Row>");

        foreach (var record in records)
        {
            sb.AppendLine("   <Row>");
            foreach (var col in columnas)
            {
                AppendDataCell(sb, col.Value(record));
            }
            sb.AppendLine("   </Row>");
        }

        sb.AppendLine("  </Table>");
        sb.AppendLine(" </Worksheet>");
        sb.AppendLine("</Workbook>");
        return sb.ToString();
    }

    private CensoCronicoViewModel BuildDefaultCronicoModel()
    {
        return new CensoCronicoViewModel
        {
            FechaIngreso = GetColombiaNow().Date,
            FechaNacimiento = GetColombiaNow().Date,
            DireccionEsValida = false,
            EstadoPaciente = CronicoEstadoActivo,
            ClinicaHeridas = "No",
            ProgramaNutricion = "No",
            EducacionPlanCuidados = "No",
            TerapiaFisica = "No",
            TerapiaRespiratoria = "No",
            TerapiaOcupacional = "No",
            Fonoaudiologia = "No",
            Nutricion = "No",
            Psicologia = "No",
            Traqueostomia = "No",
            SondaNasogastrica = "No",
            SondaGastrostomia = "No",
            Colostomia = "No",
            SondaCistostomia = "No",
            CateterPicc = "No",
            SondaVesical = "No",
            FormulaControl = "No",
            MipresPanales = "No",
            MipresNutricion = "No",
            EgresaProgramaCronico = "No"
        };
    }

    private async Task PopulateCronicoDropdownsAsync(CensoCronicoViewModel model, CancellationToken cancellationToken)
    {
        model.FuenteIngresoOptions = BuildOptions(CronicoFuenteIngresoValues);
        model.TipoIdentificacionOptions = BuildOptions(TiposIdentificacion);
        model.GeneroOptions = BuildOptions(CronicoGeneroValues);
        model.ClasificacionZonaSuraOptions = BuildOptions(ClasificacionZonaSuraValues);
        model.MunicipioResidenciaOptions = BuildOptions(MunicipiosResidenciaValues);
        model.ZonaDireccionOptions = BuildOptions(ZonaDireccionValues);
        model.AreaOptions = BuildOptions(AreaValues);
        model.ClasificacionCasoOptions = BuildOptions(CronicoClasificacionCasoValues);
        model.EstadoPacienteOptions = BuildOptions(CronicoEstadoPacienteValues);
        model.BarthelAuditadoOptions = BuildOptions(CronicoBarthelAuditadoValues);
        model.CalificacionBarthelOptions = BuildOptions(CronicoCalificacionBarthelValues);
        model.KarnofskyOptions = BuildOptions(CronicoKarnofskyValues);
        model.FastOptions = BuildOptions(CronicoFastValues);
        model.RankinOptions = BuildOptions(CronicoRankinValues);
        model.DisneaMmrcOptions = BuildOptions(CronicoDisneaMmrcValues);
        model.NyhaOptions = BuildOptions(CronicoNyhaValues);
        model.SiNoOptions = BuildOptions(CronicoSiNoValues);
        model.EstadoClinicaHeridasOptions = BuildOptions(CronicoEstadoClinicaHeridasValues);
        model.CalibreSondaVesicalOptions = BuildOptions(CronicoCalibreSondaVesicalValues);

        // El estado del paciente es derivado (no editable): se muestra según el egreso.
        model.EstadoPaciente = string.Equals(model.EgresaProgramaCronico, "Si", StringComparison.OrdinalIgnoreCase)
            ? CronicoEstadoInactivo
            : CronicoEstadoActivo;
        model.AuxiliarEnfermeriaOptions = await GetOpsAssistantOptionsAsync(cancellationToken);
        model.TallaPanalesOptions = BuildOptions(CronicoTallaValues);
        model.EstadoMipresOptions = BuildOptions(CronicoEstadoMipresValues);
        model.MotivoEgresoOptions = BuildOptions(CronicoMotivoEgresoValues);
        model.MedidaMedicamentoOptions = BuildOptions(MedidaMedicamentoValues);
        model.ViaAdministracionMedicamentoOptions = BuildOptions(ViaAdministracionMedicamentoValues);
        model.FrecuenciaAdministracionOptions = BuildOptions(FrecuenciaAdministracionMxPrincipalValues);
        var medicamentoCatalog = await GetMedicamentoCatalogAsync(cancellationToken);
        model.MedicamentoPrincipalOptions = medicamentoCatalog.Count > 0
            ? medicamentoCatalog.Select(item => item.Nombre).ToList()
            : _medicamentoFallbackValues;
        model.MedicamentoCatalog = medicamentoCatalog;
        model.ReaperturaMotivos = ReaperturaKardexMotivos.Todos;
        model.PuedeAprobarReapertura = await _currentUserPermissionService.HasPermissionAsync(
            User, SystemPermissions.Aprobacion, cancellationToken);

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

        if (string.IsNullOrWhiteSpace(model.Area))
        {
            model.Area = AreaValues[0];
        }

        ResolveCronicoCatalogFields(model);

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
        // Días de estancia: diferencia entre la fecha de ingreso y el día actual (Colombia).
        model.DiasDeEstancia = Math.Max(0, (GetColombiaNow().Date - model.FechaIngreso.Date).Days);
        await PopulateCronicoLatestRecordsAsync(model, cancellationToken);
        await PopulateCronicoAgudizacionesAsync(model, cancellationToken);
        await PopulateCronicoHospitalizacionesAsync(model, cancellationToken);
    }

    private async Task PopulateCronicoAgudizacionesAsync(CensoCronicoViewModel model, CancellationToken cancellationToken)
    {
        if (!model.EditingRecordId.HasValue)
        {
            model.Agudizaciones = [];
            return;
        }

        model.Agudizaciones = await _context.CensoCronicoAgudizaciones
            .AsNoTracking()
            .Where(x => x.CensoCronicoRecordId == model.EditingRecordId.Value)
            .OrderBy(x => x.Numero)
            .ToListAsync(cancellationToken);
    }

    private async Task PopulateCronicoHospitalizacionesAsync(CensoCronicoViewModel model, CancellationToken cancellationToken)
    {
        if (!model.EditingRecordId.HasValue)
        {
            model.Hospitalizaciones = [];
            return;
        }

        model.Hospitalizaciones = await _context.CensoCronicoHospitalizaciones
            .AsNoTracking()
            .Where(x => x.CensoCronicoRecordId == model.EditingRecordId.Value)
            .OrderBy(x => x.Numero)
            .ToListAsync(cancellationToken);
    }

    private async Task PopulateCronicoLatestRecordsAsync(CensoCronicoViewModel model, CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);

        var query = _context.CensoCronicos.AsNoTracking();
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

    private void ResolveCronicoCatalogFields(CensoCronicoViewModel model)
    {
        // Diagnóstico crónico CIE10: restringido al catálogo cerrado de patologías crónicas;
        // el grupo mostrado es el asociado a ese código (no la descripción CIE10 general).
        model.DiagnosticoCronicoCie10 = NormalizeCie10(model.DiagnosticoCronicoCie10);
        if (!string.IsNullOrWhiteSpace(model.DiagnosticoCronicoCie10)
            && CronicoDiagnosticoCatalog.TryGetValue(model.DiagnosticoCronicoCie10, out var grupo))
        {
            model.GrupoPatologiaCronica = grupo;
        }
        else if (string.IsNullOrWhiteSpace(model.DiagnosticoCronicoCie10))
        {
            model.GrupoPatologiaCronica = null;
        }

        // Diagnóstico crónico complementario: catálogo CIE10 general, excluyendo Z y R
        // (factores que influyen en el estado de salud / síntomas y signos, no diagnósticos).
        model.DiagnosticoCronicoComplementario = NormalizeCie10(model.DiagnosticoCronicoComplementario);
        if (!string.IsNullOrWhiteSpace(model.DiagnosticoCronicoComplementario)
            && !StartsWithZOrR(model.DiagnosticoCronicoComplementario)
            && _cie10Catalog.TryGetValue(model.DiagnosticoCronicoComplementario, out var diagComp))
        {
            model.GrupoPatologiaCronicaComplementario = diagComp;
        }
        else if (string.IsNullOrWhiteSpace(model.DiagnosticoCronicoComplementario))
        {
            model.GrupoPatologiaCronicaComplementario = null;
        }
    }

    private void NormalizeCronicoModel(CensoCronicoViewModel model)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);
        model.FuenteIngreso = model.FuenteIngreso?.Trim() ?? string.Empty;
        model.TipoIdentificacion = NormalizeCronicoText(model.TipoIdentificacion);
        model.NumeroIdentificacion = NormalizeIdentificationNumber(model.TipoIdentificacion, model.NumeroIdentificacion);
        model.NombrePaciente = NormalizeCronicoText(model.NombrePaciente);
        model.CorreoElectronico = string.IsNullOrWhiteSpace(model.CorreoElectronico) ? null : model.CorreoElectronico.Trim();
        model.Genero = model.Genero?.Trim() ?? string.Empty;
        model.Edad = CalculateAge(model.FechaNacimiento.Date, GetColombiaNow().Date);
        ModelState.Remove(nameof(model.Edad));
        model.Direccion = NormalizeCronicoText(model.Direccion);
        model.DetalleDireccion = NormalizeOptionalCronicoText(model.DetalleDireccion);
        model.ClasificacionZonaSura = model.ClasificacionZonaSura?.Trim() ?? string.Empty;
        model.MunicipioResidencia = model.MunicipioResidencia?.Trim() ?? string.Empty;
        model.Barrio = NormalizeCronicoText(model.Barrio);
        model.ZonaDireccionSegunMunicipio = model.ZonaDireccionSegunMunicipio?.Trim() ?? string.Empty;
        model.Area = model.Area?.Trim() ?? string.Empty;

        NormalizeCronicoGestionCasoFields(model);
        NormalizeCronicoServiciosFields(model);
        NormalizeCronicoHospitalizacionFields(model);
    }

    private void NormalizeCronicoGestionCasoFields(CensoCronicoViewModel model)
    {
        model.ClasificacionCaso = NormalizeOptionalSelect(model.ClasificacionCaso);
        model.CalificacionBarthel = NormalizeOptionalCronicoText(model.CalificacionBarthel);
        model.Karnofsky = NormalizeOptionalCronicoText(model.Karnofsky);
        model.Fast = NormalizeOptionalCronicoText(model.Fast);
        model.Rankin = NormalizeOptionalCronicoText(model.Rankin);
        model.DisneaMmrc = NormalizeOptionalCronicoText(model.DisneaMmrc);
        model.Nyha = NormalizeOptionalCronicoText(model.Nyha);
        model.Braden = NormalizeOptionalCronicoText(model.Braden);
        model.RiesgoCaida = NormalizeOptionalCronicoText(model.RiesgoCaida);
        model.RiesgoLesionPiel = NormalizeOptionalCronicoText(model.RiesgoLesionPiel);
        model.BarthelAuditado = NormalizeOptionalSelect(model.BarthelAuditado);
        ResolveCronicoCatalogFields(model);
    }

    private void NormalizeCronicoHospitalizacionFields(CensoCronicoViewModel model)
    {
        // Los campos del episodio (motivo, IPS, seguimientos, etc.) se guardan como JSON
        // en la colección Hospitalizaciones; aquí solo se normaliza el egreso del programa.
        model.EgresaProgramaCronico = NormalizeOptionalSelect(model.EgresaProgramaCronico);
        model.MotivoEgreso = NormalizeOptionalSelect(model.MotivoEgreso);
    }

    private void NormalizeCronicoServiciosFields(CensoCronicoViewModel model)
    {
        model.ClinicaHeridas = NormalizeOptionalSelect(model.ClinicaHeridas);
        model.EstadoClinicaHeridas = NormalizeOptionalSelect(model.EstadoClinicaHeridas);
        model.ProgramaNutricion = NormalizeOptionalSelect(model.ProgramaNutricion);
        model.AuxiliarAsignadoNutricion = NormalizeOptionalCronicoText(model.AuxiliarAsignadoNutricion);
        model.EducacionPlanCuidados = NormalizeOptionalSelect(model.EducacionPlanCuidados);
        model.TerapiaFisica = NormalizeOptionalSelect(model.TerapiaFisica);
        model.TerapiaRespiratoria = NormalizeOptionalSelect(model.TerapiaRespiratoria);
        model.TerapiaOcupacional = NormalizeOptionalSelect(model.TerapiaOcupacional);
        model.Fonoaudiologia = NormalizeOptionalSelect(model.Fonoaudiologia);
        model.Nutricion = NormalizeOptionalSelect(model.Nutricion);
        model.Psicologia = NormalizeOptionalSelect(model.Psicologia);
        model.Traqueostomia = NormalizeOptionalSelect(model.Traqueostomia);
        model.SondaNasogastrica = NormalizeOptionalSelect(model.SondaNasogastrica);
        model.CalibreSondaNasogastrica = NormalizeOptionalCronicoText(model.CalibreSondaNasogastrica);
        model.FrecuenciaCambioSondaNasogastrica = NormalizeOptionalCronicoText(model.FrecuenciaCambioSondaNasogastrica);
        model.SondaGastrostomia = NormalizeOptionalSelect(model.SondaGastrostomia);
        model.Colostomia = NormalizeOptionalSelect(model.Colostomia);
        model.SondaCistostomia = NormalizeOptionalSelect(model.SondaCistostomia);
        model.CateterPicc = NormalizeOptionalSelect(model.CateterPicc);
        model.SondaVesical = NormalizeOptionalSelect(model.SondaVesical);
        model.CalibreSondaVesical = NormalizeOptionalCronicoText(model.CalibreSondaVesical);
        model.FrecuenciaCambioSondaVesical = NormalizeOptionalCronicoText(model.FrecuenciaCambioSondaVesical);
        model.ObservacionCambioSonda = NormalizeOptionalCronicoText(model.ObservacionCambioSonda);
        model.FormulaControl = NormalizeOptionalSelect(model.FormulaControl);
        model.MipresPanales = NormalizeOptionalSelect(model.MipresPanales);
        model.TallaPanales = NormalizeOptionalSelect(model.TallaPanales);
        model.EstadoMipresPanales = NormalizeOptionalSelect(model.EstadoMipresPanales);
        model.MipresNutricion = NormalizeOptionalSelect(model.MipresNutricion);
        model.EstadoMipresNutricion = NormalizeOptionalSelect(model.EstadoMipresNutricion);

        if (!string.Equals(model.ClinicaHeridas, "Si", StringComparison.OrdinalIgnoreCase))
        {
            model.EstadoClinicaHeridas = null;
        }

        // Los campos del programa de nutrición solo aplican cuando está en "Si".
        if (!string.Equals(model.ProgramaNutricion, "Si", StringComparison.OrdinalIgnoreCase))
        {
            model.FechaInicioNutricion = null;
            model.AuxiliarAsignadoNutricion = null;
            model.FechaFinNutricion = null;
        }

        // Los campos de la sonda vesical solo aplican cuando está en "Si".
        if (!string.Equals(model.SondaVesical, "Si", StringComparison.OrdinalIgnoreCase))
        {
            model.CalibreSondaVesical = null;
            model.FrecuenciaCambioSondaVesical = null;
            model.FechaUltimoCambioSondaVesical = null;
            model.FechaProximoCambioSondaVesical = null;
            model.ObservacionCambioSonda = null;
        }
        else
        {
            // Fecha de próximo cambio = fecha de último cambio + frecuencia (días).
            model.FechaProximoCambioSondaVesical = null;
            if (model.FechaUltimoCambioSondaVesical.HasValue
                && int.TryParse(model.FrecuenciaCambioSondaVesical, out var diasCambioSonda)
                && diasCambioSonda >= 0)
            {
                model.FechaProximoCambioSondaVesical = model.FechaUltimoCambioSondaVesical.Value.Date.AddDays(diasCambioSonda);
            }
        }

        ModelState.Remove(nameof(model.FechaProximoCambioSondaVesical));

        // Los campos de Mipres pañales solo aplican cuando está en "Si".
        if (!string.Equals(model.MipresPanales, "Si", StringComparison.OrdinalIgnoreCase))
        {
            model.TallaPanales = null;
            model.FechaUltimaPrescripcionPanales = null;
            model.TiempoPrescripcionPanalesMeses = null;
            model.EstadoMipresPanales = null;
        }

        // Los campos de Mipres nutrición solo aplican cuando está en "Si".
        if (!string.Equals(model.MipresNutricion, "Si", StringComparison.OrdinalIgnoreCase))
        {
            model.FechaUltimaPrescripcionNutricion = null;
            model.TiempoPrescripcionNutricionMeses = null;
            model.EstadoMipresNutricion = null;
        }
    }

    private static string NormalizeCronicoText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpper(CronicoTextCulture);
    }

    private static string? NormalizeOptionalCronicoText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpper(CronicoTextCulture);
    }

    private static string? NormalizeOptionalSelect(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    // Los campos Sí/No no tienen opción vacía en la vista: si el registro no trae
    // valor (p. ej. datos anteriores), se muestran en "No" por defecto.
    private static string SiNoOrNo(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "No" : value;
    }

    private void ValidateCronicoModel(CensoCronicoViewModel model)
    {
        if (!CronicoFuenteIngresoValues.Contains(model.FuenteIngreso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.FuenteIngreso), "Selecciona una fuente de ingreso válida.");
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

        if (!CronicoGeneroValues.Contains(model.Genero, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Genero), "Selecciona un género válido.");
        }

        if (model.FechaNacimiento.Date >= GetColombiaNow().Date)
        {
            ModelState.AddModelError(nameof(model.FechaNacimiento), "La fecha de nacimiento debe ser anterior a la fecha actual.");
        }

        if (model.FechaIngreso.Date > GetColombiaNow().Date)
        {
            ModelState.AddModelError(nameof(model.FechaIngreso), "La fecha de ingreso no puede ser futura.");
        }

        ValidateCronicoAddressDropdowns(model);
        ValidateCronicoGestionCaso(model);
        ValidateCronicoServicios(model);
        ValidateCronicoHospitalizacion(model);
    }

    private void ValidateCronicoHospitalizacion(CensoCronicoViewModel model)
    {
        ValidateCronicoSiNo(model.EgresaProgramaCronico, nameof(model.EgresaProgramaCronico), "egresa programa crónico");

        if (!string.IsNullOrWhiteSpace(model.MotivoEgreso)
            && !CronicoMotivoEgresoValues.Contains(model.MotivoEgreso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoEgreso), "Selecciona un motivo de egreso válido.");
        }
    }

    private void ValidateCronicoGestionCaso(CensoCronicoViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.ClasificacionCaso)
            && !CronicoClasificacionCasoValues.Contains(model.ClasificacionCaso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ClasificacionCaso), "Selecciona una clasificación del caso válida.");
        }

        if (!string.IsNullOrWhiteSpace(model.BarthelAuditado)
            && !CronicoBarthelAuditadoValues.Contains(model.BarthelAuditado, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.BarthelAuditado), "Selecciona un valor válido para Barthel auditado.");
        }

        if (!string.IsNullOrWhiteSpace(model.DiagnosticoCronicoCie10)
            && !CronicoDiagnosticoCatalog.ContainsKey(model.DiagnosticoCronicoCie10))
        {
            ModelState.AddModelError(nameof(model.DiagnosticoCronicoCie10), "El diagnóstico crónico CIE10 debe pertenecer al listado parametrizado de patologías crónicas.");
        }

        if (!string.IsNullOrWhiteSpace(model.DiagnosticoCronicoComplementario))
        {
            if (StartsWithZOrR(model.DiagnosticoCronicoComplementario))
            {
                ModelState.AddModelError(nameof(model.DiagnosticoCronicoComplementario), "El diagnóstico crónico complementario no puede iniciar por Z ni R.");
            }
            else if (!_cie10Catalog.ContainsKey(model.DiagnosticoCronicoComplementario))
            {
                ModelState.AddModelError(nameof(model.DiagnosticoCronicoComplementario), "El diagnóstico crónico complementario no existe en el catálogo parametrizado.");
            }
        }
    }

    private static bool StartsWithZOrR(string? code)
    {
        return !string.IsNullOrEmpty(code) && (code[0] == 'Z' || code[0] == 'R');
    }

    private void ValidateCronicoServicios(CensoCronicoViewModel model)
    {
        ValidateCronicoSiNo(model.ClinicaHeridas, nameof(model.ClinicaHeridas), "clínica de heridas");
        ValidateCronicoSiNo(model.ProgramaNutricion, nameof(model.ProgramaNutricion), "programa de nutrición");
        ValidateCronicoSiNo(model.EducacionPlanCuidados, nameof(model.EducacionPlanCuidados), "educación y plan de cuidados");
        ValidateCronicoSiNo(model.TerapiaFisica, nameof(model.TerapiaFisica), "terapia física");
        ValidateCronicoSiNo(model.TerapiaRespiratoria, nameof(model.TerapiaRespiratoria), "terapia respiratoria");
        ValidateCronicoSiNo(model.TerapiaOcupacional, nameof(model.TerapiaOcupacional), "terapia ocupacional");
        ValidateCronicoSiNo(model.Fonoaudiologia, nameof(model.Fonoaudiologia), "fonoaudiología");
        ValidateCronicoSiNo(model.Nutricion, nameof(model.Nutricion), "nutrición");
        ValidateCronicoSiNo(model.Psicologia, nameof(model.Psicologia), "psicología");
        ValidateCronicoSiNo(model.Traqueostomia, nameof(model.Traqueostomia), "traqueostomía");
        ValidateCronicoSiNo(model.SondaNasogastrica, nameof(model.SondaNasogastrica), "sonda nasogástrica");
        ValidateCronicoSiNo(model.SondaGastrostomia, nameof(model.SondaGastrostomia), "sonda gastrostomía");
        ValidateCronicoSiNo(model.Colostomia, nameof(model.Colostomia), "colostomía");
        ValidateCronicoSiNo(model.SondaCistostomia, nameof(model.SondaCistostomia), "sonda cistostomía");
        ValidateCronicoSiNo(model.CateterPicc, nameof(model.CateterPicc), "catéter PICC");
        ValidateCronicoSiNo(model.SondaVesical, nameof(model.SondaVesical), "sonda vesical");
        ValidateCronicoSiNo(model.FormulaControl, nameof(model.FormulaControl), "fórmula de control");
        ValidateCronicoSiNo(model.MipresPanales, nameof(model.MipresPanales), "Mipres pañales");
        ValidateCronicoSiNo(model.MipresNutricion, nameof(model.MipresNutricion), "Mipres nutrición");

        if (!string.IsNullOrWhiteSpace(model.EstadoClinicaHeridas)
            && !CronicoEstadoClinicaHeridasValues.Contains(model.EstadoClinicaHeridas, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoClinicaHeridas), "Selecciona un estado en clínica de heridas válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.CalibreSondaVesical)
            && !CronicoCalibreSondaVesicalValues.Contains(model.CalibreSondaVesical, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.CalibreSondaVesical), "Selecciona un calibre de sonda válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.TallaPanales)
            && !CronicoTallaValues.Contains(model.TallaPanales, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.TallaPanales), "Selecciona una talla válida.");
        }

        if (!string.IsNullOrWhiteSpace(model.EstadoMipresPanales)
            && !CronicoEstadoMipresValues.Contains(model.EstadoMipresPanales, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoMipresPanales), "Selecciona un estado Mipres válido para pañales.");
        }

        if (!string.IsNullOrWhiteSpace(model.EstadoMipresNutricion)
            && !CronicoEstadoMipresValues.Contains(model.EstadoMipresNutricion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoMipresNutricion), "Selecciona un estado Mipres válido para nutrición.");
        }

        if (!string.IsNullOrWhiteSpace(model.AuxiliarAsignadoNutricion))
        {
            var canonical = model.AuxiliarEnfermeriaOptions
                .Select(x => x.Value)
                .FirstOrDefault(x => string.Equals(x, model.AuxiliarAsignadoNutricion, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(canonical))
            {
                ModelState.AddModelError(nameof(model.AuxiliarAsignadoNutricion), "Selecciona un auxiliar OPS válido.");
            }
            else
            {
                model.AuxiliarAsignadoNutricion = canonical;
            }
        }

        if (model.FechaFinNutricion.HasValue
            && model.FechaInicioNutricion.HasValue
            && model.FechaFinNutricion.Value.Date < model.FechaInicioNutricion.Value.Date)
        {
            ModelState.AddModelError(nameof(model.FechaFinNutricion), "La fecha fin de nutrición no puede ser anterior a la fecha de inicio.");
        }
    }

    private void ValidateCronicoSiNo(string? value, string fieldName, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !CronicoSiNoValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(fieldName, $"Selecciona una opción válida para {displayName}.");
        }
    }

    private void ValidateCronicoAddressDropdowns(CensoCronicoViewModel model)
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
            ModelState.AddModelError(nameof(model.Area), "Selecciona un area válida.");
        }
    }

    private void ClearCronicoAddressModelState()
    {
        foreach (var key in new[]
        {
            nameof(CensoCronicoViewModel.Direccion),
            nameof(CensoCronicoViewModel.ClasificacionZonaSura),
            nameof(CensoCronicoViewModel.MunicipioResidencia),
            nameof(CensoCronicoViewModel.Barrio),
            nameof(CensoCronicoViewModel.ZonaDireccionSegunMunicipio),
            nameof(CensoCronicoViewModel.Area)
        })
        {
            ModelState.Remove(key);
        }
    }

    private void ApplyCronicoAddressValidationResult(
        CensoCronicoViewModel model,
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

            ApplyCronicoAddressLocationDefaults(model, direccionValidation);
            return;
        }

        model.DireccionEsValida = false;
        model.DireccionSugerida = direccionValidation.SuggestedAddress;
        model.DireccionMensajeValidacion = direccionValidation.Message;
        ApplyCronicoAddressLocationDefaults(model, direccionValidation);

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

    private void ApplyCronicoAddressLocationDefaults(CensoCronicoViewModel model, AddressValidationResult validation)
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

    private static void ApplyDatosBasicosToRecord(CensoCronicoViewModel model, CensoCronicoRecord record, string direccionParaGuardar)
    {
        record.FuenteIngreso = model.FuenteIngreso;
        record.FechaIngreso = model.FechaIngreso.Date;
        record.TipoIdentificacion = model.TipoIdentificacion;
        record.NumeroIdentificacion = model.NumeroIdentificacion;
        record.NombrePaciente = model.NombrePaciente;
        record.FechaNacimiento = model.FechaNacimiento.Date;
        record.Edad = model.Edad;
        record.CorreoElectronico = model.CorreoElectronico;
        record.Genero = model.Genero;
        record.Direccion = NormalizeOptionalCronicoText(direccionParaGuardar);
        record.DireccionValidada = model.DireccionEsValida;
        record.AsumirDireccionErrada = model.AsumirDireccionErrada;
        record.DetalleDireccion = model.DetalleDireccion;
        record.ClasificacionZonaSura = string.IsNullOrWhiteSpace(model.ClasificacionZonaSura) ? null : model.ClasificacionZonaSura;
        record.MunicipioResidencia = string.IsNullOrWhiteSpace(model.MunicipioResidencia) ? null : model.MunicipioResidencia;
        record.Barrio = string.IsNullOrWhiteSpace(model.Barrio) ? null : model.Barrio;
        record.ZonaDireccionSegunMunicipio = string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio) ? null : model.ZonaDireccionSegunMunicipio;
        record.Area = string.IsNullOrWhiteSpace(model.Area) ? null : model.Area;
    }

    private static void ApplyGestionCasoToRecord(CensoCronicoRecord record, CensoCronicoViewModel model)
    {
        record.ClasificacionCaso = model.ClasificacionCaso;
        record.DiagnosticoCronicoCie10 = string.IsNullOrWhiteSpace(model.DiagnosticoCronicoCie10) ? null : model.DiagnosticoCronicoCie10;
        record.GrupoPatologiaCronica = model.GrupoPatologiaCronica;
        record.DiagnosticoCronicoComplementario = string.IsNullOrWhiteSpace(model.DiagnosticoCronicoComplementario) ? null : model.DiagnosticoCronicoComplementario;
        record.GrupoPatologiaCronicaComplementario = model.GrupoPatologiaCronicaComplementario;
        record.BarthelAuditado = model.BarthelAuditado;
        record.FechaAuditoria = model.FechaAuditoria?.Date;
        record.CalificacionBarthel = model.CalificacionBarthel;
        record.Karnofsky = model.Karnofsky;
        record.Fast = model.Fast;
        record.Rankin = model.Rankin;
        record.DisneaMmrc = model.DisneaMmrc;
        record.Nyha = model.Nyha;
        record.Braden = model.Braden;
        record.RiesgoCaida = model.RiesgoCaida;
        record.RiesgoLesionPiel = model.RiesgoLesionPiel;
    }

    private static void ApplyHospitalizacionToRecord(CensoCronicoRecord record, CensoCronicoViewModel model)
    {
        // Los episodios de hospitalización + seguimiento son multi-registro (JSON en
        // censo_cronico_hospitalizaciones); aquí solo se guarda el egreso del programa.
        record.EgresaProgramaCronico = model.EgresaProgramaCronico;
        record.MotivoEgreso = model.MotivoEgreso;
        record.FechaEgreso = model.FechaEgreso?.Date;

        // El estado del paciente se deriva del egreso del programa crónico.
        record.EstadoPaciente = string.Equals(model.EgresaProgramaCronico, "Si", StringComparison.OrdinalIgnoreCase)
            ? CronicoEstadoInactivo
            : CronicoEstadoActivo;
    }

    private static void ApplyServiciosToRecord(CensoCronicoRecord record, CensoCronicoViewModel model)
    {
        record.ClinicaHeridas = model.ClinicaHeridas;
        record.EstadoClinicaHeridas = string.Equals(model.ClinicaHeridas, "Si", StringComparison.OrdinalIgnoreCase)
            ? model.EstadoClinicaHeridas
            : null;
        record.ProgramaNutricion = model.ProgramaNutricion;
        record.FechaInicioNutricion = model.FechaInicioNutricion?.Date;
        record.AuxiliarAsignadoNutricion = model.AuxiliarAsignadoNutricion;
        record.FechaFinNutricion = model.FechaFinNutricion?.Date;
        record.EducacionPlanCuidados = model.EducacionPlanCuidados;
        record.TerapiaFisica = model.TerapiaFisica;
        record.TerapiaRespiratoria = model.TerapiaRespiratoria;
        record.TerapiaOcupacional = model.TerapiaOcupacional;
        record.Fonoaudiologia = model.Fonoaudiologia;
        record.Nutricion = model.Nutricion;
        record.Psicologia = model.Psicologia;
        record.Traqueostomia = model.Traqueostomia;
        record.SondaNasogastrica = model.SondaNasogastrica;
        record.CalibreSondaNasogastrica = model.CalibreSondaNasogastrica;
        record.FrecuenciaCambioSondaNasogastrica = model.FrecuenciaCambioSondaNasogastrica;
        record.FechaUltimoCambioSondaNasogastrica = model.FechaUltimoCambioSondaNasogastrica?.Date;
        record.SondaGastrostomia = model.SondaGastrostomia;
        record.Colostomia = model.Colostomia;
        record.SondaCistostomia = model.SondaCistostomia;
        record.CateterPicc = model.CateterPicc;
        record.SondaVesical = model.SondaVesical;
        record.CalibreSondaVesical = model.CalibreSondaVesical;
        record.FrecuenciaCambioSondaVesical = model.FrecuenciaCambioSondaVesical;
        record.FechaUltimoCambioSondaVesical = model.FechaUltimoCambioSondaVesical?.Date;
        record.FechaProximoCambioSondaVesical = model.FechaProximoCambioSondaVesical?.Date;
        record.ObservacionCambioSonda = model.ObservacionCambioSonda;
        record.FormulaControl = model.FormulaControl;
        record.MipresPanales = model.MipresPanales;
        record.TallaPanales = model.TallaPanales;
        record.FechaUltimaPrescripcionPanales = model.FechaUltimaPrescripcionPanales?.Date;
        record.TiempoPrescripcionPanalesMeses = model.TiempoPrescripcionPanalesMeses;
        record.EstadoMipresPanales = model.EstadoMipresPanales;
        record.MipresNutricion = model.MipresNutricion;
        record.FechaUltimaPrescripcionNutricion = model.FechaUltimaPrescripcionNutricion?.Date;
        record.TiempoPrescripcionNutricionMeses = model.TiempoPrescripcionNutricionMeses;
        record.EstadoMipresNutricion = model.EstadoMipresNutricion;
    }

    private static void ApplyCronicoRecordToModel(CensoCronicoViewModel model, CensoCronicoRecord record)
    {
        model.EditingRecordId = record.Id;
        model.FuenteIngreso = record.FuenteIngreso;
        model.FechaIngreso = record.FechaIngreso.Date;
        model.TipoIdentificacion = record.TipoIdentificacion;
        model.NumeroIdentificacion = record.NumeroIdentificacion;
        model.NombrePaciente = record.NombrePaciente;
        model.FechaNacimiento = record.FechaNacimiento.Date;
        model.Edad = record.Edad;
        model.CorreoElectronico = record.CorreoElectronico;
        model.Genero = record.Genero;
        model.Direccion = record.Direccion;
        model.DireccionEsValida = record.DireccionValidada;
        model.AsumirDireccionErrada = record.AsumirDireccionErrada;
        model.DetalleDireccion = record.DetalleDireccion;
        model.ClasificacionZonaSura = record.ClasificacionZonaSura;
        model.MunicipioResidencia = record.MunicipioResidencia;
        model.Barrio = record.Barrio;
        model.ZonaDireccionSegunMunicipio = record.ZonaDireccionSegunMunicipio;
        model.Area = record.Area;

        model.ClasificacionCaso = record.ClasificacionCaso;
        model.EstadoPaciente = record.EstadoPaciente;
        model.DiagnosticoCronicoCie10 = record.DiagnosticoCronicoCie10;
        model.GrupoPatologiaCronica = record.GrupoPatologiaCronica;
        model.DiagnosticoCronicoComplementario = record.DiagnosticoCronicoComplementario;
        model.GrupoPatologiaCronicaComplementario = record.GrupoPatologiaCronicaComplementario;
        model.BarthelAuditado = record.BarthelAuditado;
        model.FechaAuditoria = record.FechaAuditoria?.Date;
        model.CalificacionBarthel = record.CalificacionBarthel;
        model.Karnofsky = record.Karnofsky;
        model.Fast = record.Fast;
        model.Rankin = record.Rankin;
        model.DisneaMmrc = record.DisneaMmrc;
        model.Nyha = record.Nyha;
        model.Braden = record.Braden;
        model.RiesgoCaida = record.RiesgoCaida;
        model.RiesgoLesionPiel = record.RiesgoLesionPiel;

        model.ClinicaHeridas = SiNoOrNo(record.ClinicaHeridas);
        model.EstadoClinicaHeridas = record.EstadoClinicaHeridas;
        model.ProgramaNutricion = SiNoOrNo(record.ProgramaNutricion);
        model.FechaInicioNutricion = record.FechaInicioNutricion?.Date;
        model.AuxiliarAsignadoNutricion = record.AuxiliarAsignadoNutricion;
        model.FechaFinNutricion = record.FechaFinNutricion?.Date;
        model.EducacionPlanCuidados = SiNoOrNo(record.EducacionPlanCuidados);
        model.TerapiaFisica = SiNoOrNo(record.TerapiaFisica);
        model.TerapiaRespiratoria = SiNoOrNo(record.TerapiaRespiratoria);
        model.TerapiaOcupacional = SiNoOrNo(record.TerapiaOcupacional);
        model.Fonoaudiologia = SiNoOrNo(record.Fonoaudiologia);
        model.Nutricion = SiNoOrNo(record.Nutricion);
        model.Psicologia = SiNoOrNo(record.Psicologia);
        model.Traqueostomia = SiNoOrNo(record.Traqueostomia);
        model.SondaNasogastrica = SiNoOrNo(record.SondaNasogastrica);
        model.CalibreSondaNasogastrica = record.CalibreSondaNasogastrica;
        model.FrecuenciaCambioSondaNasogastrica = record.FrecuenciaCambioSondaNasogastrica;
        model.FechaUltimoCambioSondaNasogastrica = record.FechaUltimoCambioSondaNasogastrica?.Date;
        model.SondaGastrostomia = SiNoOrNo(record.SondaGastrostomia);
        model.Colostomia = SiNoOrNo(record.Colostomia);
        model.SondaCistostomia = SiNoOrNo(record.SondaCistostomia);
        model.CateterPicc = SiNoOrNo(record.CateterPicc);
        model.SondaVesical = SiNoOrNo(record.SondaVesical);
        model.CalibreSondaVesical = record.CalibreSondaVesical;
        model.FrecuenciaCambioSondaVesical = record.FrecuenciaCambioSondaVesical;
        model.FechaUltimoCambioSondaVesical = record.FechaUltimoCambioSondaVesical?.Date;
        model.FechaProximoCambioSondaVesical = record.FechaProximoCambioSondaVesical?.Date;
        model.ObservacionCambioSonda = record.ObservacionCambioSonda;
        model.FormulaControl = SiNoOrNo(record.FormulaControl);
        model.MipresPanales = SiNoOrNo(record.MipresPanales);
        model.TallaPanales = record.TallaPanales;
        model.FechaUltimaPrescripcionPanales = record.FechaUltimaPrescripcionPanales?.Date;
        model.TiempoPrescripcionPanalesMeses = record.TiempoPrescripcionPanalesMeses;
        model.EstadoMipresPanales = record.EstadoMipresPanales;
        model.MipresNutricion = SiNoOrNo(record.MipresNutricion);
        model.FechaUltimaPrescripcionNutricion = record.FechaUltimaPrescripcionNutricion?.Date;
        model.TiempoPrescripcionNutricionMeses = record.TiempoPrescripcionNutricionMeses;
        model.EstadoMipresNutricion = record.EstadoMipresNutricion;
        model.EgresaProgramaCronico = SiNoOrNo(record.EgresaProgramaCronico);
        model.MotivoEgreso = record.MotivoEgreso;
        model.FechaEgreso = record.FechaEgreso?.Date;
    }

    // =====================================================================
    // Kardex y requisición de agudizaciones (Programa Crónicos).
    // Flujo totalmente independiente del censo de agudos: el estado vive en
    // censo_cronico_agudizaciones y censo_cronico_kardex_reaperturas.
    // =====================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarCronicoAgudizacionDocumentos(
        long agudizacionId,
        string? kardexJson,
        string? requisicionJson,
        CancellationToken cancellationToken)
    {
        if (agudizacionId <= 0) return BadRequest(new { message = "ID de agudización inválido." });

        var agudizacion = await _context.CensoCronicoAgudizaciones
            .Include(x => x.CensoCronicoRecord)
            .FirstOrDefaultAsync(x => x.Id == agudizacionId, cancellationToken);
        if (agudizacion is null) return NotFound(new { message = "Agudización no encontrada." });

        if (agudizacion.KardexCerradoAtUtc.HasValue)
        {
            return BadRequest(new { message = "Kardex cerrado. Este documento ya no se puede editar." });
        }

        agudizacion.KardexEdicionJson = string.IsNullOrWhiteSpace(kardexJson) ? null : kardexJson.Trim();
        agudizacion.RequisicionFarmaciaJson = string.IsNullOrWhiteSpace(requisicionJson) ? null : requisicionJson.Trim();
        agudizacion.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        await _auditService.LogAsync("CENSO_CRONICO_DOCUMENTOS_GUARDADOS", "CensoCronicoAgudizacion",
            $"Doc: {agudizacion.CensoCronicoRecord.NumeroIdentificacion}, Agudización: #{agudizacion.Numero}",
            auditUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Json(new { message = $"Kardex y requisición de la agudización #{agudizacion.Numero} guardados correctamente." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarCronicoAgudizacionAFarmacia(
        long agudizacionId,
        string? kardexJson,
        string? requisicionJson,
        CancellationToken cancellationToken)
    {
        if (agudizacionId <= 0) return BadRequest(new { message = "ID de agudización inválido." });

        var agudizacion = await _context.CensoCronicoAgudizaciones
            .Include(x => x.CensoCronicoRecord)
            .FirstOrDefaultAsync(x => x.Id == agudizacionId, cancellationToken);
        if (agudizacion is null) return NotFound(new { message = "Agudización no encontrada." });

        if (agudizacion.KardexCerradoAtUtc.HasValue)
        {
            return BadRequest(new { message = "El kardex de esta agudización ya fue aprobado por farmacia y no se puede reenviar." });
        }

        if (!string.IsNullOrWhiteSpace(kardexJson))
        {
            agudizacion.KardexEdicionJson = kardexJson.Trim();
        }

        if (!string.IsNullOrWhiteSpace(requisicionJson))
        {
            agudizacion.RequisicionFarmaciaJson = requisicionJson.Trim();
        }

        var nowUtc = DateTime.UtcNow;
        agudizacion.FarmaciaEnviadoAtUtc = nowUtc;
        agudizacion.FarmaciaEstado = FarmaciaEstados.Nuevo;
        agudizacion.FarmaciaOkKardex = false;
        agudizacion.FarmaciaKardexVistoAtUtc = null;
        agudizacion.FarmaciaRequisicionVistoAtUtc = null;
        agudizacion.UpdatedAtUtc = nowUtc;
        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        await _auditService.LogAsync("CENSO_CRONICO_ENVIADO_FARMACIA", "CensoCronicoAgudizacion",
            $"Paciente: {agudizacion.CensoCronicoRecord.NombrePaciente}, Doc: {agudizacion.CensoCronicoRecord.NumeroIdentificacion}, Agudización: #{agudizacion.Numero}",
            auditUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Json(new
        {
            message = $"Kardex de la agudización #{agudizacion.Numero} enviado a farmacia correctamente.",
            agudizacionId = agudizacion.Id,
            enviadoAtUtc = nowUtc
        });
    }

    [HttpPost]
    public async Task<IActionResult> SolicitarReaperturaKardexCronico(
        long agudizacionId,
        string? motivo,
        CancellationToken cancellationToken = default)
    {
        if (agudizacionId <= 0) return BadRequest(new { message = "ID de agudización inválido." });

        var motivoNormalizado = (motivo ?? string.Empty).Trim();
        if (!ReaperturaKardexMotivos.Todos.Contains(motivoNormalizado))
        {
            return BadRequest(new { message = "Selecciona un motivo de reapertura valido." });
        }

        var agudizacion = await _context.CensoCronicoAgudizaciones
            .Include(x => x.CensoCronicoRecord)
            .FirstOrDefaultAsync(x => x.Id == agudizacionId, cancellationToken);
        if (agudizacion is null) return NotFound(new { message = "Agudización no encontrada." });

        if (!agudizacion.KardexCerradoAtUtc.HasValue)
        {
            return BadRequest(new { message = "El kardex no esta cerrado; no requiere reapertura." });
        }

        var yaPendiente = await _context.CensoCronicoKardexReaperturas.AnyAsync(
            r => r.CensoCronicoAgudizacionId == agudizacionId && r.Estado == ReaperturaKardexEstado.Pendiente,
            cancellationToken);
        if (yaPendiente)
        {
            return BadRequest(new { message = "Ya existe una solicitud de reapertura pendiente para esta agudización." });
        }

        var currentUserId = GetCurrentUserIdOrEmpty();
        var solicitante = await ResolveUserDisplayNameAsync(currentUserId, cancellationToken);

        var solicitud = new CensoCronicoKardexReapertura
        {
            CensoCronicoAgudizacionId = agudizacionId,
            Motivo = motivoNormalizado,
            Estado = ReaperturaKardexEstado.Pendiente,
            SolicitadoPorUserId = currentUserId,
            SolicitadoPorNombre = solicitante,
            SolicitadoAtUtc = DateTime.UtcNow
        };
        _context.CensoCronicoKardexReaperturas.Add(solicitud);
        await _context.SaveChangesAsync(cancellationToken);

        var emailWarning = await SendCronicoReaperturaSolicitudEmailAsync(agudizacion, solicitud, cancellationToken);

        await _auditService.LogAsync("CENSO_CRONICO_REAPERTURA_SOLICITADA", "CensoCronicoAgudizacion",
            $"Paciente: {agudizacion.CensoCronicoRecord.NombrePaciente}, Doc: {agudizacion.CensoCronicoRecord.NumeroIdentificacion}, Agudización: #{agudizacion.Numero}, Motivo: {motivoNormalizado}",
            currentUserId == Guid.Empty ? null : currentUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Json(new
        {
            message = string.IsNullOrEmpty(emailWarning)
                ? "Solicitud de reapertura enviada. Un supervisor debe aprobarla."
                : $"Solicitud de reapertura registrada. {emailWarning}",
            solicitud = MapCronicoReaperturaDto(solicitud)
        });
    }

    [HttpPost]
    [Authorize(Policy = SystemPermissions.Aprobacion)]
    public async Task<IActionResult> AprobarReaperturaKardexCronico(long solicitudId, CancellationToken cancellationToken = default)
    {
        var solicitud = await _context.CensoCronicoKardexReaperturas
            .Include(r => r.CensoCronicoAgudizacion)
            .ThenInclude(a => a.CensoCronicoRecord)
            .FirstOrDefaultAsync(r => r.Id == solicitudId && r.Estado == ReaperturaKardexEstado.Pendiente, cancellationToken);
        if (solicitud is null) return NotFound(new { message = "Solicitud de reapertura no encontrada o ya gestionada." });

        var agudizacion = solicitud.CensoCronicoAgudizacion;
        agudizacion.KardexCerradoAtUtc = null;
        agudizacion.UpdatedAtUtc = DateTime.UtcNow;

        var currentUserId = GetCurrentUserIdOrEmpty();
        solicitud.Estado = ReaperturaKardexEstado.Aprobada;
        solicitud.ResueltoPorUserId = currentUserId == Guid.Empty ? null : currentUserId;
        solicitud.ResueltoPorNombre = await ResolveUserDisplayNameAsync(currentUserId, cancellationToken);
        solicitud.ResueltoAtUtc = DateTime.UtcNow;

        // Marca persistente: la agudización tuvo reapertura de kardex (última reapertura gana).
        agudizacion.TuvoReaperturaKardex = true;
        agudizacion.ReaperturaSolicitadaPor = solicitud.SolicitadoPorNombre;
        agudizacion.ReaperturaAprobadaPor = solicitud.ResueltoPorNombre;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("CENSO_CRONICO_REAPERTURA_APROBADA", "CensoCronicoAgudizacion",
            $"Paciente: {agudizacion.CensoCronicoRecord.NombrePaciente}, Doc: {agudizacion.CensoCronicoRecord.NumeroIdentificacion}, Agudización: #{agudizacion.Numero}",
            currentUserId == Guid.Empty ? null : currentUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Json(new
        {
            message = "Reapertura aprobada. El kardex quedo habilitado para edicion.",
            agudizacionId = agudizacion.Id
        });
    }

    [HttpPost]
    [Authorize(Policy = SystemPermissions.Aprobacion)]
    public async Task<IActionResult> RechazarReaperturaKardexCronico(
        long solicitudId,
        string? observacion,
        CancellationToken cancellationToken = default)
    {
        var solicitud = await _context.CensoCronicoKardexReaperturas
            .Include(r => r.CensoCronicoAgudizacion)
            .ThenInclude(a => a.CensoCronicoRecord)
            .FirstOrDefaultAsync(r => r.Id == solicitudId && r.Estado == ReaperturaKardexEstado.Pendiente, cancellationToken);
        if (solicitud is null) return NotFound(new { message = "Solicitud de reapertura no encontrada o ya gestionada." });

        var currentUserId = GetCurrentUserIdOrEmpty();
        solicitud.Estado = ReaperturaKardexEstado.Rechazada;
        solicitud.ResueltoPorUserId = currentUserId == Guid.Empty ? null : currentUserId;
        solicitud.ResueltoPorNombre = await ResolveUserDisplayNameAsync(currentUserId, cancellationToken);
        solicitud.ResueltoAtUtc = DateTime.UtcNow;
        var obs = observacion?.Trim();
        solicitud.ObservacionResolucion = string.IsNullOrWhiteSpace(obs) ? null : obs;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("CENSO_CRONICO_REAPERTURA_RECHAZADA", "CensoCronicoAgudizacion",
            $"Paciente: {solicitud.CensoCronicoAgudizacion.CensoCronicoRecord.NombrePaciente}, Doc: {solicitud.CensoCronicoAgudizacion.CensoCronicoRecord.NumeroIdentificacion}, Agudización: #{solicitud.CensoCronicoAgudizacion.Numero}",
            currentUserId == Guid.Empty ? null : currentUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Json(new { message = "Solicitud de reapertura rechazada." });
    }

    private static object? MapCronicoReaperturaDto(CensoCronicoKardexReapertura? solicitud)
    {
        if (solicitud is null) return null;
        return new
        {
            id = solicitud.Id,
            estado = solicitud.Estado,
            motivo = solicitud.Motivo,
            solicitante = solicitud.SolicitadoPorNombre,
            solicitadaAt = solicitud.SolicitadoAtUtc
        };
    }

    private async Task<string> SendCronicoReaperturaSolicitudEmailAsync(
        CensoCronicoAgudizacion agudizacion,
        CensoCronicoKardexReapertura solicitud,
        CancellationToken cancellationToken)
    {
        var destino = Environment.GetEnvironmentVariable("REAPERTURA_GERENCIA_EMAIL")?.Trim();
        if (string.IsNullOrWhiteSpace(destino)) destino = GerenciaReaperturaEmailFallback;

        var record = agudizacion.CensoCronicoRecord;
        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(solicitud.SolicitadoAtUtc, ColombiaTimeZone);
        var paciente = WebUtility.HtmlEncode(record.NombrePaciente ?? string.Empty);
        var cedula = WebUtility.HtmlEncode($"{record.TipoIdentificacion} {record.NumeroIdentificacion}".Trim());
        var usuario = WebUtility.HtmlEncode(solicitud.SolicitadoPorNombre);
        var motivo = WebUtility.HtmlEncode(solicitud.Motivo);
        var fechaTexto = WebUtility.HtmlEncode(fechaLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));

        var html = $@"<div style=""font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937;"">
  <h2 style=""color:#b91c1c;margin-bottom:4px;"">Solicitud de reapertura de kardex - Programa Crónicos</h2>
  <p>Se ha solicitado la reapertura del kardex de la <strong>agudización #{agudizacion.Numero}</strong> del siguiente paciente:</p>
  <table style=""border-collapse:collapse;"">
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Paciente:</strong></td><td>{paciente}</td></tr>
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Documento:</strong></td><td>{cedula}</td></tr>
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Censo:</strong></td><td>Programa Crónicos - Agudización #{agudizacion.Numero}</td></tr>
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Solicitado por:</strong></td><td>{usuario}</td></tr>
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Motivo:</strong></td><td>{motivo}</td></tr>
    <tr><td style=""padding:4px 12px 4px 0;""><strong>Fecha solicitud:</strong></td><td>{fechaTexto}</td></tr>
  </table>
  <p style=""margin-top:16px;"">Por favor gestione la <strong>aprobacion o rechazo</strong> de esta reapertura en la intranet <strong>Nexa</strong>.</p>
</div>";

        var message = new EmailMessage
        {
            To = new[] { destino },
            Subject = $"Solicitud de reapertura de kardex (Crónicos) - {record.NombrePaciente}",
            HtmlBody = html
        };

        try
        {
            var result = await _emailService.SendAsync(message, cancellationToken);
            if (!result.Succeeded)
            {
                _logger.LogWarning("No se pudo enviar el correo de reapertura de crónicos: {Error}", result.ErrorMessage);
                return "No fue posible enviar el correo a gerencia, pero la solicitud quedo registrada.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando el correo de reapertura de kardex de crónicos.");
            return "No fue posible enviar el correo a gerencia, pero la solicitud quedo registrada.";
        }

        return string.Empty;
    }
}
