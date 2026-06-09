using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using IntranetPrueba.Data;
using IntranetPrueba.Data.Entities;
using IntranetPrueba.Models.ViewModels;
using IntranetPrueba.Services.Interfaces;
using IntranetPrueba.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Services;

public class FarmaciaDispatchNotificationService : IFarmaciaDispatchNotificationService
{
    private const string MedicosKardexEmail = "medicos@especialistasencasa.com";
    private const string ValorNoAplicaMedicamentoAdicional = "No";
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IUserAdministrationService _userAdministrationService;

    public FarmaciaDispatchNotificationService(
        ApplicationDbContext context,
        IEmailService emailService,
        IUserAdministrationService userAdministrationService)
    {
        _context = context;
        _emailService = emailService;
        _userAdministrationService = userAdministrationService;
    }

    public async Task<IReadOnlyList<string>> NotifyDispatchSentAsync(CensoRecord record, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var documents = await BuildDocumentModelsAsync(record, cancellationToken);

        var adjuntos = await _context.CensoAdjuntos
            .AsNoTracking()
            .Where(a => a.CensoRecordId == record.Id)
            .OrderBy(a => a.UploadedAtUtc)
            .ToListAsync(cancellationToken);

        var doctorsAttachments = new List<EmailAttachment> { BuildKardexAttachment(record, documents.Kardex) };
        foreach (var adj in adjuntos)
        {
            doctorsAttachments.Add(new EmailAttachment
            {
                FileName = adj.FileName,
                ContentType = "application/pdf",
                Content = adj.FileData
            });
        }

        var doctorsResult = await _emailService.SendAsync(new EmailMessage
        {
            To = [MedicosKardexEmail],
            Subject = BuildDoctorsKardexSubject(record),
            HtmlBody = $"""
                <p>Hola,</p>
                <p>Se deja adjunto el Kardex del paciente <strong>{HtmlEncode(record.NombrePaciente)}</strong>.</p>
                <p>Documento: {HtmlEncode(record.TipoIdentificacion)} {HtmlEncode(record.NumeroIdentificacion)}</p>
                """,
            Attachments = doctorsAttachments
        }, cancellationToken);

        if (!doctorsResult.Succeeded)
        {
            warnings.Add($"No se pudo enviar Kardex a medicos: {doctorsResult.ErrorMessage}");
        }

        if (string.IsNullOrWhiteSpace(record.AuxiliarAsignado))
        {
            return warnings;
        }

        var assistantWarnings = await SendAssistantDocumentsAsync(record, documents, adjuntos, cancellationToken);
        warnings.AddRange(assistantWarnings);
        return warnings;
    }

    public async Task<IReadOnlyList<string>> NotifyAssistantAssignedAsync(CensoRecord record, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(record.AuxiliarAsignado))
        {
            return [];
        }

