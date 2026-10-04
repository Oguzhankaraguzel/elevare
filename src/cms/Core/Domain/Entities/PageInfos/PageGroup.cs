using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.PageInfos;

/// <summary>
/// Groups pages that are translations of the same content across different languages.
/// All <see cref="PageInfo"/> records sharing the same <see cref="PageGroup"/> are
/// considered language alternates (hreflang) of each other.
/// 
/// Example:
///   PageGroup "C# Programming" links:
///     - PageInfo { Slug = "c-sharp", LanguageId = 1 (TR) }  → /yazilim/c-sharp
///     - PageInfo { Slug = "c-sharp", LanguageId = 2 (EN) }  → /en/software/c-sharp
/// </summary>
public class PageGroup
{
    public int Id { get; init; }

    /// <summary>Internal label; not shown on the front-end.</summary>
    [MaxLength(200)]
    public required string Name { get; set; }

    #region Navigation Props
    public virtual ICollection<PageInfo>? Pages { get; set; }
    #endregion
}
