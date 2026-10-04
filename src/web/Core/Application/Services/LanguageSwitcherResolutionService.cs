using AngleSharp;
using AngleSharp.Dom;
using Application.Abstraction.Data;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using Application.Abstraction.Services;
using SharedKernel.Concrete;
using System.Data.Common;

namespace Application.Services;

/// <summary>
/// Resolves "Language Switcher" slot markers — <c>&lt;a data-elevare-lang-slot="CODE"&gt;</c>
/// — embedded in GrapeJS-authored HTML to the right href, at request time. For each
/// active language, the target is: this page's own translation (same PageGroup) if
/// one exists, else the nearest ancestor's translation, else that language's homepage.
/// </summary>
public sealed class LanguageSwitcherResolutionService(IPublicReadDbContext db)
{
    private const string MarkerAttribute = "data-elevare-lang-slot";

    /// <summary>
    /// Returns the HTML with every language-slot marker's href resolved for the page
    /// identified by <paramref name="pageId"/>. A null value means the page had no
    /// HTML — passed through unchanged, not an error.
    /// <para>
    /// Failure means the lookup broke, and is reported so the caller can decide
    /// between serving the page with unresolved switcher links and serving an error.
    /// </para>
    /// </summary>
    public async Task<Result<string?>> ResolveAsync(string? html, int pageId, CancellationToken cancellationToken)
    {
        try
        {
            return await ResolveCoreAsync(html, pageId, cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            return Result.Failure<string?>(RenderErrors.ResolutionFailed("the language switcher", ex.Message));
        }
    }

    private async Task<Result<string?>> ResolveCoreAsync(string? html, int pageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html))
            return Result.Success<string?>(html);

        IBrowsingContext context = BrowsingContext.New(Configuration.Default);
        using IDocument document = await context.OpenAsync(req => req.Content(html), cancellationToken);

        List<IElement> slots = [.. document.QuerySelectorAll('[' + MarkerAttribute + ']')];
        if (slots.Count == 0)
            return Result.Success<string?>(document.Body?.InnerHtml ?? html);

        Dictionary<string, string> alternatives = await ResolveAlternativesAsync(pageId, cancellationToken);

        // Nothing resolved at all means the lookup itself came up empty — an unknown
        // page id, or a database with no published languages in it. That is a fault
        // on our side, not a statement about any particular link, so the switcher is
        // left exactly as authored. Emptying it here would turn a transient data
        // problem into a page that silently lost its language navigation.
        if (alternatives.Count == 0)
            return Result.Success<string?>(document.Body?.InnerHtml ?? html);

        foreach (IElement slot in slots)
        {
            string? code = slot.GetAttribute(MarkerAttribute);
            if (code is not null && alternatives.TryGetValue(code, out string? fullSlug))
            {
                slot.SetAttribute("href", "/" + fullSlug);
                continue;
            }

            // No target for this language, so there is nothing to link to. Leaving
            // the slot alone kept whatever the block was authored with — href="#" —
            // which reads to a visitor as a working switcher that does nothing, and
            // to a crawler as an empty link. This happens whenever a language was
            // switched off (or unpublished) after the page was saved, and used to
            // happen the moment a page was built while such a language existed.
            // Removing the anchor is the honest outcome: the switcher shows only the
            // languages the visitor can actually reach.
            //
            // The switcher is a <ul> of <li><a> now, so the row goes with the link.
            // Removing only the anchor would leave an empty list item — still a
            // bullet on the page and still an entry a screen reader announces while
            // walking "3 of 4 languages" that turns out to contain nothing.
            IElement target = slot.ParentElement is { LocalName: "li" } row ? row : slot;
            target.Remove();
        }

        return document.Body?.InnerHtml ?? html;
    }

    private async Task<Dictionary<string, string>> ResolveAlternativesAsync(int pageId, CancellationToken cancellationToken)
    {
        PublicPage? page = await db.PageInfos.FirstOrDefaultAsync(p => p.Id == pageId, cancellationToken);
        if (page is null)
            return [];

        // Only published languages belong in the switcher: offering a link to a
        // language the visitor cannot reach is worse than not offering it at all.
        List<PublicLanguage> languages = await db.Languages
            .Where(PublicLanguage.PubliclyVisible)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (PublicLanguage language in languages)
        {
            if (language.Id == page.LanguageId)
            {
                result[language.TwoLetterCode] = page.FullSlug;
                continue;
            }

            string? fullSlug = await FindTranslationAsync(page, language.Id, cancellationToken)
                ?? await FindHomeAsync(language.Id, cancellationToken);

            if (fullSlug is not null)
                result[language.TwoLetterCode] = fullSlug;
        }

        return result;
    }

    /// <summary>Walks from <paramref name="startPage"/> up through its ancestors,
    /// returning the first one that has a published translation in the target
    /// language (its PageGroup sibling), or null if none of them do.</summary>
    private async Task<string?> FindTranslationAsync(PublicPage startPage, int targetLanguageId, CancellationToken cancellationToken)
    {
        PublicPage? cursor = startPage;
        while (cursor is not null)
        {
            if (cursor.PageGroupId is not null)
            {
                string? sibling = await db.PageInfos
                    .Where(p => p.PageGroupId == cursor.PageGroupId
                        && p.LanguageId == targetLanguageId
                        && p.PageStatus == PublicPageStatus.Published
                        && p.IsActive)
                    .Select(p => p.FullSlug)
                    .FirstOrDefaultAsync(cancellationToken);

                if (sibling is not null)
                    return sibling;
            }

            cursor = cursor.ParentPageId is int parentId
                ? await db.PageInfos.FirstOrDefaultAsync(p => p.Id == parentId, cancellationToken)
                : null;
        }

        return null;
    }

    private async Task<string?> FindHomeAsync(int languageId, CancellationToken cancellationToken)
    {
        return await db.PageInfos
            .Where(p => p.Slug == "home"
                && p.ParentPageId == null
                && p.LanguageId == languageId
                && p.PageStatus == PublicPageStatus.Published
                && p.IsActive)
            .Select(p => p.FullSlug)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
