using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Tags.GetTags;

public sealed record TagResponse(int Id, string Name, string Slug);
