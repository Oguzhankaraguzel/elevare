using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.PageContents;

internal sealed class PageContentConfiguration : BaseEntityConfiguration<PageContent>
{
    public override void Configure(EntityTypeBuilder<PageContent> builder)
    {
        base.Configure(builder);

        // 1:1 with PageInfo.
        builder.HasOne(c => c.PageInfo)
            .WithOne(p => p.Content)
            .HasForeignKey<PageContent>(c => c.PageInfoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.PageInfoId).IsUnique().HasDatabaseName("UX_PageContents_PageInfoId");
    }
}
