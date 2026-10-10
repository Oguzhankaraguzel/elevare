using System.Text.RegularExpressions;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Email;
using Application.Features.Auth.Login;
using Application.Features.Commands.Users.ChangeMyPassword;
using Application.Features.Commands.Users.CreateUser;
using Application.Features.Commands.Users.IssuePasswordLink;
using Application.Features.Commands.Users.RequestPasswordReset;
using Application.Features.Commands.Users.SetPassword;
using Application.Features.Commands.Users.SetUserPasswordManually;
using Cms.Tests.Support;
using Domain.Entities.Logs;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// Getting into an account when e-mail may not be there to help: links an
/// administrator hands over, a password they typed that the user must replace at
/// the next sign-in, "forgot password", and who may reset whom.
/// </summary>
public sealed partial class PasswordAccessTests : IDisposable
{
    private const string CmsUrl = "https://cms.example.com";
    private const string Password = "Gizli1234";

    private readonly IdentityTestHost _host = new();

    public PasswordAccessTests() => SeedRolesAsync().GetAwaiter().GetResult();

    public void Dispose() => _host.Dispose();

    [GeneratedRegex(@"token=([^""&]+)")]
    private static partial Regex TokenInUrl();

    private static string TokenOf(string url) => Uri.UnescapeDataString(TokenInUrl().Match(url).Groups[1].Value);

    private string LastMailedToken() => TokenOf(_host.Emails.Sent[^1].Body);

    private async Task SeedRolesAsync()
    {
        using IServiceScope scope = _host.Scope();
        RoleManager<AppRole> roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        await roles.CreateAsync(new AppRole { Name = "Editor" });
        await roles.CreateAsync(new AppRole { Name = Roles.SuperAdmin });
    }

    private async Task<CreateUserResult> CreateAsync(string email = "ayse@example.com", string role = "Editor")
    {
        using IServiceScope scope = _host.Scope();
        IServiceProvider sp = scope.ServiceProvider;
        Result<CreateUserResult> result = await new CreateUserCommandHandler(sp.GetRequiredService<UserManager<AppUser>>(),
                sp.GetRequiredService<ICmsApplicationDbContext>(), sp.GetRequiredService<IEmailService>(),
                NullLogger<CreateUserCommandHandler>.Instance)
            .Handle(new CreateUserCommand(email, email.Split('@')[0], "Ayşe", null, null, role, CmsUrl), CancellationToken.None);
        return result.Value;
    }

    private async Task<Result<PasswordLinkResult>> IssueAsync(Guid userId, bool sendByEmail)
    {
        using IServiceScope scope = _host.Scope();
        IServiceProvider sp = scope.ServiceProvider;
        return await new IssuePasswordLinkCommandHandler(sp.GetRequiredService<UserManager<AppUser>>(),
                sp.GetRequiredService<ICmsApplicationDbContext>(), sp.GetRequiredService<IEmailService>(),
                _host.CurrentUser, NullLogger<IssuePasswordLinkCommandHandler>.Instance)
            .Handle(new IssuePasswordLinkCommand(userId, CmsUrl, sendByEmail), CancellationToken.None);
    }

    private async Task<Result> SetPasswordAsync(string token, string password = Password)
    {
        using IServiceScope scope = _host.Scope();
        IServiceProvider sp = scope.ServiceProvider;
        return await new SetPasswordCommandHandler(sp.GetRequiredService<ICmsApplicationDbContext>(), sp.GetRequiredService<UserManager<AppUser>>())
            .Handle(new SetPasswordCommand(token, password), CancellationToken.None);
    }

    private async Task<Result> SetManuallyAsync(Guid userId, string password)
    {
        using IServiceScope scope = _host.Scope();
        IServiceProvider sp = scope.ServiceProvider;
        Result result = await new SetUserPasswordManuallyCommandHandler(sp.GetRequiredService<UserManager<AppUser>>(),
                sp.GetRequiredService<ICmsApplicationDbContext>(), _host.CurrentUser)
            .Handle(new SetUserPasswordManuallyCommand(userId, password), CancellationToken.None);
        // The pipeline's final save, which retires the user's outstanding links.
        await sp.GetRequiredService<ICmsApplicationDbContext>().SaveChangesAsync(CancellationToken.None);
        return result;
    }

