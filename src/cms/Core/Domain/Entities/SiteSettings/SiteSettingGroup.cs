namespace Domain.Entities.SiteSettings;

/// <summary>
/// Divides site settings into logical groups.
/// Each group can be displayed as a separate tab in the admin panel.
/// <para>
/// <c>General</c>, <c>Social</c>, <c>Contact</c>, <c>Analytics</c>, <c>Email</c>,
/// <c>Appearance</c>, <c>Security</c>, <c>Localization</c> and <c>Advanced</c> are kept
/// only so the stored int values of <c>Identity</c>/<c>ContactSocial</c>/<c>System</c>
/// never collide with a value a pre-upgrade database still has on disk — every setting
/// that used to sit under them was reassigned to one of the three new groups by
/// <c>DatabaseSeeder.RegroupSiteSettingsAsync</c>. None of the retired names is expected
/// to hold any row going forward; do not seed new settings into them.
/// </para>
/// </summary>
public enum SiteSettingGroup
{
    General = 1,
    Seo,
    Social,
    Contact,
    Analytics,
    Email,
    Appearance,
    Security,
    Localization,
    Advanced,
    Integrations,

    /// <summary>Brand identity: name, tagline, logo, favicon, global CSS, public site URL.</summary>
    Identity,

    /// <summary>How to reach the business and where to find it online — contact fields plus social links.</summary>
    ContactSocial,

    /// <summary>Operational settings: outgoing mail identity, maintenance mode.</summary>
    System,
}
