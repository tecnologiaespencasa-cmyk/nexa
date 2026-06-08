namespace IntranetPrueba.Services.Models;

public class EmailMessage
{
    public IReadOnlyList<string> To { get; set; } = [];

    public string Subject { get; set; } = string.Empty;

    public string HtmlBody { get; set; } = string.Empty;

    public IReadOnlyList<EmailAttachment> Attachments { get; set; } = [];
}

public class EmailAttachment
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public byte[] Content { get; set; } = [];
}
