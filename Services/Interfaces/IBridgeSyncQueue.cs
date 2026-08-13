using System.Threading.Channels;
using Nexa.Services.Models;

namespace Nexa.Services.Interfaces;

/// <summary>
/// Cola en memoria que recibe los pacientes recien guardados en el censo de
/// clinica de heridas para empujarlos al puente de Supabase enseguida.
///
/// Encolar no bloquea ni puede hacer fallar el guardado: si el puente esta
/// apagado o mal configurado, el paciente simplemente no se encola.
/// </summary>
public interface IBridgeSyncQueue
{
    /// <summary>Encola un paciente. Devuelve false si no se encolo.</summary>
    bool Enqueue(BridgePatient patient);

    /// <summary>Lector que consume el proceso en segundo plano.</summary>
    ChannelReader<BridgePatient> Reader { get; }
}
