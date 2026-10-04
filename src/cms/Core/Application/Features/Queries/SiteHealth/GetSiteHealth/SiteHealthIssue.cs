namespace Application.Features.Queries.SiteHealth.GetSiteHealth;

/// <summary>
/// One thing currently worth an operator's attention.
/// </summary>
/// <param name="Code">
/// Stable identifier (e.g. <c>MaintenanceMode</c>). The UI keys off this rather
/// than off the human text, so wording/translation changes cannot break it.
/// </param>
/// <param name="Severity">How loudly to present it.</param>
/// <param name="Detail">
/// Extra technical text that only the running system can know — a provider's own
/// error message, for instance. Deliberately NOT translated: it is a diagnostic
/// quote, and rewording it would destroy the only clue an operator has. Null when
/// the issue is fully described by its <paramref name="Code"/>.
/// </param>
/// <param name="ActionPath">
/// Site-relative path of the CMS page that fixes it, when there is an obvious one
/// (e.g. <c>/admin/settings</c>). A path rather than a URL on purpose: the CMS is
/// reached under whatever host it happens to be deployed on, so hard-coding an
/// absolute address here would break every environment but one.
/// </param>
/// <remarks>
/// Carries no human-readable title on purpose. Titles and explanations are resx
/// resources resolved by the header component, which keeps this Application-layer
/// type free of the presentation project's resources and lets the same issue read
/// correctly in whichever language the operator is using.
/// </remarks>
public sealed record SiteHealthIssue(
    string Code,
    SiteHealthSeverity Severity,
    string? Detail,
    string? ActionPath);
