using AngleSharp.Html.Parser;
using Domain.Entities.PageInfos;
using SharedKernel.Content;

namespace Application.Features.Commands.Pages.Shared;

/// <summary>
/// Keeps <see cref="PageInfo.PublishedAt"/> right. Called wherever a page's live
/// content or status changes: the editor's save, the list's status switch, and an
/// approval that promotes staged content.
/// <para>
/// The date an Article block states wins, every time: it is what the reader sees,
/// so the listing card, the sort order, the structured data and the share tags
/// must all say the same. Without one, a page is published the first time it goes
/// live, and that moment is kept — taking it offline and back does not make an old
/// page new.
/// </para>
/// </summary>
public static class PagePublication
{
    /// <summary>Updates <paramref name="page"/>'s publish date from its live content and status.</summary>
    /// <param name="page">The page, with its content loaded when there is any.</param>
    /// <param name="now">The current moment (UTC), for a first publish.</param>
    public static void Stamp(PageInfo page, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(page);

        DateTime? written = ArticleDate(page.Content?.GjsHtml);
        if (written is not null)
        {
            page.PublishedAt = written;
            return;
        }

        if (page.PageStatus == PageStatus.Published && page.PublishedAt is null)
            page.PublishedAt = now;
    }

    /// <summary>
    /// The date an Article block in <paramref name="html"/> states — the byline's
    /// visible date over its datetime attribute (see HumanDateParser). Null without one.
    /// </summary>
    public static DateTime? ArticleDate(string? html)
    {
        if (string.IsNullOrWhiteSpace(html) || !html.Contains("data-elevare-article-date", StringComparison.Ordinal))
            return null;
        try
        {
            using AngleSharp.Html.Dom.IHtmlDocument document = new HtmlParser().ParseDocument(html);
            AngleSharp.Dom.IElement? time = document.QuerySelector("[data-elevare-article] [data-elevare-article-date]");
            return time is null
                ? null
                : HumanDateParser.ResolveArticleDate(time.GetAttribute("datetime"), time.ParentElement?.TextContent ?? time.TextContent);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}
