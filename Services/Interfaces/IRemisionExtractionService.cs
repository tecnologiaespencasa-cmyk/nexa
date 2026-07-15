using IntranetPrueba.Services.Models;

namespace IntranetPrueba.Services.Interfaces;

public interface IRemisionExtractionService
{
    Task<RemisionExtractionResult> ExtractRemisionDataAsync(string documentText, CancellationToken cancellationToken = default);
}
