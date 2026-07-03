using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using IntranetPrueba.Services.Interfaces;
using IntranetPrueba.Services.Models;
using Microsoft.AspNetCore.Http;

namespace IntranetPrueba.Services;

public class SharePointDocumentService : ISharePointDocumentService
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
    private const string GraphScope = "https://graph.microsoft.com/.default";
    private const long MaxFileBytes = 50L * 1024 * 1024;
    private static readonly Regex InvalidNameCharacters = new("[\"*:<>?/\\\\|#%~\\x00-\\x1F]", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SharePointDocumentService> _logger;

    public SharePointDocumentService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SharePointDocumentService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ServiceResult> UploadTerapiaAmbulatoriaDocumentsAsync(
        string patientName,
        string documentNumber,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        var validFiles = files
            .Where(file => file.Length > 0)
            .ToList();

        if (validFiles.Count == 0)
        {
            return ServiceResult.Failure("Selecciona al menos un documento para adjuntar.");
        }

        if (validFiles.Any(file => file.Length > MaxFileBytes))
        {
            return ServiceResult.Failure("Cada documento adjunto debe pesar máximo 50 MB.");
        }

        var config = GetConfig();
        if (!config.IsComplete)
        {
            return ServiceResult.Failure("SharePoint no esta configurado. Define SHAREPOINT_LIBRARY_ID, SHAREPOINT_SITE_ID y las credenciales Graph.");
        }

        try
        {
            var token = await GetAccessTokenAsync(config, cancellationToken);
            var folderName = BuildPatientFolderName(patientName, documentNumber);
            var ensured = await EnsureFolderAsync(config.LibraryId!, folderName, token, cancellationToken);
            if (!ensured.Succeeded)
            {
                return ensured;
            }

            foreach (var file in validFiles)
            {
                var fileName = SanitizeSharePointName(Path.GetFileName(file.FileName), "documento-adjunto");
                var path = EscapeGraphPath($"{folderName}/{fileName}");
                using var uploadRequest = new HttpRequestMessage(
                    HttpMethod.Put,
                    $"{GraphBaseUrl}/drives/{Uri.EscapeDataString(config.LibraryId!)}/root:/{path}:/content");
                uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                uploadRequest.Content = new StreamContent(file.OpenReadStream());
                uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);

                using var uploadResponse = await _httpClient.SendAsync(uploadRequest, cancellationToken);
                if (!uploadResponse.IsSuccessStatusCode)
                {
                    var body = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("SharePoint upload failed with status {StatusCode}: {Body}", uploadResponse.StatusCode, body);
                    return ServiceResult.Failure($"No fue posible subir {fileName} a SharePoint. Estado: {(int)uploadResponse.StatusCode}.");
                }
            }

            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SharePoint document upload failed.");
            return ServiceResult.Failure("No fue posible subir los documentos a SharePoint.");
        }
    }

    public async Task<ServiceResult<IReadOnlyList<SharePointDocumentItem>>> ListTerapiaAmbulatoriaDocumentsAsync(
        string patientName,
        string documentNumber,
        CancellationToken cancellationToken = default)
    {
        var config = GetConfig();
        if (!config.IsComplete)
        {
            return ServiceResult<IReadOnlyList<SharePointDocumentItem>>.Failure(
                "SharePoint no esta configurado. Define SHAREPOINT_LIBRARY_ID, SHAREPOINT_SITE_ID y las credenciales Graph.");
        }

        try
        {
            var token = await GetAccessTokenAsync(config, cancellationToken);
            var folderName = BuildPatientFolderName(patientName, documentNumber);
            var path = EscapeGraphPath(folderName);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{GraphBaseUrl}/drives/{Uri.EscapeDataString(config.LibraryId!)}/root:/{path}:/children?$select=name,webUrl,size,lastModifiedDateTime,file&$orderby=name");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ServiceResult<IReadOnlyList<SharePointDocumentItem>>.Success([]);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SharePoint list failed with status {StatusCode}: {Body}", response.StatusCode, body);
                return ServiceResult<IReadOnlyList<SharePointDocumentItem>>.Failure(
                    $"No fue posible consultar los adjuntos en SharePoint. Estado: {(int)response.StatusCode}.");
            }

            var result = JsonSerializer.Deserialize<GraphChildrenResponse>(body, JsonOptions);
            var items = result?.Value
                .Where(item => item.File is not null)
                .Select(item => new SharePointDocumentItem
                {
                    Name = item.Name ?? "Documento",
                    WebUrl = item.WebUrl ?? string.Empty,
                    Size = item.Size,
                    LastModifiedAt = item.LastModifiedDateTime
                })
                .ToList() ?? [];

            return ServiceResult<IReadOnlyList<SharePointDocumentItem>>.Success(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SharePoint document list failed.");
            return ServiceResult<IReadOnlyList<SharePointDocumentItem>>.Failure(
                "No fue posible consultar los adjuntos en SharePoint.");
        }
    }

    private async Task<ServiceResult> EnsureFolderAsync(
        string driveId,
        string folderName,
        string token,
        CancellationToken cancellationToken)
    {
        var escapedFolder = EscapeGraphPath(folderName);
        using (var getRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{GraphBaseUrl}/drives/{Uri.EscapeDataString(driveId)}/root:/{escapedFolder}"))
        {
            getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var getResponse = await _httpClient.SendAsync(getRequest, cancellationToken);
            if (getResponse.IsSuccessStatusCode)
            {
                return ServiceResult.Success();
            }

            if (getResponse.StatusCode != HttpStatusCode.NotFound)
            {
                var body = await getResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("SharePoint folder lookup failed with status {StatusCode}: {Body}", getResponse.StatusCode, body);
                return ServiceResult.Failure($"No fue posible validar la carpeta del paciente en SharePoint. Estado: {(int)getResponse.StatusCode}.");
            }
        }

        using var createRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{GraphBaseUrl}/drives/{Uri.EscapeDataString(driveId)}/root/children");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        createRequest.Content = JsonContent.Create(new Dictionary<string, object?>
        {
            ["name"] = folderName,
            ["folder"] = new { },
            ["@microsoft.graph.conflictBehavior"] = "fail"
        });

        using var createResponse = await _httpClient.SendAsync(createRequest, cancellationToken);
        if (createResponse.IsSuccessStatusCode || createResponse.StatusCode == HttpStatusCode.Conflict)
        {
            return ServiceResult.Success();
        }

        var createBody = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("SharePoint folder create failed with status {StatusCode}: {Body}", createResponse.StatusCode, createBody);
        return ServiceResult.Failure($"No fue posible crear la carpeta del paciente en SharePoint. Estado: {(int)createResponse.StatusCode}.");
    }

    private async Task<string> GetAccessTokenAsync(SharePointConfig config, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(config.TenantId!)}/oauth2/v2.0/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId!,
            ["client_secret"] = config.ClientSecret!,
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

    private SharePointConfig GetConfig()
    {
        return new SharePointConfig
        {
            LibraryId = GetConfigValue("SHAREPOINT_LIBRARY_ID", "SharePoint:LibraryId"),
            SiteId = GetConfigValue("SHAREPOINT_SITE_ID", "SharePoint:SiteId"),
            TenantId = GetConfigValue("SHAREPOINT_TENANT_ID", "SharePoint:TenantId")
                ?? GetConfigValue("GRAPH_TENANT_ID", "Graph:TenantId"),
            ClientId = GetConfigValue("SHAREPOINT_CLIENT_ID", "SharePoint:ClientId")
                ?? GetConfigValue("GRAPH_CLIENT_ID", "Graph:ClientId"),
            ClientSecret = GetConfigValue("SHAREPOINT_CLIENT_SECRET", "SharePoint:ClientSecret")
                ?? GetConfigValue("GRAPH_CLIENT_SECRET", "Graph:ClientSecret")
        };
    }

    private string? GetConfigValue(string environmentVariableName, string configurationKey)
    {
        return Environment.GetEnvironmentVariable(environmentVariableName)?.Trim()
            ?? _configuration[configurationKey]?.Trim();
    }

    private static string BuildPatientFolderName(string patientName, string documentNumber)
    {
        var rawName = $"{patientName} - {documentNumber}".Trim();
        return SanitizeSharePointName(rawName, "paciente");
    }

    private static string SanitizeSharePointName(string value, string fallback)
    {
        var sanitized = InvalidNameCharacters.Replace(value, "-");
        sanitized = Regex.Replace(sanitized, "\\s+", " ").Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = fallback;
        }

        return sanitized.Length > 140 ? sanitized[..140].Trim(' ', '.') : sanitized;
    }

    private static string EscapeGraphPath(string path)
    {
        return string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }

    private sealed class SharePointConfig
    {
        public string? LibraryId { get; init; }
        public string? SiteId { get; init; }
        public string? TenantId { get; init; }
        public string? ClientId { get; init; }
        public string? ClientSecret { get; init; }

        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(LibraryId)
            && !string.IsNullOrWhiteSpace(SiteId)
            && !string.IsNullOrWhiteSpace(TenantId)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret);
    }

    private sealed class GraphChildrenResponse
    {
        [JsonPropertyName("value")]
        public List<GraphDriveItem> Value { get; set; } = [];
    }

    private sealed class GraphDriveItem
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("webUrl")]
        public string? WebUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("lastModifiedDateTime")]
        public DateTimeOffset? LastModifiedDateTime { get; set; }

        [JsonPropertyName("file")]
        public object? File { get; set; }
    }
}
