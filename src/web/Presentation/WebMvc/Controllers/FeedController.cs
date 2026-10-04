using Application.Abstraction.Services;
using Application.Features.Queries.Feeds.GetArticleFeed;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SharedKernel.Concrete;

namespace WebMvc.Controllers;

/// <summary>
/// The article feed (RSS 2.0) of each language: <c>/feed.xml</c> for the default
/// language, <c>/{code}/feed.xml</c> for the others — the same shape as the pages'
/// own addresses. Cached like a page and dropped with them on every CMS change, so
/// a new article is in the feed as soon as it is live.
/// </summary>
[Route("")]
public sealed class FeedController(ISender sender, ILanguageDirectory languageDirectory) : BaseController
{
    public const string FeedFileName = "feed.xml";

    // Site paths are "/"-rooted by definition, not file-system paths.
#pragma warning disable S1075
    private const string Root = "/";
#pragma warning restore S1075

    [HttpGet(FeedFileName)]
    [OutputCache(PolicyName = PageController.OutputCachePolicy)]
    public Task<IActionResult> Default(CancellationToken cancellationToken) =>
        ServeAsync(languageDirectory.DefaultLanguageCode, Root, Root + FeedFileName, cancellationToken);

    [HttpGet("{languageCode}/" + FeedFileName)]
    [OutputCache(PolicyName = PageController.OutputCachePolicy)]
    public Task<IActionResult> ForLanguage(string languageCode, CancellationToken cancellationToken)
    {
        // The default language's feed has one address, not two.
        if (!languageDirectory.IsKnownLanguageCode(languageCode)
            || string.Equals(languageCode, languageDirectory.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
            throw NotFoundResource($"No feed for language '{languageCode}'.");
        return ServeAsync(languageCode, Root + languageCode, $"{Root}{languageCode}/{FeedFileName}", cancellationToken);
    }

    private async Task<IActionResult> ServeAsync(string languageCode, string homePath, string feedPath, CancellationToken cancellationToken)
    {
        string siteUrl = $"{Request.Scheme}://{Request.Host}";
        Result<string> result = await sender.Send(new GetArticleFeedQuery(languageCode, siteUrl, homePath, feedPath), cancellationToken);
        if (result.IsFailure)
            throw NotFoundResource(result.Error.Description);
        return Content(result.Value, "application/rss+xml; charset=utf-8");
    }
}
