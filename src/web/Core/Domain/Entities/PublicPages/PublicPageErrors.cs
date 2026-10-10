using SharedKernel.Concrete;

namespace Domain.Entities.PublicPages;

public static class PublicPageErrors
{
    public static Error NotFound(string fullSlug, string languageCode, string defaultLanguageCode) =>
        Error.NotFound("Page.NotFound", $"No page exists with full slug '{fullSlug}' (language '{languageCode}', default language '{defaultLanguageCode}').");

    public static Error NotPublished(string fullSlug, PublicPageStatus status) =>
        Error.NotFound("Page.NotPublished", $"Page '{fullSlug}' exists but its status is '{status}'. Only Published pages are publicly visible — publish it in the CMS page editor.");

    public static Error Inactive(string fullSlug) =>
        Error.NotFound("Page.Inactive", $"Page '{fullSlug}' exists and is Published, but it is marked inactive.");

    public static Error LanguageNotServed(string fullSlug, string languageCode) =>
        Error.NotFound("Page.LanguageNotServed", $"Page '{fullSlug}' is Published, but it is not a page of the publicly visible language '{languageCode}' (its language is unpublished or inactive in the CMS, or the address belongs to another language).");

    public static Error PreviewNotFound(int pageId) =>
        Error.NotFound("Page.PreviewNotFound", $"No page exists with id '{pageId}'.");
}
