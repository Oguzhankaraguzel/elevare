using Application.Features.Commands.Forms.RecordFormSubmission;
using Domain.Entities.PublicForms;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Features;

/// <summary>
/// The one place an anonymous visitor writes rows to the CMS's database, so the
/// guards matter more than the happy path: everything reaching this handler came
/// from a form the caller could have edited before submitting.
/// </summary>
public sealed class RecordFormSubmissionCommandHandlerTests
{
    private const int ExistingPageId = 5;

    private readonly DbContextOptions<AnalyticsDbContext> _options =
        new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private readonly DbContextOptions<PublicReadDbContext> _readOptions = TestDbFactory.CreateOptions();

    private AnalyticsDbContext CreateDb() => new(_options);

    /// <summary>A read context holding one page, so the existence check can pass.</summary>
    private PublicReadDbContext CreateReadDb()
    {
        PublicReadDbContext read = TestDbFactory.Create(_readOptions);
        if (!read.PageInfos.Any(p => p.Id == ExistingPageId))
        {
            read.PageInfos.Add(new PublicPage
            {
                Id = ExistingPageId,
                Slug = "iletisim",
                FullSlug = "iletisim",
                LanguageId = 1,
                IsActive = true,
            });
            read.SaveChanges();
        }

        return read;
    }

    private async Task<Result<int>> HandleAsync(AnalyticsDbContext db, RecordFormSubmissionCommand command)
    {
        using PublicReadDbContext read = CreateReadDb();
        return await new RecordFormSubmissionCommandHandler(db, read).Handle(command, CancellationToken.None);
    }

    [Fact]
    public async Task A_valid_submission_is_stored_and_its_id_returned()
    {
        using AnalyticsDbContext db = CreateDb();

        Result<int> result = await HandleAsync(db,
            new RecordFormSubmissionCommand(ExistingPageId, "İletişim Formu", """{"name":"Ayşe"}"""));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeGreaterThan(0);

        using AnalyticsDbContext read = CreateDb();
        PublicFormSubmission stored = await read.FormSubmissions.SingleAsync();
        stored.PageInfoId.ShouldBe(ExistingPageId);
        stored.FormName.ShouldBe("İletişim Formu");
        stored.FieldsJson.ShouldBe("""{"name":"Ayşe"}""");
        stored.SubmittedAtUtc.ShouldNotBe(default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_submission_without_a_real_page_is_rejected(int pageId)
    {
        using AnalyticsDbContext db = CreateDb();

        Result<int> result = await HandleAsync(db,
            new RecordFormSubmissionCommand(pageId, null, """{"a":"b"}"""));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FormSubmission.InvalidPage");
        using AnalyticsDbContext read = CreateDb();
        (await read.FormSubmissions.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task A_submission_for_a_page_that_does_not_exist_is_refused_not_left_to_the_foreign_key()
    {
        // A positive id is not a real one. Left to the database, this reached the INSERT
        // and the FK threw: the endpoint answered 500 with an HTML error page instead of
        // JSON, and any anonymous caller could trigger it at will with a made-up id.
        using AnalyticsDbContext db = CreateDb();

        Result<int> result = await HandleAsync(db,
            new RecordFormSubmissionCommand(999_999, "hayalet", """{"a":"b"}"""));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FormSubmission.InvalidPage");
        using AnalyticsDbContext read = CreateDb();
        (await read.FormSubmissions.CountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_payload_is_rejected(string? fieldsJson)
    {
        using AnalyticsDbContext db = CreateDb();

        Result<int> result = await HandleAsync(db,
            new RecordFormSubmissionCommand(ExistingPageId, null, fieldsJson!));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FormSubmission.EmptyFields");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("  {}  ")]
    [InlineData("[]")]
    public async Task A_submission_carrying_no_fields_at_all_is_rejected(string fieldsJson)
    {
        // The endpoint serialises a missing body to "{}", which is neither null nor
        // whitespace — so the original guard passed it straight through and rows with
        // nothing in them collected in the table.
        using AnalyticsDbContext db = CreateDb();

        Result<int> result = await HandleAsync(db,
            new RecordFormSubmissionCommand(ExistingPageId, "bos", fieldsJson));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FormSubmission.EmptyFields");
        using AnalyticsDbContext read = CreateDb();
        (await read.FormSubmissions.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task An_oversized_payload_is_refused_rather_than_truncated()
    {
        // Truncating would store JSON that no longer parses, turning a spam attempt
        // into a row the CMS cannot display.
        using AnalyticsDbContext db = CreateDb();

        Result<int> result = await HandleAsync(db,
            new RecordFormSubmissionCommand(ExistingPageId, null, new string('x', 20_001)));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FormSubmission.TooLarge");
        using AnalyticsDbContext read = CreateDb();
        (await read.FormSubmissions.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task An_overlong_form_name_is_trimmed_to_the_column_limit()
    {
        using AnalyticsDbContext db = CreateDb();

        await HandleAsync(db,
            new RecordFormSubmissionCommand(ExistingPageId, new string('n', 250), """{"a":"b"}"""));

        using AnalyticsDbContext read = CreateDb();
        (await read.FormSubmissions.SingleAsync()).FormName!.Length.ShouldBe(200);
    }

    [Fact]
    public async Task A_blank_form_name_is_stored_as_null_not_as_empty_text()
    {
        // The CMS list shows "no form name" for null; an empty string would render
        // as a nameless blank cell instead.
        using AnalyticsDbContext db = CreateDb();

        await HandleAsync(db,
            new RecordFormSubmissionCommand(ExistingPageId, "   ", """{"a":"b"}"""));

        using AnalyticsDbContext read = CreateDb();
        (await read.FormSubmissions.SingleAsync()).FormName.ShouldBeNull();
    }

    /// <summary>
    /// Attachments are stored beside the submission, each pointing at it — the
    /// endpoint has already vetted them, the handler only keeps the cap.
    /// </summary>
    [Fact]
    public async Task Attachments_are_stored_with_the_submission()
    {
        using AnalyticsDbContext db = CreateDb();
        byte[] pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 1, 2, 3];

        Result<int> result = await HandleAsync(db, new RecordFormSubmissionCommand(
            ExistingPageId, "Basvuru", """{"name":"Ali","cv":"cv.pdf (1 KB)"}""",
            [new FormAttachmentInput("cv", "cv.pdf", "application/pdf", pdf)]));

        result.IsSuccess.ShouldBeTrue();
        using AnalyticsDbContext read = CreateDb();
        PublicFormSubmissionAttachment stored = await read.FormSubmissionAttachments.SingleAsync();
        stored.FormSubmissionId.ShouldBe(result.Value);
        stored.FileName.ShouldBe("cv.pdf");
        stored.ContentType.ShouldBe("application/pdf");
        stored.Size.ShouldBe(pdf.Length);
        stored.Content.ShouldBe(pdf);
    }

    [Fact]
    public async Task More_attachments_than_the_policy_allows_are_refused()
    {
        using AnalyticsDbContext db = CreateDb();
        FormAttachmentInput one = new("f", "a.pdf", "application/pdf", [1]);

        Result<int> result = await HandleAsync(db, new RecordFormSubmissionCommand(
            ExistingPageId, null, """{"name":"Ali"}""", [one, one, one, one]));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FormSubmission.AttachmentPolicy");
    }
}
