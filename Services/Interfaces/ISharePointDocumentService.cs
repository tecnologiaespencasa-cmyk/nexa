using IntranetPrueba.Services.Models;
using Microsoft.AspNetCore.Http;

namespace IntranetPrueba.Services.Interfaces;

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
}
