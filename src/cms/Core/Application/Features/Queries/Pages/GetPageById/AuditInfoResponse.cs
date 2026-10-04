namespace Application.Features.Queries.Pages.GetPageById;

/// <summary>
/// Who made this record and who last touched it. The columns have been on
/// <c>BaseEntity</c> since the start; this is what finally carries them to a screen,
/// so "who published this and when" stops being a database question.
/// <para>
/// Names, not ids: a Guid answers nobody's question. Null names mean the user row is
/// gone (deleted account), which the UI shows as an em dash rather than pretending.
/// </para>
/// </summary>
public sealed record AuditInfoResponse(
    string? CreatedBy,
    DateTime CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    /// <summary>When the page counts as published — see <c>PageInfo.PublishedAt</c>.</summary>
    DateTime? PublishedAt = null);
