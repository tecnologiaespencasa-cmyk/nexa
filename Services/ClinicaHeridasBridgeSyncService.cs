using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Nexa.Data;
using Nexa.Helpers;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Nexa.Services;

/// <summary>
/// Envia los pacientes del censo de clinica de heridas a la Edge Function
/// sync-pacientes-heridas de Supabase.
///
/// Como se identifica un paciente de clinica de heridas: el censo de clinica de
/// heridas es una tabla propia (censo_clinica_heridas / DbSet CensoClinicaHeridas).
/// No existe una columna "servicio" ni un catalogo de programas: pertenecer al
/// programa equivale a tener registro en esa tabla. Por eso la consulta no lleva
/// ningun filtro por servicio y ningun paciente de otros censos entra aqui.
///
/// LOGGING: solo datos tecnicos (requestId, conteos, estado HTTP, duracion).
/// Nunca documento, nombre, HMAC, payload ni secretos.
/// </summary>
public class ClinicaHeridasBridgeSyncService : IClinicaHeridasBridgeSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(1);

    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly SupabaseBridgeOptions _options;
    private readonly ILogger<ClinicaHeridasBridgeSyncService> _logger;

    public ClinicaHeridasBridgeSyncService(
        HttpClient httpClient,
        ApplicationDbContext context,
        IOptions<SupabaseBridgeOptions> options,
        ILogger<ClinicaHeridasBridgeSyncService> logger)
    {
        _httpClient = httpClient;
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<int> CountPatientsAsync(CancellationToken cancellationToken = default)
    {
        var patients = await LoadPatientsAsync(cancellationToken);
        return patients.Count;
    }

    public async Task<ServiceResult<BridgeSyncSummary>> SyncAsync(
        int? limit,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        if (!dryRun && !IsConfigured)
        {
            return ServiceResult<BridgeSyncSummary>.Failure(
                "El puente de Supabase no esta configurado. Define SupabaseBridge:ProjectUrl y SupabaseBridge:ApiSecret en User Secrets o variables de entorno.");
        }

        if (limit is <= 0)
        {
            return ServiceResult<BridgeSyncSummary>.Failure("El limite de pacientes debe ser mayor que cero.");
        }

        var patients = await LoadPatientsAsync(cancellationToken);
        var toSend = limit.HasValue ? patients.Take(limit.Value).ToList() : patients;

        return await SendAsync(toSend, patients.Count, dryRun, cancellationToken);
    }

    public async Task<ServiceResult<BridgeSyncSummary>> PushPatientsAsync(
        IReadOnlyList<BridgePatient> patients,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return ServiceResult<BridgeSyncSummary>.Failure(
                "El puente de Supabase no esta configurado. Define SupabaseBridge:ProjectUrl y SupabaseBridge:ApiSecret en User Secrets o variables de entorno.");
        }

        var validos = Deduplicate(patients);
        return await SendAsync(validos, validos.Count, dryRun: false, cancellationToken);
    }

    private async Task<ServiceResult<BridgeSyncSummary>> SendAsync(
        List<BridgePatient> toSend,
        int patientsFound,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var summary = new BridgeSyncSummary
        {
            DryRun = dryRun,
            PatientsFound = patientsFound,
            PatientsSent = toSend.Count
        };

        var batchSize = Math.Clamp(_options.BatchSize, 1, 500);
        var totalBatches = (int)Math.Ceiling(toSend.Count / (double)batchSize);

        _logger.LogInformation(
            "Sincronizacion puente clinica de heridas iniciada. Pacientes encontrados: {Found}. A enviar: {ToSend}. Lotes: {Batches}. Simulacion: {DryRun}.",
            summary.PatientsFound, summary.PatientsSent, totalBatches, dryRun);

        if (dryRun || toSend.Count == 0)
        {
            stopwatch.Stop();
            summary.DurationMs = stopwatch.ElapsedMilliseconds;
            _logger.LogInformation(
                "Sincronizacion puente clinica de heridas finalizada sin llamadas HTTP. Simulacion: {DryRun}. Duracion: {DurationMs} ms.",
                dryRun, summary.DurationMs);
            return ServiceResult<BridgeSyncSummary>.Success(summary);
        }

        for (var index = 0; index < totalBatches; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = toSend.Skip(index * batchSize).Take(batchSize).ToList();
            var result = await SendBatchWithRetriesAsync(batch, index + 1, totalBatches, cancellationToken);

            if (result.Succeeded && result.Value is not null)
            {
                summary.BatchesSent++;
                summary.PatientsProcessed += result.Value.Processed;
                summary.Inserted += result.Value.Inserted;
                summary.Updated += result.Value.Updated;
            }
            else
            {
                summary.BatchesFailed++;
                summary.Errors.Add($"Lote {index + 1}/{totalBatches}: {result.ErrorMessage}");
            }
        }

        stopwatch.Stop();
        summary.DurationMs = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation(
            "Sincronizacion puente clinica de heridas finalizada. Enviados: {Sent}. Procesados: {Processed}. Lotes ok: {BatchesOk}. Lotes con error: {BatchesFailed}. Duracion: {DurationMs} ms.",
            summary.PatientsSent, summary.PatientsProcessed, summary.BatchesSent, summary.BatchesFailed, summary.DurationMs);

        return summary.BatchesFailed > 0 && summary.BatchesSent == 0
            ? ServiceResult<BridgeSyncSummary>.Failure(
                $"No fue posible sincronizar con Supabase. {string.Join(" | ", summary.Errors)}")
            : ServiceResult<BridgeSyncSummary>.Success(summary);
    }

    /// <summary>
    /// Lee los pacientes del censo de clinica de heridas y los deduplica por
    /// documento normalizado, conservando el registro mas reciente (que es el
    /// que tiene el nombre vigente). Solo se leen las dos columnas necesarias.
    /// </summary>
    private async Task<List<BridgePatient>> LoadPatientsAsync(CancellationToken cancellationToken)
    {
        var records = await _context.CensoClinicaHeridas
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new BridgePatient(x.NumeroIdentificacion, x.NombrePaciente))
            .ToListAsync(cancellationToken);

        return Deduplicate(records);
    }

    /// <summary>
    /// Deja un solo registro por documento normalizado (gana el primero de la
    /// lista, que viene ordenada del mas reciente al mas antiguo) y descarta los
    /// que no tienen documento o nombre utilizable: la Edge Function los
    /// rechazaria y tumbaria el lote completo.
    /// </summary>
    private static List<BridgePatient> Deduplicate(IReadOnlyList<BridgePatient> patients)
    {
        var result = new List<BridgePatient>(patients.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var patient in patients)
        {
            var normalizedDocument = BridgeIdentityNormalizer.NormalizeDocument(patient.Document);
            var normalizedName = BridgeIdentityNormalizer.NormalizeName(patient.Name);

            if (normalizedDocument.Length == 0 || normalizedName.Length == 0)
            {
                continue;
            }

            if (seen.Add(normalizedDocument))
            {
                result.Add(patient);
            }
        }

        return result;
    }

    private async Task<ServiceResult<BridgeSyncResponsePayload>> SendBatchWithRetriesAsync(
        IReadOnlyList<BridgePatient> batch,
        int batchNumber,
        int totalBatches,
        CancellationToken cancellationToken)
    {
        var attempts = Math.Clamp(_options.MaxRetries, 0, 5) + 1;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await SendBatchAsync(batch, batchNumber, totalBatches, attempt, cancellationToken);
            if (result.Succeeded || !result.IsTransient || attempt == attempts)
            {
                return result.Result;
            }

            // Espera exponencial con jitter; no se reintenta indefinidamente.
            var delay = TimeSpan.FromMilliseconds(
                BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1) + Random.Shared.Next(0, 250));
            _logger.LogWarning(
                "Lote {Batch}/{Total} fallo de forma transitoria en el intento {Attempt}. Reintentando en {DelayMs} ms.",
                batchNumber, totalBatches, attempt, (int)delay.TotalMilliseconds);
            await Task.Delay(delay, cancellationToken);
        }

        return ServiceResult<BridgeSyncResponsePayload>.Failure("Se agotaron los reintentos.");
    }

    private async Task<(bool Succeeded, bool IsTransient, ServiceResult<BridgeSyncResponsePayload> Result)> SendBatchAsync(
        IReadOnlyList<BridgePatient> batch,
        int batchNumber,
        int totalBatches,
        int attempt,
        CancellationToken cancellationToken)
    {
        // Un requestId nuevo por intento: la Edge Function rechaza requestId
        // repetidos (anti-replay) y la escritura es idempotente por documento.
        var requestId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var rawBody = BuildRawBody(requestId, timestamp, batch);
        var signature = BridgeIdentityNormalizer.ComputeRequestSignature(
            _options.ApiSecret, timestamp.ToString(), requestId, rawBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildFunctionUrl());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiSecret);
        request.Headers.TryAddWithoutValidation("X-Bridge-Timestamp", timestamp.ToString());
        request.Headers.TryAddWithoutValidation("X-Bridge-Request-Id", requestId);
        request.Headers.TryAddWithoutValidation("X-Bridge-Signature", signature);
        request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            _logger.LogInformation(
                "Lote {Batch}/{Total} enviado. RequestId: {RequestId}. Registros: {Count}. Estado HTTP: {StatusCode}. Duracion: {DurationMs} ms.",
                batchNumber, totalBatches, requestId, batch.Count, statusCode, stopwatch.ElapsedMilliseconds);

            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonSafeAsync(cancellationToken);
                return payload is { Success: true }
                    ? (true, false, ServiceResult<BridgeSyncResponsePayload>.Success(payload))
                    : (false, false, ServiceResult<BridgeSyncResponsePayload>.Failure(
                        $"Respuesta inesperada de Supabase (HTTP {statusCode})."));
            }

            var errorCode = await response.Content.ReadErrorCodeAsync(cancellationToken);
            var isTransient = IsTransientStatus(response.StatusCode);
            _logger.LogWarning(
                "Lote {Batch}/{Total} rechazado. RequestId: {RequestId}. Estado HTTP: {StatusCode}. Codigo: {ErrorCode}. Transitorio: {IsTransient}.",
                batchNumber, totalBatches, requestId, statusCode, errorCode, isTransient);

            return (false, isTransient, ServiceResult<BridgeSyncResponsePayload>.Failure(
                $"HTTP {statusCode} ({errorCode})"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // Timeout del HttpClient: es transitorio.
            stopwatch.Stop();
            _logger.LogWarning(
                "Lote {Batch}/{Total} agoto el tiempo de espera en el intento {Attempt}. RequestId: {RequestId}. Duracion: {DurationMs} ms.",
                batchNumber, totalBatches, attempt, requestId, stopwatch.ElapsedMilliseconds);
            return (false, true, ServiceResult<BridgeSyncResponsePayload>.Failure("Tiempo de espera agotado."));
        }
        catch (HttpRequestException exception)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "Lote {Batch}/{Total} fallo de red en el intento {Attempt}. RequestId: {RequestId}. Motivo: {Reason}.",
                batchNumber, totalBatches, attempt, requestId, exception.HttpRequestError);
            return (false, true, ServiceResult<BridgeSyncResponsePayload>.Failure(
                $"Fallo de red ({exception.HttpRequestError})."));
        }
    }

    /// <summary>
    /// Serializa el cuerpo una sola vez: exactamente estos bytes son los que se
    /// firman y los que se envian, para que la firma coincida en el servidor.
    /// </summary>
    private string BuildRawBody(string requestId, long timestamp, IReadOnlyList<BridgePatient> batch)
    {
        var patients = new List<BridgeSyncRequestPatient>(batch.Count);
        foreach (var patient in batch)
        {
            if (_options.HashInIntranet)
            {
                patients.Add(new BridgeSyncRequestPatient
                {
                    DocumentHmac = BridgeIdentityNormalizer.ComputeHmacHex(
                        _options.HmacSecret, BridgeIdentityNormalizer.NormalizeDocument(patient.Document)),
                    NameHmac = BridgeIdentityNormalizer.ComputeHmacHex(
                        _options.HmacSecret, BridgeIdentityNormalizer.NormalizeName(patient.Name))
                });
            }
            else
            {
                patients.Add(new BridgeSyncRequestPatient
                {
                    Document = patient.Document,
                    Name = patient.Name
                });
            }
        }

        return JsonSerializer.Serialize(
            new BridgeSyncRequestPayload
            {
                RequestId = requestId,
                Timestamp = timestamp,
                Patients = patients
            },
            JsonOptions);
    }

    private string BuildFunctionUrl() =>
        $"{_options.ProjectUrl.TrimEnd('/')}/functions/v1/{_options.FunctionName}";

    private static bool IsTransientStatus(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            || (int)statusCode >= 500;
}

internal static class BridgeHttpContentExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Lee la respuesta tecnica; devuelve null si no es JSON valido.</summary>
    public static async Task<BridgeSyncResponsePayload?> ReadFromJsonSafeAsync(
        this HttpContent content,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<BridgeSyncResponsePayload>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Extrae solo el codigo de error tecnico del cuerpo de la respuesta. No se
    /// registra el cuerpo completo para no arrastrar informacion innecesaria.
    /// </summary>
    public static async Task<string> ReadErrorCodeAsync(this HttpContent content, CancellationToken cancellationToken)
    {
        try
        {
            var body = await content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<BridgeSyncResponsePayload>(body, JsonOptions);
            return string.IsNullOrWhiteSpace(payload?.Error) ? "sin_codigo" : payload.Error;
        }
        catch (JsonException)
        {
            return "respuesta_no_json";
        }
    }
}
