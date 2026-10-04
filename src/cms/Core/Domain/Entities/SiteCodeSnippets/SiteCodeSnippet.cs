using System.ComponentModel.DataAnnotations;
using Domain.Entities.Abstractions;

namespace Domain.Entities.SiteCodeSnippets;

/// <summary>
/// One piece of code injected into every public page — an analytics tag, a consent
/// banner, a verification meta, a chat widget.
/// <para>
/// This replaces the free-text <c>Analytics.CustomHeadScript</c> /
/// <c>CustomBodyScript</c> / <c>Integrations.CookieConsentScript</c> settings. A
/// settings field can hold exactly one snippet, cannot be switched off without
/// losing it, carries no record of what it is for, and is written straight to the
/// page unchecked. As a row instead, each tag can be named, disabled while
/// debugging, ordered against its neighbours, and validated before it ever reaches
/// a visitor's browser.
/// </para>
/// </summary>
public class SiteCodeSnippet : BaseEntity
{
    /// <summary>
    /// What this is for, in the author's words — "Google Ads dönüşüm takibi".
    /// The only thing the list shows, because nobody recognises their own tags by
    /// the first line of minified script three months later.
    /// </summary>
    [MaxLength(200)]
    public required string Name { get; set; }

    public SiteCodePreset Preset { get; set; } = SiteCodePreset.Custom;

    public SiteCodePlacement Placement { get; set; } = SiteCodePlacement.HeadEnd;

    public SiteCodeKind Kind { get; set; } = SiteCodeKind.Script;

    /// <summary>The markup emitted verbatim. Normalised on save when <see cref="Preset"/> knows how.</summary>
    public string? Content { get; set; }

    /// <summary>
    /// Off means "keep the code, stop serving it". Switching a tag off is the first
    /// thing anyone does when the site starts misbehaving, and it must not cost them
    /// the snippet they would otherwise have to fetch from the vendor again.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Order within a placement, ascending. Ties fall back to <see cref="BaseEntity.Id"/>.</summary>
    public int SortOrder { get; set; }

    /// <summary>Free note — why it was added, who asked for it, when it can be removed.</summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>The pages this snippet is switched off on — see <see cref="PageInfoSiteCodeExclusion"/>.</summary>
    public virtual ICollection<PageInfos.PageInfo>? ExcludedOnPages { get; set; }
}
