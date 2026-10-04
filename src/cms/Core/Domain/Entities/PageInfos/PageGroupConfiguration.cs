using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.PageInfos;

internal sealed class PageGroupConfiguration : IEntityTypeConfiguration<PageGroup>
{
    public void Configure(EntityTypeBuilder<PageGroup> builder)
    {
        builder.ToTable("PageGroups");
        builder.HasKey(pg => pg.Id);

        builder.Property(pg => pg.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(pg => pg.Name).HasDatabaseName("IX_PageGroups_Name");

        builder.HasMany(pg => pg.Pages)
            .WithOne(p => p.PageGroup)
            .HasForeignKey(p => p.PageGroupId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
