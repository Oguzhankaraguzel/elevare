using SharedKernel.Concrete;

namespace Domain.Entities.PublicPages;

public static class PublicFeedErrors
{
    public static Error LanguageNotFound(string languageCode) =>
        Error.NotFound("Feed.LanguageNotFound", $"No published language '{languageCode}' to build a feed for.");
}
