namespace Application.Abstraction.Services.Email;

/// <summary>Represents a file attached to an outgoing e-mail.</summary>
public sealed class EmailAttachment
{
    /// <summary>File name shown to the recipient (e.g. <c>invoice.pdf</c>).</summary>
    public required string FileName { get; init; }

    /// <summary>Raw content of the attachment.</summary>
    public required Stream Content { get; init; }

    /// <summary>MIME type of the attachment (e.g. <c>application/pdf</c>).</summary>
    public string ContentType { get; init; } = "application/octet-stream";
}
