using Application.Abstraction.Security;
using Application.Features.Queries.Tags.GetTags;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Tags.CreateTag;

/// <summary>Creates a new tag, or — if a tag with the same slug already exists —
/// returns the existing one, so the page editor's inline "create tag" action never
/// races into a duplicate-slug conflict against a concurrently-created tag.</summary>
public sealed record CreateTagCommand(string Name) : ICommand<TagResponse>, IRequirePermission
{
    // Same gate as UpdateTag/DeleteTag: a tag is page metadata, created inline while
    // editing a page, so whoever may edit pages is exactly who may mint a tag. Closes
    // the gap where any signed-in user could create tags.
    public static string RequiredPermission => PermissionKeys.PagesEdit;
}
