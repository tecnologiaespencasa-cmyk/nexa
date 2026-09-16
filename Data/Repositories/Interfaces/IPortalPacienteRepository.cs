using Nexa.Data.Repositories.Models;

namespace Nexa.Data.Repositories.Interfaces;

/// <summary>
/// Lectura, por documento del paciente, de lo que el Portal Administrativo registra en Neon sobre
/// él. La intranet nunca escribe en esa base.
/// </summary>
public interface IPortalPacienteRepository
{
    /// <summary>
    /// Novedades que nombran al paciente, de la más reciente a la más antigua. El documento se
    /// compara solo por letras y dígitos, porque el portal lo recibe escrito a mano.
    /// </summary>
    Task<IReadOnlyList<PortalNovedadPacienteRow>> GetNovedadesPorDocumentoAsync(
        string documento,
        CancellationToken cancellationToken = default);

    /// <summary>Reportes de ronda intramural del paciente, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<PortalRondaPacienteRow>> GetRondasPorDocumentoAsync(
        string documento,
        CancellationToken cancellationToken = default);
}
