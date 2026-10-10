namespace Domain.Entities.PublicRedirects;

/// <summary>Read-only projection of the CMS's <c>Redirects</c> table.</summary>
public sealed class PublicRedirect
{
    public int Id { get; set; }
    public string OldPath { get; set; } = null!;
    public string? NewPath { get; set; }
    public int? SourcePageId { get; set; }

    /// <summary>302 rather than 301 — see the CMS's <c>Redirect.IsTemporary</c>.</summary>
    public bool IsTemporary { get; set; }
    public bool IsDeleted { get; set; }
}
