using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.SiteSettings;

internal sealed class SiteSettingConfiguration : BaseEntityConfiguration<SiteSetting>
{
    public override void Configure(EntityTypeBuilder<SiteSetting> builder)
    {
        base.Configure(builder);

        builder.ToTable("SiteSettings");

        builder.Property(s => s.Key).HasMaxLength(200).IsRequired();
        builder.Property(s => s.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(500);
        // Value has no max-length; can hold large JSON blobs
        builder.Property(s => s.DataType).HasMaxLength(50);

        // Key must be unique within a group — filtered to live rows only, same
        // reasoning as UX_PageInfos_Slug_Language, so a soft-deleted setting doesn't
        // block a restore or a new setting from reusing its Key+Group.
        builder.HasIndex(s => new { s.Key, s.Group })
            .IsUnique()
            .HasDatabaseName("UX_SiteSettings_Key_Group")
            .HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(s => s.Group).HasDatabaseName("IX_SiteSettings_Group");
        builder.HasIndex(s => s.IsSystem).HasDatabaseName("IX_SiteSettings_IsSystem");
    }
}
