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

    public int BatchSize { get; set; } = 100;

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Reintentos adicionales por lote ante fallos transitorios (5xx / red).</summary>
    public int MaxRetries { get; set; } = 3;

    // --- Ejecucion automatica en segundo plano ------------------------------

    /// <summary>
    /// Envia el paciente al puente en cuanto se guarda su registro en el censo
    /// de clinica de heridas. Es la via principal de sincronizacion.
    /// </summary>
    public bool PushOnSave { get; set; } = true;

    /// <summary>
    /// Habilita ademas una reconciliacion periodica del censo completo, util
    /// para recuperar envios que fallaron con Supabase caido. Apagada por
    /// defecto: con PushOnSave basta para el dia a dia.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Maximo de pacientes por ejecucion; 0 o menos significa todos. Sirve para
    /// la puesta en marcha escalonada: 1, luego 5, luego 0.
    /// </summary>
    public int MaxPatientsPerRun { get; set; }

    /// <summary>Horas entre sincronizaciones automaticas.</summary>
    public double IntervalHours { get; set; } = 24;

    /// <summary>Espera antes de la primera sincronizacion tras arrancar la aplicacion.</summary>
    public int InitialDelaySeconds { get; set; } = 60;

    /// <summary>
    /// true simula: cuenta los pacientes y arma los lotes, pero no llama a
    /// Supabase. Util para validar la seleccion antes de enviar de verdad.
    /// </summary>
    public bool DryRun { get; set; }

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ProjectUrl)
        && !string.IsNullOrWhiteSpace(FunctionName)
        && !string.IsNullOrWhiteSpace(ApiSecret);
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

/// <summary>
/// Documento y nombre reales. Viajan por HTTPS y solo existen en memoria: la
/// Edge Function los convierte en documento_hmac, nombre_hmac y nombre_encrypted
/// y nunca los persiste.
/// </summary>
internal sealed class BridgeSyncRequestPatient
{
    [JsonPropertyName("document")]
    public string Document { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
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
