using System.Text.Json.Serialization;

namespace Nexa.Services.Models;

/// <summary>
/// Configuracion del puente hacia Supabase. Se enlaza desde la seccion
/// "SupabaseBridge" (appsettings + User Secrets + variables de entorno).
/// Los secretos NUNCA se escriben en appsettings.json versionado.
/// </summary>
public sealed class SupabaseBridgeOptions
{
    public const string SectionName = "SupabaseBridge";

    /// <summary>URL del proyecto, por ejemplo https://abcdefgh.supabase.co</summary>
    public string ProjectUrl { get; set; } = string.Empty;

    /// <summary>Nombre de la Edge Function que recibe la sincronizacion.</summary>
    public string FunctionName { get; set; } = "sync-pacientes-heridas";

    /// <summary>Secreto de autenticacion y firma de la peticion (BRIDGE_API_SECRET).</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// Secreto de derivacion de los HMAC (BRIDGE_HMAC_SECRET). Solo es
    /// obligatorio cuando <see cref="HashInIntranet"/> es true.
    /// </summary>
    public string HmacSecret { get; set; } = string.Empty;

    /// <summary>
    /// false (por defecto): se envian documento y nombre reales por HTTPS y la
    /// Edge Function los normaliza y convierte en HMAC.
    /// true: la intranet calcula los HMAC y el dato real nunca sale de aqui.
    /// En ambos casos Supabase solo almacena los HMAC.
    /// </summary>
    public bool HashInIntranet { get; set; }

    public int BatchSize { get; set; } = 100;

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Reintentos adicionales por lote ante fallos transitorios (5xx / red).</summary>
    public int MaxRetries { get; set; } = 3;

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ProjectUrl)
        && !string.IsNullOrWhiteSpace(FunctionName)
        && !string.IsNullOrWhiteSpace(ApiSecret)
        && (!HashInIntranet || !string.IsNullOrWhiteSpace(HmacSecret));
}

/// <summary>Un paciente listo para enviar. Solo se usa en memoria.</summary>
public sealed record BridgePatient(string Document, string Name);

/// <summary>Resultado tecnico de una sincronizacion. No contiene datos personales.</summary>
public sealed class BridgeSyncSummary
{
    public bool DryRun { get; set; }

    /// <summary>Pacientes unicos encontrados en el censo de clinica de heridas.</summary>
    public int PatientsFound { get; set; }

    /// <summary>Pacientes efectivamente enviados (tras aplicar el limite).</summary>
    public int PatientsSent { get; set; }

    /// <summary>Pacientes confirmados por Supabase.</summary>
    public int PatientsProcessed { get; set; }

    public int Inserted { get; set; }

    public int Updated { get; set; }

    public int BatchesSent { get; set; }

    public int BatchesFailed { get; set; }

    public long DurationMs { get; set; }

    /// <summary>Errores tecnicos (codigo HTTP / codigo de error), sin datos personales.</summary>
    public List<string> Errors { get; } = [];
}

/// <summary>Cuerpo que se envia a la Edge Function.</summary>
internal sealed class BridgeSyncRequestPayload
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    [JsonPropertyName("patients")]
    public IReadOnlyList<BridgeSyncRequestPatient> Patients { get; init; } = [];
}

internal sealed class BridgeSyncRequestPatient
{
    [JsonPropertyName("document")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Document { get; init; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("documentHmac")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DocumentHmac { get; init; }

    [JsonPropertyName("nameHmac")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NameHmac { get; init; }
}

/// <summary>Respuesta tecnica de la Edge Function.</summary>
internal sealed class BridgeSyncResponsePayload
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("processed")]
    public int Processed { get; init; }

    [JsonPropertyName("inserted")]
    public int Inserted { get; init; }

    [JsonPropertyName("updated")]
    public int Updated { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
