using Application.Abstraction.Data;
using Application.Abstraction.Services.Files;
using Application.Abstraction.Services.Storage;
using Domain.Entities.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;
using SkiaSharp;

namespace Infrastructure.Files;

/// <summary>
/// See <see cref="IFaviconService"/>. The source is located through the media
/// library (the setting holds the file's public URL; the library knows its storage
/// key), decoded once, and written as <c>uploads/favicons/favicon-{size}.png</c> —
/// fixed names, so the public layout can link them without asking anything.
/// </summary>
internal sealed class FaviconService(ICmsApplicationDbContext db, IBlobStorage blobStorage, ILogger<FaviconService> logger) : IFaviconService
{
    public const string Folder = "uploads/favicons";

    public async Task<Result<bool>> RegenerateAsync(string? sourcePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) return Result.Success(false);

        // The setting holds whatever was pasted — on the live site an absolute URL
        // on the public host — while the library stores the site-relative path.
        // Cut at the upload marker, the same way the public site matches images.
        int marker = sourcePath.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
        string libraryPath = marker >= 0 ? sourcePath[marker..] : sourcePath;
        int cut = libraryPath.IndexOfAny(['?', '#']);
        if (cut >= 0) libraryPath = libraryPath[..cut];

        MediaFile? file = await db.MediaFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.FilePath == libraryPath && !m.IsDeleted, cancellationToken);
        if (file is null || file.MediaType != MediaType.Image) return Result.Success(false);

        Result<Stream> opened = await blobStorage.OpenReadAsync($"{file.FolderPath}/{file.FileName}", cancellationToken);
        if (opened.IsFailure) return Result.Failure<bool>(opened.Error);

        byte[] bytes;
        using (Stream stream = opened.Value)
        using (MemoryStream buffer = new())
        {
            await stream.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
        }

        // .ico is a container the codec may or may not read; SVG has no pixels. Either
        // way "cannot decode" means "keep the source", not an error.
        using var codec = SKCodec.Create(new MemoryStream(bytes, writable: false), out SKCodecResult probe);
        if (probe != SKCodecResult.Success) return Result.Success(false);
        using var source = SKBitmap.Decode(codec);
        if (source is null || source.Width <= 0) return Result.Success(false);

        foreach (int size in IFaviconService.Sizes)
        {
            using SKBitmap square = SquareFit(source, size);
            using var image = SKImage.FromBitmap(square);
            using SKData png = image.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("Favicon could not be encoded.");
            using MemoryStream stream = new(png.ToArray(), writable: false);
            Result saved = await blobStorage.SaveAsync(stream, $"{Folder}/favicon-{size}.png", "image/png", cancellationToken);
            if (saved.IsFailure) return Result.Failure<bool>(saved.Error);
        }

        logger.LogInformation("[Favicon] Sized set regenerated from '{Source}'.", sourcePath);
        return Result.Success(true);
    }

    // Centred on a transparent square, scaled to fit — a wide logo is not stretched
    // into a square, it sits in one.
    private static SKBitmap SquareFit(SKBitmap source, int size)
    {
        var target = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.Transparent);
        float scale = Math.Min(size / (float)source.Width, size / (float)source.Height);
        float w = source.Width * scale;
        float h = source.Height * scale;
        var dest = new SKRect((size - w) / 2f, (size - h) / 2f, (size + w) / 2f, (size + h) / 2f);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, dest, new SKSamplingOptions(SKCubicResampler.Mitchell));
        return target;
    }
}
