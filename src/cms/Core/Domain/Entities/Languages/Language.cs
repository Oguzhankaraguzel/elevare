using System.ComponentModel.DataAnnotations;
using Domain.Entities.Abstractions;
using Domain.Entities.Media;

namespace Domain.Entities.Languages;

public class Language : BaseEntity
{
    [MaxLength(50)]
    public required string NameInNative { get; set; }

    [MaxLength(50)]
    public required string NameInEnglish { get; set; }

    /// <summary>
    /// The single identifier this language is known by everywhere: URL prefix,
    /// hreflang, sitemap section, structured data's <c>inLanguage</c>, and
    /// <c>&lt;html lang&gt;</c>. Despite the name, this isn't limited to two
    /// letters — up to 5 characters, enough for a full region-qualified tag like
    /// "en-gb", which is exactly how two variants of the same language (British
    /// vs. American English, say) get told apart. See the Knowledge Base's
    /// "Two-Letter Code" entry for the full explanation.
    /// </summary>
    [MaxLength(5)]
    public required string TwoLetterCode { get; set; }

    public bool IsDefault { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsRtl { get; set; }

    public bool IsPublished { get; set; }

    #region Foreign Keys
    public int? FlagIconFileId { get; set; }
    #endregion

    #region Navigation Properties
    public MediaFile? FlagIconFiles { get; set; }
    #endregion
}
