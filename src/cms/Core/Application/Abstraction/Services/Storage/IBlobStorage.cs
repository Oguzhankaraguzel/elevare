using SharedKernel.Concrete;

namespace Application.Abstraction.Services.Storage;

/// <summary>
/// Physical byte storage for uploaded media — the strategy behind <c>IFileService</c>.
/// <paramref name="relativePath"/> everywhere below is the STORAGE-INTERNAL key
/// (e.g. <c>images/2026/07/abc.jpg</c>, matching <c>MediaFile.FolderPath</c> +
/// <c>FileName</c>), never the public URL — the two diverge once a provider like
/// S3/CDN is in play, where the servable URL lives on a different host entirely.
/// Which implementation is active is read live from <c>ObjectStorage:Provider</c>
/// on every call — see <see cref="IBlobStorageFactory"/> — so switching Local
/// disk ↔ S3 from the CMS's "Sırlar" screen applies immediately, no restart.
/// <para>
/// Every member returns <see cref="Result"/> rather than throwing. This is a remote
/// I/O boundary — a full disk, a revoked S3 key, a missing bucket, a network blip —
/// and the caller genuinely needs to tell those apart from success and report them
/// to the operator. See <c>BlobStorageErrors</c> for the catalog; implementations
/// must translate provider exceptions into those errors and never let a raw
/// <c>AmazonS3Exception</c> or <c>IOException</c> escape.
/// </para>
/// </summary>
public interface IBlobStorage
{
    Task<Result> SaveAsync(Stream content, string relativePath, string contentType, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the stored object. Fails with <c>BlobStorageErrors.NotFound</c> when
    /// nothing exists at that key — distinct from a read failure, so the caller can
    /// answer 404 without having to guess which happened.
    /// </summary>
    Task<Result<Stream>> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// The URL visitors/the CMS use to reach the file — persisted as
    /// <c>MediaFile.FilePath</c>. Async because the local-disk implementation
    /// reads the CDN-prefix Site Setting fresh on every call (a content setting,
    /// safe to change live — unlike the storage provider choice itself).
    /// </summary>
    Task<Result<string>> GetPublicUrlAsync(string relativePath, CancellationToken cancellationToken = default);
}
