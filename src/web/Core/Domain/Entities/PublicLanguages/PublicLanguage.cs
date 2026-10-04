using System.Linq.Expressions;

namespace Domain.Entities.PublicLanguages;

/// <summary>Read-only projection of the CMS's <c>Languages</c> table.</summary>
public sealed class PublicLanguage
{
    public int Id { get; set; }
    public string TwoLetterCode { get; set; } = null!;

    /// <summary>
    /// Display label for the language switcher. Nullable, unlike the CMS's own
    /// required column of the same name: every other property here is load-bearing
    /// for routing, and a pile of existing tests construct <see cref="PublicLanguage"/>
    /// without this one — it is purely cosmetic, so callers fall back to
    /// <see cref="TwoLetterCode"/> rather than this type inheriting a constraint it
    /// doesn't actually need.
    /// </summary>
    public string? NameInNative { get; set; }
    public bool IsDefault { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>
    /// The language is available for authoring in the CMS. It says nothing about
    /// the public site — a language can be actively worked on for months before
    /// anyone outside sees it.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// The language is live on the public site. This is the flag that decides
    /// visibility here; <see cref="IsActive"/> alone must never be enough, or
    /// translations still being written would go public the moment they are saved.
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// The single rule the public site resolves languages by: authorable *and*
    /// released. An expression rather than a method so EF can translate it into
    /// SQL, and stated once so the two halves cannot drift apart across the
    /// several services that need it.
    /// </summary>
    public static Expression<Func<PublicLanguage, bool>> PubliclyVisible
        => l => l.IsActive && l.IsPublished;
}
