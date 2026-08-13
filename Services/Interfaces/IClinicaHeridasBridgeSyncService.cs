using Nexa.Services.Models;

namespace Nexa.Services.Interfaces;

/// <summary>
/// Sincroniza los pacientes del censo de clinica de heridas hacia la base
/// puente de Supabase, siempre a traves de la Edge Function por HTTPS.
/// </summary>
public interface IClinicaHeridasBridgeSyncService
{
    /// <summary>Indica si la seccion SupabaseBridge tiene la configuracion minima.</summary>
    bool IsConfigured { get; }

    /// <summary>Pacientes unicos (por documento normalizado) del censo de clinica de heridas.</summary>
    Task<int> CountPatientsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta la sincronizacion.
    /// </summary>
    /// <param name="limit">Maximo de pacientes a enviar; null envia todos.</param>
    /// <param name="dryRun">true simula: cuenta y arma los lotes pero no llama a Supabase.</param>
    Task<ServiceResult<BridgeSyncSummary>> SyncAsync(
        int? limit,
        bool dryRun,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia unos pacientes concretos, sin leer el censo. Lo usa el envio
    /// inmediato al guardar un registro. Es idempotente: reenviar al mismo
    /// paciente no crea duplicados.
    /// </summary>
    Task<ServiceResult<BridgeSyncSummary>> PushPatientsAsync(
        IReadOnlyList<BridgePatient> patients,
        CancellationToken cancellationToken = default);
}
