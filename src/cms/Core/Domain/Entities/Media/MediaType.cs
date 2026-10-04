namespace Domain.Entities.Media;

/// <summary>
/// Specifies the type of the uploaded file.
/// Differentiates between image, video, document, etc.; used in filtering and preview logic.
/// </summary>
public enum MediaType
{
    Image = 1,
    Video,
    Document,
    Audio,
    Other
}
