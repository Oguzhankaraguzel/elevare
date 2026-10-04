using Domain.Entities.Media;

namespace Application.Abstraction.Services.Files;

/// <summary>
/// Carries all data needed to persist a single uploaded file.
/// The <see cref="Content"/> stream is consumed once and should be disposed by the caller.
/// </summary>
public sealed class FileUploadRequest
{
    /// <summary>Original file name as supplied by the client (e.g. <c>photo.jpg</c>).</summary>
    public required string FileName { get; init; }

    /// <summary>MIME type reported by the client (e.g. <c>image/jpeg</c>).</summary>
    public required string ContentType { get; init; }

    /// <summary>Readable stream of the file bytes.</summary>
    public required Stream Content { get; init; }

    /// <summary>Total byte length of the file (used for validation before streaming).</summary>
    public long FileSize { get; init; }

    /// <summary>Accessibility / SEO alt text for the file (images).</summary>
    public string? AltText { get; init; }

    /// <summary>Human-friendly title shown in the media library.</summary>
    public string? Title { get; init; }
}
