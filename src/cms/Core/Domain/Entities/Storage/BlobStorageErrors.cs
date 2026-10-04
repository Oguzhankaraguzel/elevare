using SharedKernel.Concrete;

namespace Domain.Entities.Storage;

/// <summary>
/// Errors produced by <c>IBlobStorage</c> implementations.
/// <para>
/// Every one of these carries the underlying provider's own message. Storage
/// failures are diagnosed almost entirely from that text — "Access is denied",
/// "The specified bucket does not exist", "There is not enough space on the disk",
/// an expired access key — so collapsing them into a generic "storage error" would
/// throw away the only part an operator can act on.
/// </para>
/// </summary>
public static class BlobStorageErrors
{
    public static Error SaveFailed(string detail) =>
        Error.Problem("BlobStorage.SaveFailed", $"Storing the file failed: {detail}");

    public static Error DeleteFailed(string detail) =>
        Error.Problem("BlobStorage.DeleteFailed", $"Deleting the file failed: {detail}");

    public static Error ReadFailed(string detail) =>
        Error.Problem("BlobStorage.ReadFailed", $"Reading the file failed: {detail}");

    public static Error UrlResolutionFailed(string detail) =>
        Error.Problem("BlobStorage.UrlResolutionFailed", $"Building the public URL failed: {detail}");

    /// <summary>
    /// Code of <see cref="NotFound"/>, so a caller can recognise that specific outcome
    /// without rebuilding the error just to compare it (the description embeds the path,
    /// which would make equality comparison fragile).
    /// </summary>
    public const string NotFoundCode = "BlobStorage.NotFound";

    /// <summary>
    /// No object exists at that key. Deliberately distinct from <see cref="ReadFailed"/>:
    /// a missing file is an expected outcome the caller reports as 404, whereas a read
    /// failure means the storage backend itself is in trouble.
    /// </summary>
    public static Error NotFound(string relativePath) =>
        Error.NotFound(NotFoundCode, $"No stored object was found at '{relativePath}'.");
}
