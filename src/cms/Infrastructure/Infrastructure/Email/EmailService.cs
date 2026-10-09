using System.Net;
using System.Net.Mail;
using Application.Abstraction.Data;
using Application.Abstraction.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Strings;

namespace Infrastructure.Email;

/// <summary>
/// SMTP-based implementation of <see cref="IEmailService"/> using <see cref="SmtpClient"/>.
/// Supports HTML bodies, attachments, CC/BCC, reply-to, priorities and HTML template files
/// with <c>{{Key}}</c> placeholder substitution.
/// </summary>
/// <remarks>
/// SMTP host/port/credentials are read from <see cref="EmailOptions"/>, which by the time
/// this runs may already be an override loaded from the encrypted <c>IntegrationSecrets</c>
/// table (the CMS's "Sırlar" screen) rather than appsettings.json — see
/// <c>IntegrationSecretsBootstrap</c>. Read via <see cref="IOptionsMonitor{TOptions}"/>
/// rather than <c>IOptions&lt;T&gt;</c> so a value saved from "Sırlar" applies to the very
/// next send with no restart — the service neither knows nor cares whether today's
/// snapshot came from appsettings.json or the database.
/// The "From" identity (address/display name) is the one part a SuperAdmin can override
/// from /admin/settings without a redeploy — see <c>Email.FromAddress</c>/<c>Email.FromDisplayName</c>
/// in <c>DatabaseSeeder</c>, falling back to <see cref="EmailOptions"/> when unset.
/// </remarks>
internal sealed class EmailService : IEmailService
{
    private const string FromAddressKey = "Email.FromAddress";
    private const string FromDisplayNameKey = "Email.FromDisplayName";

