using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.PageTemplates.UpdatePageTemplate;
using Domain.Entities.PageTemplates;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// A linked page template propagates to every page that references it the instant it
/// saves — it doesn't wait for anyone to open those pages again. Gating Sayfa alone
/// left a hole: an editor could move content into a linked template and publish
/// straight past the review the page itself required. These pin the fix — a linked
/// template falls back to whatever gates Sayfa when nothing gates Şablon directly —
/// and that an unlinked (copy) template, which never reaches a live page on its own,
/// is untouched by that fallback.
/// </summary>
public sealed class LinkedTemplateApprovalTests
{
    private static readonly Guid ApproverRoleId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            // ApplicationDbContext wraps SaveChanges in a transaction, which the
            // InMemory provider cannot honour.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new TemplateApprovalTestUserContext());

    private async Task<int> SeedTemplateAsync(bool isLinked)
    {
        using ApplicationDbContext db = CreateDb();
        var template = new PageTemplate
        {
            Name = "Duyuru Şeridi",
            Type = PageTemplateType.Other,
            IsLinked = isLinked,
            GjsHtml = "<div>sürüm 1</div>",
        };
        db.PageTemplates.Add(template);
        await db.SaveChangesAsync();
        return template.Id;
    }

    private async Task SeedActiveWorkflowAsync(WorkflowContentType contentType)
    {
        using ApplicationDbContext db = CreateDb();
        db.WorkflowDefinitions.Add(new WorkflowDefinition
        {
            Name = contentType + " Onayı",
            ContentType = contentType,
            IsActive = true,
            Steps =
            [
                new WorkflowStep { StepOrder = 1, RequiredRoleId = ApproverRoleId },
            ],
        });
        await db.SaveChangesAsync();
    }

    private async Task<Result> UpdateAsync(int templateId, bool isLinked, string html)
    {
        using ApplicationDbContext db = CreateDb();
        Result result = await new UpdatePageTemplateCommandHandler(db, new TemplateApprovalTestUserContext()).Handle(
            new UpdatePageTemplateCommand(templateId, "Duyuru Şeridi", PageTemplateType.Other, isLinked, html, null, null),
            CancellationToken.None);
        // The handler relies on SaveChangesPipelineBehavior to persist — calling it
        // directly, outside MediatR, skips that behavior, so the test does its job.
        await db.SaveChangesAsync(CancellationToken.None);
        return result;
    }

    [Fact]
    public async Task A_linked_template_is_staged_when_only_the_page_workflow_is_active()
    {
        int templateId = await SeedTemplateAsync(isLinked: true);
        await SeedActiveWorkflowAsync(WorkflowContentType.Page);

        Result result = await UpdateAsync(templateId, isLinked: true, html: "<div>sürüm 2</div>");

        result.IsSuccess.ShouldBeTrue();

        using ApplicationDbContext db = CreateDb();
        PageTemplate template = await db.PageTemplates.SingleAsync(t => t.Id == templateId);
        template.GjsHtml.ShouldBe("<div>sürüm 1</div>", "the live content must not change until approved");
        template.PreviewGjsHtml.ShouldBe("<div>sürüm 2</div>");

        ApprovalRequest request = await db.ApprovalRequests.SingleAsync(a => a.ContentId == templateId);
        request.ContentType.ShouldBe(WorkflowContentType.PageTemplate);
        request.Status.ShouldBe(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task An_unlinked_template_is_not_staged_by_the_page_workflow()
    {
        // A copy template never touches a live page on its own — a page picks up its
        // content only at the moment an editor inserts it, and that insertion is the
        // page's own save. Falling back to Sayfa's workflow here would gate every copy
        // template edit for no reason.
        int templateId = await SeedTemplateAsync(isLinked: false);
        await SeedActiveWorkflowAsync(WorkflowContentType.Page);

        Result result = await UpdateAsync(templateId, isLinked: false, html: "<div>sürüm 2</div>");

        result.IsSuccess.ShouldBeTrue();

        using ApplicationDbContext db = CreateDb();
        PageTemplate template = await db.PageTemplates.SingleAsync(t => t.Id == templateId);
        template.GjsHtml.ShouldBe("<div>sürüm 2</div>");
        template.PreviewGjsHtml.ShouldBeNull();
        (await db.ApprovalRequests.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task A_template_specific_workflow_is_used_over_the_page_fallback_when_both_are_active()
    {
        int templateId = await SeedTemplateAsync(isLinked: true);
        await SeedActiveWorkflowAsync(WorkflowContentType.Page);
        await SeedActiveWorkflowAsync(WorkflowContentType.PageTemplate);

        await UpdateAsync(templateId, isLinked: true, html: "<div>sürüm 2</div>");

        using ApplicationDbContext db = CreateDb();
        ApprovalRequest request = await db.ApprovalRequests.SingleAsync(a => a.ContentId == templateId);
        WorkflowDefinition usedWorkflow = await db.WorkflowDefinitions.SingleAsync(w => w.Id == request.WorkflowDefinitionId);
        usedWorkflow.ContentType.ShouldBe(WorkflowContentType.PageTemplate);
    }

    [Fact]
    public async Task A_linked_template_publishes_directly_when_nothing_gates_either_content_type()
    {
        int templateId = await SeedTemplateAsync(isLinked: true);

        Result result = await UpdateAsync(templateId, isLinked: true, html: "<div>sürüm 2</div>");

        result.IsSuccess.ShouldBeTrue();

        using ApplicationDbContext db = CreateDb();
        PageTemplate template = await db.PageTemplates.SingleAsync(t => t.Id == templateId);
        template.GjsHtml.ShouldBe("<div>sürüm 2</div>");
        (await db.ApprovalRequests.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Turning_a_template_linked_in_the_same_save_that_edits_it_is_still_gated()
    {
        // IsLinked is applied to the tracked entity before the staging check runs, so
        // a save that both flips the flag and changes the content must be judged by
        // what it's becoming, not what it was a moment ago.
        int templateId = await SeedTemplateAsync(isLinked: false);
        await SeedActiveWorkflowAsync(WorkflowContentType.Page);

        await UpdateAsync(templateId, isLinked: true, html: "<div>sürüm 2</div>");

        using ApplicationDbContext db = CreateDb();
        PageTemplate template = await db.PageTemplates.SingleAsync(t => t.Id == templateId);
        template.IsLinked.ShouldBeTrue();
        template.GjsHtml.ShouldBe("<div>sürüm 1</div>");
        template.PreviewGjsHtml.ShouldBe("<div>sürüm 2</div>");
    }

    private sealed class TemplateApprovalTestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
