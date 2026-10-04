using Application.Abstraction.Services.Authentication;
using Domain.Entities.Users;
using Application.Features.Commands.Workflows.DecideApproval;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// An <see cref="ApprovalRequest"/> can go stale: deactivate the workflow that
/// gated it (or just let an editor's next save land after nothing gates the
/// content anymore) and that next save publishes straight to live, clearing
/// <c>PreviewGjsHtml</c>/<c>PreviewGjsCss</c> to null in the process — but nothing
/// resolves the original request, so it sits there Pending, wired to content that
/// has already moved on. Approving it used to copy that null straight over the
/// live <c>GjsHtml</c>/<c>GjsCss</c>, blanking a page a separate, more recent save
/// had already published. Found by deactivating a leftover test workflow mid-session
/// and then approving its now-stale backlog — nine published pages went blank.
/// </summary>
public sealed class StaleApprovalRequestTests
{
    private static readonly Guid ApproverRoleId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            // ApplicationDbContext wraps SaveChanges in a transaction, which the
            // InMemory provider cannot honour.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new StaleApprovalTestUserContext());

    /// <summary>
    /// A page whose ApprovalRequest is stale: staged content once sat in Preview*,
    /// but a later direct publish (workflow off) already promoted a DIFFERENT,
    /// newer version straight to GjsHtml/GjsCss and cleared Preview* — exactly
    /// what UpdatePageCommandHandler does on every non-staged save.
    /// </summary>
    private async Task<(int PageId, int ApprovalRequestId)> SeedStalePendingRequestAsync()
    {
        using ApplicationDbContext db = CreateDb();

        db.Roles.Add(new AppRole { Id = ApproverRoleId, Name = "SuperAdmin", NormalizedName = "SUPERADMIN" });
        await db.SaveChangesAsync();

        var page = new PageInfo
        {
            Slug = "test-sayfa",
            PageStatus = PageStatus.Published,
            SeoMeta = new SeoMeta { Title = "Test Sayfa" },
            Content = new PageContent
            {
                GjsHtml = "<h1>Canlı, onaydan sonra da kaybolmamalı</h1>",
                GjsCss = "h1{color:red}",
                PreviewGjsHtml = null,
                PreviewGjsCss = null,
            },
        };
        db.PageInfos.Add(page);
        await db.SaveChangesAsync();

        var workflow = new WorkflowDefinition
        {
            Name = "Sayfa Yayın Onayı",
            ContentType = WorkflowContentType.Page,
            IsActive = false, // deactivated — matches the real sequence
            Steps = [new WorkflowStep { StepOrder = 1, RequiredRoleId = ApproverRoleId }],
        };
        db.WorkflowDefinitions.Add(workflow);
        await db.SaveChangesAsync();

        var request = new ApprovalRequest
        {
            ContentType = WorkflowContentType.Page,
            ContentId = page.Id,
            WorkflowDefinitionId = workflow.Id,
            CurrentStepOrder = 1,
            Status = ApprovalStatus.Pending,
        };
        db.ApprovalRequests.Add(request);
        await db.SaveChangesAsync();

        return (page.Id, request.Id);
    }

    [Fact]
    public async Task Approving_a_stale_request_does_not_blank_content_a_later_publish_already_promoted()
    {
        (int pageId, int approvalRequestId) = await SeedStalePendingRequestAsync();

        using ApplicationDbContext db = CreateDb();
        Result result = await new DecideApprovalCommandHandler(db, new StaleApprovalTestUserContext()).Handle(
            new DecideApprovalCommand(approvalRequestId, ApprovalDecisionType.Approved, Comment: null),
            CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        using ApplicationDbContext read = CreateDb();
        PageContent content = (await read.PageInfos.Include(p => p.Content).SingleAsync(p => p.Id == pageId)).Content!;
        content.GjsHtml.ShouldBe("<h1>Canlı, onaydan sonra da kaybolmamalı</h1>");
        content.GjsCss.ShouldBe("h1{color:red}");
    }

    [Fact]
    public async Task Approving_a_request_with_genuinely_staged_content_still_promotes_it()
    {
        // The guard must not turn every approval into a no-op — only one with
        // nothing left to promote.
        using ApplicationDbContext db = CreateDb();

        db.Roles.Add(new AppRole { Id = ApproverRoleId, Name = "SuperAdmin", NormalizedName = "SUPERADMIN" });
        await db.SaveChangesAsync();

        var page = new PageInfo
        {
            Slug = "test-sayfa-2",
            PageStatus = PageStatus.Draft,
            PendingStatus = PageStatus.Published,
            SeoMeta = new SeoMeta { Title = "Test Sayfa 2" },
            Content = new PageContent
            {
                GjsHtml = "<h1>Eski sürüm</h1>",
                PreviewGjsHtml = "<h1>Onaylanacak yeni sürüm</h1>",
                PreviewGjsCss = "h1{color:blue}",
            },
        };
        db.PageInfos.Add(page);
        await db.SaveChangesAsync();

        var workflow = new WorkflowDefinition
        {
            Name = "Sayfa Yayın Onayı",
            ContentType = WorkflowContentType.Page,
            IsActive = true,
            Steps = [new WorkflowStep { StepOrder = 1, RequiredRoleId = ApproverRoleId }],
        };
        db.WorkflowDefinitions.Add(workflow);
        await db.SaveChangesAsync();

        var request = new ApprovalRequest
        {
            ContentType = WorkflowContentType.Page,
            ContentId = page.Id,
            WorkflowDefinitionId = workflow.Id,
            CurrentStepOrder = 1,
            Status = ApprovalStatus.Pending,
        };
        db.ApprovalRequests.Add(request);
        await db.SaveChangesAsync();

        using ApplicationDbContext decideDb = CreateDb();
        Result result = await new DecideApprovalCommandHandler(decideDb, new StaleApprovalTestUserContext()).Handle(
            new DecideApprovalCommand(request.Id, ApprovalDecisionType.Approved, Comment: null),
            CancellationToken.None);
        await decideDb.SaveChangesAsync(CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        using ApplicationDbContext read = CreateDb();
        PageInfo saved = await read.PageInfos.Include(p => p.Content).SingleAsync(p => p.Id == page.Id);
        saved.Content!.GjsHtml.ShouldBe("<h1>Onaylanacak yeni sürüm</h1>");
        saved.Content.GjsCss.ShouldBe("h1{color:blue}");
        saved.Content.PreviewGjsHtml.ShouldBeNull();
        saved.PageStatus.ShouldBe(PageStatus.Published);
        saved.PendingStatus.ShouldBeNull();
    }

    private sealed class StaleApprovalTestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
