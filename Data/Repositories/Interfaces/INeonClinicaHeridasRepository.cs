using Nexa.Data.Repositories.Models;

namespace Nexa.Data.Repositories.Interfaces;

public interface INeonClinicaHeridasRepository
{
    /// <summary>
    /// Devuelve todos los seguimientos de un paciente, ordenados del más reciente al más antiguo,
    /// con sus fotos y el auxiliar que los registró.
    /// </summary>
    /// <param name="carpetaDriveItemId">
    /// Identificador de la carpeta del paciente en SharePoint. Es la única forma de enlazar un
    /// documento con su historial: en Neon el paciente se identifica con un seudónimo
    /// (<c>pacienteRef</c>) que la intranet no puede calcular.
    /// </param>
    Task<IReadOnlyList<ClinicaHeridasSeguimientoRow>> GetSeguimientosPorCarpetaAsync(
        string carpetaDriveItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma que un archivo de SharePoint corresponde a una foto de seguimiento registrada en
    /// Neon. Evita que el proxy de imágenes de la intranet sirva cualquier archivo del sitio.
    /// </summary>
    Task<ClinicaHeridasFotoRow?> GetFotoPorDriveItemIdAsync(
        string driveItemId,
        CancellationToken cancellationToken = default);
}
