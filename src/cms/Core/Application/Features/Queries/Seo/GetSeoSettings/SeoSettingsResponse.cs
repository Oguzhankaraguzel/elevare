namespace Application.Features.Queries.Seo.GetSeoSettings;

public sealed record SeoSettingsResponse(
    string? MetaTitleSuffix,
    string? MetaDescription,
    string? OgImage,
    string? RobotsTxt,
    string? LlmsTxt);