        var documents = await BuildDocumentModelsAsync(record, cancellationToken);
        var adjuntos = await _context.CensoAdjuntos
            .AsNoTracking()
            .Where(a => a.CensoRecordId == record.Id)
            .OrderBy(a => a.UploadedAtUtc)
            .ToListAsync(cancellationToken);
        return await SendAssistantDocumentsAsync(record, documents, adjuntos, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> SendAssistantDocumentsAsync(
        CensoRecord record,
        DispatchDocumentModels documents,
        IReadOnlyList<Data.Entities.CensoAdjunto> adjuntos,
        CancellationToken cancellationToken)
    {
        var assistantEmail = await GetAssignedAssistantEmailAsync(record.AuxiliarAsignado, cancellationToken);
        if (string.IsNullOrWhiteSpace(assistantEmail))
        {
            return ["No se encontro correo del auxiliar asignado."];
        }

        var attachments = new List<EmailAttachment>
        {
            BuildKardexAttachment(record, documents.Kardex),
            BuildRequisitionAttachment(record, documents.Requisition)
        };

        foreach (var adj in adjuntos)
        {
            attachments.Add(new EmailAttachment
            {
                FileName = adj.FileName,
                ContentType = "application/pdf",
                Content = adj.FileData
            });
        }

        var result = await _emailService.SendAsync(new EmailMessage
        {
            To = [assistantEmail],
            Subject = BuildAssistantEmailSubject(record),
            HtmlBody = BuildAssistantEmailBody(record),
            Attachments = attachments
        }, cancellationToken);

        return result.Succeeded
            ? []
            : [$"No se pudo enviar correo al auxiliar: {result.ErrorMessage}"];
    }

    private async Task<DispatchDocumentModels> BuildDocumentModelsAsync(CensoRecord record, CancellationToken cancellationToken)
    {
        var medications = await _context.Medicamentos
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        return new DispatchDocumentModels(
            BuildDocumentModel(record, medications, "kardex"),
            BuildDocumentModel(record, medications, "requisicion"));
    }

    private static FarmaciaDocumentViewModel BuildDocumentModel(
        CensoRecord record,
        IReadOnlyList<Medicamento> medicamentos,
        string tipoDocumento)
    {
        var medicationCatalog = medicamentos
            .GroupBy(x => NormalizeCatalogKey(x.Nombre), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var fechaInicio = FormatNullableDate(record.FechaInicioTratamiento);
        var fechaFin = FormatNullableDate(record.FechaFinTratamiento);
        var isolationValue = string.Equals(record.Aislamiento, "Si", StringComparison.OrdinalIgnoreCase)
            ? record.TipoAislamiento ?? "Si"
            : "No";

        var model = new FarmaciaDocumentViewModel
        {
            Id = record.Id,
            TipoDocumento = tipoDocumento,
            NombrePaciente = record.NombrePaciente,
            TipoIdentificacion = record.TipoIdentificacion,
            NumeroIdentificacion = record.NumeroIdentificacion,
            Asegurador = record.Asegurador,
            DiagnosticoDescriptivo = record.DiagnosticoDescriptivo,
            CodigoCie10 = record.CodigoCie10,
            Edad = record.Edad,
            Direccion = record.Direccion,
            Telefonos = string.Join(" / ", new[] { record.Telefono1, record.Telefono2, record.Telefono3 }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Observaciones = record.ObservacionesPlanManejo,
            ResponsableLlamadaBienvenida = record.ResponsableLlamadaBienvenida,
            AuxiliarAsignado = record.AuxiliarAsignado,
            NombreRealizaKardex = record.NombreRealizaKardex,
            Autorizacion = record.AutorizacionEvento,
            Medicamentos =
            [
                BuildMedicationRow(1, record.NombreMedicamentoPrincipalTratante, record.DosisMedicamentoPrincipal, record.MedidaMedicamentoPrincipal, record.ViaAdministracionMedicamentoPrincipal, record.FrecuenciaAdministracionMxPrincipal, record.DiasMedicamentoPrincipal, fechaInicio, fechaFin, isolationValue, medicationCatalog),
                BuildMedicationRow(2, record.NombreMedicamentoNumero2, record.DosisMedicamento2, record.MedidaMedicamento2, record.ViaAdministracionMedicamento2, record.FrecuenciaAdministracionMedicamento2, record.DiasMedicamento2, fechaInicio, fechaFin, isolationValue, medicationCatalog),
                BuildMedicationRow(3, record.NombreMedicamentoNumero3, record.DosisMedicamento3, record.MedidaMedicamento3, record.ViaAdministracionMedicamento3, record.FrecuenciaAdministracionMedicamento3, record.DiasMedicamento3, fechaInicio, fechaFin, isolationValue, medicationCatalog)
            ],
            RequisicionItems = BuildRequisitionRows(record, medicationCatalog, fechaInicio, fechaFin)
        };

        if (tipoDocumento == "requisicion")
        {
            ApplyStoredRequisition(model, record.RequisicionFarmaciaJson);
        }

        return model;
    }

    private static FarmaciaKardexMedicationViewModel BuildMedicationRow(
        int row,
        string? name,
        decimal? dose,
        string? measure,
        string? route,
        string? frequency,
        int? days,
        string fechaInicio,
        string fechaFin,
        string isolationValue,
        IReadOnlyDictionary<string, Medicamento> medicationCatalog)
    {
        var hasMedication = HasMedication(name);
        medicationCatalog.TryGetValue(NormalizeCatalogKey(name), out var metadata);
        var presentacion = hasMedication
            ? string.Join("\n", new[] { name, metadata?.PresentacionRequisicion }.Where(x => !string.IsNullOrWhiteSpace(x)))
            : $"Medicamento {row}";

        return new FarmaciaKardexMedicationViewModel
        {
            Row = row,
            Presentacion = presentacion,
            DosisFrecuencia = BuildDoseText(dose, measure, route, frequency, days),
            VehiculoReconstitucion = metadata?.VehiculoReconstitucion,
            VolumenDilucion = metadata?.DilucionRecomendada,
            TiempoInfusion = FormatInfusionTime(metadata),
            Fotosensible = metadata?.EquipoFotosensible,
            CadenaFrio = metadata?.CadenaFrio,
            Aislamiento = hasMedication ? isolationValue : string.Empty,
            Estabilidad = metadata?.TiempoEstabilidad,
            BombaInfusion = metadata?.BombaInfusion,
            FechaInicio = hasMedication ? fechaInicio : string.Empty,
            FechaFin = hasMedication ? fechaFin : string.Empty
        };
    }

    private static IReadOnlyList<FarmaciaRequisicionItemViewModel> BuildRequisitionRows(
        CensoRecord record,
        IReadOnlyDictionary<string, Medicamento> medicationCatalog,
        string fechaInicio,
        string fechaFin)
    {
        var medications = new[]
        {
            new RequisitionMedication(record.NombreMedicamentoPrincipalTratante, record.DosisMedicamentoPrincipal, record.FrecuenciaAdministracionMxPrincipal, record.NumeroDosisDiaMedicamentoPrincipal, record.DiasMedicamentoPrincipal),
            new RequisitionMedication(record.NombreMedicamentoNumero2, record.DosisMedicamento2, record.FrecuenciaAdministracionMedicamento2, record.NumeroDosisMedicamento2, record.DiasMedicamento2),
            new RequisitionMedication(record.NombreMedicamentoNumero3, record.DosisMedicamento3, record.FrecuenciaAdministracionMedicamento3, record.NumeroDosisMedicamento3, record.DiasMedicamento3)
        }
            .Where(x => HasMedication(x.Name))
            .ToList();

        var rows = new List<FarmaciaRequisicionItemViewModel>();
        foreach (var medication in medications)
        {
            rows.Add(new FarmaciaRequisicionItemViewModel
            {
                Descripcion = medication.Name!.Trim(),
                Cantidad = FormatQuantity(CalculateMedicationQuantity(medication)),
                FechaInicio = fechaInicio,
                FechaFin = fechaFin
            });
        }

        foreach (var detail in medications
            .Select(x =>
            {
                medicationCatalog.TryGetValue(NormalizeCatalogKey(x.Name), out var metadata);
                return metadata?.SolucionParaDilucion;
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new FarmaciaRequisicionItemViewModel
            {
                Descripcion = "SOLUCION PARA DILUCION",
                Detalle = detail,
                FechaInicio = fechaInicio,
                FechaFin = fechaFin
            });
        }

        var totalApplications = ParseDecimal(record.AplicacionesTotales);
        foreach (var detail in medications
            .Select(x =>
            {
                medicationCatalog.TryGetValue(NormalizeCatalogKey(x.Name), out var metadata);
                return metadata?.Jeringa;
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new FarmaciaRequisicionItemViewModel
            {
                Descripcion = "JERINGA",
                Detalle = detail,
                Cantidad = FormatQuantity(totalApplications),
                FechaInicio = fechaInicio,
                FechaFin = fechaFin
            });
        }

        var treatmentDays = ParseDecimal(record.DiasTratamientoIv);
        rows.Add(new() { Descripcion = "JELCO #22", Cantidad = FormatQuantity(CalculateEveryThreeDaysQuantity(treatmentDays, true)), FechaInicio = fechaInicio, FechaFin = fechaFin });
        rows.Add(new() { Descripcion = "ATI", Cantidad = FormatQuantity(CalculateEveryThreeDaysQuantity(treatmentDays)), FechaInicio = fechaInicio, FechaFin = fechaFin });
        rows.Add(new() { Descripcion = "MACROGOTERO", Cantidad = FormatQuantity(CalculateEveryThreeDaysQuantity(treatmentDays)), FechaInicio = fechaInicio, FechaFin = fechaFin });
        rows.Add(new() { Descripcion = "BURETRA", Cantidad = FormatQuantity(CalculateEveryThreeDaysQuantity(treatmentDays)), FechaInicio = fechaInicio, FechaFin = fechaFin });
        rows.Add(new() { Descripcion = "GUANTES (PAR)", Cantidad = FormatQuantity(totalApplications), FechaInicio = fechaInicio, FechaFin = fechaFin });

        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].Item = i + 1;
        }

        return rows;
    }

    private static EmailAttachment BuildKardexAttachment(CensoRecord record, FarmaciaDocumentViewModel model)
    {
        return new EmailAttachment
        {
            FileName = $"Kardex_{SanitizeFileName(record.TipoIdentificacion)}_{SanitizeFileName(record.NumeroIdentificacion)}.html",
            ContentType = "text/html",
            Content = Encoding.UTF8.GetBytes(BuildKardexAttachmentHtml(model))
        };
    }

    private static EmailAttachment BuildRequisitionAttachment(CensoRecord record, FarmaciaDocumentViewModel model)
    {
        return new EmailAttachment
        {
            FileName = $"Requisicion_{SanitizeFileName(record.TipoIdentificacion)}_{SanitizeFileName(record.NumeroIdentificacion)}.html",
            ContentType = "text/html",
            Content = Encoding.UTF8.GetBytes(BuildRequisitionAttachmentHtml(model))
        };
    }

    private async Task<string?> GetAssignedAssistantEmailAsync(string? assistantName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assistantName))
        {
            return null;
        }

        var normalizedName = NormalizeCatalogKey(assistantName);
        var assistants = await _userAdministrationService.GetOpsAssistantsAsync(onlyActive: true, cancellationToken);
        return assistants
            .FirstOrDefault(x => NormalizeCatalogKey(x.Name) == normalizedName)
            ?.Email
            ?.Trim();
    }

    private static string BuildAssistantEmailSubject(CensoRecord record)
    {
        return $"Despacho farmacia - {record.TipoIdentificacion} {record.NumeroIdentificacion} - {record.NombrePaciente}";
    }

    private static string BuildAssistantEmailBody(CensoRecord record)
    {
        return $"""
            <p>Hola,</p>
            <p>Se comparte el Kardex y la Requisicion del despacho de farmacia asociado al paciente <strong>{HtmlEncode(record.NombrePaciente)}</strong>.</p>
            <p><strong>Tipo documento:</strong> {HtmlEncode(record.TipoIdentificacion)}</p>
            <p><strong>Documento:</strong> {HtmlEncode(record.NumeroIdentificacion)}</p>
            <p><strong>Direccion:</strong> {HtmlEncode(record.Direccion)}</p>
            <p><strong>Telefonos:</strong> {HtmlEncode(string.Join(" / ", new[] { record.Telefono1, record.Telefono2, record.Telefono3 }.Where(x => !string.IsNullOrWhiteSpace(x))))}</p>
            <p>Se adjuntan los documentos para consulta.</p>
            """;
    }

    private static string BuildDoctorsKardexSubject(CensoRecord record)
    {
        return $"{record.TipoIdentificacion} {record.NumeroIdentificacion}, {record.NombrePaciente} - Kardex";
    }

    private static string BuildKardexAttachmentHtml(FarmaciaDocumentViewModel model)
    {
        var medicationRows = string.Join(Environment.NewLine, model.Medicamentos.Select(row => $"""
            <tr><td>{HtmlEncode(row.Presentacion)}</td><td>{HtmlEncode(row.DosisFrecuencia)}</td><td>{HtmlEncode(row.VehiculoReconstitucion)}</td><td>{HtmlEncode(row.VolumenDilucion)}</td><td>{HtmlEncode(row.TiempoInfusion)}</td><td>{HtmlEncode(row.Fotosensible)}</td><td>{HtmlEncode(row.CadenaFrio)}</td><td>{HtmlEncode(row.Aislamiento)}</td><td>{HtmlEncode(row.Estabilidad)}</td><td>{HtmlEncode(row.BombaInfusion)}</td><td>{HtmlEncode(row.FechaInicio)}</td><td>{HtmlEncode(row.FechaFin)}</td></tr>
            """));

        return $$"""
            <!doctype html><html lang="es"><head><meta charset="utf-8" />
            <style>body{font-family:Arial,Helvetica,sans-serif;color:#111827}h1{font-size:18px;text-align:center}table{border-collapse:collapse;width:100%;margin-bottom:14px}th,td{border:1px solid #111827;padding:5px;font-size:11px;vertical-align:top;white-space:pre-line}th{background:#ff100b;color:#fff}</style>
            </head><body>
            <h1>KARDEX APLICACION DE MEDICAMENTOS</h1>
            <table>
            <tr><th>Paciente</th><td>{{HtmlEncode(model.NombrePaciente)}}</td><th>Documento</th><td>{{HtmlEncode(model.TipoIdentificacion)}} {{HtmlEncode(model.NumeroIdentificacion)}}</td></tr>
            <tr><th>Asegurador</th><td>{{HtmlEncode(model.Asegurador)}}</td><th>CIE10</th><td>{{HtmlEncode(model.CodigoCie10)}}</td></tr>
            <tr><th>Direccion</th><td>{{HtmlEncode(model.Direccion)}}</td><th>Telefonos</th><td>{{HtmlEncode(model.Telefonos)}}</td></tr>
            <tr><th>Diagnostico</th><td colspan="3">{{HtmlEncode(model.DiagnosticoDescriptivo)}}</td></tr>
            </table>
            <table><thead><tr><th>Presentacion medicamentos</th><th>Dosis y frecuencia</th><th>Vehiculo</th><th>Volumen dilucion</th><th>Tiempo infusion</th><th>Fotosensible</th><th>Cadena frio</th><th>Aislamiento</th><th>Estabilidad</th><th>Bomba</th><th>Inicio</th><th>Fin</th></tr></thead><tbody>{{medicationRows}}</tbody></table>
            <table><tr><th>Observacion</th><td>{{HtmlEncode(model.Observaciones)}}</td></tr><tr><th>Auxiliar asignado</th><td>{{HtmlEncode(model.AuxiliarAsignado)}}</td></tr></table>
            </body></html>
            """;
    }

    private static string BuildRequisitionAttachmentHtml(FarmaciaDocumentViewModel model)
    {
        var rows = string.Join(Environment.NewLine, model.RequisicionItems.Select(row => $"""
            <tr><td>{HtmlEncode(row.Item.ToString(CultureInfo.InvariantCulture))}</td><td>{HtmlEncode(row.Descripcion)}</td><td>{HtmlEncode(row.Detalle)}</td><td>{HtmlEncode(row.Cantidad)}</td><td>{HtmlEncode(row.FechaInicio)}</td><td>{HtmlEncode(row.FechaFin)}</td></tr>
            """));

        return $$"""
            <!doctype html><html lang="es"><head><meta charset="utf-8" />
            <style>body{font-family:Arial,Helvetica,sans-serif;color:#111827}h1{font-size:18px;text-align:center}table{border-collapse:collapse;width:100%;margin-bottom:14px}th,td{border:1px solid #111827;padding:5px;font-size:11px;vertical-align:top;white-space:pre-line}th{background:#ff100b;color:#fff}</style>
            </head><body>
            <h1>REQUISICION APLICACION DE MEDICAMENTOS</h1>
            <table>
            <tr><th>Auxiliar encargado</th><td>{{HtmlEncode(model.AuxiliarAsignado)}}</td><th>Realizado por</th><td>{{HtmlEncode(model.NombreRealizaKardex)}}</td></tr>
            <tr><th>Paciente</th><td>{{HtmlEncode(model.NombrePaciente)}}</td><th>Autorizacion</th><td>{{HtmlEncode(model.Autorizacion)}}</td></tr>
            <tr><th>Documento</th><td>{{HtmlEncode(model.NumeroIdentificacion)}}</td><th>Fecha solicitud</th><td>{{HtmlEncode(model.FechaSolicitudRequisicion)}}</td></tr>
            <tr><th>Direccion</th><td>{{HtmlEncode(model.Direccion)}}</td><th>Telefonos</th><td>{{HtmlEncode(model.Telefonos)}}</td></tr>
            <tr><th>Diagnostico</th><td colspan="3">{{HtmlEncode($"{model.CodigoCie10} {model.DiagnosticoDescriptivo}".Trim())}}</td></tr>
            </table>
            <table><thead><tr><th>Item</th><th>Descripcion</th><th>Detalle</th><th>Cantidad</th><th>Fecha inicio</th><th>Fecha finalizacion</th></tr></thead><tbody>{{rows}}</tbody></table>
            </body></html>
            """;
    }

    private static void ApplyStoredRequisition(FarmaciaDocumentViewModel model, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        RequisitionPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<RequisitionPayload>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return;
        }

        if (payload is null)
        {
            return;
        }

        if (payload.Fields.TryGetValue("auxiliarEncargado", out var auxiliarEncargado))
        {
            model.AuxiliarAsignado = auxiliarEncargado;
        }

        if (payload.Fields.TryGetValue("realizadoPor", out var realizadoPor))
        {
            model.NombreRealizaKardex = realizadoPor;
        }

        if (payload.Fields.TryGetValue("paciente", out var paciente) && !string.IsNullOrWhiteSpace(paciente))
        {
            model.NombrePaciente = paciente;
        }

        if (payload.Fields.TryGetValue("autorizacion", out var autorizacion))
        {
            model.Autorizacion = autorizacion;
        }

        if (payload.Fields.TryGetValue("documento", out var documento) && !string.IsNullOrWhiteSpace(documento))
        {
            model.NumeroIdentificacion = documento;
        }

        if (payload.Fields.TryGetValue("fechaSolicitud", out var fechaSolicitud))
        {
            model.FechaSolicitudRequisicion = fechaSolicitud;
        }

        if (payload.Fields.TryGetValue("direccion", out var direccion))
        {
            model.Direccion = direccion ?? string.Empty;
        }

        if (payload.Fields.TryGetValue("telefonos", out var telefonos))
        {
            model.Telefonos = telefonos ?? string.Empty;
        }

        if (payload.Fields.TryGetValue("diagnostico", out var diagnostico))
        {
            model.CodigoCie10 = string.Empty;
            model.DiagnosticoDescriptivo = diagnostico ?? string.Empty;
        }

        var rows = payload.Rows
            .Select((row, index) => new FarmaciaRequisicionItemViewModel
            {
                Item = int.TryParse(row.Item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var item) && item > 0 ? item : index + 1,
                Descripcion = row.Descripcion ?? string.Empty,
                Detalle = row.Detalle,
                Cantidad = row.Cantidad ?? string.Empty,
                FechaInicio = row.FechaInicio ?? string.Empty,
                FechaFin = row.FechaFin ?? string.Empty
            })
            .Where(row =>
                !string.IsNullOrWhiteSpace(row.Descripcion)
                || !string.IsNullOrWhiteSpace(row.Detalle)
                || !string.IsNullOrWhiteSpace(row.Cantidad))
            .ToList();

        if (rows.Count > 0)
        {
            model.RequisicionItems = rows;
        }
    }

