using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetPreviewLink;

/// <summary>Builds a signed, ~15-minute preview link for the given page — see
/// <c>IPreviewLinkSigner</c> and the Web app's <c>PageController.Preview</c>.
/// <paramref name="AnalysisOrigin"/>, when given, is the CMS origin the SEO analyzer
/// frames the preview from (see <c>PreviewAnalysis</c>).</summary>
public sealed record GetPreviewLinkQuery(int PageId, string? AnalysisOrigin = null) : IQuery<string>;
