using System.Net;

namespace Application.UnitTests.Fakes;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that answers from a script instead of the
/// network, so the services that talk to third parties (captcha providers, the
/// error webhook) can be tested without one.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    /// <summary>Requests the handler received, in order — lets a test assert what was sent.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Request bodies captured before the caller disposes the content.</summary>
    public List<string> RecordedBodies { get; } = [];

    private StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        _responder = responder;

    public static StubHttpMessageHandler RespondingWith(HttpStatusCode status, string body = "") =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

    /// <summary>Simulates the request never completing — DNS failure, refused connection, TLS error.</summary>
    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new(_ => throw exception);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (request.Content is not null)
        {
            string body = await request.Content.ReadAsStringAsync(cancellationToken);
            RecordedBodies.Add(body);
        }

        return _responder(request);
    }
}