    private readonly IOptionsMonitor<EmailOptions> _optionsMonitor;
    private readonly ICmsApplicationDbContext _db;
    private readonly IHostEnvironment _env;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptionsMonitor<EmailOptions> optionsMonitor,
        ICmsApplicationDbContext db,
        IHostEnvironment env,
        ILogger<EmailService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _db      = db;
        _env     = env;
        _logger  = logger;
    }

    // ── IEmailService ─────────────────────────────────────────────────────────

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        EmailOptions options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.Host))
            return false;

        (string fromAddress, _) = await ResolveFromIdentityAsync(options, cancellationToken);
        return !string.IsNullOrWhiteSpace(fromAddress);
    }

    public async Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.To.Count == 0)
            return Result.Failure(EmailErrors.NoRecipients);

        return await ExecuteWithRetryAsync(message, cancellationToken);
    }

    public async Task<Result> SendBulkAsync(
        IEnumerable<EmailMessage> messages,
        CancellationToken cancellationToken = default)
    {
        foreach (EmailMessage message in messages)
        {
            Result result = await SendAsync(message, cancellationToken);
            if (result.IsFailure)
                return result;
        }
        return Result.Success();
    }

    public async Task<Result> SendFromTemplateAsync(
        string templateName,
        string to,
        string subject,
        IDictionary<string, string> placeholders,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            return Result.Failure(EmailErrors.TemplateNameEmpty);

        string templatePath = Path.Combine(
            _env.ContentRootPath,
            _optionsMonitor.CurrentValue.TemplatesFolder,
            $"{templateName}.html");

        if (!File.Exists(templatePath))
        {
            _logger.LogWarning("[Email] Template '{Template}' not found at '{Path}'.", templateName, templatePath);
            return Result.Failure(EmailErrors.TemplateNotFound(templateName));
        }

        string body = await File.ReadAllTextAsync(templatePath, cancellationToken);

        foreach ((string key, string value) in placeholders)
            body = body.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);

        var message = EmailMessage.Create(to, subject, body);
        return await SendAsync(message, cancellationToken);
    }

    // ── Core send logic ───────────────────────────────────────────────────────

    private async Task<Result> ExecuteWithRetryAsync(
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        // Read once per send, not once per attempt: if a retry loop straddled a
        // Sırlar save it could otherwise start on the old SMTP host and end on the
        // new one — one consistent snapshot per logical send is the correct unit.
        EmailOptions options = _optionsMonitor.CurrentValue;

        // No host is a fresh install, not an outage: answered at once instead of
        // after three retries against an SmtpClient that can only throw.
        if (string.IsNullOrWhiteSpace(options.Host))
            return Result.Failure(EmailErrors.NotConfigured);

        (string fromAddress, string fromDisplayName) = await ResolveFromIdentityAsync(options, cancellationToken);

        // Checked once, before the retry loop: a missing sender is a configuration
        // state, not a transient fault, so retrying it three times with backoff only
        // delays the same answer. MailAddress would otherwise throw ArgumentException
        // straight past the SmtpException filter below and out of the service.
        if (string.IsNullOrWhiteSpace(fromAddress))
            return Result.Failure(EmailErrors.SenderNotConfigured);

        Exception? lastException = null;

        for (int attempt = 1; attempt <= options.MaxRetryAttempts; attempt++)
        {
            try
            {
                await SendInternalAsync(message, fromAddress, fromDisplayName, options, cancellationToken);
                _logger.LogInformation(
                    "[Email] Sent '{Subject}' to {Recipients}.",
                    message.Subject,
                    string.Join(", ", message.To));
                return Result.Success();
            }
            // SocketException/IOException surface as the inner exception of an
            // SmtpException here, but a host that does not resolve at all can also
            // arrive as a bare InvalidOperationException — all three mean "the mail
            // server did not accept this", which is a reportable failure, not a crash.
            catch (Exception ex) when (ex is SmtpException or InvalidOperationException or IOException)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "[Email] Attempt {Attempt}/{Max} failed for '{Subject}'.",
                    attempt, options.MaxRetryAttempts, message.Subject);

                if (attempt < options.MaxRetryAttempts)
                    await Task.Delay(options.RetryDelayMs * attempt, cancellationToken);
            }
        }

        _logger.LogError(
            lastException,
            "[Email] All {Max} attempts failed for '{Subject}'.",
            options.MaxRetryAttempts, message.Subject);

        return Result.Failure(EmailErrors.SendFailed(DescribeFailure(lastException)));
    }

    /// <summary>
    /// The whole exception chain, not just the outermost message. SmtpException's own
    /// text is the useless "Failure sending mail." for every transport problem there
    /// is — the sentence that names the actual cause ("No such host is known",
    /// "The remote certificate is invalid", an authentication refusal) is always the
    /// inner one, and dropping it leaves an operator with nothing to act on.
    /// </summary>
    private static string DescribeFailure(Exception? exception)
    {
        if (exception is null)
            return "Unknown SMTP error";

        List<string> parts = [];
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (!parts.Contains(current.Message, StringComparer.Ordinal))
                parts.Add(current.Message);
        }

        return string.Join(" → ", parts);
    }

    private async Task SendInternalAsync(
        EmailMessage message, string fromAddress, string fromDisplayName, EmailOptions options, CancellationToken cancellationToken)
    {
        using SmtpClient smtp = BuildSmtpClient(options);
        using MailMessage mail = BuildMailMessage(message, fromAddress, fromDisplayName);

        await smtp.SendMailAsync(mail, cancellationToken);
    }

    private async Task<(string Address, string DisplayName)> ResolveFromIdentityAsync(EmailOptions options, CancellationToken cancellationToken)
    {
        Dictionary<string, string?> overrides = await _db.SiteSettings
            .AsNoTracking()
            .Where(s => s.Key == FromAddressKey || s.Key == FromDisplayNameKey)
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string address = overrides.GetValueOrDefault(FromAddressKey).HasValue()
            ? overrides[FromAddressKey]!
            : options.FromAddress;
        string displayName = overrides.GetValueOrDefault(FromDisplayNameKey).HasValue()
            ? overrides[FromDisplayNameKey]!
            : options.FromDisplayName;

        return (address, displayName);
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static SmtpClient BuildSmtpClient(EmailOptions options) =>
        new(options.Host, options.Port)
        {
            EnableSsl   = options.EnableSsl,
            Credentials = new NetworkCredential(options.UserName, options.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

    private static MailMessage BuildMailMessage(EmailMessage message, string fromAddress, string fromDisplayName)
    {
        MailMessage mail = new()
        {
            From       = new MailAddress(fromAddress, fromDisplayName),
            Subject    = message.Subject,
            Body       = message.Body,
            IsBodyHtml = message.IsHtml,
            Priority   = MapPriority(message.Priority),
        };

        foreach (string to in message.To)
            mail.To.Add(to);

        foreach (string cc in message.Cc)
            mail.CC.Add(cc);

        foreach (string bcc in message.Bcc)
            mail.Bcc.Add(bcc);

        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
            mail.ReplyToList.Add(message.ReplyTo);

        foreach (EmailAttachment attachment in message.Attachments)
            mail.Attachments.Add(new Attachment(attachment.Content, attachment.FileName, attachment.ContentType));

        return mail;
    }

    private static MailPriority MapPriority(EmailPriority priority) => priority switch
    {
        EmailPriority.High => MailPriority.High,
        EmailPriority.Low  => MailPriority.Low,
        _                  => MailPriority.Normal,
    };
}
