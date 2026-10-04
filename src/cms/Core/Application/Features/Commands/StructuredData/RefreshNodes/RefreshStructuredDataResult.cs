namespace Application.Features.Commands.StructuredData.RefreshNodes;

/// <summary>
/// What the refresh did, reported per outcome rather than as a single total —
/// "12 pages updated" hides that three others were skipped because their JSON is
/// broken, and those are exactly the ones needing attention.
/// </summary>
/// <param name="Updated">Pages whose graph changed and was saved.</param>
/// <param name="Unchanged">Pages already holding the current values.</param>
/// <param name="Skipped">
/// Pages whose stored JSON could not be parsed. Left untouched on purpose: an
/// editor's hand-written data, however malformed, is not this command's to discard.
/// </param>
public sealed record RefreshStructuredDataResult(int Updated, int Unchanged, IReadOnlyList<string> Skipped);
