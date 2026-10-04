using SharedKernel.Concrete;

namespace Application.Features.Commands.SiteSettings.SendTestEmail;

/// <summary>
/// Reasons the test could not be ATTEMPTED. An SMTP server that refused the message
/// is not one of these — that failure comes back from <c>IEmailService</c> carrying
/// the server's own words, which is the only part an operator can act on.
/// </summary>
public static class TestEmailErrors
{
    public static Error InvalidRecipient(string value) =>
        Error.Failure(
            "TestEmail.InvalidRecipient",
            string.IsNullOrWhiteSpace(value)
                ? "Enter an address to send the test to."
                : $"'{value}' is not a valid e-mail address.");
}