    private async Task<Result<LoginResponse>> LoginAsync(string identifier, string password)
    {
        using IServiceScope scope = _host.Scope();
        IServiceProvider sp = scope.ServiceProvider;
        Result<LoginResponse> result = await new LoginCommandHandler(sp.GetRequiredService<UserManager<AppUser>>(),
                sp.GetRequiredService<RoleManager<AppRole>>(), new FixedTokenProvider(), new SilentAuthEventLogger(),
                sp.GetRequiredService<ICmsApplicationDbContext>())
            .Handle(new LoginCommand(identifier, password), CancellationToken.None);
        await sp.GetRequiredService<ICmsApplicationDbContext>().SaveChangesAsync(CancellationToken.None);
        return result;
    }

    private async Task RequestResetAsync(string identifier)
    {
        using IServiceScope scope = _host.Scope();
        IServiceProvider sp = scope.ServiceProvider;
        Result result = await new RequestPasswordResetCommandHandler(sp.GetRequiredService<UserManager<AppUser>>(),
                sp.GetRequiredService<ICmsApplicationDbContext>(), sp.GetRequiredService<IEmailService>(),
                NullLogger<RequestPasswordResetCommandHandler>.Instance)
            .Handle(new RequestPasswordResetCommand(identifier, CmsUrl), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
    }

    private async Task<AppUser> UserAsync(Guid userId)
    {
        using IServiceScope scope = _host.Scope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByIdAsync(userId.ToString()))!;
    }

    private async Task<bool> PasswordWorksAsync(Guid userId, string password)
    {
        using IServiceScope scope = _host.Scope();
        UserManager<AppUser> users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return await users.CheckPasswordAsync((await users.FindByIdAsync(userId.ToString()))!, password);
    }

    // ── No mail server ────────────────────────────────────────────────────

