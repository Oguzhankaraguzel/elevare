using Application.Abstraction.Services.Email;
using SharedKernel.Concrete;

namespace Cms.Tests.Support;

/// <summary>Keeps every message instead of sending it, so a test can read the link a user was sent.</summary>
internal sealed class RecordingEmailService : IEmailService
{
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>False plays a fresh install with no mail server: nothing is sent.</summary>
    public bool Configured { get; set; } = true;

    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default) => Task.FromResult(Configured);

    public Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!Configured)
            return Task.FromResult(Result.Failure(Error.Failure("Email.NotConfigured", "No mail server.")));

        Sent.Add(message);
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> SendBulkAsync(IEnumerable<EmailMessage> messages, CancellationToken cancellationToken = default)
    {
        foreach (EmailMessage message in messages)
            await SendAsync(message, cancellationToken);
        return Result.Success();
    }

    public Task<Result> SendFromTemplateAsync(string templateName, string to, string subject,
        IDictionary<string, string> placeholders, CancellationToken cancellationToken = default) =>
        SendAsync(EmailMessage.Create(to, subject, string.Join("\n", placeholders.Select(p => $"{p.Key}={p.Value}"))), cancellationToken);
}
