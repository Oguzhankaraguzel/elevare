using System.ComponentModel.DataAnnotations;
using Domain.Entities.Abstractions;

namespace Domain.Entities.SiteSettings;

/// <summary>
/// Stores site-wide configuration values (site name, logo, SMTP, Google Analytics code, etc.)
/// as key-value pairs.
/// Settings with IsSystem = true cannot be deleted; only their Value can be changed.
/// The DataType hint (string | bool | int | json) allows the application layer to parse the value correctly.
/// </summary>
public class SiteSetting : BaseEntity
{
    [MaxLength(200)]
    public required string Key { get; set; }

    /// <summary>The setting value. Large JSON blobs are also supported.</summary>
    public string? Value { get; set; }

    [MaxLength(200)]
    public required string DisplayName { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public SiteSettingGroup Group { get; set; } = SiteSettingGroup.General;

    /// <summary>When true the record cannot be deleted by the admin.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Value type hint used by the CMS Site Settings UI to pick an input widget:
    /// "string" | "bool" | "int" | "json" | "url" | "css" | "html" | "captcha-provider".</summary>
    [MaxLength(50)]
    public string? DataType { get; set; }
}
