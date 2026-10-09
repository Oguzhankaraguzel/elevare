using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Users.RequestPasswordReset;

/// <summary>
/// "Forgot password": mails a short-lived reset link to the account with this e-mail
/// address or user name. Reached anonymously, like <c>SetPasswordCommand</c>, so it
/// always succeeds — whether the account exists, is active or got a mail is nobody's
/// business who only knows an address.
/// </summary>
/// <param name="CmsBaseUrl">
/// The CMS's own address, from configuration — never from the request: a link built
/// from the Host header would let anyone send a real user a reset link that leads to
/// their own server.
/// </param>
// CA1054: only ever concatenated into a link, never parsed as a Uri.
#pragma warning disable CA1054
public sealed record RequestPasswordResetCommand(string Identifier, string CmsBaseUrl) : ICommand;
#pragma warning restore CA1054
