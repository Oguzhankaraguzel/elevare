using SharedKernel.Concrete;

namespace Application.Abstraction.Services.Email;

/// <summary>
/// Contract for sending e-mail messages.
/// Implementations live in the Infrastructure layer (SMTP, SendGrid, etc.).
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Whether a mail server and a sender address are configured at all. Says nothing
    /// about whether a send would succeed — only that there is something to try, so a
    /// screen can tell "mail is not set up" apart from "mail failed this time".
    /// </summary>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends a single e-mail message.</summary>
    Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends multiple messages sequentially.
    /// Returns the first failure encountered; prior successes are not rolled back.
    /// </summary>
    Task<Result> SendBulkAsync(
        IEnumerable<EmailMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a templated e-mail by loading an HTML file from the templates folder,
    /// replacing <c>{{Key}}</c> placeholders with the supplied <paramref name="placeholders"/>,
    /// then delivering the result to <paramref name="to"/>.
    /// </summary>
    /// <param name="templateName">
    ///     Template file name without extension (e.g. <c>"WelcomeEmail"</c>).
    /// </param>
    /// <param name="placeholders">
    ///     Key/value pairs whose keys match the <c>{{Key}}</c> tokens in the template.
    /// </param>
    Task<Result> SendFromTemplateAsync(
        string templateName,
        string to,
        string subject,
        IDictionary<string, string> placeholders,
        CancellationToken cancellationToken = default);
}
