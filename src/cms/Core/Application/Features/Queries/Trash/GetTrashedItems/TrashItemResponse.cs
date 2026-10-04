using Application.Features.Trash;

namespace Application.Features.Queries.Trash.GetTrashedItems;

public sealed record TrashItemResponse(
    TrashEntityType EntityType,
    int EntityId,
    string DisplayName,
    DateTime DeletedAt,
    string? DeletedByUserName,
    TrashConflictKind ConflictKind = TrashConflictKind.None,

    /// <summary>
    /// Computed here rather than on the click: the screen can then disable the button
    /// and say why, instead of offering an action that is guaranteed to fail.
    /// </summary>
    TrashPurgeBlock PurgeBlock = TrashPurgeBlock.None);
