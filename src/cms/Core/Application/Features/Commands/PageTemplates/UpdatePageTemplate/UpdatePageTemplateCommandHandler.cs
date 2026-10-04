using Application.Features.Commands.Pages;
using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.PageTemplates.Shared;
using Application.Features.Workflows;
using Application.Security;
using Domain.Entities.PageTemplates;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.PageTemplates.UpdatePageTemplate;

internal sealed class UpdatePageTemplateCommandHandler(ICmsApplicationDbContext db, IUserContext userContext)
    : ICommandHandler<UpdatePageTemplateCommand>
{
    public async Task<Result> Handle(UpdatePageTemplateCommand request, CancellationToken cancellationToken)
    {
        if (CustomCodeGuard.ContainsCustomCode(request.GjsHtml) && !userContext.CanAuthorCustomCode)
            return Result.Failure(PageTemplateErrors.CustomCodeNotAllowed);

        PageTemplate? template = await db.PageTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (template is null)
            return Result.Failure(PageTemplateErrors.NotFound);

        if (request.ExpectedFingerprint is not null && !request.Overwrite
            && request.ExpectedFingerprint != PageFingerprint.Of(template))
            return Result.Failure(PageTemplateErrors.EditedElsewhere);

        template.Name = request.Name.Trim();
        template.Type = request.Type;
        template.IsLinked = request.IsLinked;
        template.LanguageId = request.LanguageId;
        // See UpdatePageCommandHandler for why GjsData always updates live.
        template.GjsData = request.GjsData;

        // A linked template propagates to every page that references it the instant it
        // saves — it doesn't wait for anyone to open those pages again. So if no one has
        // separately gated Şablon, a linked template falls back to whatever gates Sayfa,
        // rather than publishing straight past it. An unlinked (copy) template never
        // reaches a live page on its own — inserting it is the page's own save — so it
        // only stages when Şablon itself is explicitly gated.
        bool staged = await ApprovalWorkflowResolution.StageIfWorkflowActiveAsync(
            db, WorkflowContentType.PageTemplate, template.Id, cancellationToken,
            alsoGovernedBy: template.IsLinked ? WorkflowContentType.Page : null);

        if (staged)
        {
            template.PreviewGjsHtml = request.GjsHtml;
            template.PreviewGjsCss = request.GjsCss;
        }
        else
        {
            TemplateOwnContent.Apply(template, request.GjsHtml);
            template.GjsHtml = request.GjsHtml;
            template.GjsCss = request.GjsCss;
            template.PreviewGjsHtml = null;
            template.PreviewGjsCss = null;
        }

        return Result.Success();
    }
}
