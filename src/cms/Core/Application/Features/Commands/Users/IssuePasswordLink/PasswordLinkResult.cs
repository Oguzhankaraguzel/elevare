namespace Application.Features.Commands.Users.IssuePasswordLink;

/// <param name="Url">The link, whenever it was not mailed; null once it is in the user's inbox.</param>
// CA1054/CA1056: a link built for an administrator to copy, never parsed as a Uri.
#pragma warning disable CA1054, CA1056
public sealed record PasswordLinkResult(bool EmailSent, string? Url, DateTime ExpiresAtUtc);
#pragma warning restore CA1054, CA1056
