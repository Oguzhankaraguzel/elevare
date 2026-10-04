using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Application.Abstraction.Services.Storage;
using Domain.Entities.Storage;
using Microsoft.Extensions.Options;
using SharedKernel.Concrete;

namespace Infrastructure.Storage;

/// <summary>
/// S3(-compatible) <see cref="IBlobStorage"/> — used when
/// <c>ObjectStorage:Provider</c> is "S3", resolved fresh per call by
/// <see cref="IBlobStorageFactory"/>. Works against real AWS S3 or any
/// S3-compatible endpoint (MinIO, DigitalOcean Spaces, Cloudflare R2, ...) via
/// <see cref="ObjectStorageOptions.ServiceUrl"/>.
/// <para>
/// Deliberately thin: the actual <see cref="AmazonS3Client"/> lives in
/// <see cref="IS3ClientProvider"/> (a true singleton, rebuilt only when
/// credentials change), and bucket/region/URL fields are read fresh from
/// <see cref="IOptionsMonitor{TOptions}"/> on every call — this class itself is
/// cheap to construct, holds nothing that needs disposing, and is resolved anew
/// per scope, so a Sırlar save is visible on the very next call with no restart.
/// </para>
/// </summary>
internal sealed class S3BlobStorage(IS3ClientProvider clientProvider, IOptionsMonitor<ObjectStorageOptions> optionsMonitor) : IBlobStorage
{
    public async Task<Result> SaveAsync(
        Stream content, string relativePath, string contentType, CancellationToken cancellationToken = default)
    {
        ObjectStorageOptions options = optionsMonitor.CurrentValue;
        try
        {
            PutObjectRequest request = new()
            {
                BucketName = options.BucketName,
                Key = relativePath,
                InputStream = content,
                ContentType = contentType,
            };
            await clientProvider.Current.PutObjectAsync(request, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex) when (IsStorageFault(ex))
        {
            return Result.Failure(BlobStorageErrors.SaveFailed(Describe(ex)));
        }
    }

    public async Task<Result> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            await clientProvider.Current.DeleteObjectAsync(optionsMonitor.CurrentValue.BucketName, relativePath, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (IsStorageFault(ex))
        {
            return Result.Failure(BlobStorageErrors.DeleteFailed(Describe(ex)));
        }
    }

    public async Task<Result<Stream>> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            GetObjectResponse response = await clientProvider.Current.GetObjectAsync(
                optionsMonitor.CurrentValue.BucketName, relativePath, cancellationToken);
            return Result.Success(response.ResponseStream);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Result.Failure<Stream>(BlobStorageErrors.NotFound(relativePath));
        }
        catch (Exception ex) when (IsStorageFault(ex))
        {
            return Result.Failure<Stream>(BlobStorageErrors.ReadFailed(Describe(ex)));
        }
    }

    public Task<Result<string>> GetPublicUrlAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        // Pure string composition against configuration — no request is made, so
        // there is nothing here that can fail at runtime.
        ObjectStorageOptions options = optionsMonitor.CurrentValue;

        if (!string.IsNullOrWhiteSpace(options.PublicBaseUrl))
            return Task.FromResult(Result.Success($"{options.PublicBaseUrl.TrimEnd('/')}/{relativePath}"));

        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
            return Task.FromResult(Result.Success($"{options.ServiceUrl.TrimEnd('/')}/{options.BucketName}/{relativePath}"));

        return Task.FromResult(Result.Success($"https://{options.BucketName}.s3.{options.Region}.amazonaws.com/{relativePath}"));
    }

    /// <summary>
    /// Faults that mean "the object store said no": protocol/permission errors from
    /// the SDK plus the transport errors underneath it.
    /// </summary>
    private static bool IsStorageFault(Exception ex) =>
        ex is AmazonS3Exception or AmazonServiceException or HttpRequestException or TaskCanceledException or IOException;

    /// <summary>
    /// S3's HTTP status and error code are the useful part of a failure — "AccessDenied"
    /// versus "NoSuchBucket" versus a 503 are three completely different fixes — so they
    /// are included alongside the message.
    /// </summary>
    private static string Describe(Exception ex) =>
        ex is AmazonS3Exception s3
            ? $"{(int)s3.StatusCode} {s3.ErrorCode}: {s3.Message}"
            : ex.Message;
}
