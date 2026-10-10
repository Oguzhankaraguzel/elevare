using Application.Abstraction.Data;
using Application.Abstraction.Services.Email;
using Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users;

/// <summary>Why a link is being issued — it only changes the e-mail's wording.</summary>
internal enum PasswordLinkKind
{
    /// <summary>The first link of a new account.</summary>
    Setup,

    /// <summary>A new password for an account that may already have one.</summary>
    Reset,
}

/// <summary>
/// Issues the one-time links that let a user choose their own password, and mails
/// them when there is a mail server to mail them with. One place for all of it, so
/// creating a user, an administrator's reset, "forgot password" and the change that
/// follows an administrator-typed password all agree on what a link is.
/// A static helper taking its dependencies as parameters rather than its own
/// DI-registered service, since every call site already has them injected and the
/// Application layer doesn't otherwise register bespoke services (see
/// ApplicationServiceRegistration — only MediatR/FluentValidation).
/// </summary>
internal static class PasswordLinks
{
    /// <summary>
    /// A link an administrator issues. Short enough that one pasted into a chat and
    /// forgotten stops working soon; long enough to cover a weekend.
    /// </summary>
    public static readonly TimeSpan IssuedByAdministrator = TimeSpan.FromHours(48);

    /// <summary>"Forgot password": the person asking is the one who will use it, right away.</summary>
    public static readonly TimeSpan SelfService = TimeSpan.FromHours(1);

    /// <summary>
    /// The step between signing in with a password an administrator typed and choosing
    /// one's own. It is used the moment it is made; it only has to survive a slow typist.
    /// </summary>
    public static readonly TimeSpan ForcedChange = TimeSpan.FromMinutes(15);

    /// <summary>
    /// A fresh link for the user. Every link they still had is retired first: only the
    /// newest one should work, wherever the older ones ended up.
    /// </summary>
    /// <returns>The raw token, which exists only in the link — the database keeps its hash.</returns>
    public static async Task<(string Token, DateTime ExpiresAtUtc)> IssueAsync(
        ICmsApplicationDbContext db, Guid userId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        await RetireAsync(db, userId, now, cancellationToken);

        string rawToken = PasswordSetupTokenGenerator.CreateRawToken();
        DateTime expires = now.Add(lifetime);
        db.PasswordSetupTokens.Add(new PasswordSetupToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = PasswordSetupTokenGenerator.Hash(rawToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = expires,
        });
        await db.SaveChangesAsync(cancellationToken);

        return (rawToken, expires);
    }

    /// <summary>Retires every outstanding link of the user. Staged; the caller saves.</summary>
    public static async Task RetireAsync(
        ICmsApplicationDbContext db, Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        List<PasswordSetupToken> outstanding = await db.PasswordSetupTokens
            .Where(t => t.UserId == userId && t.ConsumedAtUtc == null && t.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        foreach (PasswordSetupToken token in outstanding)
            token.ConsumedAtUtc = now;
    }

    public static string BuildUrl(string cmsBaseUrl, string token, bool forcedChange = false) =>
        $"{cmsBaseUrl.TrimEnd('/')}/account/set-password?token={Uri.EscapeDataString(token)}"
        + (forcedChange ? "&mode=change" : "");

    /// <summary>
    /// Mails the link. A failure is logged, not returned as an error: the link exists
    /// either way, and every caller can hand it to an administrator instead.
    /// </summary>
    public static async Task<bool> SendAsync(
        IEmailService emailService,
        ILogger logger,
        AppUser user,
        string url,
        TimeSpan lifetime,
        PasswordLinkKind kind,
        CancellationToken cancellationToken)
    {
        if (!await emailService.IsConfiguredAsync(cancellationToken))
            return false;

        string name = System.Net.WebUtility.HtmlEncode(user.FirstName ?? user.UserName);
        string validFor = lifetime >= TimeSpan.FromHours(1)
            ? $"{(int)lifetime.TotalHours} saat"
            : $"{(int)lifetime.TotalMinutes} dakika";

        (string subject, string body) = kind switch
        {
            PasswordLinkKind.Reset => (
                "Elevare CMS - Şifre sıfırlama",
                $"""
                <p>Merhaba {name},</p>
                <p>Elevare CMS hesabınız için şifre sıfırlama isteği aldık. Aşağıdaki bağlantıdan yeni şifrenizi belirleyebilirsiniz. Bağlantı <strong>{validFor}</strong> boyunca geçerlidir ve yalnızca bir kez kullanılabilir.</p>
                <p><a href="{url}">Yeni şifremi belirle</a></p>
                <p>Bu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz; şifreniz değişmez.</p>
                """),
            _ => (
                "Elevare CMS - Şifrenizi Belirleyin",
                $"""
                <p>Merhaba {name},</p>
                <p>Elevare CMS'te sizin için bir hesap oluşturuldu. Aşağıdaki bağlantıdan kendi şifrenizi belirleyebilirsiniz. Bağlantı <strong>{validFor}</strong> boyunca geçerlidir.</p>
                <p><a href="{url}">Şifremi belirle</a></p>
                <p>Bağlantının süresi dolduysa yöneticinizden yeni bir bağlantı isteyebilirsiniz.</p>
                """),
        };

        Result sent = await emailService.SendAsync(EmailMessage.Create(user.Email!, subject, body), cancellationToken);
        if (sent.IsFailure)
        {
            logger.LogWarning(
                "Could not send the password link e-mail to {Email} for user {UserId}: {Error}",
                user.Email, user.Id, sent.Error.Description);
        }

        return sent.IsSuccess;
    }
}
