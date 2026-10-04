using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Application.Security;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Pages.SavePreviewContent;

internal sealed class SavePreviewContentCommandHandler(ICmsApplicationDbContext db, IUserContext userContext)
    : ICommandHandler<SavePreviewContentCommand>
{
    public async Task<Result> Handle(SavePreviewContentCommand request, CancellationToken cancellationToken)
    {
        if (CustomCodeGuard.ContainsCustomCode(request.GjsHtml) && !userContext.CanAuthorCustomCode)
            return Result.Failure(PageInfoErrors.CustomCodeNotAllowed);

        PageInfo? page = await db.PageInfos
            .Include(p => p.Content)
            .FirstOrDefaultAsync(p => p.Id == request.PageId, cancellationToken);

        if (page is null)
            return Result.Failure(PageInfoErrors.NotFound);

        page.Content ??= new PageContent { PageInfoId = page.Id };
        page.Content.PreviewGjsHtml = request.GjsHtml;
        page.Content.PreviewGjsCss = request.GjsCss;

        return Result.Success();
    }
}
