namespace Domain.Entities.PublicPages;

/// <summary>Read-only projection of the CMS's <c>PageContents</c> table.</summary>
public sealed class PublicPageContent
{
    public int Id { get; set; }
    public int PageInfoId { get; set; }
    public bool IsDeleted { get; set; }
    public string? GjsHtml { get; set; }
    public string? GjsCss { get; set; }
    public string? PreviewGjsHtml { get; set; }
    public string? PreviewGjsCss { get; set; }
}
