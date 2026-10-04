namespace Infrastructure.Email;

/// <summary>
/// SMTP configuration bound from <c>appsettings.json → Email</c> — or, if set, from
/// the encrypted <c>IntegrationSecrets</c> table (the CMS's "Sırlar" screen), layered
/// on top of the file at startup; see <c>IntegrationSecretsBootstrap</c>.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>SMTP host name (e.g. <c>smtp.gmail.com</c>).</summary>
    public required string Host { get; init; }

    /// <summary>SMTP port. Common values: 25 (plain), 587 (STARTTLS), 465 (SSL).</summary>
    public int Port { get; init; } = 587;

    /// <summary>Enable SSL/TLS. Typically <c>true</c> for port 587 or 465.</summary>
    public bool EnableSsl { get; init; } = true;

    /// <summary>SMTP authentication user name.</summary>
    public required string UserName { get; init; }

    /// <summary>SMTP authentication password or app-specific password.</summary>
    public required string Password { get; init; }

    /// <summary>The "From" address used for all outgoing e-mails.</summary>
    public required string FromAddress { get; init; }

    /// <summary>The display name shown in the "From" field.</summary>
    public string FromDisplayName { get; init; } = "CMS";

    /// <summary>
    /// Folder (relative to the application content root) that holds <c>.html</c> template files.
    /// Defaults to <c>EmailTemplates</c>.
    /// </summary>
    public string TemplatesFolder { get; init; } = "EmailTemplates";

    /// <summary>
    /// Maximum number of send attempts before giving up.
    /// Defaults to 3.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>Base delay in milliseconds between retry attempts.</summary>
    public int RetryDelayMs { get; init; } = 500;
}
