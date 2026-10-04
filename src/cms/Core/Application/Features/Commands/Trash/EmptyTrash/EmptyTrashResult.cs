namespace Application.Features.Commands.Trash.EmptyTrash;

/// <param name="Purged">Rows permanently deleted.</param>
/// <param name="Skipped">
/// Rows a safety check protected. Reported so "empty trash" never silently leaves
/// items behind — the screen says how many stayed and the row itself says why.
/// </param>
public sealed record EmptyTrashResult(int Purged, int Skipped);
