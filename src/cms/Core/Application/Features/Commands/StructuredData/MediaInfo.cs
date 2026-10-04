namespace Application.Features.Commands.StructuredData;

/// <summary>What the media library knows about one file, keyed by its stored path.</summary>
internal sealed record MediaInfo(int? Width, int? Height, string? MimeType, string? AltText);
