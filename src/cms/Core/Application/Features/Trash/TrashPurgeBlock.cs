namespace Application.Features.Trash;

/// <summary>
/// Why a trashed item cannot be destroyed permanently. Separate from
/// <see cref="TrashConflictKind"/> on purpose: that one is about whether a restore
/// would collide, this one is about whether a delete would take something with it.
/// An item can be fine on one and blocked on the other.
/// </summary>
public enum TrashPurgeBlock
{
    /// <summary>Nothing in the way — permanent deletion is allowed.</summary>
    None = 0,

    /// <summary>Visitors submitted forms on this page; destroying it would destroy those enquiries.</summary>
    HasFormSubmissions = 1,

    /// <summary>Sub-pages still point at this page, and that reference is not nullable.</summary>
    HasChildren = 2,
}
