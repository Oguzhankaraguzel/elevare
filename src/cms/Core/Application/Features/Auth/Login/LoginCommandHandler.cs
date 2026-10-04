using SharedKernel.Abstraction.Messaging;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.Logs;
using Domain.Entities.Permissions;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using SharedKernel.Concrete;

namespace Application.Features.Auth.Login;

internal sealed class LoginCommandHandler : ICommandHandler<LoginCommand, LoginResponse>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly ITokenProvider _tokenProvider;
    private readonly IAuthEventLogger _authEventLogger;

    public LoginCommandHandler(
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        ITokenProvider tokenProvider,
        IAuthEventLogger authEventLogger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenProvider = tokenProvider;
        _authEventLogger = authEventLogger;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Accept either identifier. People remember the name they were given far
        // more reliably than which address the account was opened with, and the two
        // namespaces cannot collide: an e-mail always contains '@', a user name is
        // rejected by Identity if it does.
        string identifier = request.Identifier.Trim();
        AppUser? user = identifier.Contains('@', StringComparison.Ordinal)
            ? await _userManager.FindByEmailAsync(identifier)
            : await _userManager.FindByNameAsync(identifier);

        // Fall back to the other lookup rather than refusing: an operator may have
        // created a user whose name happens to look like an address.
        user ??= await _userManager.FindByNameAsync(identifier);

        if (user is null || !user.IsActive)
        {
            // saveImmediately: false everywhere in this handler — LoginCommand is a
            // command, so SaveChangesPipelineBehavior commits everything staged here
            // (this row, and on success the user's LastLoginDate) in one round trip
            // once the handler returns, instead of each write paying for its own.
            await _authEventLogger.LogAsync(AuthEventType.LoginFailed, user?.Id, identifier, saveImmediately: false, cancellationToken);
            return Result.Failure<LoginResponse>(AuthErrors.InvalidCredentials);
        }

        // Checked BEFORE the password: a locked account must stay locked even when
        // the attacker eventually guesses right, and verifying the hash first would
        // also leak timing about whether the guess was correct.
        if (await _userManager.IsLockedOutAsync(user))
        {
            await _authEventLogger.LogAsync(AuthEventType.LoginFailed, user.Id, identifier, saveImmediately: false, cancellationToken);
            return Result.Failure<LoginResponse>(AuthErrors.AccountLocked);
        }

        bool validPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
        {
            // The counter that makes the lockout policy real. CheckPasswordAsync on
            // its own never records anything, which is why the schema had
            // AccessFailedCount and LockoutEnd columns that stayed at zero forever.
            await _userManager.AccessFailedAsync(user);
            await _authEventLogger.LogAsync(AuthEventType.LoginFailed, user.Id, identifier, saveImmediately: false, cancellationToken);

            // Report the lockout that this very attempt caused, rather than another
            // "wrong password" that leaves the person retrying against a wall.
            return await _userManager.IsLockedOutAsync(user)
                ? Result.Failure<LoginResponse>(AuthErrors.AccountLocked)
                : Result.Failure<LoginResponse>(AuthErrors.InvalidCredentials);
        }

        // Staged, not saved — the row rides along with whichever save below actually
        // hits the database (ResetAccessFailedCountAsync only writes if there was
        // something to reset; the LastLoginDate update always does).
        await _authEventLogger.LogAsync(AuthEventType.LoginSucceeded, user.Id, identifier, saveImmediately: false, cancellationToken);

        // A correct password clears the tally; five failures spread over months are
        // not an attack and must not accumulate into a lockout.
        await _userManager.ResetAccessFailedCountAsync(user);

        // The column existed and was read in three places — the Users list column,
        // its "last login after" filter, and now the profile page — but nothing ever
        // wrote it, so all three quietly reported "never" for everyone.
        user.LastLoginDate = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        IList<string> roles = await _userManager.GetRolesAsync(user);

        HashSet<string> permissions = [];
        foreach (string roleName in roles)
        {
            AppRole? role = await _roleManager.FindByNameAsync(roleName);
            if (role is null) continue;

            foreach (System.Security.Claims.Claim claim in await _roleManager.GetClaimsAsync(role))
                if (claim.Type == PermissionKeys.ClaimType)
                    permissions.Add(claim.Value);
        }

        string token = _tokenProvider.GenerateToken(
            user.Id.ToString(),
            user.UserName!,
            user.Email!,
            [.. roles],
            [.. permissions]);

        return Result.Success(new LoginResponse(
            UserId: user.Id,
            Email: user.Email!,
            FullName: user.FullName,
            AvatarUrl: user.AvatarUrl,
            Roles: roles.ToList().AsReadOnly(),
            Token: token,
            ExpiresAt: DateTime.UtcNow.AddHours(8)));
    }
}
