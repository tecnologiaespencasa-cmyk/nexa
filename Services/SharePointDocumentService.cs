using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;
using Microsoft.AspNetCore.Http;

namespace Nexa.Services;

public class SharePointDocumentService : ISharePointDocumentService
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
    private const string GraphScope = "https://graph.microsoft.com/.default";
    private const long MaxFileBytes = 50L * 1024 * 1024;
    private static readonly Regex InvalidNameCharacters = new("[\"*:<>?/\\\\|#%~\\x00-\\x1F]", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // El token de aplicación dura una hora y no depende del usuario, así que se reutiliza entre
    // peticiones: una pantalla de seguimientos pide una decena de fotos y cada una necesitaba su
    // propio viaje a login.microsoftonline.com.
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static string? _cachedToken;
    private static string? _cachedTokenKey;
    private static DateTimeOffset _cachedTokenExpiresAt;

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
            return ServiceResult.Failure("SharePoint no esta configurado. Define SHAREPOINT_LIBRARY, SHAREPOINT_LIBRARY_ID, SHAREPOINT_SITE_ID y las credenciales Graph.");
        }

        try
        {
            var token = await GetAccessTokenAsync(config, cancellationToken);
            var folderName = BuildPatientFolderName(patientName, documentNumber);
            var folderSegments = BuildFolderSegments(config.LibraryName!, folderName);
            var folderPath = string.Join("/", folderSegments);
            var ensured = await EnsureFolderPathAsync(config.LibraryId!, folderSegments, token, cancellationToken);
            if (!ensured.Succeeded)
            {
                return ensured;
            }

            foreach (var file in validFiles)
            {
                var fileName = SanitizeSharePointName(Path.GetFileName(file.FileName), "documento-adjunto");
                var path = EscapeGraphPath($"{folderPath}/{fileName}");
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
                "SharePoint no esta configurado. Define SHAREPOINT_LIBRARY, SHAREPOINT_LIBRARY_ID, SHAREPOINT_SITE_ID y las credenciales Graph.");
        }

        try
        {
            var token = await GetAccessTokenAsync(config, cancellationToken);
            var folderName = BuildPatientFolderName(patientName, documentNumber);
            var folderSegments = BuildFolderSegments(config.LibraryName!, folderName);
            var path = EscapeGraphPath(string.Join("/", folderSegments));
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

    public async Task<ServiceResult<SharePointFolderRef?>> FindClinicaHeridasPatientFolderAsync(
        string documentNumber,
        CancellationToken cancellationToken = default)
    {
        var documentKey = NormalizeDocumentKey(documentNumber);
        if (documentKey.Length == 0)
        {
            return ServiceResult<SharePointFolderRef?>.Success(null);
        }

        var config = GetConfig();
        if (!config.IsComplete)
        {
            return ServiceResult<SharePointFolderRef?>.Failure(
                "SharePoint no esta configurado. Define SHAREPOINT_LIBRARY, SHAREPOINT_LIBRARY_ID, SHAREPOINT_SITE_ID y las credenciales Graph.");
        }

        try
        {
            var token = await GetAccessTokenAsync(config, cancellationToken);
            var rootSegments = BuildClinicaHeridasRootSegments();
            var path = EscapeGraphPath(string.Join("/", rootSegments));
            var nextUrl =
                $"{GraphBaseUrl}/drives/{Uri.EscapeDataString(config.LibraryId!)}/root:/{path}:/children"
                + "?$select=id,name,webUrl,folder&$top=200";

            while (!string.IsNullOrWhiteSpace(nextUrl))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // Todavía no existe la carpeta ClinicaDeHeridas: ningún paciente tiene seguimientos.
                    return ServiceResult<SharePointFolderRef?>.Success(null);
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "SharePoint ClinicaHeridas folder search failed with status {StatusCode}: {Body}",
                        response.StatusCode,
                        body);
                    return ServiceResult<SharePointFolderRef?>.Failure(
                        $"No fue posible consultar las carpetas de clínica de heridas en SharePoint. Estado: {(int)response.StatusCode}.");
                }

                var page = JsonSerializer.Deserialize<GraphChildrenResponse>(body, JsonOptions);
                var match = page?.Value.FirstOrDefault(item =>
                    item.Folder is not null && FolderNameMatchesDocument(item.Name, documentKey));

                if (match is not null)
                {
                    return ServiceResult<SharePointFolderRef?>.Success(new SharePointFolderRef
                    {
                        Id = match.Id ?? string.Empty,
                        Name = match.Name ?? string.Empty,
                        WebUrl = match.WebUrl ?? string.Empty
                    });
                }

                nextUrl = page?.NextLink;
            }

            return ServiceResult<SharePointFolderRef?>.Success(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SharePoint ClinicaHeridas folder search failed.");
            return ServiceResult<SharePointFolderRef?>.Failure(
                "No fue posible consultar las carpetas de clínica de heridas en SharePoint.");
        }
    }

    public async Task<ServiceResult<SharePointFileContent>> GetClinicaHeridasPhotoAsync(
        string driveItemId,
        bool thumbnail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(driveItemId))
        {
            return ServiceResult<SharePointFileContent>.Failure("La foto solicitada no existe.");
        }

        var config = GetConfig();
        if (!config.IsComplete)
        {
            return ServiceResult<SharePointFileContent>.Failure(
                "SharePoint no esta configurado. Define SHAREPOINT_LIBRARY, SHAREPOINT_LIBRARY_ID, SHAREPOINT_SITE_ID y las credenciales Graph.");
        }

        try
        {
            var token = await GetAccessTokenAsync(config, cancellationToken);
            var itemUrl =
                $"{GraphBaseUrl}/drives/{Uri.EscapeDataString(config.LibraryId!)}/items/{Uri.EscapeDataString(driveItemId)}";
            var contentUrl = thumbnail
                ? $"{itemUrl}/thumbnails/0/large/content"
                : $"{itemUrl}/content";

            using var request = new HttpRequestMessage(HttpMethod.Get, contentUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Un archivo sin miniatura generada responde 404 solo en /thumbnails: se reintenta
                // con el original antes de dar la foto por perdida.
                if (thumbnail && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return await GetClinicaHeridasPhotoAsync(driveItemId, thumbnail: false, cancellationToken);
                }

                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "SharePoint ClinicaHeridas photo download failed with status {StatusCode}: {Body}",
                    response.StatusCode,
                    errorBody);
                return ServiceResult<SharePointFileContent>.Failure(
                    $"No fue posible descargar la foto desde SharePoint. Estado: {(int)response.StatusCode}.");
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return ServiceResult<SharePointFileContent>.Success(new SharePointFileContent
            {
                Content = content,
                ContentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg",
                Name = driveItemId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SharePoint ClinicaHeridas photo download failed.");
            return ServiceResult<SharePointFileContent>.Failure("No fue posible descargar la foto desde SharePoint.");
        }
    }

    public async Task<ServiceResult> UploadPanAmericanDocumentsAsync(
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
            return ServiceResult.Failure("SharePoint no esta configurado. Define SHAREPOINT_LIBRARY, SHAREPOINT_LIBRARY_ID, SHAREPOINT_SITE_ID y las credenciales Graph.");
        }

        try
        {
            var token = await GetAccessTokenAsync(config, cancellationToken);
            var folderSegments = BuildPanAmericanFolderSegments();
            var folderPath = string.Join("/", folderSegments);
            var ensured = await EnsureFolderPathAsync(config.LibraryId!, folderSegments, token, cancellationToken);
            if (!ensured.Succeeded)
            {
                return ensured;
            }

            var patientPrefix = BuildPatientFolderName(patientName, documentNumber);
            foreach (var file in validFiles)
            {
                var originalName = SanitizeSharePointName(Path.GetFileName(file.FileName), "documento-adjunto");
                var fileName = SanitizeSharePointName($"{patientPrefix} - {originalName}", "documento-adjunto");
                var path = EscapeGraphPath($"{folderPath}/{fileName}");
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
                    _logger.LogWarning("SharePoint PanAmerican upload failed with status {StatusCode}: {Body}", uploadResponse.StatusCode, body);
                    return ServiceResult.Failure($"No fue posible subir {fileName} a SharePoint. Estado: {(int)uploadResponse.StatusCode}.");
                }
            }

            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SharePoint PanAmerican document upload failed.");
            return ServiceResult.Failure("No fue posible subir los documentos a SharePoint.");
        }
    }

    public async Task<ServiceResult<IReadOnlyList<SharePointDocumentItem>>> ListPanAmericanDocumentsAsync(
        string patientName,
        string documentNumber,
        CancellationToken cancellationToken = default)
    {
        var config = GetConfig();
        if (!config.IsComplete)
        {
            return ServiceResult<IReadOnlyList<SharePointDocumentItem>>.Failure(
                "SharePoint no esta configurado. Define SHAREPOINT_LIBRARY, SHAREPOINT_LIBRARY_ID, SHAREPOINT_SITE_ID y las credenciales Graph.");
        }

        try
        {
            var token = await GetAccessTokenAsync(config, cancellationToken);
            var folderSegments = BuildPanAmericanFolderSegments();
            var path = EscapeGraphPath(string.Join("/", folderSegments));
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
                _logger.LogWarning("SharePoint PanAmerican list failed with status {StatusCode}: {Body}", response.StatusCode, body);
                return ServiceResult<IReadOnlyList<SharePointDocumentItem>>.Failure(
                    $"No fue posible consultar los adjuntos en SharePoint. Estado: {(int)response.StatusCode}.");
            }

            var patientPrefix = BuildPatientFolderName(patientName, documentNumber);
            var result = JsonSerializer.Deserialize<GraphChildrenResponse>(body, JsonOptions);
            var items = result?.Value
                .Where(item => item.File is not null
                    && (item.Name ?? string.Empty).StartsWith(patientPrefix, StringComparison.OrdinalIgnoreCase))
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
            _logger.LogError(ex, "SharePoint PanAmerican document list failed.");
            return ServiceResult<IReadOnlyList<SharePointDocumentItem>>.Failure(
                "No fue posible consultar los adjuntos en SharePoint.");
        }
    }

    private IReadOnlyList<string> BuildClinicaHeridasRootSegments()
    {
        var folderPath = GetConfigValue("SHAREPOINT_CLINICA_HERIDAS_FOLDER", "SharePoint:ClinicaHeridasFolder");
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            folderPath = "ClinicaDeHeridas";
        }

        var segments = folderPath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => SanitizeSharePointName(segment, "ClinicaDeHeridas"))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        if (segments.Count == 0)
        {
            segments.Add("ClinicaDeHeridas");
        }

        return segments;
    }

    /// <summary>
    /// La carpeta del paciente se llama "NOMBRE - DOCUMENTO" (aplicación del portal) o
    /// "NOMBRE - TIPO DOCUMENTO" (carpetas históricas creadas desde la intranet). En ambos casos el
    /// documento es el último bloque del nombre, así que se compara contra él.
    /// </summary>
    private static bool FolderNameMatchesDocument(string? folderName, string documentKey)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return false;
        }

        var lastToken = folderName
            .Split([' ', '\t', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        return NormalizeDocumentKey(lastToken) == documentKey;
    }

    private static string NormalizeDocumentKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    private IReadOnlyList<string> BuildPanAmericanFolderSegments()
    {
        var folderPath = GetConfigValue("SHAREPOINT_PANAMERICAN_FOLDER", "SharePoint:PanAmericanFolder");
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            folderPath = "DocumentosPamAmerican";
        }

        var segments = folderPath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => SanitizeSharePointName(segment, "DocumentosPamAmerican"))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        if (segments.Count == 0)
        {
            segments.Add("DocumentosPamAmerican");
        }

        return segments;
    }

    private async Task<ServiceResult> EnsureFolderPathAsync(
        string driveId,
        IReadOnlyList<string> folderSegments,
        string token,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < folderSegments.Count; index++)
        {
            var currentSegments = folderSegments.Take(index + 1).ToList();
            var currentPath = EscapeGraphPath(string.Join("/", currentSegments));
            using (var getRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"{GraphBaseUrl}/drives/{Uri.EscapeDataString(driveId)}/root:/{currentPath}"))
            {
                getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var getResponse = await _httpClient.SendAsync(getRequest, cancellationToken);
                if (getResponse.IsSuccessStatusCode)
                {
                    continue;
                }

                if (getResponse.StatusCode != HttpStatusCode.NotFound)
                {
                    var body = await getResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("SharePoint folder lookup failed with status {StatusCode}: {Body}", getResponse.StatusCode, body);
                    return ServiceResult.Failure($"No fue posible validar la carpeta en SharePoint. Estado: {(int)getResponse.StatusCode}.");
                }
            }

            var parentSegments = folderSegments.Take(index).ToList();
            var createUrl = parentSegments.Count == 0
                ? $"{GraphBaseUrl}/drives/{Uri.EscapeDataString(driveId)}/root/children"
                : $"{GraphBaseUrl}/drives/{Uri.EscapeDataString(driveId)}/root:/{EscapeGraphPath(string.Join("/", parentSegments))}:/children";
            using var createRequest = new HttpRequestMessage(HttpMethod.Post, createUrl);
            createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            createRequest.Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["name"] = folderSegments[index],
                ["folder"] = new { },
                ["@microsoft.graph.conflictBehavior"] = "fail"
            });

            using var createResponse = await _httpClient.SendAsync(createRequest, cancellationToken);
            if (createResponse.IsSuccessStatusCode || createResponse.StatusCode == HttpStatusCode.Conflict)
            {
                continue;
            }

            var createBody = await createResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("SharePoint folder create failed with status {StatusCode}: {Body}", createResponse.StatusCode, createBody);
            return ServiceResult.Failure($"No fue posible crear la carpeta en SharePoint. Estado: {(int)createResponse.StatusCode}.");
        }

        return ServiceResult.Success();
    }

    private async Task<string> GetAccessTokenAsync(SharePointConfig config, CancellationToken cancellationToken)
    {
        var tokenKey = $"{config.TenantId}|{config.ClientId}";
        if (TryGetCachedToken(tokenKey, out var cached))
        {
            return cached;
        }

        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            // Otra petición pudo renovarlo mientras se esperaba el turno.
            if (TryGetCachedToken(tokenKey, out cached))
            {
                return cached;
            }

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
            var token = document.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Graph no retorno access_token.");

            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expiresElement)
                && expiresElement.TryGetInt32(out var seconds)
                    ? seconds
                    : 3600;

            _cachedToken = token;
            _cachedTokenKey = tokenKey;
            // Se descuenta un minuto para no usar un token que caduque en pleno viaje.
            _cachedTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
            return token;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private static bool TryGetCachedToken(string tokenKey, out string token)
    {
        var cached = _cachedToken;
        if (cached is not null
            && string.Equals(_cachedTokenKey, tokenKey, StringComparison.Ordinal)
            && _cachedTokenExpiresAt > DateTimeOffset.UtcNow)
        {
            token = cached;
            return true;
        }

        token = string.Empty;
        return false;
    }

    private SharePointConfig GetConfig()
    {
        return new SharePointConfig
        {
            LibraryName = GetConfigValue("SHAREPOINT_LIBRARY", "SharePoint:Library"),
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

    private static IReadOnlyList<string> BuildFolderSegments(string baseFolderPath, string patientFolderName)
    {
        var baseSegments = baseFolderPath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => SanitizeSharePointName(segment, "TerapiasAmbulatorias"))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        if (baseSegments.Count == 0)
        {
            baseSegments.Add("TerapiasAmbulatorias");
        }

        baseSegments.Add(patientFolderName);
        return baseSegments;
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
        public string? LibraryName { get; init; }
        public string? LibraryId { get; init; }
        public string? SiteId { get; init; }
        public string? TenantId { get; init; }
        public string? ClientId { get; init; }
        public string? ClientSecret { get; init; }

        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(LibraryName)
            && !string.IsNullOrWhiteSpace(LibraryId)
            && !string.IsNullOrWhiteSpace(SiteId)
            && !string.IsNullOrWhiteSpace(TenantId)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret);
    }

    private sealed class GraphChildrenResponse
    {
        [JsonPropertyName("value")]
        public List<GraphDriveItem> Value { get; set; } = [];

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }
    }

    private sealed class GraphDriveItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("folder")]
        public object? Folder { get; set; }

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
