using Domain.Entities.SiteCodeSnippets;

namespace Application.Features.Queries.SiteCodeSnippets.GetSiteCodeSnippetSummaries;

/// <summary>A snippet as the page editor's "Site Kodları" dialog lists it — everything but the code.</summary>
public sealed record SiteCodeSnippetSummaryResponse(
    int Id,
    string Name,
    SiteCodePreset Preset,
    SiteCodePlacement Placement,
    SiteCodeKind Kind,
    bool IsEnabled);
