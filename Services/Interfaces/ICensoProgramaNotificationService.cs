using Nexa.Data.Entities;

namespace Nexa.Services.Interfaces;

/// <summary>
/// Avisa a programas especiales cuando a un paciente se le asigna uno de los programas que ese
/// equipo atiende: clínica de heridas, crónicos y NPT.
/// </summary>
public interface ICensoProgramaNotificationService
{
    /// <summary>
    /// Envía el aviso si el programa es de los que se notifican. Nunca lanza: un correo que no
    /// sale no puede impedir que el programa quede agregado.
    /// </summary>
    /// <returns>Un aviso para mostrar en pantalla si el correo no salió; vacío si todo fue bien.</returns>
    Task<string> NotificarProgramaAgregadoAsync(
        CensoPaciente paciente,
        string programa,
        string agregadoPor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Avisa que el extractor de remisiones encontró en un documento palabras que hacen pensar en
    /// clínica de heridas. Nunca lanza: un correo que no sale no puede tumbar la extracción.
    /// </summary>
    /// <returns>Un aviso para mostrar en pantalla si el correo no salió; vacío si todo fue bien.</returns>
    Task<string> NotificarCandidatoClinicaHeridasAsync(
        CandidatoClinicaHeridasAviso candidato,
        CancellationToken cancellationToken);
}

/// <param name="NombrePaciente">Nombre extraído del documento; puede faltar.</param>
/// <param name="Documento">Tipo y número de identificación extraídos; puede faltar.</param>
/// <param name="Origen">De dónde salió la remisión: el nombre del archivo o "Texto pegado".</param>
/// <param name="ProcesadoPor">Quien corrió el extractor.</param>
public sealed record CandidatoClinicaHeridasAviso(
    string? NombrePaciente,
    string? Documento,
    string Origen,
    string ProcesadoPor,
    IReadOnlyList<Nexa.Helpers.CoincidenciaClinicaHeridas> Coincidencias);
