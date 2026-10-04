using SharedKernel.Concrete;

namespace Domain.Entities.Media;

public static class MediaFileErrors
{
    public static readonly Error NotFound = Error.NotFound("MediaFile.NotFound", "The media file was not found.");
}
