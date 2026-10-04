using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetAdjacentPreview;

/// <summary>
/// The pages an "Önceki / Sonraki Yazı" block will link on this page, for the
/// editor's canvas — which otherwise shows the block's sample titles.
/// </summary>
public sealed record GetAdjacentPreviewQuery(int PageId) : IQuery<AdjacentPreviewResponse>;
