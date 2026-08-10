namespace Nexa.Services.Models;

public class SharePointDocumentItem
{
    public string Name { get; set; } = string.Empty;

    public string WebUrl { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTimeOffset? LastModifiedAt { get; set; }
}
