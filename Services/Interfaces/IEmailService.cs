using Nexa.Services.Models;

namespace Nexa.Services.Interfaces;

public interface IEmailService
{
    Task<ServiceResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
