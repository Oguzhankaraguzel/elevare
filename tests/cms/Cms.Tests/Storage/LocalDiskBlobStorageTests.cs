using Application.Abstraction.Data;
using Domain.Entities.SiteSettings;
using Domain.Entities.Storage;
using Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Storage;

/// <summary>
/// Covers <see cref="LocalDiskBlobStorage"/>, the default media store, against a real
/// temporary directory. Two behaviours matter beyond the plain round-trip: a missing
/// object must be reported as NotFound rather than as a storage fault (the caller
/// answers 404 on that distinction), and the CDN prefix must be read fresh on every
/// call so switching CDNs on does not need an app restart.
/// </summary>
public sealed class LocalDiskBlobStorageTests : IDisposable
{
    private const string CdnSettingKey = "Integrations.CdnBaseUrl";
    private const string RelativePath = "images/2026/07/photo.jpg";

    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "elevare-blob-tests", Guid.NewGuid().ToString("N"));
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new BlobTestUserContext());

    private LocalDiskBlobStorage CreateStorage(ICmsApplicationDbContext db)
    {
        Directory.CreateDirectory(_webRoot);
        return new LocalDiskBlobStorage(new StubWebHostEnvironment(_webRoot), db);
    }

    private void SeedCdnBaseUrl(string? value)
    {
        using ApplicationDbContext db = CreateDb();
        db.SiteSettings.Add(new SiteSetting
        {
            Id = 1,
            Key = CdnSettingKey,
            Value = value,
            DisplayName = "CDN",
            Group = SiteSettingGroup.Integrations,
            DataType = "url",
        });
        db.SaveChanges();
    }

    private static MemoryStream Bytes(string content) =>
        new(System.Text.Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Saved_bytes_can_be_read_back()
    {
        using ApplicationDbContext db = CreateDb();
        LocalDiskBlobStorage storage = CreateStorage(db);
        using MemoryStream content = Bytes("hello");

        Result saved = await storage.SaveAsync(content, RelativePath, "image/jpeg", CancellationToken.None);
        Result<Stream> opened = await storage.OpenReadAsync(RelativePath, CancellationToken.None);

        saved.IsSuccess.ShouldBeTrue();
        opened.IsSuccess.ShouldBeTrue();

        using StreamReader reader = new(opened.Value);
        (await reader.ReadToEndAsync(CancellationToken.None)).ShouldBe("hello");
    }

    [Fact]
    public async Task Save_creates_the_nested_folders_it_needs()
    {
        using ApplicationDbContext db = CreateDb();
        LocalDiskBlobStorage storage = CreateStorage(db);
        using MemoryStream content = Bytes("x");

        await storage.SaveAsync(content, "documents/2030/01/deep/file.pdf", "application/pdf", CancellationToken.None);

        File.Exists(Path.Combine(_webRoot, "documents", "2030", "01", "deep", "file.pdf")).ShouldBeTrue();
    }

    [Fact]
    public async Task Reading_a_missing_object_reports_NotFound_not_a_storage_fault()
    {
        // The caller turns this specific error into a 404; a generic read failure
        // would instead read as "storage is broken" and mask a simple missing file.
        using ApplicationDbContext db = CreateDb();
        LocalDiskBlobStorage storage = CreateStorage(db);

        Result<Stream> opened = await storage.OpenReadAsync("images/nope.jpg", CancellationToken.None);

        opened.IsFailure.ShouldBeTrue();
        opened.Error.Code.ShouldBe(BlobStorageErrors.NotFoundCode);
    }

    [Fact]
    public async Task Deleting_removes_the_file()
    {
        using ApplicationDbContext db = CreateDb();
        LocalDiskBlobStorage storage = CreateStorage(db);
        using MemoryStream content = Bytes("x");
        await storage.SaveAsync(content, RelativePath, "image/jpeg", CancellationToken.None);

        Result deleted = await storage.DeleteAsync(RelativePath, CancellationToken.None);

        deleted.IsSuccess.ShouldBeTrue();
        File.Exists(Path.Combine(_webRoot, "images", "2026", "07", "photo.jpg")).ShouldBeFalse();
    }

    [Fact]
    public async Task Deleting_something_that_is_already_gone_succeeds()
    {
        // The caller's goal is "these bytes are gone", and they are. Failing here
        // would make a retried delete look like a new problem.
        using ApplicationDbContext db = CreateDb();
        LocalDiskBlobStorage storage = CreateStorage(db);

        Result deleted = await storage.DeleteAsync("images/never-existed.jpg", CancellationToken.None);

        deleted.IsSuccess.ShouldBeTrue();
    }

    // CA1054 asks for Uri-typed parameters, but these are raw SETTING VALUES —
    // including the invalid/blank ones the test exists to cover — so string is the
    // type under test.
#pragma warning disable CA1054
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Public_url_is_site_relative_when_no_CDN_is_configured(string? cdnBaseUrl)
    {
        // Relative on purpose: an absolute URL would pin media to one host and break
        // the moment the site is served from dev/staging or a new domain.
        SeedCdnBaseUrl(cdnBaseUrl);
        using ApplicationDbContext db = CreateDb();
        LocalDiskBlobStorage storage = CreateStorage(db);

        Result<string> url = await storage.GetPublicUrlAsync(RelativePath, CancellationToken.None);

        url.Value.ShouldBe("/" + RelativePath);
    }

    [Theory]
    [InlineData("https://cdn.example.com")]
    [InlineData("https://cdn.example.com/")]
    public async Task Public_url_is_prefixed_when_a_CDN_is_configured(string cdnBaseUrl)
    {
        // A trailing slash in the setting must not produce a double slash in the URL.
        SeedCdnBaseUrl(cdnBaseUrl);
        using ApplicationDbContext db = CreateDb();
        LocalDiskBlobStorage storage = CreateStorage(db);

        Result<string> url = await storage.GetPublicUrlAsync(RelativePath, CancellationToken.None);

        url.Value.ShouldBe("https://cdn.example.com/" + RelativePath);
    }
#pragma warning restore CA1054

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }

    /// <summary>Minimal host environment — the storage only ever reads WebRootPath.</summary>
    private sealed class StubWebHostEnvironment(string webRootPath) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = webRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = webRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string EnvironmentName { get; set; } = "Test";
    }

    private sealed class BlobTestUserContext : Application.Abstraction.Services.Authentication.IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
