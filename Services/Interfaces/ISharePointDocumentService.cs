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

    Task<ServiceResult> UploadClinicaHeridasDocumentsAsync(
        string patientName,
        string documentType,
        string documentNumber,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<SharePointDocumentItem>>> ListClinicaHeridasDocumentsAsync(
        string patientName,
        string documentType,
        string documentNumber,
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
