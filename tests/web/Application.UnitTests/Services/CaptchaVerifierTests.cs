using System.Net;
using Application.Abstraction.Services;
using Application.UnitTests.Fakes;
using Domain.Entities.PublicSiteSettings;
using Infrastructure.Captcha;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Covers <see cref="CaptchaVerifier"/>, the only thing standing between the public
/// forms and a spam bot. These cases are deliberately about the DIFFERENCE between
/// failures: before the verifier returned a <c>Result</c>, "the visitor failed the
/// challenge" and "the provider is unreachable" were both a bare <c>false</c>, which
/// made an outage indistinguishable from ordinary traffic.
/// </summary>
public sealed class CaptchaVerifierTests : IDisposable
{
    private const string ProviderSettingKey = "Integrations.CaptchaProvider";

    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();
    private readonly List<IDisposable> _disposables = [];

    private void SeedProvider(string? provider)
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        db.SiteSettings.Add(new PublicSiteSetting { Id = 1, Key = ProviderSettingKey, Value = provider });
        db.SaveChanges();
    }

    private CaptchaVerifier CreateVerifier(StubHttpMessageHandler handler, string secretKey = "test-secret")
    {
        // disposeHandler: false — the handler is owned by the test so its recorded
        // requests stay readable after the client goes away.
        var client = new HttpClient(handler, disposeHandler: false);
        PublicReadDbContext db = TestDbFactory.Create(_options);
        _disposables.Add(client);
        _disposables.Add(db);
        _disposables.Add(handler);

        return new CaptchaVerifier(
            client, db, new TestOptionsMonitor<CaptchaOptions>(new CaptchaOptions { SecretKey = secretKey }),
            NullLogger<CaptchaVerifier>.Instance);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Passes_when_no_provider_is_configured(string? provider)
    {
        SeedProvider(provider);
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK);
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync("anything", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        handler.Requests.ShouldBeEmpty();   // no provider means there is no call to make
    }

    [Fact]
    public async Task Rejects_a_missing_token_without_calling_the_provider()
    {
        SeedProvider("recaptcha");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{"success":true}""");
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync(null, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CaptchaErrors.Rejected);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Passes_when_the_provider_confirms_the_token()
    {
        SeedProvider("recaptcha");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{"success":true}""");
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync("token", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        handler.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Rejects_when_the_provider_says_the_challenge_failed()
    {
        SeedProvider("hcaptcha");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{"success":false}""");
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync("token", CancellationToken.None);

        result.Error.ShouldBe(CaptchaErrors.Rejected);
    }

    [Fact]
    public async Task Fails_closed_when_the_secret_key_was_never_configured()
    {
        // The dangerous case: captcha is switched ON in the CMS but the deploy-time
        // secret is missing. Passing here would leave the forms wide open while the
        // settings page claims they are protected.
        SeedProvider("recaptcha");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{"success":true}""");
        CaptchaVerifier verifier = CreateVerifier(handler, secretKey: "");

        Result result = await verifier.VerifyAsync("token", CancellationToken.None);

        result.Error.ShouldBe(CaptchaErrors.SecretKeyMissing);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Fails_closed_on_a_provider_name_this_build_cannot_call()
    {
        SeedProvider("some-provider-we-cannot-call");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK);
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync("token", CancellationToken.None);

        result.Error.Code.ShouldBe("Captcha.UnknownProvider");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reports_an_unreachable_provider_as_unavailable_not_as_a_failed_challenge()
    {
        SeedProvider("recaptcha");
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("no such host"));
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync("token", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();                    // still fails closed
        result.Error.Code.ShouldBe("Captcha.Unavailable");   // but for a different reason
    }

    [Fact]
    public async Task Reports_an_error_status_from_the_provider_as_unavailable()
    {
        SeedProvider("recaptcha");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.ServiceUnavailable);
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync("token", cancellationToken: CancellationToken.None);

        result.Error.Code.ShouldBe("Captcha.Unavailable");
        result.Error.Description.ShouldContain("503");
    }

    [Fact]
    public async Task Rejects_when_recaptcha_score_is_below_threshold()
    {
        SeedProvider("recaptcha");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{"success":true,"score":0.2}""");
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync("token", cancellationToken: CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CaptchaErrors.Rejected);
    }

    [Fact]
    public async Task Passes_when_recaptcha_score_is_sufficient()
    {
        SeedProvider("recaptcha");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{"success":true,"score":0.9}""");
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync("token", cancellationToken: CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Rejects_when_the_returned_action_does_not_match_what_was_requested()
    {
        // A token minted for a different action being replayed here — the provider
        // itself says success:true, but the action mismatch means this token was
        // never meant to authorize a form submission.
        SeedProvider("recaptcha");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{"success":true,"score":0.9,"action":"login"}""");
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync("token", cancellationToken: CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CaptchaErrors.Rejected);
    }

    [Fact]
    public async Task Passes_when_the_returned_action_matches()
    {
        SeedProvider("recaptcha");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{"success":true,"score":0.9,"action":"submit"}""");
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync("token", cancellationToken: CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Passes_when_turnstile_provider_confirms_the_token()
    {
        SeedProvider("turnstile");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{"success":true}""");
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync("token", cancellationToken: CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].RequestUri!.AbsoluteUri.ShouldBe("https://challenges.cloudflare.com/turnstile/v0/siteverify");
    }

    [Fact]
    public async Task Sends_remoteip_when_provided()
    {
        SeedProvider("recaptcha");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{"success":true,"score":0.9}""");
        CaptchaVerifier verifier = CreateVerifier(handler);

        Result result = await verifier.VerifyAsync("token", remoteIp: "203.0.113.195", cancellationToken: CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        handler.Requests.ShouldHaveSingleItem();
        handler.RecordedBodies.ShouldHaveSingleItem();
        handler.RecordedBodies[0].ShouldContain("remoteip=203.0.113.195");
    }

    public void Dispose()
    {
        foreach (IDisposable disposable in _disposables)
            disposable.Dispose();
    }
}
