using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Files;
using Application.Abstraction.Services.Storage;
using Domain.Entities.Media;
using Infrastructure.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Persistence.Data;
using SharedKernel.Concrete;
using SharedKernel.Media;
using Shouldly;
using SkiaSharp;

namespace Cms.Tests.Storage;

/// <summary>
/// What an image upload leaves behind, end to end through <see cref="FileService"/>
/// against an in-memory blob store: one row, a capped master at the upload's own
/// address, renditions beside it and recorded on that row — and all of it gone
/// together on delete. This is the contract the media library and the public site
/// both rely on, so it is proved on the real service rather than assumed from the
/// pieces.
/// </summary>
public sealed class ImageUploadPipelineTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly MemoryBlobStorage _blobs = new();
    private readonly FileService _service;

    public ImageUploadPipelineTests()
    {
        DbContextOptions<ApplicationDbContext> dbOptions =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        _db = new ApplicationDbContext(dbOptions, new UploadTestUserContext());
        _service = new FileService(
            Options.Create(new FileServiceOptions { UploadFolder = "uploads", OrganizeByDate = false }),
            _db, _blobs, NullLogger<FileService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static byte[] Jpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.DarkOliveGreen);
            using var paint = new SKPaint { Color = SKColors.Gold };
            canvas.DrawRect(width / 4f, height / 4f, width / 2f, height / 2f, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        return data.ToArray();
    }

    private async Task<FileResult> UploadAsync(string name, string contentType, byte[] bytes)
    {
        Result<FileResult> result = await _service.UploadAsync(new FileUploadRequest
        {
            FileName = name, ContentType = contentType, Content = new MemoryStream(bytes), FileSize = bytes.Length,
            Title = "Test", AltText = "Test görseli",
        }, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : "");
        await _db.SaveChangesAsync();
        return result.Value;
    }

    [Fact]
    public async Task A_large_jpeg_becomes_one_row_with_a_capped_master_and_renditions_beside_it()
    {
        byte[] upload = Jpeg(4000, 2000);
        FileResult stored = await UploadAsync("photo.jpg", "image/jpeg", upload);

        // One row, and it is the master: capped, lighter, at the upload's own address.
        (await _db.MediaFiles.CountAsync()).ShouldBe(1);
        MediaFile row = await _db.MediaFiles.SingleAsync();
        row.Width.ShouldBe(2560);
        row.Height.ShouldBe(1280);
        row.FileSize.ShouldBeLessThan(upload.Length);
        _blobs.Files[$"uploads/images/{row.FileName}"].Length.ShouldBe((int)row.FileSize);
        stored.FilePath.ShouldBe($"/uploads/images/{row.FileName}");

        // Renditions: WebP + JPEG at 480/640/960/1440/1920, WebP at the master's
        // size — every one of them an actual file in storage, none of them a row.
        IReadOnlyList<ImageRendition> renditions = ImageRenditions.Parse(row.Renditions);
        renditions.Select(r => r.Width).ShouldBe([480, 640, 960, 1440, 1920, 2560]);
        renditions.Take(5).ShouldAllBe(r => r.WebpPath != null && r.FallbackPath != null);
        renditions[5].FallbackPath.ShouldBeNull();
        foreach (ImageRendition r in renditions)
        {
            if (r.WebpPath is not null) _blobs.Files.ShouldContainKey("uploads/images/" + Path.GetFileName(r.WebpPath));
            if (r.FallbackPath is not null) _blobs.Files.ShouldContainKey("uploads/images/" + Path.GetFileName(r.FallbackPath));
        }
        _blobs.Files.Count.ShouldBe(1 + 11);
    }

    [Fact]
    public async Task A_gif_is_stored_as_it_came_with_nothing_derived()
    {
        byte[] gif = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0, 1, 0, 0x80, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0x02, 0x02, 0x44, 0x01, 0, 0x3B];
        await UploadAsync("dot.gif", "image/gif", gif);

        MediaFile row = await _db.MediaFiles.SingleAsync();
        row.Renditions.ShouldBeNull();
        row.FileSize.ShouldBe(gif.Length);
        _blobs.Files.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Deleting_the_master_takes_its_renditions_with_it()
    {
        FileResult stored = await UploadAsync("photo.jpg", "image/jpeg", Jpeg(3000, 1500));
        _blobs.Files.Count.ShouldBe(12);

        Result deleted = await _service.DeleteAsync(stored.Id, deletePhysicalFile: true, CancellationToken.None);

        deleted.IsSuccess.ShouldBeTrue();
        _blobs.Files.ShouldBeEmpty();
        (await _db.MediaFiles.CountAsync()).ShouldBe(0);
    }

    // ── Doubles ───────────────────────────────────────────────────────────────

    private sealed class MemoryBlobStorage : IBlobStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public async Task<Result> SaveAsync(Stream content, string relativePath, string contentType, CancellationToken cancellationToken = default)
        {
            using MemoryStream buffer = new();
            await content.CopyToAsync(buffer, cancellationToken);
            Files[relativePath] = buffer.ToArray();
            return Result.Success();
        }

        public Task<Result> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            Files.Remove(relativePath);
            return Task.FromResult(Result.Success());
        }

        public Task<Result<Stream>> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<Stream>(new MemoryStream(Files[relativePath])));

        public Task<Result<string>> GetPublicUrlAsync(string relativePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success("/" + relativePath));
    }

    private sealed class UploadTestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
