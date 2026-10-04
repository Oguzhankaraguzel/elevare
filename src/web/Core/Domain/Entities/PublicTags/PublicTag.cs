namespace Domain.Entities.PublicTags;

/// <summary>Read-only projection of the CMS's <c>Tags</c> table.</summary>
public sealed class PublicTag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsDeleted { get; set; }
}
