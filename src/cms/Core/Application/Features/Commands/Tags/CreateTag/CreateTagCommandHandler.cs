using Application.Abstraction.Data;
using Application.Features.Queries.Tags.GetTags;
using Domain.Entities.Tags;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Strings;

namespace Application.Features.Commands.Tags.CreateTag;

internal sealed class CreateTagCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<CreateTagCommand, TagResponse>
{
    public async Task<Result<TagResponse>> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        string name = request.Name.Trim();
        string slug = name.ToSlug();
        if (name.IsNullOrWhiteSpace() || slug.IsNullOrWhiteSpace())
            return Result.Failure<TagResponse>(TagErrors.InvalidName);

        Tag? existing = await db.Tags.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
        if (existing is not null)
            return Result.Success(new TagResponse(existing.Id, existing.Name, existing.Slug));

        var tag = new Tag { Name = name, Slug = slug };
        db.Tags.Add(tag);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new TagResponse(tag.Id, tag.Name, tag.Slug));
    }
}
