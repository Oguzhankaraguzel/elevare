namespace Domain.Entities.Tags;

/// <summary>Plain many-to-many join row (no audit/soft-delete concerns of its own — a
/// tag association is added/removed outright, never "restored" independently of the
/// page or tag it links).</summary>
public sealed class PageInfoTag
{
    public int PageInfoId { get; set; }
    public int TagId { get; set; }
}
