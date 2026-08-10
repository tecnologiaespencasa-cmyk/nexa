using Nexa.Services.Models;

namespace Nexa.Services.Interfaces;

public interface IRemisionExtractionService
{
    Task<RemisionExtractionResult> ExtractRemisionDataAsync(string documentText, CancellationToken cancellationToken = default);
}
