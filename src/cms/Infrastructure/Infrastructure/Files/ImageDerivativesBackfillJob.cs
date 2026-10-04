using Application.Abstraction.Data;
using Application.Abstraction.Services.Files;
using Application.Abstraction.Services.Storage;
using Domain.Entities.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;
using SharedKernel.Media;

namespace Infrastructure.Files;

/// <summary>
/// Brings images uploaded before masters and renditions existed up to the same
/// state a fresh upload gets (see <see cref="ImageDerivatives"/>): the stored file
/// is capped and re-encoded where that applies — a 2 MB camera JPEG shrinks in
/// place, same path, same URL — and its renditions are generated and recorded on
/// the row. Queued once at every CMS start; an image that already carries a
/// complete set of renditions is skipped, so a library that is up to date costs
/// one query. "Complete" is measured against <see cref="ImageDerivatives.Widths"/>,
/// so adding a width there is enough for every existing image to get it at the
/// next start — the master is left alone then, only the ladder is remade.
/// <para>
/// Runs on Hangfire, not in the request that started the app: a library of a few
/// hundred photographs is minutes of CPU, and nothing waits for it — a page whose
/// image has no renditions yet is served as it was until they exist.
/// </para>
/// </summary>
public sealed class ImageDerivativesBackfillJob(
    ICmsApplicationDbContext db,
    IBlobStorage blobStorage,
    IFileService fileService,
    ILogger<ImageDerivativesBackfillJob> logger)
{
    private const int BatchSize = 25;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (fileService is not FileService files)
        {
            logger.LogWarning("[Media] Rendition backfill skipped: file service is {Type}.", fileService.GetType().Name);
            return;
        }

        List<MediaFile> images = await db.MediaFiles
            .Where(m => m.MediaType == MediaType.Image
                        && (m.MimeType == "image/jpeg" || m.MimeType == "image/png" || m.MimeType == "image/webp"))
            .OrderBy(m => m.Id)
            .ToListAsync(cancellationToken);
        List<MediaFile> candidates = [.. images.Where(NeedsRenditions)];

        if (candidates.Count == 0) return;
        logger.LogInformation("[Media] Building masters and renditions for {Count} existing images.", candidates.Count);

        int done = 0;
        foreach (MediaFile image in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key = $"{image.FolderPath}/{image.FileName}";
            try
            {
                Result<Stream> opened = await blobStorage.OpenReadAsync(key, cancellationToken);
                if (opened.IsFailure)
                {
                    logger.LogWarning("[Media] '{Path}' could not be read: {Error}", image.FilePath, opened.Error.Description);
                    continue;
                }
                byte[] bytes;
                using (Stream stream = opened.Value)
                using (MemoryStream buffer = new())
                {
                    await stream.CopyToAsync(buffer, cancellationToken);
                    bytes = buffer.ToArray();
                }

                // A master already made once is not made again — only an image the
                // pipeline has never touched (no renditions at all) gets capped and
                // re-encoded; for the rest the stored file is the master as it is.
                bool fresh = image.Renditions is null;
                ImageDerivatives.Master? master = fresh
                    ? ImageDerivatives.BuildMaster(bytes, image.MimeType)
                    : new ImageDerivatives.Master(bytes, image.Width ?? 0, image.Height ?? 0, Reencoded: false);
                if (master is null)
                {
                    logger.LogWarning("[Media] '{Path}' is not a decodable image; left as is.", image.FilePath);
                    continue;
                }
                if (master.Reencoded)
                {
                    // Same key, so every page that embeds the file keeps working —
                    // it just gets a lighter file at that address from now on.
                    using MemoryStream stream = new(master.Bytes, writable: false);
                    Result saved = await blobStorage.SaveAsync(stream, key, image.MimeType, cancellationToken);
                    if (saved.IsFailure)
                    {
                        logger.LogWarning("[Media] Master of '{Path}' could not be stored: {Error}", image.FilePath, saved.Error.Description);
                        continue;
                    }
                    image.FileSize = master.Bytes.LongLength;
                }
                if (fresh)
                {
                    image.Width = master.Width;
                    image.Height = master.Height;
                }

                await files.GenerateRenditionsAsync(image, master.Bytes, cancellationToken);
                done++;
                if (done % BatchSize == 0) await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[Media] Master/renditions failed for '{Path}'.", image.FilePath);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("[Media] Masters and renditions built for {Done} existing images.", done);
    }

    /// <summary>
    /// No renditions at all, or a ladder missing a width the master is wide enough
    /// for. A master narrower than every rung legitimately has only its own-size
    /// WebP (or, for a WebP master, nothing), and is not redone for that.
    /// </summary>
    internal static bool NeedsRenditions(MediaFile image)
    {
        if (image.Renditions is null) return true;
        if (image.Width is not > 0) return false;
        HashSet<int> have = [.. ImageRenditions.Parse(image.Renditions).Select(r => r.Width)];
        return ImageDerivatives.Widths.Any(w => w < image.Width.Value && !have.Contains(w));
    }
}
