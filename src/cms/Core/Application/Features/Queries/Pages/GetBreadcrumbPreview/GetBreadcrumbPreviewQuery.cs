using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetBreadcrumbPreview;

/// <summary>
/// The trail a "Sayfa Yolu" block will show on this page, for the editor's canvas —
/// which otherwise shows only the block's one template crumb.
/// </summary>
public sealed record GetBreadcrumbPreviewQuery(int PageId) : IQuery<BreadcrumbPreviewResponse>;
