using Amazon.S3;

namespace Infrastructure.Storage;

/// <summary>
/// Holds the live <see cref="AmazonS3Client"/> for whichever S3 credentials are
/// currently configured — separated out from <see cref="S3BlobStorage"/> itself
/// so the (expensive-ish, connection-holding) client can be a true singleton,
/// rebuilt only when credentials actually change, while <see cref="S3BlobStorage"/>
/// stays a cheap, disposable-per-scope wrapper resolved by
/// <see cref="Application.Abstraction.Services.Storage.IBlobStorageFactory"/>.
/// </summary>
internal interface IS3ClientProvider
{
    AmazonS3Client Current { get; }
}
