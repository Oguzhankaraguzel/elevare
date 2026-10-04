using System.ComponentModel.DataAnnotations;
using Domain.Entities.Abstractions;
using Domain.Entities.PageInfos;

namespace Domain.Entities.Tags;

public class Tag : BaseEntity
{
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(120)]
    public string Slug { get; set; } = string.Empty;

    public virtual ICollection<PageInfo>? PageInfos { get; set; }
}
