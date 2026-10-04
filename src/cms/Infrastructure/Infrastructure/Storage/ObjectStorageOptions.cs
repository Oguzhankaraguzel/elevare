namespace Infrastructure.Storage;

/// <summary>
/// Object storage configuration, bound from <c>appsettings.json → ObjectStorage</c> —
/// or, if set, from the encrypted <c>IntegrationSecrets</c> table (the CMS's "Sırlar"
/// screen), layered on top of the file at startup; see
/// <c>IntegrationSecretsBootstrap</c>. Read via <c>IOptionsMonitor</c> throughout, so a
/// save from "Sırlar" — including switching <see cref="Provider"/> itself — applies
/// live with no restart; see <c>IBlobStorageFactory</c>/<c>BlobStorageFactory</c> for
/// how the provider switch is resolved per-call instead of once at DI-registration
/// time, and <c>IS3ClientProvider</c> for how S3 credential changes rebuild the
/// underlying client.
/// </summary>
public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>"Local" (default) or "S3".</summary>
    public string Provider { get; init; } = "Local";

    public string BucketName { get; init; } = "";
    public string Region { get; init; } = "";
    public string AccessKey { get; init; } = "";
    public string SecretKey { get; init; } = "";

    /// <summary>
    /// Optional — set this for S3-compatible non-AWS endpoints (MinIO, DigitalOcean
    /// Spaces, Cloudflare R2, etc.). Leave empty to use AWS's own regional endpoint.
    /// </summary>
    public string? ServiceUrl { get; init; }

    /// <summary>
    /// Optional public base URL for the bucket (e.g. a CDN or custom domain
    /// fronting it). Falls back to the bucket's own regional/service URL when unset.
    /// </summary>
    public string? PublicBaseUrl { get; init; }
}
