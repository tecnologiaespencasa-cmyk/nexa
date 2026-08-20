namespace Nexa.Services.Models;

public class SharePointDocumentItem
{
    public string Name { get; set; } = string.Empty;

    public string WebUrl { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTimeOffset? LastModifiedAt { get; set; }
}

/// <summary>Carpeta de SharePoint localizada por su nombre.</summary>
public class SharePointFolderRef
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string WebUrl { get; set; } = string.Empty;
}

/// <summary>Contenido binario de un archivo de SharePoint, listo para reenviar al navegador.</summary>
public class SharePointFileContent
{
    public byte[] Content { get; set; } = [];

    public string ContentType { get; set; } = "application/octet-stream";

    public string Name { get; set; } = string.Empty;
}
