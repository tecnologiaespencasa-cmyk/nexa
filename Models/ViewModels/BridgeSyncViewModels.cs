using Nexa.Services.Models;

namespace Nexa.Models.ViewModels;

/// <summary>
/// Estado del puente hacia Supabase. Nunca expone secretos: solo indica si
/// estan definidos y muestra el host del proyecto (que no es un secreto).
/// </summary>
public class BridgeSyncIndexViewModel
{
    public bool IsConfigured { get; set; }

    public string ProjectHost { get; set; } = string.Empty;

    public string FunctionName { get; set; } = string.Empty;

    public bool HasApiSecret { get; set; }

    public bool HasHmacSecret { get; set; }

    public bool HashInIntranet { get; set; }

    public int BatchSize { get; set; }

    public int TimeoutSeconds { get; set; }

    public int MaxRetries { get; set; }

    /// <summary>Pacientes unicos del censo de clinica de heridas listos para enviar.</summary>
    public int PatientsAvailable { get; set; }

    public string? PatientsAvailableError { get; set; }

    /// <summary>Resultado de la ultima ejecucion lanzada desde esta pantalla.</summary>
    public BridgeSyncSummary? LastRun { get; set; }

    public string? LastRunTitle { get; set; }
}
