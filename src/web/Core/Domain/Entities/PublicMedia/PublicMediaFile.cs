namespace Domain.Entities.PublicMedia;

/// <summary>
/// Read-only projection of the CMS's <c>MediaFiles</c> table — only the columns the
/// public site needs to turn an embedded <c>&lt;img&gt;</c> into a responsive one.
/// <para>
/// The site never writes here. Uploading, renaming and deleting all stay in the CMS;
/// this exists so a rendered page can answer "what other sizes of this image are
/// there, and what is its alt text?" without the two apps sharing more than the
/// database they already share.
/// </para>
/// </summary>
public sealed class PublicMediaFile
{
    public int Id { get; set; }

    /// <summary>
    /// Serving URL as stored by the CMS — normally site-relative
    /// (<c>/uploads/images/2026/09/abc.jpg</c>), or absolute when a CDN or S3 is
    /// configured. Matched against the <c>src</c> of images found in page HTML.
    /// </summary>
    public string FilePath { get; set; } = null!;

    public string? AltText { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    /// <summary>What the file is — <c>image/webp</c> and the like. Decides which
    /// <c>&lt;picture&gt;</c> source a variant goes into.</summary>
    public string MimeType { get; set; } = "";

    /// <summary>
    /// Groups the size variants of one logical image. Images sharing this id are the
    /// same picture at different widths, which is exactly what a <c>srcset</c> is.
    /// </summary>
    /// <summary>
    /// The WebP and resized copies the CMS made of this image, as JSON — parsed with
    /// <c>SharedKernel.Media.ImageRenditions</c>. Null for images the CMS has not
    /// (yet) derived anything from; those are served as they are.
    /// </summary>
    public string? Renditions { get; set; }

    public bool IsDeleted { get; set; }
}
