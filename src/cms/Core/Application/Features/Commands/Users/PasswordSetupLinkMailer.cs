using Application.Abstraction.Data;
using Application.Abstraction.Services.Email;
using Domain.Entities.Users;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users;

/// <summary>
/// Issues a fresh password-setup link and e-mails it — shared between
/// <c>CreateUserCommandHandler</c> (the first link a new user gets) and
/// <c>ResendPasswordSetupCommandHandler</c> (a SuperAdmin asking for another one
/// after the first expired). A static helper taking its dependencies as parameters
/// rather than its own DI-registered service, since both call sites already have
/// every dependency injected and the Application layer doesn't otherwise register
/// bespoke services (see ApplicationServiceRegistration — only MediatR/FluentValidation).
/// </summary>
internal static class PasswordSetupLinkMailer
{
    /// <summary>
    /// How long a password-setup link stays valid. If nobody sets a password within
    /// this window, a SuperAdmin can resend a fresh link or set one directly — see
    /// ResendPasswordSetupCommand / SetUserPasswordManuallyCommand.
    /// </summary>
    public static readonly TimeSpan SetupLinkLifetime = TimeSpan.FromDays(7);

    public static async Task<bool> SendAsync(
        ICmsApplicationDbContext db,
        IEmailService emailService,
        ILogger logger,
        AppUser user,
        string cmsBaseUrl,
        CancellationToken cancellationToken)
    {
        string rawToken = PasswordSetupTokenGenerator.CreateRawToken();
        DateTime now = DateTime.UtcNow;

        db.PasswordSetupTokens.Add(new PasswordSetupToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = PasswordSetupTokenGenerator.Hash(rawToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(SetupLinkLifetime),
        });
        await db.SaveChangesAsync(cancellationToken);

        string link = $"{cmsBaseUrl.TrimEnd('/')}/account/set-password?token={Uri.EscapeDataString(rawToken)}";
        string body = $"""
            <p>Merhaba {System.Net.WebUtility.HtmlEncode(user.FirstName ?? user.UserName)},</p>
            <p>Elevare CMS'te sizin için bir hesap oluşturuldu. Aşağıdaki bağlantıdan kendi şifrenizi belirleyebilirsiniz. Bu bağlantı <strong>7 gün</strong> boyunca geçerlidir.</p>
            <p><a href="{link}">Şifremi belirle</a></p>
            <p>Bağlantının süresi dolduysa, yöneticinizden yeni bir bağlantı göndermesini isteyebilirsiniz.</p>
            """;

        // A failed send is not fatal to the caller — the token row already exists, so
        // a SuperAdmin can resend from the Users list without anything having to be
        // redone. Only logged, not surfaced as a command failure.
        Result sent = await emailService.SendAsync(
            EmailMessage.Create(user.Email!, "Elevare CMS - Şifrenizi Belirleyin", body),
            cancellationToken);

        if (sent.IsFailure)
        {
            logger.LogWarning(
                "Could not send the password-setup e-mail to {Email} for user {UserId}: {Error}",
                user.Email, user.Id, sent.Error.Description);
        }

        return sent.IsSuccess;
    }
}
