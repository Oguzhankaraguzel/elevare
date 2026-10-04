namespace Infrastructure.Files;

/// <summary>
/// File-storage configuration bound from <c>appsettings.json → FileStorage</c>.
/// </summary>
public sealed class FileServiceOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Root folder relative to <c>wwwroot</c> where all uploads are stored.
    /// Defaults to <c>uploads</c>.
    /// </summary>
    public string UploadFolder { get; init; } = "uploads";

    /// <summary>Maximum allowed file size in bytes. Defaults to 50 MB.</summary>
    public long MaxFileSizeBytes { get; init; } = 50 * 1024 * 1024;

    /// <summary>
    /// MIME types that are accepted for upload.
    /// An empty list means all types are accepted (not recommended for production).
    /// </summary>
    public List<string> AllowedMimeTypes { get; init; } =
    [
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "image/svg+xml", "image/bmp", "image/tiff",
        "image/x-icon", "image/vnd.microsoft.icon",
        "video/mp4", "video/webm", "video/ogg",
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain", "text/csv",
        "audio/mpeg", "audio/wav", "audio/ogg",
    ];

    /// <summary>
    /// When <c>true</c>, sub-folders are created per year and month
    /// (e.g. <c>uploads/images/2025/03/</c>). Defaults to <c>true</c>.
    /// </summary>
    public bool OrganizeByDate { get; init; } = true;
}
