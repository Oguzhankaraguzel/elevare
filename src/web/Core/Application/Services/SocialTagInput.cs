using SharedKernel.Social;

namespace Application.Services;

/// <summary>What the builder needs from the page: the five core fields the page keeps in
/// its own columns, the locale already derived from its language, and the rest.</summary>
public sealed record SocialTagInput(
    string Title,
    string? Description,
    string? Type,
    string? Image,
    string? Link,
    string? TwitterCard,
    string? TwitterSite,
    string Locale,
    IReadOnlyList<string> AlternateLocales,
    SocialMeta Social);
