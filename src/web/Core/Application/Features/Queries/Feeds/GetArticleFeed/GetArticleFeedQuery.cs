using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Feeds.GetArticleFeed;

/// <summary>
/// The RSS 2.0 feed of a language's articles — the pages whose type is Article in
/// the CMS, newest first by publish date. What feed readers and Google Discover's
/// "Follow" subscribe to.
/// </summary>
/// <param name="LanguageCode">The feed's language.</param>
/// <param name="SiteAddress">The site's own address ("https://www.example.com"), for absolute links.</param>
/// <param name="HomePath">The language's home page path ("/" or "/en"), the channel's link.</param>
/// <param name="FeedPath">The feed's own path, for its self link.</param>
public sealed record GetArticleFeedQuery(string LanguageCode, string SiteAddress, string HomePath, string FeedPath) : IQuery<string>;
