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
}
