using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Concrete;

namespace Infrastructure.Configuration;

/// <summary>
/// Applies an <c>IntegrationSecrets</c> write everywhere it matters: reloads the
/// CMS's own configuration snapshot, then — only for a <c>Captcha:*</c> key,
/// which the Web app also reads — calls Web's <c>POST /api/secrets/reload</c>.
/// <para>
/// Reuses <see cref="CacheClearOptions"/> (<c>Cache:ClearSecret</c> /
/// <c>Cache:InternalWebBaseUrl</c>) rather than adding a third shared secret:
/// both this and <see cref="CacheClearService"/> are the same underlying concern
/// — "the CMS reaching across into the separately-deployed Web app for an
/// internal admin operation" — so the base-URL resolution logic below is
/// deliberately a copy of <c>CacheClearService.ResolveBaseUrlAsync</c> rather
/// than a new abstraction over a working, already-duplicated service.
/// </para>
/// </summary>
internal sealed class IntegrationSecretsChangeNotifier(
    HttpClient httpClient,
    ICmsApplicationDbContext db,
    IIntegrationSecretsReloader localReloader,
    IOptions<CacheClearOptions> options,
    ILogger<IntegrationSecretsChangeNotifier> logger) : IIntegrationSecretsChangeNotifier
{
    private const string PublicSiteBaseUrlKey = "Advanced.PublicSiteBaseUrl";
    private const string SecretHeaderName = "X-Cache-Secret";

    public async Task<Result> NotifyAsync(string key, CancellationToken cancellationToken = default)
    {
        Result local = await localReloader.ReloadAsync(cancellationToken);
        if (local.IsFailure)
            return local;

        // Email/ObjectStorage secrets are CMS-only concerns — Web never reads them.
        if (!key.StartsWith("Captcha:", StringComparison.Ordinal))
            return Result.Success();

        Result<string> baseUrl = await ResolveBaseUrlAsync(cancellationToken);
        if (baseUrl.IsFailure)
            return Result.Failure(baseUrl.Error);

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, $"{baseUrl.Value}/api/secrets/reload");
            request.Headers.Add(SecretHeaderName, options.Value.ClearSecret);

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? Result.Success()
                : Result.Failure(DescribeStatus(response));
        }
        catch (Exception ex) when (IsTransport(ex))
        {
            logger.LogWarning(ex, "Failed to reach the Web app's secrets-reload endpoint.");
            return Result.Failure(Unreachable(ex));
        }
    }

    private async Task<Result<string>> ResolveBaseUrlAsync(CancellationToken cancellationToken)
    {
        string internalUrl = options.Value.InternalWebBaseUrl;
        if (!string.IsNullOrWhiteSpace(internalUrl))
            return Result.Success(internalUrl.TrimEnd('/'));

        string? baseUrl = await db.SiteSettings
            .AsNoTracking()
            .Where(s => s.Key == PublicSiteBaseUrlKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(baseUrl)
            ? Result.Failure<string>(IntegrationSecretsChangeErrors.BaseUrlNotConfigured(PublicSiteBaseUrlKey))
            : Result.Success(baseUrl.TrimEnd('/'));
    }

    private static Error DescribeStatus(HttpResponseMessage response) =>
        response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            ? IntegrationSecretsChangeErrors.SecretMismatch
            : IntegrationSecretsChangeErrors.RequestFailed((int)response.StatusCode, response.ReasonPhrase);

    private static Error Unreachable(Exception ex) =>
        IntegrationSecretsChangeErrors.Unreachable(ex);

    private static bool IsTransport(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or UriFormatException;
}
