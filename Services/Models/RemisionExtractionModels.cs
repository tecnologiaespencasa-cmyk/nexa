namespace Nexa.Services.Models;

public sealed class RemisionExtractionResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Json { get; init; }

    public static RemisionExtractionResult Success(string json) => new() { Succeeded = true, Json = json };

    public static RemisionExtractionResult Failure(string message) => new() { Succeeded = false, ErrorMessage = message };
}
