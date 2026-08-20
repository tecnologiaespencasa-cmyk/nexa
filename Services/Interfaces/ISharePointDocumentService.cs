using Nexa.Services.Models;
using Microsoft.AspNetCore.Http;

namespace Nexa.Services.Interfaces;

public interface ISharePointDocumentService
{
    Task<ServiceResult> UploadTerapiaAmbulatoriaDocumentsAsync(
        string patientName,
        string documentNumber,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<SharePointDocumentItem>>> ListTerapiaAmbulatoriaDocumentsAsync(
        string patientName,
        string documentNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca dentro de ClinicaDeHeridas la carpeta del paciente cuyo nombre termina en el número de
    /// documento. Es la carpeta que crea la aplicación del Portal Administrativo y el enlace entre
    /// el documento del paciente y su historial en Neon.
    /// </summary>
    Task<ServiceResult<SharePointFolderRef?>> FindClinicaHeridasPatientFolderAsync(
        string documentNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarga una foto de la herida. Con <paramref name="thumbnail"/> en true trae la miniatura
    /// de SharePoint en vez del original, que puede pesar varios MB.
    /// </summary>
    Task<ServiceResult<SharePointFileContent>> GetClinicaHeridasPhotoAsync(
        string driveItemId,
        bool thumbnail,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UploadPanAmericanDocumentsAsync(
        string patientName,
        string documentNumber,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<SharePointDocumentItem>>> ListPanAmericanDocumentsAsync(
        string patientName,
        string documentNumber,
        CancellationToken cancellationToken = default);
}
