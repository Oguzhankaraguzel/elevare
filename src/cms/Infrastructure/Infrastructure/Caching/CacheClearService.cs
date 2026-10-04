using System.Net.Http.Json;
using System.Text.Json;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Concrete;

namespace Infrastructure.Caching;

/// <summary>
/// Calls the Web app's cache admin endpoints, locating the public site through the
/// same <c>Advanced.PublicSiteBaseUrl</c> Site Setting the preview-link feature uses.
/// </summary>
/// <remarks>
/// Reads through <see cref="ICmsApplicationDbContextFactory"/> — its own,
/// independent context — rather than the shared scoped one: this service is called
/// from <c>GetSiteHealthQueryHandler</c>, which opts out of the per-circuit
/// DbContext-concurrency gate (<c>IBypassDbConcurrencyGuard</c>) precisely because
/// nothing on its call graph may touch the shared instance. In this deployment the
/// lookup below is usually skipped anyway (<c>Cache:InternalWebBaseUrl</c> short-
/// circuits it), but a deployment without that set would hit it on every call.
/// </remarks>
internal sealed class CacheClearService(
    HttpClient httpClient,
    ICmsApplicationDbContextFactory dbFactory,
    IOptions<CacheClearOptions> options,
    ILogger<CacheClearService> logger) : ICacheClearService
{
    private const string PublicSiteBaseUrlKey = "Advanced.PublicSiteBaseUrl";
    private const string SecretHeaderName = "X-Cache-Secret";

    public async Task<Result> ClearAsync(CancellationToken cancellationToken = default)
    {
        Result<string> baseUrl = await ResolveBaseUrlAsync(cancellationToken);
        if (baseUrl.IsFailure)
            return Result.Failure(baseUrl.Error);

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, $"{baseUrl.Value}/api/cache/clear");
            request.Headers.Add(SecretHeaderName, options.Value.ClearSecret);

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? Result.Success()
                : Result.Failure(DescribeStatus(response));
        }
        catch (Exception ex) when (IsTransport(ex))
        {
            logger.LogWarning(ex, "Failed to reach the Web app's cache-clear endpoint.");
            return Result.Failure(Unreachable(ex));
        }
    }

    public async Task<Result<SiteCacheStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        Result<string> baseUrl = await ResolveBaseUrlAsync(cancellationToken);
        if (baseUrl.IsFailure)
            return Result.Failure<SiteCacheStatus>(baseUrl.Error);

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, $"{baseUrl.Value}/api/cache/status");
            request.Headers.Add(SecretHeaderName, options.Value.ClearSecret);

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Result.Failure<SiteCacheStatus>(DescribeStatus(response));

            SiteCacheStatus? status = await response.Content.ReadFromJsonAsync<SiteCacheStatus>(cancellationToken);
            return status is not null
                ? Result.Success(status)
                : Result.Failure<SiteCacheStatus>(CacheClearErrors.StatusUnreadable);
        }
        catch (Exception ex) when (IsTransport(ex) || ex is JsonException)
        {
            logger.LogWarning(ex, "Failed to read the Web app's cache status.");
            return Result.Failure<SiteCacheStatus>(Unreachable(ex));
        }
    }

    /// <summary>
    /// Prefers the configured internal address, and only falls back to the public
    /// one when there is none.
    /// <para>
    /// This is a server calling a server. Going out through the public hostname
    /// makes the request leave the host and come back through DNS, the reverse
    /// proxy and any CDN or bot protection in front of the site — none of which
    /// exist to serve the CMS, and any of which can answer this request with
    /// something other than the app's own 204. On the container network the two
    /// apps are neighbours; addressing the neighbour directly removes every one of
    /// those moving parts from a call that never needed them.
    /// </para>
    /// </summary>
    private async Task<Result<string>> ResolveBaseUrlAsync(CancellationToken cancellationToken)
    {
        string internalUrl = options.Value.InternalWebBaseUrl;
        if (!string.IsNullOrWhiteSpace(internalUrl))
            return Result.Success(internalUrl.TrimEnd('/'));

        string? baseUrl = await dbFactory.ExecuteAsync(
            db => db.SiteSettings
                .AsNoTracking()
                .Where(s => s.Key == PublicSiteBaseUrlKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(cancellationToken));

        return string.IsNullOrWhiteSpace(baseUrl)
            ? Result.Failure<string>(CacheClearErrors.BaseUrlNotConfigured(PublicSiteBaseUrlKey))
            : Result.Success(baseUrl.TrimEnd('/'));
    }

    private static Error DescribeStatus(HttpResponseMessage response) =>
        response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            ? CacheClearErrors.SecretMismatch
            : CacheClearErrors.RequestFailed((int)response.StatusCode, response.ReasonPhrase);

    private static Error Unreachable(Exception ex) =>
        CacheClearErrors.Unreachable(ex);

    private static bool IsTransport(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or UriFormatException;
}
