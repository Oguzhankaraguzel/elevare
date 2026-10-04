using System.Text.RegularExpressions;
using Application.Abstraction.Data;
using Application.Abstraction.Services.Email;
using Application.Features.Commands.Users.ChangeMyPassword;
using Application.Features.Commands.Users.CreateUser;
using Application.Features.Commands.Users.ResendPasswordSetup;
using Application.Features.Commands.Users.SetPassword;
using Application.Features.Commands.Users.SetUserPasswordManually;
using Cms.Tests.Support;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// The account lifecycle against a real UserManager: an account is created with no
/// password and a one-time setup link, the link sets the password once, and the
/// other ways in — changing your own, an administrator setting one, sending a new
/// link — each hold to their own rule.
/// </summary>
public sealed partial class UserAccountTests : IDisposable
{
    private const string CmsUrl = "https://cms.example.com";
    private const string Password = "Gizli1234";

    private readonly IdentityTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [GeneratedRegex(@"set-password\?token=([^""&]+)")]
    private static partial Regex SetupLink();

    private string LastSetupToken() =>
        Uri.UnescapeDataString(SetupLink().Match(_host.Emails.Sent[^1].Body).Groups[1].Value);

    private async Task SeedRoleAsync(string name)
    {
        using IServiceScope scope = _host.Scope();
        await scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>().CreateAsync(new AppRole { Name = name });
    }

    private async Task<Result<CreateUserResult>> CreateAsync(string email = "ayse@example.com", string role = "Editor")
    {
        using IServiceScope scope = _host.Scope();
        IServiceProvider sp = scope.ServiceProvider;
        return await new CreateUserCommandHandler(sp.GetRequiredService<UserManager<AppUser>>(), sp.GetRequiredService<ICmsApplicationDbContext>(),
                sp.GetRequiredService<IEmailService>(), NullLogger<CreateUserCommandHandler>.Instance)
            .Handle(new CreateUserCommand(email, email.Split('@')[0], "Ayşe", null, null, role, CmsUrl), CancellationToken.None);
    }

    private async Task<Result> SetPasswordAsync(string token, string password = Password)
    {
        using IServiceScope scope = _host.Scope();
        IServiceProvider sp = scope.ServiceProvider;
        return await new SetPasswordCommandHandler(sp.GetRequiredService<ICmsApplicationDbContext>(), sp.GetRequiredService<UserManager<AppUser>>())
            .Handle(new SetPasswordCommand(token, password), CancellationToken.None);
    }

    private async Task<bool> PasswordWorksAsync(Guid userId, string password)
    {
        using IServiceScope scope = _host.Scope();
        UserManager<AppUser> users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return await users.CheckPasswordAsync((await users.FindByIdAsync(userId.ToString()))!, password);
    }

    [Fact]
    public async Task A_new_account_has_no_password_its_role_and_a_setup_link_by_email()
    {
        await SeedRoleAsync("Editor");

        Result<CreateUserResult> result = await CreateAsync();

        result.Value.EmailSent.ShouldBeTrue();
        _host.Emails.Sent.Single().To.ShouldBe(["ayse@example.com"]);
        _host.Emails.Sent.Single().Body.ShouldContain($"{CmsUrl}/account/set-password?token=");
        using IServiceScope scope = _host.Scope();
        UserManager<AppUser> users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        AppUser user = (await users.FindByIdAsync(result.Value.UserId.ToString()))!;
        (await users.HasPasswordAsync(user)).ShouldBeFalse();
        (await users.IsInRoleAsync(user, "Editor")).ShouldBeTrue();
    }

    [Fact]
    public async Task An_email_already_in_use_is_refused_as_such()
    {
        await SeedRoleAsync("Editor");
        (await CreateAsync()).IsSuccess.ShouldBeTrue();

        Result<CreateUserResult> again = await CreateAsync();

        again.Error.ShouldBe(AppUserErrors.EmailAlreadyInUse);
    }

    [Fact]
    public async Task The_setup_link_sets_the_password_once()
    {
        await SeedRoleAsync("Editor");
        Guid userId = (await CreateAsync()).Value.UserId;
        string token = LastSetupToken();

        (await SetPasswordAsync(token)).IsSuccess.ShouldBeTrue();
        (await PasswordWorksAsync(userId, Password)).ShouldBeTrue();

        (await SetPasswordAsync(token, "Baska5678")).Error.ShouldBe(PasswordSetupErrors.InvalidOrExpiredToken);
        (await PasswordWorksAsync(userId, Password)).ShouldBeTrue();
    }

