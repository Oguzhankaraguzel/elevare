using Application.Abstraction.Data;
using Domain.Entities.PageTemplates;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.PageTemplates.CreatePageTemplate;

internal sealed class CreatePageTemplateCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<CreatePageTemplateCommand, int>
{
    public async Task<Result<int>> Handle(CreatePageTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = new PageTemplate
        {
            Name = request.Name.Trim(),
            Type = request.Type,
            IsLinked = request.IsLinked,
            LanguageId = request.LanguageId
        };

        db.PageTemplates.Add(template);

        // Save explicitly so the generated identity is available for the response;
        // the SaveChanges pipeline behavior then runs as a harmless no-op.
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(template.Id);
    }
}
