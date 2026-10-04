using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Files;
using Application.Abstraction.Services.Storage;
using Infrastructure.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Storage;

/// <summary>
/// Upload validation, and specifically the hole it exists to close.
/// <para>
/// The content type is reported by the client, and the stored file keeps the
/// client's extension. <c>MediaEndpoints</c> then decides the response's
/// Content-Type from that extension — so a file named <c>evil.html</c> declared as
/// <c>image/png</c> would pass a MIME-only check and come back as <c>text/html</c>
/// from the CMS's own origin. That is stored XSS, and <c>nosniff</c> does not help
/// because the type really is text/html by then.
/// </para>
/// </summary>
public sealed class FileUploadValidationTests : IDisposable
{
    // Every context the tests create, disposed together at the end. The alternative
    // is a using per test, which would make each one read as plumbing first.
    private readonly List<ApplicationDbContext> _contexts = [];

    public void Dispose()
    {
        foreach (ApplicationDbContext context in _contexts)
            context.Dispose();
    }

    private FileService CreateService()
    {
        DbContextOptions<ApplicationDbContext> dbOptions =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

        FileServiceOptions options = new()
        {
            UploadFolder = "uploads",
            MaxFileSizeBytes = 52_428_800,
            AllowedMimeTypes = ["image/jpeg", "image/png", "image/gif", "image/webp", "application/pdf"],
        };

        ApplicationDbContext db = new(dbOptions, new UploadTestUserContext());
        _contexts.Add(db);

        // Storage is never reached: validation runs before anything is written, and
        // a stub that throws proves it.
        return new FileService(
            Options.Create(options),
            db,
            new ThrowingBlobStorage(),
            NullLogger<FileService>.Instance);
    }

    private static FileUploadRequest Request(string fileName, string contentType, int size = 1024) => new()
    {
        FileName = fileName,
        ContentType = contentType,
        Content = new MemoryStream(new byte[size]),
        FileSize = size,
    };

    [Fact]
    public async Task An_html_file_declared_as_an_image_is_refused()
    {
        // The attack this rule exists for.
        Result<FileResult> result = await CreateService()
            .UploadAsync(Request("evil.html", "image/png"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("File.ExtensionMismatch");
    }

    [Theory]
    [InlineData("payload.svg", "image/png")]
    [InlineData("payload.xhtml", "image/jpeg")]
    [InlineData("payload.js", "image/gif")]
    public async Task Any_extension_that_disagrees_with_the_declared_type_is_refused(
        string fileName, string contentType)
    {
        Result<FileResult> result = await CreateService()
            .UploadAsync(Request(fileName, contentType), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("File.ExtensionMismatch");
    }

    [Fact]
    public async Task A_file_with_no_extension_at_all_is_refused()
    {
        // Nothing maps it, so nothing can promise how it would later be served.
        Result<FileResult> result = await CreateService()
            .UploadAsync(Request("noextension", "image/png"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("File.ExtensionMismatch");
    }

    [Fact]
    public async Task A_disallowed_type_is_still_refused_before_the_extension_check()
    {
        // Consistent name and type, but not a type this site accepts.
        Result<FileResult> result = await CreateService()
            .UploadAsync(Request("document.html", "text/html"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("File.MimeTypeNotAllowed");
    }

    [Theory]
    [InlineData("photo.png", "image/png")]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("brochure.pdf", "application/pdf")]
    public async Task A_matching_name_and_type_passes_validation(string fileName, string contentType)
    {
        // Getting past validation is what is being asserted, not a successful upload:
        // the storage stub throws, and UploadAsync turns that into File.UploadFailed.
        // Checking "not a validation refusal" rather than that exact code keeps the
        // test about the rule instead of about how storage failures are wrapped.
        Result<FileResult> result = await CreateService()
            .UploadAsync(Request(fileName, contentType), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldNotBe("File.ExtensionMismatch");
        result.Error.Code.ShouldNotBe("File.MimeTypeNotAllowed");
    }

    private sealed class ThrowingBlobStorage : IBlobStorage
    {
        public Task<Result> SaveAsync(Stream content, string relativePath, string contentType, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("validation passed");

        public Task<Result> DeleteAsync(string relativePath, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("validation passed");

        public Task<Result<Stream>> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("validation passed");

        public Task<Result<string>> GetPublicUrlAsync(string relativePath, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("validation passed");
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