    [Fact]
    public async Task An_expired_or_unknown_link_sets_nothing()
    {
        await SeedRoleAsync("Editor");
        await CreateAsync();
        string token = LastSetupToken();
        using (IServiceScope scope = _host.Scope())
        {
            ICmsApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ICmsApplicationDbContext>();
            PasswordSetupToken row = await db.PasswordSetupTokens.SingleAsync();
            row.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        (await SetPasswordAsync(token)).Error.ShouldBe(PasswordSetupErrors.InvalidOrExpiredToken);
        (await SetPasswordAsync("uydurma")).Error.ShouldBe(PasswordSetupErrors.InvalidOrExpiredToken);
    }

    [Fact]
    public async Task A_password_that_breaks_the_policy_is_refused_and_the_link_stays_usable()
    {
        await SeedRoleAsync("Editor");
        await CreateAsync();
        string token = LastSetupToken();

        (await SetPasswordAsync(token, "kisa")).Error.Code.ShouldBe("PasswordSetup.SetFailed");
        (await SetPasswordAsync(token)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Changing_your_own_password_needs_the_current_one()
    {
        await SeedRoleAsync("Editor");
        Guid userId = (await CreateAsync()).Value.UserId;
        await SetPasswordAsync(LastSetupToken());
        _host.CurrentUser.UserId = userId;

        async Task<Result> ChangeAsync(string current, string next)
        {
            using IServiceScope scope = _host.Scope();
            return await new ChangeMyPasswordCommandHandler(scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(), _host.CurrentUser)
                .Handle(new ChangeMyPasswordCommand(current, next), CancellationToken.None);
        }

        (await ChangeAsync("Yanlis1234", "Yeni12345")).Error.ShouldBe(AppUserErrors.InvalidPassword);
        (await ChangeAsync(Password, "Yeni12345")).IsSuccess.ShouldBeTrue();
        (await PasswordWorksAsync(userId, "Yeni12345")).ShouldBeTrue();
        (await PasswordWorksAsync(userId, Password)).ShouldBeFalse();
    }

    [Fact]
    public async Task An_administrator_can_set_or_replace_a_password()
    {
        await SeedRoleAsync("Editor");
        Guid userId = (await CreateAsync()).Value.UserId;

        async Task<Result> SetAsync(string password)
        {
            using IServiceScope scope = _host.Scope();
            return await new SetUserPasswordManuallyCommandHandler(scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>())
                .Handle(new SetUserPasswordManuallyCommand(userId, password), CancellationToken.None);
        }

        (await SetAsync(Password)).IsSuccess.ShouldBeTrue();      // none yet
        (await SetAsync("Degisti99")).IsSuccess.ShouldBeTrue();   // replacing one
        (await PasswordWorksAsync(userId, "Degisti99")).ShouldBeTrue();
        (await PasswordWorksAsync(userId, Password)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_new_link_is_only_sent_to_someone_who_has_not_set_a_password()
    {
        await SeedRoleAsync("Editor");
        Guid userId = (await CreateAsync()).Value.UserId;

        async Task<Result<bool>> ResendAsync()
        {
            using IServiceScope scope = _host.Scope();
            IServiceProvider sp = scope.ServiceProvider;
            return await new ResendPasswordSetupCommandHandler(sp.GetRequiredService<UserManager<AppUser>>(), sp.GetRequiredService<ICmsApplicationDbContext>(),
                    sp.GetRequiredService<IEmailService>(), NullLogger<ResendPasswordSetupCommandHandler>.Instance)
                .Handle(new ResendPasswordSetupCommand(userId, CmsUrl), CancellationToken.None);
        }

        (await ResendAsync()).Value.ShouldBeTrue();
        _host.Emails.Sent.Count.ShouldBe(2);

        await SetPasswordAsync(LastSetupToken());
        (await ResendAsync()).Error.ShouldBe(PasswordSetupErrors.AlreadyHasPassword);
        _host.Emails.Sent.Count.ShouldBe(2);
    }
}
