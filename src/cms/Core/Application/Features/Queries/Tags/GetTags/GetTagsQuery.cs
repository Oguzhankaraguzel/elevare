using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Tags.GetTags;

/// <summary>All tags, for the page editor's tag picker.</summary>
public sealed record GetTagsQuery : IQuery<List<TagResponse>>;
