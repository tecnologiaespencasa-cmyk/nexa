using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IntranetPrueba.Services.Interfaces;
using IntranetPrueba.Services.Models;

namespace IntranetPrueba.Services;

public class GraphEmailService : IEmailService
{
    private const string GraphScope = "https://graph.microsoft.com/.default";
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GraphEmailService> _logger;

    public GraphEmailService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GraphEmailService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ServiceResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var senderEmail = GetConfigValue("GRAPH_SENDER_EMAIL", "Graph:SenderEmail");
        var tenantId = GetConfigValue("GRAPH_TENANT_ID", "Graph:TenantId");
        var clientId = GetConfigValue("GRAPH_CLIENT_ID", "Graph:ClientId");
        var clientSecret = GetConfigValue("GRAPH_CLIENT_SECRET", "Graph:ClientSecret");

        if (string.IsNullOrWhiteSpace(senderEmail)
            || string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret))
        {
            return ServiceResult.Failure("Graph no esta configurado. Define GRAPH_SENDER_EMAIL, GRAPH_TENANT_ID, GRAPH_CLIENT_ID y GRAPH_CLIENT_SECRET.");
        }

        var recipients = message.To
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            return ServiceResult.Failure("No hay destinatarios para enviar el correo.");
        }

        try
        {
            var token = await GetAccessTokenAsync(tenantId, clientId, clientSecret, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(senderEmail)}/sendMail");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new
            {
                message = new
                {
                    subject = message.Subject,
                    body = new
                    {
                        contentType = "HTML",
                        content = message.HtmlBody
                    },
                    toRecipients = recipients.Select(email => new
                    {
                        emailAddress = new { address = email }
                    }),
                    attachments = message.Attachments.Select(attachment => new Dictionary<string, object?>
                    {
                        ["@odata.type"] = "#microsoft.graph.fileAttachment",
                        ["name"] = attachment.FileName,
                        ["contentType"] = attachment.ContentType,
                        ["contentBytes"] = Convert.ToBase64String(attachment.Content)
                    })
                },
                saveToSentItems = true
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ServiceResult.Success();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Graph sendMail failed with status {StatusCode}: {Body}", response.StatusCode, body);
            return ServiceResult.Failure($"Graph no pudo enviar el correo. Estado: {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graph sendMail failed.");
            return ServiceResult.Failure("No fue posible enviar el correo por Graph.");
        }
    }

    private async Task<string> GetAccessTokenAsync(
        string tenantId,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenantId)}/oauth2/v2.0/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = GraphScope,
            ["grant_type"] = "client_credentials"
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Graph no retorno access_token.");
    }

    private string? GetConfigValue(string environmentVariableName, string configurationKey)
    {
        return Environment.GetEnvironmentVariable(environmentVariableName)?.Trim()
            ?? _configuration[configurationKey]?.Trim();
    }
}
