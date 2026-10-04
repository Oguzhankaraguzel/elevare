using Domain.Entities.Abstractions;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Tags;

internal sealed class TagConfiguration : BaseEntityConfiguration<Tag>
{
    public override void Configure(EntityTypeBuilder<Tag> builder)
    {
        base.Configure(builder);

        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(120).IsRequired();

        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName("UX_Tags_Slug")
            .HasFilter("\"IsDeleted\" = false");

        builder.HasMany(t => t.PageInfos)
            .WithMany(p => p.Tags)
            .UsingEntity<PageInfoTag>(
                j => j.HasOne<PageInfo>().WithMany().HasForeignKey(x => x.PageInfoId).OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<Tag>().WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.HasKey(x => new { x.PageInfoId, x.TagId });
                    j.ToTable("PageInfoTags");
                });
    }
}
