namespace Application.Features.Trash;

/// <summary>A heads-up shown next to a trashed <c>PageInfo</c> before the admin restores it.</summary>
public enum TrashConflictKind
{
    None = 0,

    /// <summary>Another live page now occupies this page's old slug+language.</summary>
    SlugTaken = 1,

    /// <summary>A live redirect rule now claims this page's old FullSlug.</summary>
    RedirectExists = 2,
}
