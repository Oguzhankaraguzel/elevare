using Domain.Entities.Media;

namespace Wasm.Components.Shared.Utilities;

public partial class FilePicker
{
    private sealed record MediaFileDto(
        int Id,
        string FileName,
        string FilePath,
        string MimeType,
        long FileSize,
        string? AltText,
        string? Title,
        MediaType MediaType,
        int? Width,
        int? Height,
        DateTime CreateDate);
}
