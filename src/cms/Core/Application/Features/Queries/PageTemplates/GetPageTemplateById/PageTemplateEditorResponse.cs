using Domain.Entities.PageTemplates;

namespace Application.Features.Queries.PageTemplates.GetPageTemplateById;

public sealed record PageTemplateEditorResponse(
    int Id,
    string Name,
    PageTemplateType Type,
    bool IsLinked,
    string? GjsHtml,
    string? GjsCss,
    string? GjsData,
    int? LanguageId,
    /// <summary>
    /// <c>ContentChangedAt ?? UpdateDate ?? CreateDate</c> — when the template's own
    /// content (linked templates inside it aside) last changed.
    /// Stamped onto an unlinked insertion as a snapshot marker, so a later editor
    /// session can tell whether the template has since changed underneath it.
    /// </summary>
    DateTime Version,
    /// <summary>Hand back with the save — see <c>PageFingerprint</c>.</summary>
    string Fingerprint);
