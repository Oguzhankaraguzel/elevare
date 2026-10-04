using Domain.Entities.PageInfos;

namespace Application.Features.Queries.Pages.GetPageById;

public sealed record PageEditorResponse(
    int Id,
    string Title,
    string Slug,
    string FullSlug,
    int LanguageId,
    PageStatus Status,
    PageStatus? PendingStatus,
    string? GjsHtml,
    string? GjsCss,
    string? GjsData,
    SeoMetaResponse Seo,
    int? ParentPageId,
    int? SeoScore,
    List<int> TagIds,
    PageKind Kind,
    AuditInfoResponse Audit,
    /// <summary>Site codes switched off on this page — see <c>PageInfoSiteCodeExclusion</c>.</summary>
    List<int> ExcludedSiteCodeSnippetIds,
    /// <summary>Hand back with the save — see <c>PageFingerprint</c>.</summary>
    string Fingerprint);
