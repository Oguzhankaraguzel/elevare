using System.ComponentModel.DataAnnotations;
using Domain.Entities.Abstractions;

namespace Domain.Entities.Media;

/// <summary>
/// Stores all media files uploaded to the CMS (images, videos, documents) in a single table.
/// All content types (article cover images, category images, etc.) reference this table via FK,
/// centralizing media management and preventing duplicate uploads.
/// <para>
/// A media file has exactly one state that matters: deleted or not. <c>IsActive</c>,
/// inherited from <see cref="BaseEntity"/>, is deliberately NOT used here — nothing
/// reads it and nothing writes it, so every row simply carries the default true.
/// It used to be surfaced as an "Aktiflik" switch, which promised more than it did:
/// the file kept being served at its URL either way (<c>/uploads/{**path}</c> never
/// consulted it), so the only thing it actually changed was whether the variant made
/// it into a <c>srcset</c> — a rule nobody could have guessed from the label. Hiding
/// a file from the site is what deleting it is for.
/// </para>
/// </summary>
public class MediaFile : BaseEntity
{
    [MaxLength(255)]
    public required string FileName { get; set; }

    [MaxLength(255)]
    public required string OriginalFileName { get; set; }

    /// <summary>Full path or URL on the server or storage service.</summary>
    [MaxLength(500)]
    public required string FilePath { get; set; }

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }

    [MaxLength(100)]
    public required string MimeType { get; set; }

    /// <summary>Accessibility text (alt text) for images.</summary>
    [MaxLength(500)]
    public string? AltText { get; set; }

    /// <summary>Title displayed in the media library.</summary>
    [MaxLength(255)]
    public string? Title { get; set; }

    /// <summary>Logical folder path (e.g., /uploads/2025/articles).</summary>
    [MaxLength(500)]
    public string? FolderPath { get; set; }

    /// <summary>Width in pixels (images only).</summary>
    public int? Width { get; set; }

    /// <summary>Height in pixels (images only).</summary>
    public int? Height { get; set; }

    /// <summary>
    /// The WebP and resized copies the CMS made of this image at upload, as JSON —
    /// see <c>SharedKernel.Media.ImageRenditions</c> for the shape and for why
    /// they live here rather than as rows of their own. Null for anything that is
    /// not a derivable image (documents, videos, GIF, SVG) and for images uploaded
    /// before renditions existed, until the backfill job reaches them.
    /// <para>
    /// The image itself is the "master": stored once, capped at 2560 pixels on its
    /// long edge (nothing on a web page needs more, and the 2 MB camera originals
    /// that were being served as-is were most of what Lighthouse counted against
    /// the homepage). Every rendition is made from it and can be remade from it.
    /// </para>
    /// </summary>
    public string? Renditions { get; set; }

    public MediaType MediaType { get; set; } = MediaType.Other;
}