    [Fact]
    public async Task Without_a_mail_server_a_new_user_s_link_is_handed_to_the_administrator()
    {
        _host.Emails.Configured = false;

        CreateUserResult created = await CreateAsync();

        created.EmailSent.ShouldBeFalse();
        _host.Emails.Sent.ShouldBeEmpty();
        created.SetupUrl.ShouldStartWith($"{CmsUrl}/account/set-password?token=");
        created.LinkExpiresAtUtc.ShouldBe(DateTime.UtcNow.AddHours(48), TimeSpan.FromMinutes(1));

        (await SetPasswordAsync(TokenOf(created.SetupUrl!))).IsSuccess.ShouldBeTrue();
        (await PasswordWorksAsync(created.UserId, Password)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_mailed_setup_link_is_not_shown_to_the_administrator()
    {
        CreateUserResult created = await CreateAsync();

        created.EmailSent.ShouldBeTrue();
        created.SetupUrl.ShouldBeNull();
    }

    // ── Links an administrator issues ─────────────────────────────────────

    [Fact]
    public async Task A_new_link_retires_the_old_one()
    {
        Guid userId = (await CreateAsync()).UserId;
        string first = LastMailedToken();

        PasswordLinkResult second = (await IssueAsync(userId, sendByEmail: false)).Value;

        second.EmailSent.ShouldBeFalse();
        (await SetPasswordAsync(first)).Error.ShouldBe(PasswordSetupErrors.InvalidOrExpiredToken);
        (await SetPasswordAsync(TokenOf(second.Url!))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Someone_who_forgot_their_password_can_be_sent_a_reset_link()
    {
        Guid userId = (await CreateAsync()).UserId;
        await SetPasswordAsync(LastMailedToken());

        PasswordLinkResult link = (await IssueAsync(userId, sendByEmail: true)).Value;

        link.EmailSent.ShouldBeTrue();
        link.Url.ShouldBeNull();
        _host.Emails.Sent[^1].Subject.ShouldContain("sıfırlama");
        (await PasswordWorksAsync(userId, Password)).ShouldBeTrue();   // until the link is used
        (await SetPasswordAsync(LastMailedToken(), "Yepyeni42")).IsSuccess.ShouldBeTrue();
        (await PasswordWorksAsync(userId, "Yepyeni42")).ShouldBeTrue();
    }

    [Fact]
    public async Task A_mail_that_cannot_go_out_hands_the_link_over_instead()
    {
        Guid userId = (await CreateAsync()).UserId;
        _host.Emails.Configured = false;

        PasswordLinkResult link = (await IssueAsync(userId, sendByEmail: true)).Value;

        link.EmailSent.ShouldBeFalse();
        link.Url.ShouldNotBeNull();
    }

    [Fact]
    public async Task Only_a_SuperAdmin_can_reset_a_SuperAdmin()
    {
        Guid superAdmin = (await CreateAsync("kurucu@example.com", Roles.SuperAdmin)).UserId;
        _host.CurrentUser.Roles = ["Admin"];

        (await IssueAsync(superAdmin, sendByEmail: false)).Error.ShouldBe(PasswordSetupErrors.SuperAdminOnly);
        (await SetManuallyAsync(superAdmin, Password)).Error.ShouldBe(PasswordSetupErrors.SuperAdminOnly);

        _host.CurrentUser.Roles = [Roles.SuperAdmin];
        (await IssueAsync(superAdmin, sendByEmail: false)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task An_inactive_account_gets_no_link()
    {
        Guid userId = (await CreateAsync()).UserId;
        using (IServiceScope scope = _host.Scope())
        {
            UserManager<AppUser> users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            AppUser user = (await users.FindByIdAsync(userId.ToString()))!;
            user.IsActive = false;
            await users.UpdateAsync(user);
        }

        (await IssueAsync(userId, sendByEmail: false)).Error.ShouldBe(PasswordSetupErrors.UserInactive);
    }

    // ── A password an administrator typed ─────────────────────────────────

    [Fact]
    public async Task A_password_set_by_an_administrator_has_to_be_replaced_before_any_session()
    {
        Guid userId = (await CreateAsync()).UserId;
        (await SetManuallyAsync(userId, Password)).IsSuccess.ShouldBeTrue();
        (await UserAsync(userId)).MustChangePassword.ShouldBeTrue();

        LoginResponse first = (await LoginAsync("ayse@example.com", Password)).Value;

        first.Token.ShouldBeEmpty();
        first.PasswordChangeToken.ShouldNotBeNull();

        // Keeping the given password would leave it one the administrator knows.
        (await SetPasswordAsync(first.PasswordChangeToken, Password)).Error.ShouldBe(PasswordSetupErrors.SameAsTemporary);
        (await SetPasswordAsync(first.PasswordChangeToken, "Kendi4567")).IsSuccess.ShouldBeTrue();

        (await UserAsync(userId)).MustChangePassword.ShouldBeFalse();
        LoginResponse second = (await LoginAsync("ayse@example.com", "Kendi4567")).Value;
        second.Token.ShouldNotBeEmpty();
        second.PasswordChangeToken.ShouldBeNull();
    }

    [Fact]
    public async Task Setting_a_password_by_hand_retires_outstanding_links()
    {
        Guid userId = (await CreateAsync()).UserId;
        string mailed = LastMailedToken();

        await SetManuallyAsync(userId, Password);

        (await SetPasswordAsync(mailed, "Baska5678")).Error.ShouldBe(PasswordSetupErrors.InvalidOrExpiredToken);
    }

    [Fact]
    public async Task Changing_your_own_password_clears_the_flag()
    {
        Guid userId = (await CreateAsync()).UserId;
        await SetManuallyAsync(userId, Password);
        _host.CurrentUser.UserId = userId;

        using (IServiceScope scope = _host.Scope())
        {
            (await new ChangeMyPasswordCommandHandler(scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(), _host.CurrentUser)
                .Handle(new ChangeMyPasswordCommand(Password, "Kendi4567"), CancellationToken.None)).IsSuccess.ShouldBeTrue();
        }

        (await UserAsync(userId)).MustChangePassword.ShouldBeFalse();
    }

    // ── Forgot password ───────────────────────────────────────────────────

    [Fact]
    public async Task Forgot_password_mails_a_one_hour_link_by_address_or_user_name()
    {
        Guid userId = (await CreateAsync()).UserId;
        await SetPasswordAsync(LastMailedToken());
        int before = _host.Emails.Sent.Count;

        await RequestResetAsync("ayse");

        _host.Emails.Sent.Count.ShouldBe(before + 1);
        _host.Emails.Sent[^1].Body.ShouldContain("1 saat");
        (await SetPasswordAsync(LastMailedToken(), "Unuttum123")).IsSuccess.ShouldBeTrue();
        (await PasswordWorksAsync(userId, "Unuttum123")).ShouldBeTrue();
    }

    [Fact]
    public async Task Forgot_password_says_nothing_about_accounts_that_do_not_exist()
    {
        await RequestResetAsync("kimse@example.com");

        _host.Emails.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Forgot_password_without_a_mail_server_issues_nothing()
    {
        await CreateAsync();
        _host.Emails.Configured = false;
        int before = _host.Emails.Sent.Count;

        await RequestResetAsync("ayse@example.com");

        _host.Emails.Sent.Count.ShouldBe(before);
    }

    private sealed class FixedTokenProvider : ITokenProvider
    {
        public string GenerateToken(string userId, string userName, string email, string[] roles, string[] permissions, DateTime? expiration = null) =>
            "jwt";
    }

    private sealed class SilentAuthEventLogger : IAuthEventLogger
    {
        public Task LogAsync(AuthEventType eventType, Guid? userId, string userNameSnapshot,
            bool saveImmediately = true, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
