using Application.Abstraction.Data;
using Application.Abstraction.Services.Storage;
using Domain.Entities.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Infrastructure.Storage;

/// <summary>
/// Default <see cref="IBlobStorage"/> — stores files under <c>wwwroot</c>, exactly
/// as <c>FileService</c> did before this abstraction existed. Prefixes the public
/// URL with <c>Integrations.CdnBaseUrl</c> (Site Settings) when one is configured,
/// read fresh on every call, same as the storage PROVIDER choice itself (see
/// <c>IBlobStorageFactory</c>) — both are safe to change without restarting the app.
/// </summary>
internal sealed class LocalDiskBlobStorage(IWebHostEnvironment env, ICmsApplicationDbContext db) : IBlobStorage
{
    public async Task<Result> SaveAsync(
        Stream content, string relativePath, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            string physicalPath = ToPhysicalPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

            await using FileStream fs = new(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            await content.CopyToAsync(fs, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex) when (IsStorageFault(ex))
        {
            return Result.Failure(BlobStorageErrors.SaveFailed(ex.Message));
        }
    }

    public Task<Result> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            string physicalPath = ToPhysicalPath(relativePath);
            if (File.Exists(physicalPath))
                File.Delete(physicalPath);

            // An already-absent file is success, not failure: the caller's goal is
            // "these bytes are gone", and they are.
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex) when (IsStorageFault(ex))
        {
            return Task.FromResult(Result.Failure(BlobStorageErrors.DeleteFailed(ex.Message)));
        }
    }

    public Task<Result<Stream>> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            string physicalPath = ToPhysicalPath(relativePath);
            if (!File.Exists(physicalPath))
                return Task.FromResult(Result.Failure<Stream>(BlobStorageErrors.NotFound(relativePath)));

#pragma warning disable CA2000 // Caller is responsible for disposing the returned stream
            Stream stream = File.OpenRead(physicalPath);
#pragma warning restore CA2000
            return Task.FromResult(Result.Success(stream));
        }
        catch (Exception ex) when (IsStorageFault(ex))
        {
            return Task.FromResult(Result.Failure<Stream>(BlobStorageErrors.ReadFailed(ex.Message)));
        }
    }

    public async Task<Result<string>> GetPublicUrlAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            string? cdnBaseUrl = await db.SiteSettings
                .Where(s => s.Key == "Integrations.CdnBaseUrl")
                .Select(s => s.Value)
                .FirstOrDefaultAsync(cancellationToken);

            string url = string.IsNullOrWhiteSpace(cdnBaseUrl)
                ? $"/{relativePath}"
                : $"{cdnBaseUrl.TrimEnd('/')}/{relativePath}";

            return Result.Success(url);
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or TimeoutException)
        {
            // The CDN prefix lives in the database, so this call can fail even though
            // building a URL sounds like pure string work.
            return Result.Failure<string>(BlobStorageErrors.UrlResolutionFailed(ex.Message));
        }
    }

    /// <summary>
    /// Faults that mean "the filesystem said no" — a full disk, a denied ACL, a path
    /// the OS rejects. Deliberately does not catch everything: a bug in our own path
    /// construction should still surface as a crash in development rather than being
    /// reported to the user as a storage problem.
    /// </summary>
    private static bool IsStorageFault(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or NotSupportedException;

    private string ToPhysicalPath(string relativePath) =>
        Path.Combine(env.WebRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