    private static bool HasMedication(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !string.Equals(value, ValorNoAplicaMedicamentoAdicional, StringComparison.OrdinalIgnoreCase);

    private static string BuildDoseText(decimal? dose, string? measure, string? route, string? frequency, int? days)
    {
        var doseText = dose.HasValue ? dose.Value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(doseText) || !string.IsNullOrWhiteSpace(measure))
        {
            parts.Add($"{doseText} {measure}".Trim());
        }

        if (!string.IsNullOrWhiteSpace(route))
        {
            parts.Add(route);
        }

        if (!string.IsNullOrWhiteSpace(frequency))
        {
            parts.Add(frequency);
        }

        if (days.HasValue)
        {
            parts.Add($"{days.Value.ToString(CultureInfo.InvariantCulture)} dias");
        }

        return string.Join(" / ", parts);
    }

    private static decimal CalculateMedicationQuantity(RequisitionMedication medication)
    {
        var dose = medication.Dose.GetValueOrDefault(1);
        if (dose <= 0)
        {
            dose = 1;
        }

        var dailyDoses = ParseDecimal(medication.DailyDoses);
        if (dailyDoses <= 0)
        {
            dailyDoses = ParseFrequencyPerDay(medication.Frequency);
        }

        var days = medication.Days.GetValueOrDefault();
        return days <= 0 ? 0 : dose * dailyDoses * days;
    }

