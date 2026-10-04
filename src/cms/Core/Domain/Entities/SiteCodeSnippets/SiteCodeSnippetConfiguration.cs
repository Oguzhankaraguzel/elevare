using Domain.Entities.Abstractions;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.SiteCodeSnippets;

internal sealed class SiteCodeSnippetConfiguration : BaseEntityConfiguration<SiteCodeSnippet>
{
    public override void Configure(EntityTypeBuilder<SiteCodeSnippet> builder)
    {
        base.Configure(builder);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Notes).HasMaxLength(1000);

        // Every public page render reads this set filtered and sorted exactly this
        // way (see the Web layout), so the index matches the read rather than the row.
        builder.HasIndex(s => new { s.Placement, s.SortOrder })
            .HasDatabaseName("IX_SiteCodeSnippets_Placement_SortOrder");

        // Same shape as Tags ↔ PageInfos (TagConfiguration): a join table with
        // cascade on both sides, so purging a snippet from the Trash, or a page,
        // takes its exclusions with it.
        builder.HasMany(s => s.ExcludedOnPages)
            .WithMany(p => p.ExcludedSiteCodeSnippets)
            .UsingEntity<PageInfoSiteCodeExclusion>(
                j => j.HasOne<PageInfo>().WithMany().HasForeignKey(x => x.PageInfoId).OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<SiteCodeSnippet>().WithMany().HasForeignKey(x => x.SiteCodeSnippetId).OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.HasKey(x => new { x.PageInfoId, x.SiteCodeSnippetId });
                    j.ToTable("PageInfoSiteCodeExclusions");
                });
    }
}
