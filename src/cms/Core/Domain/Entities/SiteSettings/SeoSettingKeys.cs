namespace Domain.Entities.SiteSettings;

/// <summary>
/// The settings the SEO screen owns. Holding <c>Seo.Manage</c> grants write access to
/// exactly these keys and no others — the allowlist is what stops that permission
/// from becoming a second, quieter way to edit any row in the settings table.
/// </summary>
public static class SeoSettingKeys
{
    public const string MetaTitleSuffix = "Seo.DefaultMetaTitleSuffix";
    public const string MetaDescription = "Seo.DefaultMetaDescription";
    public const string OgImage = "Seo.DefaultOgImageUrl";
    public const string RobotsTxt = "Seo.RobotsTxt";
    public const string LlmsTxt = "Seo.LlmsTxt";

    public static readonly string[] All =
        [MetaTitleSuffix, MetaDescription, OgImage, RobotsTxt, LlmsTxt];
}
