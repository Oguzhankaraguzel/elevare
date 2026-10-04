using Domain.Entities.SiteCodeSnippets;

namespace Application.Features.Queries.SiteCodeSnippets.GetSiteCodeSnippets;

public sealed record SiteCodeSnippetResponse(
    int Id,
    string Name,
    SiteCodePreset Preset,
    SiteCodePlacement Placement,
    SiteCodeKind Kind,
    string? Content,
    bool IsEnabled,
    int SortOrder,
    string? Notes,
    DateTime? UpdateDate);
