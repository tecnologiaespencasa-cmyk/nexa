using IntranetPrueba.Services.Models;

namespace IntranetPrueba.Services.Interfaces;

public interface IEmailService
{
    Task<ServiceResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
