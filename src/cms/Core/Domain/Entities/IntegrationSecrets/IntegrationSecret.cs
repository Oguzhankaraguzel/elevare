using System.ComponentModel.DataAnnotations;
using Domain.Entities.Abstractions;

namespace Domain.Entities.IntegrationSecrets;

/// <summary>
/// Configuration for an outbound integration (SMTP, CAPTCHA, S3/CDN) that used to live
/// only in <c>appsettings.json</c>. Kept in its own table, separate from
/// <see cref="Domain.Entities.SiteSettings.SiteSetting"/>, for one reason: that table's
/// read query has no permission check (it never needed one — nothing in it was ever
/// sensitive), and adding real credentials as more rows there would have inherited that
/// gap. This table's query is gated on its own, narrower permission instead — see
/// <c>PermissionKeys.SecretsManage</c>.
/// <para>
/// <see cref="Key"/> uses <c>IConfiguration</c>'s own <c>:</c> section separator (e.g.
/// <c>"Email:Password"</c>) rather than SiteSettings' <c>.</c> convention, because these
/// rows are read back at startup to override the matching <c>appsettings.json</c> path
/// directly — see <c>IntegrationSecretsBootstrap</c>.
/// </para>
/// </summary>
public class IntegrationSecret : BaseEntity
{
    [MaxLength(200)]
    public required string Key { get; set; }

    /// <summary>
    /// The value — encrypted at rest (ASP.NET Data Protection) when
    /// <see cref="IsSecret"/> is true, plain text otherwise (a port number or a
    /// provider name gains nothing from encryption and loses readability).
    /// </summary>
    public string? Value { get; set; }

    [MaxLength(200)]
    public required string DisplayName { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public IntegrationSecretCategory Category { get; set; }

    /// <summary>
    /// True for a credential (password, access key) — encrypted at rest, and never
    /// returned in plain text by the read query; only whether it is set. False for
    /// plain configuration (host, port, provider) shown and edited as-is.
    /// </summary>
    public bool IsSecret { get; set; }

    /// <summary>When true the record cannot be deleted; only its Value can change.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Value type hint for the CMS UI's input widget: "string" | "int" | "bool" | "select".</summary>
    [MaxLength(50)]
    public string? DataType { get; set; }
}
