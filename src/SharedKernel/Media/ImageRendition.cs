using System.Text.Json.Serialization;

namespace SharedKernel.Media;

/// <summary>
/// One derived copy of an uploaded image: the same picture at <see cref="Width"/>
/// pixels, as a WebP file and/or as a file in the upload's own format (the fallback
/// for a reader without WebP). Either path may be absent — a WebP upload gets no
/// second WebP of itself at full size, and the full-size entry has no fallback
/// because the stored image itself is that fallback.
/// </summary>
public sealed record ImageRendition(
    [property: JsonPropertyName("w")] int Width,
    [property: JsonPropertyName("h")] int Height,
    [property: JsonPropertyName("webp")] string? WebpPath,
    [property: JsonPropertyName("fb")] string? FallbackPath);
