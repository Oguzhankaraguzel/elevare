using Application.Abstraction.Data;
using Application.Abstraction.Services.Files;
using Application.Abstraction.Services.Storage;
using Domain.Entities.Media;
using Microsoft.AspNetCore.StaticFiles;
using Domain.Entities.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Linq;
using SharedKernel.Media;

namespace Infrastructure.Files;

/// <summary>
/// Implementation of <see cref="IFileService"/> — owns the <c>MediaFile</c>
/// database bookkeeping; the actual bytes are read/written through
/// <see cref="IBlobStorage"/> (local disk by default, S3 when configured), so
/// this class never touches the file system or a bucket directly.
/// </summary>
internal sealed class FileService : IFileService
{
    private readonly FileServiceOptions _options;
    private readonly ICmsApplicationDbContext _db;
    private readonly IBlobStorage _blobStorage;
    private readonly ILogger<FileService> _logger;

    // ── MIME → MediaType mapping ───────────────────────────────────────────────
    private static readonly Dictionary<string, MediaType> MimeToMediaType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"]  = MediaType.Image, ["image/png"]  = MediaType.Image,
        ["image/gif"]   = MediaType.Image, ["image/webp"] = MediaType.Image,
        ["image/bmp"]   = MediaType.Image, ["image/tiff"] = MediaType.Image,
        ["image/svg+xml"] = MediaType.Image,
        ["image/x-icon"] = MediaType.Image, ["image/vnd.microsoft.icon"] = MediaType.Image,
        ["video/mp4"]   = MediaType.Video, ["video/webm"] = MediaType.Video,
        ["video/ogg"]   = MediaType.Video, ["video/quicktime"] = MediaType.Video,
        ["audio/mpeg"]  = MediaType.Audio, ["audio/wav"]  = MediaType.Audio,
        ["audio/ogg"]   = MediaType.Audio, ["audio/aac"]  = MediaType.Audio,
        ["application/pdf"]    = MediaType.Document,
        ["application/msword"] = MediaType.Document,
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = MediaType.Document,
        ["application/vnd.ms-excel"] = MediaType.Document,
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = MediaType.Document,
        ["application/vnd.ms-powerpoint"] = MediaType.Document,
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = MediaType.Document,
        ["text/plain"]  = MediaType.Document, ["text/csv"] = MediaType.Document,
    };

    public FileService(
        IOptions<FileServiceOptions> options,
        ICmsApplicationDbContext db,
        IBlobStorage blobStorage,
        ILogger<FileService> logger)
    {
        _options = options.Value;
        _db      = db;
        _blobStorage = blobStorage;
        _logger  = logger;
    }

    // ── Upload ────────────────────────────────────────────────────────────────

    public async Task<Result<FileResult>> UploadAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        Result validationResult = Validate(request);
        if (validationResult.IsFailure)
            return Result.Failure<FileResult>(validationResult.Error);

        try
        {
            return await PersistFileAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[File] Upload failed for '{FileName}'.", request.FileName);
            return Result.Failure<FileResult>(FileErrors.UploadFailed(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<FileResult>>> UploadManyAsync(
        IEnumerable<FileUploadRequest> requests,
        CancellationToken cancellationToken = default)
    {
        List<FileResult> results = [];

        foreach (FileUploadRequest request in requests)
        {
            Result<FileResult> result = await UploadAsync(request, cancellationToken);
            if (result.IsFailure)
                return Result.Failure<IReadOnlyList<FileResult>>(result.Error);

            results.Add(result.Value);
        }

        return Result.Success<IReadOnlyList<FileResult>>(results);
    }

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<FileResult>>> ListAsync(
        FileListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<MediaFile> q = _db.MediaFiles
            .AsNoTracking()
            .Where(f => !f.IsDeleted);

        q = q.WhereIf(query.MediaType.HasValue,   f => f.MediaType  == query.MediaType!.Value);
        q = q.WhereIf(!string.IsNullOrEmpty(query.FolderPath),  f => f.FolderPath!.StartsWith(query.FolderPath!));
        q = q.WhereIf(!string.IsNullOrEmpty(query.SearchTerm),  f =>
            f.OriginalFileName.Contains(query.SearchTerm!) ||
            f.Title != null && f.Title.Contains(query.SearchTerm!));

        int total = await q.CountAsync(cancellationToken);

        List<MediaFile> files = await q
            .OrderByDescending(f => f.CreateDate)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var pagedResult = PagedResult<FileResult>.Create(files.Select(MapToResult).ToList().AsReadOnly(), total, query.Page, query.PageSize);
        return Result.Success(pagedResult);
    }

    // ── Get by ID ─────────────────────────────────────────────────────────────

    public async Task<Result<FileResult>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        MediaFile? file = await _db.MediaFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted, cancellationToken);

        if (file is null)
            return Result.Failure<FileResult>(FileErrors.NotFound(id));

        return Result.Success(MapToResult(file));
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task<Result> DeleteAsync(
        int id,
        bool deletePhysicalFile = true,
        CancellationToken cancellationToken = default)
    {
        MediaFile? file = await _db.MediaFiles
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted, cancellationToken);

        if (file is null)
            return Result.Failure(FileErrors.NotFound(id));

        if (deletePhysicalFile)
        {
            // The renditions are the master's, not files of their own (see
            // MediaFile.Renditions); they leave with it.
            foreach (string key in RenditionKeys(file))
                await _blobStorage.DeleteAsync(key, cancellationToken);

            string relativePath = BuildRelativePath(file);
            Result deleted = await _blobStorage.DeleteAsync(relativePath, cancellationToken);

            // A storage failure does not abort the delete: the user asked for the file
            // to go away, and leaving the row behind would make the CMS show a file the
            // operator already considers deleted. The orphaned bytes are logged so they
            // can be cleaned up, which is the lesser of the two problems.
            if (deleted.IsFailure)
                _logger.LogWarning(
                    "[File] Storage delete failed for {Path}, removing the record anyway: {Error}",
                    relativePath, deleted.Error.Description);
            else
                _logger.LogInformation("[File] Deleted from storage: {Path}", relativePath);
        }

        // EF Core audit (IsDeleted = true) is handled by ApplicationDbContext.AuditEntities()
        _db.MediaFiles.Remove(file);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // ── Stream ────────────────────────────────────────────────────────────────

    public async Task<Result<Stream>> GetStreamAsync(int id, CancellationToken cancellationToken = default)
    {
        MediaFile? file = await _db.MediaFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted, cancellationToken);

        if (file is null)
            return Result.Failure<Stream>(FileErrors.NotFound(id));

        Result<Stream> stream = await _blobStorage.OpenReadAsync(BuildRelativePath(file), cancellationToken);
        if (stream.IsSuccess)
            return stream;

        // "Missing" keeps its own error so callers still answer 404 for a file whose
        // row outlived its bytes; anything else is a storage fault and is passed
        // through verbatim, because that message is what an operator needs.
        return stream.Error.Code == BlobStorageErrors.NotFoundCode
            ? Result.Failure<Stream>(FileErrors.PhysicalFileNotFound(file.FileName))
            : stream;
    }

    // ── Exists ────────────────────────────────────────────────────────────────

    public async Task<bool> ExistsAsync(
        string originalFileName,
        string folderPath,
        CancellationToken cancellationToken = default) =>
        await _db.MediaFiles.AnyAsync(
            f => !f.IsDeleted &&
                 f.OriginalFileName == originalFileName &&
                 f.FolderPath == folderPath,
            cancellationToken);

    /// <summary>
    /// The same extension-to-type map <c>MediaEndpoints</c> serves with. Shared on
    /// purpose: validation and serving must agree about what a file is.
    /// </summary>
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    // ── Private helpers ───────────────────────────────────────────────────────

    private Result Validate(FileUploadRequest request)
    {
        if (request.FileSize <= 0)
            return Result.Failure(FileErrors.EmptyFile);

        if (request.FileSize > _options.MaxFileSizeBytes)
            return Result.Failure(FileErrors.FileTooLarge(_options.MaxFileSizeBytes));

        if (_options.AllowedMimeTypes.Count > 0 &&
            !_options.AllowedMimeTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
            return Result.Failure(FileErrors.MimeTypeNotAllowed(request.ContentType));

        // The declared content type alone is not enough: it comes from the client, and
        // the stored file keeps the client's extension. MediaEndpoints then derives the
        // response Content-Type from that extension — so "evil.html" declared as
        // image/png would pass the check above and be served back as text/html from
        // this origin. Requiring the extension to agree with the declared type closes
        // that, and using the same provider MediaEndpoints uses means the two can
        // never drift apart.
        if (!ContentTypes.TryGetContentType(request.FileName, out string? typeFromExtension) ||
            !string.Equals(NormalizeIcoAlias(typeFromExtension), NormalizeIcoAlias(request.ContentType), StringComparison.OrdinalIgnoreCase))
            return Result.Failure(FileErrors.ExtensionMismatch(request.FileName, request.ContentType));

        return Result.Success();
    }

    // .ico has two MIME strings in real use — FileExtensionContentTypeProvider always
    // reports "image/x-icon", but which one a browser attaches to the picked file
    // varies by OS file-association and browser, unlike every other type this service
    // accepts. Treated as the same type here only for that one pair, so the spoofing
    // check above stays exact for everything else.
    private static string NormalizeIcoAlias(string contentType) =>
        string.Equals(contentType, "image/vnd.microsoft.icon", StringComparison.OrdinalIgnoreCase)
            ? "image/x-icon"
            : contentType;

    private async Task<Result<FileResult>> PersistFileAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken)
    {
        MediaType mediaType = ResolveMediaType(request.ContentType);
        string folderPath   = BuildFolderPath(mediaType);
        string baseName     = Guid.NewGuid().ToString();
        string uniqueName   = $"{baseName}{Path.GetExtension(request.FileName)}";
        string relativePath = $"{folderPath}/{uniqueName}";

        // A derivable image is read into memory once: the master, its dimensions and
        // its renditions all come off the same bytes, and the stream a browser hands
        // over (the editor's drag-drop upload) cannot be rewound. Videos, documents,
        // GIF and SVG keep streaming — nothing derives from them.
        //
        // What is stored is the MASTER, not the upload as it came: capped at 2560 px,
        // the right way up, a JPEG re-encoded at quality 90 (see ImageDerivatives).
        // The 2 MB camera originals that used to sit behind 500-pixel logos are the
        // reason; nothing on a page can show more than the master holds.
        ImageDerivatives.Master? master = null;
        Stream content = request.Content;
        long fileSize = request.FileSize;
        if (mediaType == MediaType.Image && ImageDerivatives.CanDerive(request.ContentType))
        {
            using MemoryStream buffer = new();
            await request.Content.CopyToAsync(buffer, cancellationToken);
            byte[] upload = buffer.ToArray();
            try { master = ImageDerivatives.BuildMaster(upload, request.ContentType); }
            catch (Exception ex) { _logger.LogWarning(ex, "[File] Master could not be built for '{Name}'; storing the upload as it came.", request.FileName); }
            byte[] bytes = master?.Bytes ?? upload;
            fileSize = bytes.LongLength;
            content = new MemoryStream(bytes, writable: false);
        }

        // Read before storing: SaveAsync consumes the stream, and the dimensions are
        // what let the public site emit width/height (see ImageDimensionReader).
        // The master already knows its own; only the formats that get no master
        // (GIF, BMP, …) are measured here. A null is normal, not a failure.
        (int Width, int Height)? dimensions = null;
        if (master is not null) dimensions = (master.Width, master.Height);
        else if (mediaType == MediaType.Image) dimensions = ImageDimensionReader.TryRead(content);

        Result saved = await _blobStorage.SaveAsync(content, relativePath, request.ContentType, cancellationToken);
        if (saved.IsFailure)
            return Result.Failure<FileResult>(saved.Error);

        Result<string> publicUrl = await _blobStorage.GetPublicUrlAsync(relativePath, cancellationToken);
        if (publicUrl.IsFailure)
            return Result.Failure<FileResult>(publicUrl.Error);

        string relativeUrl = publicUrl.Value;

        MediaFile entity = new()
        {
            FileName         = uniqueName,
            OriginalFileName = request.FileName,
            FilePath         = relativeUrl,
            FolderPath       = folderPath,
            FileSize         = fileSize,
            MimeType         = request.ContentType,
            MediaType        = mediaType,
            AltText          = request.AltText,
            Title            = request.Title ?? Path.GetFileNameWithoutExtension(request.FileName),
            // IsActive is deliberately not set: media has no active/passive state any
            // more, only deleted or not (see MediaFile). BaseEntity defaults it to
            // true, which is what every row now carries.
            Width            = dimensions?.Width,
            Height           = dimensions?.Height,
        };

        await _db.MediaFiles.AddAsync(entity, cancellationToken);

        _logger.LogInformation("[File] Uploaded '{Original}' → '{Path}' ({Size} bytes).",
            request.FileName, relativeUrl, request.FileSize);

        if (master is not null)
            await GenerateRenditionsAsync(entity, master.Bytes, cancellationToken);

        return Result.Success(MapToResult(entity));
    }

    /// <summary>
    /// The WebP and resized copies of a master (see <see cref="ImageDerivatives"/>),
    /// stored next to it and recorded on its row as <see cref="MediaFile.Renditions"/>
    /// — which is how the public site finds them for <c>srcset</c>. A derivation
    /// that fails is logged and skipped: the master is stored and usable regardless,
    /// only heavier than it could be.
    /// </summary>
    public async Task GenerateRenditionsAsync(MediaFile master, byte[] bytes, CancellationToken cancellationToken)
    {
        ImageDerivatives.DerivedSet derived;
        try
        {
            derived = ImageDerivatives.Generate(bytes, master.MimeType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[File] Renditions could not be generated for '{Path}'.", master.FilePath);
            return;
        }
        if (derived.SourceWidth > 0)
        {
            master.Width = derived.SourceWidth;
            master.Height = derived.SourceHeight;
        }
        if (derived.Items.Count == 0) return;

        string folderPath = master.FolderPath ?? "uploads/images";
        string baseName = Path.GetFileNameWithoutExtension(master.FileName);
        Dictionary<int, (int Height, string? Webp, string? Fallback)> byWidth = [];

        foreach (ImageDerivatives.Derived d in derived.Items)
        {
            string path = $"{folderPath}/{baseName}-{d.Width}w{d.Extension}";
            using MemoryStream stream = new(d.Bytes, writable: false);
            Result saved = await _blobStorage.SaveAsync(stream, path, d.ContentType, cancellationToken);
            if (saved.IsFailure)
            {
                _logger.LogWarning("[File] Rendition {Width}w of '{Path}' not stored: {Error}", d.Width, master.FilePath, saved.Error.Description);
                continue;
            }
            Result<string> url = await _blobStorage.GetPublicUrlAsync(path, cancellationToken);
            if (url.IsFailure) continue;

            (int _, string? webp, string? fallback) = byWidth.GetValueOrDefault(d.Width);
            if (d.ContentType == "image/webp") webp = url.Value; else fallback = url.Value;
            byWidth[d.Width] = (d.Height, webp, fallback);
        }

        master.Renditions = ImageRenditions.ToJson([.. byWidth
            .OrderBy(kv => kv.Key)
            .Select(kv => new ImageRendition(kv.Key, kv.Value.Height, kv.Value.Webp, kv.Value.Fallback))]);

        _logger.LogInformation("[File] {Count} renditions generated for '{Path}'.", byWidth.Count, master.FilePath);
    }

    // Storage keys of a master's renditions, from the public paths the row holds:
    // a rendition sits in the master's folder under its own file name.
    private static IEnumerable<string> RenditionKeys(MediaFile file)
    {
        string folder = file.FolderPath ?? "uploads/images";
        foreach (ImageRendition r in ImageRenditions.Parse(file.Renditions))
        {
            if (r.WebpPath is not null) yield return $"{folder}/{Path.GetFileName(r.WebpPath)}";
            if (r.FallbackPath is not null) yield return $"{folder}/{Path.GetFileName(r.FallbackPath)}";
        }
    }

    // ── Folder name per MediaType ─────────────────────────────────────────────
    private static readonly Dictionary<MediaType, string> FolderByMediaType = new()
    {
        [MediaType.Image]    = "images",
        [MediaType.Video]    = "videos",
        [MediaType.Document] = "documents",
        [MediaType.Audio]    = "audio",
        [MediaType.Other]    = "other",
    };

    private string BuildFolderPath(MediaType mediaType)
    {
        string typeSegment = FolderByMediaType.GetValueOrDefault(mediaType, "other");

        if (!_options.OrganizeByDate)
            return $"{_options.UploadFolder}/{typeSegment}";

        DateTime now = DateTime.UtcNow;
        return $"{_options.UploadFolder}/{typeSegment}/{now.Year}/{now.Month:D2}";
    }

    private static MediaType ResolveMediaType(string mimeType) =>
        MimeToMediaType.TryGetValue(mimeType, out MediaType t) ? t : MediaType.Other;

    // The storage-internal key is reconstructed from FolderPath + FileName (both
    // already persisted at upload time) rather than parsed out of FilePath, since
    // FilePath may be a full external URL (S3/CDN) that no longer resembles the key.
    private static string BuildRelativePath(MediaFile file) => $"{file.FolderPath}/{file.FileName}";

    private static FileResult MapToResult(MediaFile f) => new()
    {
        Id               = f.Id,
        FileName         = f.FileName,
        OriginalFileName = f.OriginalFileName,
        Url              = f.FilePath,
        FilePath         = f.FilePath,
        FolderPath       = f.FolderPath ?? string.Empty,
        FileSize         = f.FileSize,
        MimeType         = f.MimeType,
        MediaType        = f.MediaType,
        AltText          = f.AltText,
        Title            = f.Title,
        Width            = f.Width,
        Height           = f.Height,
        Renditions       = f.Renditions,
        UploadedAt       = f.CreateDate,
    };
}
