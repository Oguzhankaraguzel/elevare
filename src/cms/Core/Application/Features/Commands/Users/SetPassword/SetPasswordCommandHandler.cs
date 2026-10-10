using Application.Features.Commands.Users;
using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users.SetPassword;

internal sealed class SetPasswordCommandHandler(
    ICmsApplicationDbContext db,
    UserManager<AppUser> userManager) : ICommandHandler<SetPasswordCommand>
{
    public async Task<Result> Handle(SetPasswordCommand request, CancellationToken cancellationToken)
    {
        string hash = PasswordSetupTokenGenerator.Hash(request.Token);
        DateTime now = DateTime.UtcNow;

        PasswordSetupToken? tokenRow = await db.PasswordSetupTokens
            .Where(t => t.TokenHash == hash && t.ConsumedAtUtc == null && t.ExpiresAtUtc > now)
            .FirstOrDefaultAsync(cancellationToken);

        if (tokenRow is null)
            return Result.Failure(PasswordSetupErrors.InvalidOrExpiredToken);

        AppUser? user = await userManager.FindByIdAsync(tokenRow.UserId.ToString());
        if (user is null || !user.IsActive)
            return Result.Failure(PasswordSetupErrors.InvalidOrExpiredToken);

        // Arriving here from a sign-in with an administrator's password: keeping that
        // password would leave it one somebody else knows, which is the whole reason
        // for the detour.
        if (user.MustChangePassword && await userManager.CheckPasswordAsync(user, request.NewPassword))
            return Result.Failure(PasswordSetupErrors.SameAsTemporary);

        // Saved by the password call below, together with the password itself.
        user.MustChangePassword = false;

        // AddPasswordAsync only succeeds when the account has none yet (the normal
        // case for a fresh invite). If a SuperAdmin already set one manually — or the
        // user is re-using an older, still-valid e-mail after already finishing setup
        // once — fall back to the reset flow instead of failing the request.
        IdentityResult result = await userManager.HasPasswordAsync(user)
            ? await ResetExistingPasswordAsync(user, request.NewPassword)
            : await userManager.AddPasswordAsync(user, request.NewPassword);

        if (!result.Succeeded)
        {
            return Result.Failure(PasswordSetupErrors.SetFailed(result.Describe()));
        }

        // Every other link the user had goes too: the question they answered is settled.
        await PasswordLinks.RetireAsync(db, user.Id, now, cancellationToken);
        tokenRow.ConsumedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<IdentityResult> ResetExistingPasswordAsync(AppUser user, string newPassword)
    {
        string resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        return await userManager.ResetPasswordAsync(user, resetToken, newPassword);
    }
}
