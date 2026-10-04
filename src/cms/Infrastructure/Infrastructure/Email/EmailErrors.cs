using SharedKernel.Concrete;

namespace Infrastructure.Email;

/// <summary>Domain errors produced by <see cref="EmailService"/>.</summary>
internal static class EmailErrors
{
    public static readonly Error NoRecipients =
        new("Email.NoRecipients", "At least one recipient address is required.", ErrorType.Validation);

    public static readonly Error TemplateNameEmpty =
        new("Email.TemplateNameEmpty", "Template name must not be empty.", ErrorType.Validation);

    public static Error TemplateNotFound(string name) =>
        new("Email.TemplateNotFound", $"E-mail template '{name}' was not found.", ErrorType.NotFound);

    public static Error SendFailed(string reason) =>
        new("Email.SendFailed", $"Failed to send e-mail: {reason}", ErrorType.Problem);

    /// <summary>
    /// No usable "From" address anywhere — neither the <c>Email.FromAddress</c> site
    /// setting nor <c>EmailOptions.FromAddress</c> in appsettings.json. Reported
    /// rather than thrown: an unconfigured mail server is an ordinary state for a
    /// fresh install, and every caller already handles a failed Result. Left as an
    /// exception it surfaced as a raw ArgumentException from deep inside
    /// System.Net.Mail, which told the operator nothing about what to fix.
    /// </summary>
    public static readonly Error SenderNotConfigured =
        new("Email.SenderNotConfigured",
            "No sender address is configured. Set one under Site Settings > System, "
            + "or Email:FromAddress in appsettings.json.",
            ErrorType.Validation);
}
