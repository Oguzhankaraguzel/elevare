using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;

namespace Infrastructure.Storage;

/// <summary>
/// Singleton — rebuilds the <see cref="AmazonS3Client"/> whenever
/// <see cref="ObjectStorageOptions"/> changes (a Sırlar save to any S3 field:
/// AccessKey, SecretKey, Region, ServiceUrl), so credential edits apply to the
/// very next call with no restart. Registered lazily: with <c>Provider</c> set to
/// "Local", nothing ever resolves this type, so it is never constructed and no
/// S3 client is built for a deployment that doesn't use S3.
/// <para>
/// Swaps the client reference atomically (<c>volatile</c>, matching
/// <c>RolePermissionCache</c>'s snapshot-swap style) rather than mutating one in
/// place, so a request mid-flight keeps using the client it started with. The old
/// client is disposed after a short grace period rather than immediately —
/// disposing it the instant a newer one takes over would tear down the
/// connection out from under any S3 call that was already in progress.
/// </para>
/// </summary>
internal sealed class S3ClientProvider : IS3ClientProvider, IDisposable
{
    private static readonly TimeSpan DisposeGrace = TimeSpan.FromSeconds(30);

    private readonly IDisposable? _changeSubscription;
    private volatile AmazonS3Client _client;

    public S3ClientProvider(IOptionsMonitor<ObjectStorageOptions> optionsMonitor)
    {
        _client = BuildClient(optionsMonitor.CurrentValue);
        _changeSubscription = optionsMonitor.OnChange(OnOptionsChanged);
    }

    public AmazonS3Client Current => _client;

    private void OnOptionsChanged(ObjectStorageOptions options)
    {
        AmazonS3Client old = _client;
        _client = BuildClient(options);
        _ = DisposeAfterGraceAsync(old);
    }

    private static async Task DisposeAfterGraceAsync(AmazonS3Client client)
    {
        try
        {
            await Task.Delay(DisposeGrace);
        }
        finally
        {
            client.Dispose();
        }
    }

    private static AmazonS3Client BuildClient(ObjectStorageOptions options)
    {
        AmazonS3Config config = new();
        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.ServiceURL = options.ServiceUrl;
            config.ForcePathStyle = true; // required by most non-AWS S3-compatible services
        }
        else if (!string.IsNullOrWhiteSpace(options.Region))
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
        }

        return new AmazonS3Client(options.AccessKey, options.SecretKey, config);
    }

    public void Dispose()
    {
        _changeSubscription?.Dispose();
        _client.Dispose();
    }
}
