using System.Globalization;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntranetPrueba.Data.Entities;
using IntranetPrueba.Models.ViewModels;
using IntranetPrueba.Services.Models;
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
    private static readonly string[] CronicoBarthelAuditadoValues = ["Si", "No", "Sin dato"];
    private static readonly string[] CronicoSiNoValues = ["Si", "No"];
    private static readonly string[] CronicoEstadoClinicaHeridasValues = ["Activo", "Inactivo"];
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
            ApplyValidacionesToRecord(record, model);
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
            ApplyValidacionesToRecord(record, model);
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

        return GuardarCronicoSeccionAsync(
            model,
            validateSection: ValidateCronicoGestionCaso,
            applySectionToRecord: ApplyGestionCasoToRecord,
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar la gestión del caso.",
            auditAction: "CENSO_CRONICO_GESTION_CASO_ACTUALIZADA",
            successMessage: "Gestión del caso guardada correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarCronicoValidaciones(CensoCronicoViewModel model, CancellationToken cancellationToken)
    {
        NormalizeCronicoValidacionesFields(model);

        return GuardarCronicoSeccionAsync(
            model,
            validateSection: ValidateCronicoValidaciones,
            applySectionToRecord: ApplyValidacionesToRecord,
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar las validaciones.",
            auditAction: "CENSO_CRONICO_VALIDACIONES_ACTUALIZADAS",
            successMessage: "Validaciones guardadas correctamente.",
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
    public IActionResult BuscarMotivoAgudizacion(string codigo)
    {
        var normalizedCode = NormalizeCronicoCatalogCode(codigo);
        if (string.IsNullOrWhiteSpace(normalizedCode)
            || !_motivoAgudizacionCatalog.TryGetValue(normalizedCode, out var item))
        {
            return Json(new
            {
                found = false,
                codigo = normalizedCode,
                descripcion = string.Empty,
                grupo = string.Empty
            });
        }

        return Json(new
        {
            found = true,
            codigo = normalizedCode,
            descripcion = item.Descripcion,
            grupo = item.Grupo
        });
    }

    private CensoCronicoViewModel BuildDefaultCronicoModel()
    {
        return new CensoCronicoViewModel
        {
            FechaIngreso = GetColombiaNow().Date,
            FechaNacimiento = GetColombiaNow().Date,
            DireccionEsValida = false
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
        model.SiNoOptions = BuildOptions(CronicoSiNoValues);
        model.EstadoClinicaHeridasOptions = BuildOptions(CronicoEstadoClinicaHeridasValues);
        model.AuxiliarEnfermeriaOptions = await GetOpsAssistantOptionsAsync(cancellationToken);
        model.TallaPanalesOptions = BuildOptions(CronicoTallaValues);
        model.EstadoMipresOptions = BuildOptions(CronicoEstadoMipresValues);
        model.MotivoEgresoOptions = BuildOptions(CronicoMotivoEgresoValues);

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
        await PopulateCronicoLatestRecordsAsync(model, cancellationToken);
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
        model.MotivoAgudizacion = NormalizeCronicoCatalogCode(model.MotivoAgudizacion);
        if (!string.IsNullOrWhiteSpace(model.MotivoAgudizacion)
            && _motivoAgudizacionCatalog.TryGetValue(model.MotivoAgudizacion, out var motivo))
        {
            model.DescripcionAgudizacion = motivo.Descripcion;
            model.DetalleDescripcionCie10 = motivo.Grupo;
        }
        else if (string.IsNullOrWhiteSpace(model.MotivoAgudizacion))
        {
            model.DescripcionAgudizacion = null;
            model.DetalleDescripcionCie10 = null;
        }

        model.DiagnosticoCronicoCie10 = NormalizeCie10(model.DiagnosticoCronicoCie10);
        if (!string.IsNullOrWhiteSpace(model.DiagnosticoCronicoCie10)
            && _cie10Catalog.TryGetValue(model.DiagnosticoCronicoCie10, out var diag))
        {
            model.GrupoPatologiaCronica = diag;
        }
        else if (string.IsNullOrWhiteSpace(model.DiagnosticoCronicoCie10))
        {
            model.GrupoPatologiaCronica = null;
        }

        model.DiagnosticoCronicoComplementario = NormalizeCie10(model.DiagnosticoCronicoComplementario);
        if (!string.IsNullOrWhiteSpace(model.DiagnosticoCronicoComplementario)
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
        NormalizeCronicoValidacionesFields(model);
    }

    private void NormalizeCronicoGestionCasoFields(CensoCronicoViewModel model)
    {
        model.ClasificacionCaso = NormalizeOptionalSelect(model.ClasificacionCaso);
        model.EstadoPaciente = NormalizeOptionalSelect(model.EstadoPaciente);
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

    private void NormalizeCronicoValidacionesFields(CensoCronicoViewModel model)
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
        model.MotivoHospitalizacion = NormalizeOptionalCronicoText(model.MotivoHospitalizacion);
        model.RemitidoPor = NormalizeOptionalCronicoText(model.RemitidoPor);
        model.IpsIntramural = NormalizeOptionalCronicoText(model.IpsIntramural);
        model.EgresaProgramaCronico = NormalizeOptionalSelect(model.EgresaProgramaCronico);
        model.MotivoEgreso = NormalizeOptionalSelect(model.MotivoEgreso);

        if (!string.Equals(model.ClinicaHeridas, "Si", StringComparison.OrdinalIgnoreCase))
        {
            model.EstadoClinicaHeridas = null;
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

    private static string NormalizeCronicoCatalogCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = new string(value.Trim().Where(char.IsLetterOrDigit).ToArray());
        return cleaned.ToUpperInvariant();
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
        ValidateCronicoValidaciones(model);
    }

    private void ValidateCronicoGestionCaso(CensoCronicoViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.ClasificacionCaso)
            && !CronicoClasificacionCasoValues.Contains(model.ClasificacionCaso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ClasificacionCaso), "Selecciona una clasificación del caso válida.");
        }

        if (!string.IsNullOrWhiteSpace(model.EstadoPaciente)
            && !CronicoEstadoPacienteValues.Contains(model.EstadoPaciente, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoPaciente), "Selecciona un estado del paciente válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.BarthelAuditado)
            && !CronicoBarthelAuditadoValues.Contains(model.BarthelAuditado, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.BarthelAuditado), "Selecciona un valor válido para Barthel auditado.");
        }

        if (!string.IsNullOrWhiteSpace(model.MotivoAgudizacion)
            && !_motivoAgudizacionCatalog.ContainsKey(model.MotivoAgudizacion))
        {
            ModelState.AddModelError(nameof(model.MotivoAgudizacion), "El motivo de agudización ingresado no existe en el catálogo parametrizado.");
        }

        if (!string.IsNullOrWhiteSpace(model.DiagnosticoCronicoCie10)
            && !_cie10Catalog.ContainsKey(model.DiagnosticoCronicoCie10))
        {
            ModelState.AddModelError(nameof(model.DiagnosticoCronicoCie10), "El diagnóstico crónico CIE10 no existe en el catálogo parametrizado.");
        }

        if (!string.IsNullOrWhiteSpace(model.DiagnosticoCronicoComplementario)
            && !_cie10Catalog.ContainsKey(model.DiagnosticoCronicoComplementario))
        {
            ModelState.AddModelError(nameof(model.DiagnosticoCronicoComplementario), "El diagnóstico crónico complementario no existe en el catálogo parametrizado.");
        }
    }

    private void ValidateCronicoValidaciones(CensoCronicoViewModel model)
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
        ValidateCronicoSiNo(model.EgresaProgramaCronico, nameof(model.EgresaProgramaCronico), "egresa programa crónico");

        if (!string.IsNullOrWhiteSpace(model.EstadoClinicaHeridas)
            && !CronicoEstadoClinicaHeridasValues.Contains(model.EstadoClinicaHeridas, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoClinicaHeridas), "Selecciona un estado en clínica de heridas válido.");
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

        if (!string.IsNullOrWhiteSpace(model.MotivoEgreso)
            && !CronicoMotivoEgresoValues.Contains(model.MotivoEgreso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoEgreso), "Selecciona un motivo de egreso válido.");
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
        record.EstadoPaciente = model.EstadoPaciente;
        record.NumeroAgudizacionesUltimoAnio = model.NumeroAgudizacionesUltimoAnio;
        record.FechaAgudizacion = model.FechaAgudizacion?.Date;
        record.MotivoAgudizacion = string.IsNullOrWhiteSpace(model.MotivoAgudizacion) ? null : model.MotivoAgudizacion;
        record.DescripcionAgudizacion = model.DescripcionAgudizacion;
        record.DetalleDescripcionCie10 = model.DetalleDescripcionCie10;
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

    private static void ApplyValidacionesToRecord(CensoCronicoRecord record, CensoCronicoViewModel model)
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
        record.FechaHospitalizacion = model.FechaHospitalizacion?.Date;
        record.MotivoHospitalizacion = model.MotivoHospitalizacion;
        record.RemitidoPor = model.RemitidoPor;
        record.IpsIntramural = model.IpsIntramural;
        record.FechaPrimerSeguimiento24Horas = model.FechaPrimerSeguimiento24Horas?.Date;
        record.FechaSegundoSeguimiento48Horas = model.FechaSegundoSeguimiento48Horas?.Date;
        record.FechaTercerSeguimiento72Horas = model.FechaTercerSeguimiento72Horas?.Date;
        record.FechaCuartoSeguimientoSemana1 = model.FechaCuartoSeguimientoSemana1?.Date;
        record.FechaQuintoSeguimientoSemana2 = model.FechaQuintoSeguimientoSemana2?.Date;
        record.FechaSextoSeguimientoSemana3 = model.FechaSextoSeguimientoSemana3?.Date;
        record.FechaSeptimoSeguimientoSemana4 = model.FechaSeptimoSeguimientoSemana4?.Date;
        record.FechaAltaHospitalizacion = model.FechaAltaHospitalizacion?.Date;
        record.NumeroHospitalizacionesUltimoAnio = model.NumeroHospitalizacionesUltimoAnio;
        record.EgresaProgramaCronico = model.EgresaProgramaCronico;
        record.MotivoEgreso = model.MotivoEgreso;
        record.FechaEgreso = model.FechaEgreso?.Date;
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
        model.NumeroAgudizacionesUltimoAnio = record.NumeroAgudizacionesUltimoAnio;
        model.FechaAgudizacion = record.FechaAgudizacion?.Date;
        model.MotivoAgudizacion = record.MotivoAgudizacion;
        model.DescripcionAgudizacion = record.DescripcionAgudizacion;
        model.DetalleDescripcionCie10 = record.DetalleDescripcionCie10;
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

        model.ClinicaHeridas = record.ClinicaHeridas;
        model.EstadoClinicaHeridas = record.EstadoClinicaHeridas;
        model.ProgramaNutricion = record.ProgramaNutricion;
        model.FechaInicioNutricion = record.FechaInicioNutricion?.Date;
        model.AuxiliarAsignadoNutricion = record.AuxiliarAsignadoNutricion;
        model.FechaFinNutricion = record.FechaFinNutricion?.Date;
        model.EducacionPlanCuidados = record.EducacionPlanCuidados;
        model.TerapiaFisica = record.TerapiaFisica;
        model.TerapiaRespiratoria = record.TerapiaRespiratoria;
        model.TerapiaOcupacional = record.TerapiaOcupacional;
        model.Fonoaudiologia = record.Fonoaudiologia;
        model.Nutricion = record.Nutricion;
        model.Psicologia = record.Psicologia;
        model.Traqueostomia = record.Traqueostomia;
        model.SondaNasogastrica = record.SondaNasogastrica;
        model.CalibreSondaNasogastrica = record.CalibreSondaNasogastrica;
        model.FrecuenciaCambioSondaNasogastrica = record.FrecuenciaCambioSondaNasogastrica;
        model.FechaUltimoCambioSondaNasogastrica = record.FechaUltimoCambioSondaNasogastrica?.Date;
        model.SondaGastrostomia = record.SondaGastrostomia;
        model.Colostomia = record.Colostomia;
        model.SondaCistostomia = record.SondaCistostomia;
        model.CateterPicc = record.CateterPicc;
        model.SondaVesical = record.SondaVesical;
        model.CalibreSondaVesical = record.CalibreSondaVesical;
        model.FrecuenciaCambioSondaVesical = record.FrecuenciaCambioSondaVesical;
        model.FechaUltimoCambioSondaVesical = record.FechaUltimoCambioSondaVesical?.Date;
        model.FechaProximoCambioSondaVesical = record.FechaProximoCambioSondaVesical?.Date;
        model.ObservacionCambioSonda = record.ObservacionCambioSonda;
        model.FormulaControl = record.FormulaControl;
        model.MipresPanales = record.MipresPanales;
        model.TallaPanales = record.TallaPanales;
        model.FechaUltimaPrescripcionPanales = record.FechaUltimaPrescripcionPanales?.Date;
        model.TiempoPrescripcionPanalesMeses = record.TiempoPrescripcionPanalesMeses;
        model.EstadoMipresPanales = record.EstadoMipresPanales;
        model.MipresNutricion = record.MipresNutricion;
        model.FechaUltimaPrescripcionNutricion = record.FechaUltimaPrescripcionNutricion?.Date;
        model.TiempoPrescripcionNutricionMeses = record.TiempoPrescripcionNutricionMeses;
        model.EstadoMipresNutricion = record.EstadoMipresNutricion;
        model.FechaHospitalizacion = record.FechaHospitalizacion?.Date;
        model.MotivoHospitalizacion = record.MotivoHospitalizacion;
        model.RemitidoPor = record.RemitidoPor;
        model.IpsIntramural = record.IpsIntramural;
        model.FechaPrimerSeguimiento24Horas = record.FechaPrimerSeguimiento24Horas?.Date;
        model.FechaSegundoSeguimiento48Horas = record.FechaSegundoSeguimiento48Horas?.Date;
        model.FechaTercerSeguimiento72Horas = record.FechaTercerSeguimiento72Horas?.Date;
        model.FechaCuartoSeguimientoSemana1 = record.FechaCuartoSeguimientoSemana1?.Date;
        model.FechaQuintoSeguimientoSemana2 = record.FechaQuintoSeguimientoSemana2?.Date;
        model.FechaSextoSeguimientoSemana3 = record.FechaSextoSeguimientoSemana3?.Date;
        model.FechaSeptimoSeguimientoSemana4 = record.FechaSeptimoSeguimientoSemana4?.Date;
        model.FechaAltaHospitalizacion = record.FechaAltaHospitalizacion?.Date;
        model.NumeroHospitalizacionesUltimoAnio = record.NumeroHospitalizacionesUltimoAnio;
        model.EgresaProgramaCronico = record.EgresaProgramaCronico;
        model.MotivoEgreso = record.MotivoEgreso;
        model.FechaEgreso = record.FechaEgreso?.Date;
    }

    private static IReadOnlyDictionary<string, MotivoAgudizacionCatalogItem> LoadMotivoAgudizacionCatalog(string contentRootPath)
    {
        var path = Path.Combine(contentRootPath, "Data", "Seed", "motivo_agudizacion_catalog.json");
        if (!System.IO.File.Exists(path))
        {
            return new Dictionary<string, MotivoAgudizacionCatalogItem>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = System.IO.File.ReadAllText(path, Encoding.UTF8);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, MotivoAgudizacionCatalogItem>>(json)
                ?? new Dictionary<string, MotivoAgudizacionCatalogItem>();

            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value is not null)
                .GroupBy(x => NormalizeCronicoCatalogCode(x.Key), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, MotivoAgudizacionCatalogItem>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed class MotivoAgudizacionCatalogItem
    {
        [JsonPropertyName("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [JsonPropertyName("grupo")]
        public string Grupo { get; set; } = string.Empty;
    }
}
