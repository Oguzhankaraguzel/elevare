namespace Domain.Entities.PublicPages;

/// <summary>Read-only projection of the CMS's <c>PageTemplates</c> table.</summary>
public sealed class PublicPageTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public PublicPageTemplateType Type { get; set; }
    public bool IsLinked { get; set; }
    public bool IsDeleted { get; set; }
    public string? GjsHtml { get; set; }
    public string? GjsCss { get; set; }
}
