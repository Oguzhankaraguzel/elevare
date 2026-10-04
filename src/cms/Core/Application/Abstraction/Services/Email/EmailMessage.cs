namespace Application.Abstraction.Services.Email;

/// <summary>
/// Represents a fully constructed outgoing e-mail message.
/// At least one address in <see cref="To"/> is required.
/// </summary>
public sealed class EmailMessage
{
    /// <summary>Primary recipient addresses.</summary>
    public required IReadOnlyList<string> To { get; init; }

    /// <summary>Carbon-copy recipient addresses.</summary>
    public IReadOnlyList<string> Cc { get; init; } = [];

    /// <summary>Blind carbon-copy recipient addresses.</summary>
    public IReadOnlyList<string> Bcc { get; init; } = [];

    /// <summary>E-mail subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>Body content — HTML by default, plain text when <see cref="IsHtml"/> is <c>false</c>.</summary>
    public required string Body { get; init; }

    /// <summary>When <c>true</c> (default) the body is rendered as HTML.</summary>
    public bool IsHtml { get; init; } = true;

    /// <summary>Optional reply-to address — overrides the sender address for replies.</summary>
    public string? ReplyTo { get; init; }

    /// <summary>Delivery priority. Defaults to <see cref="EmailPriority.Normal"/>.</summary>
    public EmailPriority Priority { get; init; } = EmailPriority.Normal;

    /// <summary>Files to attach to the message.</summary>
    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = [];

    // ── Convenience factory helpers ───────────────────────────────────────────

    /// <summary>Creates a simple single-recipient HTML message.</summary>
    public static EmailMessage Create(string to, string subject, string htmlBody) =>
        new() { To = [to], Subject = subject, Body = htmlBody };

    /// <summary>Creates a simple single-recipient plain-text message.</summary>
    public static EmailMessage CreatePlainText(string to, string subject, string body) =>
        new() { To = [to], Subject = subject, Body = body, IsHtml = false };
}
