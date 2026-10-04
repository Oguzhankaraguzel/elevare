using System.Net;
using Application.Abstraction.Services;
using Application.UnitTests.Fakes;
using Domain.Entities.PublicSiteSettings;
using Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Covers <see cref="ErrorWebhookForwarder"/>. The contract that matters: forwarding
/// is a COPY of a log entry that has already been stored, so its failures must be
/// reportable without ever being able to break the local <c>AppLogs</c> write.
/// </summary>
public sealed class ErrorWebhookForwarderTests : IDisposable
{
    private const string WebhookSettingKey = "Integrations.ErrorWebhookUrl";

    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();
    private readonly List<IDisposable> _disposables = [];

    private void SeedWebhookSetting(string? value)
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        db.SiteSettings.Add(new PublicSiteSetting { Id = 1, Key = WebhookSettingKey, Value = value });
        db.SaveChanges();
    }

    private ErrorWebhookForwarder CreateForwarder(StubHttpMessageHandler handler)
    {
        var client = new HttpClient(handler, disposeHandler: false);
        PublicReadDbContext db = TestDbFactory.Create(_options);
        _disposables.Add(client);
        _disposables.Add(db);
        _disposables.Add(handler);

        return new ErrorWebhookForwarder(client, db, NullLogger<ErrorWebhookForwarder>.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Succeeds_without_calling_anything_when_no_webhook_is_configured(string? configuredValue)
    {
        // "Nothing to do" is success, not failure — otherwise every error on a site
        // with no webhook would produce a second, misleading error.
        SeedWebhookSetting(configuredValue);
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK);
        ErrorWebhookForwarder forwarder = CreateForwarder(handler);

        Result result = await forwarder.ForwardAsync("boom", null, "/x", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Posts_the_entry_when_a_webhook_is_configured()
    {
        SeedWebhookSetting("https://hooks.example.com/elevare");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK);
        ErrorWebhookForwarder forwarder = CreateForwarder(handler);

        Result result = await forwarder.ForwardAsync("boom", "at Foo()", "/x", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        HttpRequestMessage sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Post);
        sent.RequestUri!.ToString().ShouldBe("https://hooks.example.com/elevare");
    }

    [Fact]
    public async Task Reports_an_error_status_from_the_endpoint()
    {
        SeedWebhookSetting("https://hooks.example.com/elevare");
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.InternalServerError);
        ErrorWebhookForwarder forwarder = CreateForwarder(handler);

        Result result = await forwarder.ForwardAsync("boom", null, "/x", CancellationToken.None);

        result.Error.ShouldBe(WebhookErrors.RejectedByEndpoint(500));
    }

    [Fact]
    public async Task Reports_a_failed_request_without_throwing()
    {
        SeedWebhookSetting("https://hooks.example.com/elevare");
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("connection refused"));
        ErrorWebhookForwarder forwarder = CreateForwarder(handler);

        Result result = await forwarder.ForwardAsync("boom", null, "/x", CancellationToken.None);

        result.Error.Code.ShouldBe("Webhook.RequestFailed");
        result.Error.Description.ShouldContain("connection refused");
    }

    public void Dispose()
    {
        foreach (IDisposable disposable in _disposables)
            disposable.Dispose();
    }
}