    private static decimal CalculateEveryThreeDaysQuantity(decimal days, bool minimumTwoWhenLessThanOne = false)
    {
        if (days <= 0)
        {
            return minimumTwoWhenLessThanOne ? 2 : 0;
        }

        var raw = days / 3;
        return minimumTwoWhenLessThanOne && raw < 1 ? 2 : Math.Ceiling(raw);
    }

    private static decimal ParseFrequencyPerDay(string? frequency)
    {
        if (string.IsNullOrWhiteSpace(frequency))
        {
            return 0;
        }

        var normalized = frequency.Trim().ToUpperInvariant();
        var numbers = normalized.Split(new[] { ' ', '/', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => decimal.TryParse(part.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0)
            .Where(value => value > 0)
            .ToList();

        if (normalized.Contains("24") && normalized.Contains("HORA")) return 1;
        if (normalized.Contains("12") && normalized.Contains("HORA")) return 2;
        if (normalized.Contains("8") && normalized.Contains("HORA")) return 3;
        if (normalized.Contains("6") && normalized.Contains("HORA")) return 4;
        if (normalized.Contains("4") && normalized.Contains("HORA")) return 6;
        if (numbers.Count > 0 && normalized.Contains("HORA")) return 24 / numbers[0];
        if (normalized.Contains("DIA") || normalized.Contains("DIARIA")) return numbers.Count > 0 ? numbers[0] : 1;
        return numbers.Count > 0 ? numbers[0] : 0;
    }

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var numericText = new string(value.Where(ch => char.IsDigit(ch) || ch is ',' or '.').ToArray()).Replace(',', '.');
        return decimal.TryParse(numericText, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    private static string FormatQuantity(decimal value) =>
        value <= 0 ? string.Empty : value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string? FormatInfusionTime(Medicamento? medicamento)
    {
        if (medicamento is null || string.IsNullOrWhiteSpace(medicamento.TiempoInfusionMinutos))
        {
            return null;
        }

        return $"{medicamento.TiempoInfusionMinutos} min";
    }

    private static string FormatNullableDate(DateTime? value) =>
        value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string NormalizeCatalogKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : ' ');
            }
        }

        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string HtmlEncode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string SanitizeFileName(string? value)
    {
        var safe = new string((value ?? string.Empty).Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "paciente" : safe;
    }

    private sealed record DispatchDocumentModels(
        FarmaciaDocumentViewModel Kardex,
        FarmaciaDocumentViewModel Requisition);

    private sealed record RequisitionMedication(
        string? Name,
        decimal? Dose,
        string? Frequency,
        string? DailyDoses,
        int? Days);

    private sealed class RequisitionPayload
    {
        public Dictionary<string, string?> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public List<RequisitionPayloadRow> Rows { get; set; } = [];
    }

    private sealed class RequisitionPayloadRow
    {
        public string? Item { get; set; }

        public string? Descripcion { get; set; }

        public string? Detalle { get; set; }

        public string? Cantidad { get; set; }

        public string? FechaInicio { get; set; }

        public string? FechaFin { get; set; }
    }
}
