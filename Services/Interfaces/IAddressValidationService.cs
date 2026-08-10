using Nexa.Services.Models;

namespace Nexa.Services.Interfaces;

public interface IAddressValidationService
{
    Task<AddressValidationResult> ValidateAddressAsync(string address, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SearchNeighborhoodsAsync(string municipality, string term, CancellationToken cancellationToken = default);
}
