using Application.Abstraction.Data;
using Application.Features.Queries.Tags.GetTags;
using Domain.Entities.Tags;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Strings;

namespace Application.Features.Commands.Tags.UpdateTag;

internal sealed class UpdateTagCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<UpdateTagCommand, TagResponse>
{
    public async Task<Result<TagResponse>> Handle(UpdateTagCommand request, CancellationToken cancellationToken)
    {
        string name = request.Name.Trim();
        string slug = name.ToSlug();
        if (name.IsNullOrWhiteSpace() || slug.IsNullOrWhiteSpace())
            return Result.Failure<TagResponse>(TagErrors.InvalidName);

        Tag? tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (tag is null)
            return Result.Failure<TagResponse>(TagErrors.NotFound);

        // Compared on the slug, not the name: "Web Tasarım" and "web tasarim" are the
        // same tag as far as every URL and lookup is concerned.
        bool taken = await db.Tags.AnyAsync(t => t.Id != tag.Id && t.Slug == slug, cancellationToken);
        if (taken)
            return Result.Failure<TagResponse>(TagErrors.NameAlreadyExists);

        // The pages keep their association: the join row references the id, which is
        // not changing. Renaming a tag renames it everywhere, which is the point.
        tag.Name = name;
        tag.Slug = slug;

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new TagResponse(tag.Id, tag.Name, tag.Slug));
    }
}
