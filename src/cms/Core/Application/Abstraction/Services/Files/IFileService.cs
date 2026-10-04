using SharedKernel.Concrete;

namespace Application.Abstraction.Services.Files;

/// <summary>
/// Contract for all file-storage operations in the CMS.
/// Implementations live in the Infrastructure layer and write files to the local file system
/// while keeping the <c>MediaFile</c> entity in sync with the database.
/// </summary>
public interface IFileService
{
    /// <summary>Validates, persists and registers a single uploaded file.</summary>
    Task<Result<FileResult>> UploadAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads multiple files in one call.
    /// Aborts on the first validation failure; already-persisted files in the same
    /// call are NOT rolled back (caller is responsible for cleanup).
    /// </summary>
    Task<Result<IReadOnlyList<FileResult>>> UploadManyAsync(
        IEnumerable<FileUploadRequest> requests,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a paged, optionally filtered list of files from the media library.</summary>
    Task<Result<PagedResult<FileResult>>> ListAsync(
        FileListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the full metadata record for a single file.</summary>
    Task<Result<FileResult>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the database record and, when <paramref name="deletePhysicalFile"/> is
    /// <c>true</c> (default), also removes the file from the file system.
    /// </summary>
    Task<Result> DeleteAsync(
        int id,
        bool deletePhysicalFile = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read stream for the file identified by <paramref name="id"/>.
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    Task<Result<Stream>> GetStreamAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a file with the same original name already exists in the given folder.
    /// Useful for detecting duplicate uploads before streaming the content.
    /// </summary>
    Task<bool> ExistsAsync(
        string originalFileName,
        string folderPath,
        CancellationToken cancellationToken = default);
}
