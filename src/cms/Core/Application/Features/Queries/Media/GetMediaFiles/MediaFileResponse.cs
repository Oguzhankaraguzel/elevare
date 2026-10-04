using Domain.Entities.Media;

namespace Application.Features.Queries.Media.GetMediaFiles;

public sealed record MediaFileResponse(
    int Id, string FileName, string OriginalFileName, string FilePath, string MimeType,
    long FileSize, string? AltText, string? Title,
    MediaType MediaType, int? Width, int? Height, DateTime CreateDate,
    string? Renditions);
