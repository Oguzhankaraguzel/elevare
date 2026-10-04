namespace Application.Features.Queries.Languages.GetLanguages;

public sealed record LanguageResponse(
    int Id,
    string NameInNative,
    string NameInEnglish,
    string TwoLetterCode,
    int? FlagIconFileId,
    string? FlagIconPath,
    string? FlagIconAltText,
    bool IsDefault,
    bool IsRtl,
    bool IsPublished,
    int DisplayOrder,
    bool IsActive);
