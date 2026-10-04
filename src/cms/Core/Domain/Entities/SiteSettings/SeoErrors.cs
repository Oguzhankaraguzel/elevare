using SharedKernel.Concrete;

namespace Domain.Entities.SiteSettings;

public static class SeoErrors
{
    public static readonly Error RobotsMissingUserAgent = Error.Failure(
        "Seo.RobotsMissingUserAgent",
        "robots.txt needs at least one 'User-agent:' line — without it every rule below is ignored and the file does nothing.");

    public static readonly Error RobotsTooLarge = Error.Failure(
        "Seo.RobotsTooLarge",
        "robots.txt is too long. Crawlers stop reading long files, so the rules at the end would never apply.");

    public static readonly Error LlmsTooLarge = Error.Failure(
        "Seo.LlmsTooLarge",
        "llms.txt is too long. It is meant to be a short map of the site, not a copy of it.");
}
