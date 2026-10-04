using System.Globalization;
using Application.Abstraction.Services.Email;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.SiteSettings.SendTestEmail;

internal sealed class SendTestEmailCommandHandler(IEmailService email)
    : ICommandHandler<SendTestEmailCommand>
{
    public async Task<Result> Handle(SendTestEmailCommand request, CancellationToken cancellationToken)
    {
        string to = (request.ToAddress ?? string.Empty).Trim();

        // Cheap shape check only — the SMTP server is the real authority on whether an
        // address is deliverable, and its refusal is a more useful message than ours.
        if (to.Length == 0 || !to.Contains('@', StringComparison.Ordinal) || to.StartsWith('@') || to.EndsWith('@'))
            return Result.Failure(TestEmailErrors.InvalidRecipient(to));

        string sentAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

        // Plain text on purpose: an HTML body that renders means the mail client works,
        // not that delivery does, and a spam filter is measurably harsher on a one-line
        // HTML mail from a brand-new sender than on the plain-text equivalent.
        var message = EmailMessage.CreatePlainText(
            to,
            "Elevare — test",
            $"This is a test message sent from Elevare's Site Settings screen at {sentAt}.\r\n\r\n"
            + "If you are reading it, the SMTP host, credentials and sender address are all working.\r\n"
            + "Nothing else was sent and no one else received a copy.");

        return await email.SendAsync(message, cancellationToken);
    }
}
