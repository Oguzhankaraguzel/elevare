namespace Wasm.Models.Languages;

public sealed class LanguageFormModel
{
    public string NameInNative { get; set; } = "";
    public string NameInEnglish { get; set; } = "";
    public string TwoLetterCode { get; set; } = "";
    public string? FlagIcon { get; set; }
    public bool IsDefault { get; set; }
    public bool IsRtl { get; set; }
    /// <summary>
    /// A new language starts authorable but not live. Defaulting this to true would
    /// put an empty language on the public site the moment it is created, before a
    /// single page has been translated into it.
    /// </summary>
    public bool IsPublished { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
