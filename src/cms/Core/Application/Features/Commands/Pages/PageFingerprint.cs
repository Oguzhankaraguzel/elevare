using System.Security.Cryptography;
using System.Text;
using Domain.Entities.PageInfos;
using Domain.Entities.PageTemplates;
using SharedKernel.Social;

namespace Application.Features.Commands.Pages;

/// <summary>
/// A short digest of everything the page editor loads and saves back — content,
/// address, status, SEO — so a save can tell whether the page changed under the
/// editor since it was opened.
/// <para>
/// Without it the last save simply won: a second editor, a status change from the
/// page list, a bulk find/replace, or the links rewritten when a page or the
/// default language moves were all silently undone by whoever saved an editor
/// opened before them. A timestamp would not do: changing only the SEO fields (how
/// og:url and structured data get rewritten) does not stamp the page's UpdateDate.
/// </para>
/// <para>
/// The SEO score is left out: the editor's own analyser writes it on every save.
/// </para>
/// </summary>
public static class PageFingerprint
{
    private const char Separator = '\u001f';

    /// <param name="page">With <c>Content</c>, <c>Tags</c> and <c>ExcludedSiteCodeSnippets</c> loaded.</param>
    public static string Of(PageInfo page)
    {
        SeoMeta seo = page.SeoMeta;
        object?[] parts =
        [
            page.Slug, page.ParentPageId, page.PageStatus, page.PendingStatus, page.Kind,
            seo.Title, seo.IsCanonical, seo.CanonicalUrl, seo.MetaDescription, seo.MetaAuthor,
            seo.NoIndex, seo.NoFollow, seo.FocusKeyword, seo.StructuredData,
            seo.OgTitle, seo.OgDescription, seo.OgType, seo.OgImage, seo.OgUrl,
            // Re-serialized, not as stored: the column is jsonb, which hands the
            // JSON back with its keys reordered and spaces added. Compared raw, a
            // page just saved never matched itself on the next save.
            seo.TwitterCard, seo.TwitterSite, SocialMeta.FromJson(seo.SocialJson).ToJson(),
            // Not the preview columns: the editor's own "Önizle" writes them without
            // saving. Every other writer changes the live content or GjsData too.
            page.Content?.GjsHtml, page.Content?.GjsCss, page.Content?.GjsData,
            string.Join(',', (page.Tags ?? []).Select(t => t.Id).Order()),
            string.Join(',', (page.ExcludedSiteCodeSnippets ?? []).Select(s => s.Id).Order()),
        ];
        return Hash(parts);
    }

    /// <summary>The same for a template: its editor writes the menu and footer links a move rewrites.</summary>
    public static string Of(PageTemplate template) => Hash(
    [
        template.Name, template.Type, template.IsLinked, template.LanguageId,
        template.GjsHtml, template.GjsCss, template.GjsData, template.PreviewGjsHtml, template.PreviewGjsCss,
    ]);

    private static string Hash(object?[] parts)
    {
        StringBuilder text = new();
        foreach (object? part in parts)
            text.Append(part).Append(Separator);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexString(hash, 0, 16);
    }
}
