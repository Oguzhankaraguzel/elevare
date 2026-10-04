using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Languages;

internal sealed class LanguageConfiguration : BaseEntityConfiguration<Language>
{
    public override void Configure(EntityTypeBuilder<Language> builder)
    {
        base.Configure(builder);
        builder.Property(l => l.NameInNative).HasMaxLength(50).IsRequired();
        builder.Property(l => l.NameInEnglish).HasMaxLength(50).IsRequired();
        builder.Property(l => l.TwoLetterCode).HasMaxLength(5).IsRequired();
        builder.Property(l => l.IsDefault).HasDefaultValue(false);

        // Configure the optional relationship to MediaFile for the flag icon
        builder.HasOne(x => x.FlagIconFiles)
               .WithMany()
               .HasForeignKey(x => x.FlagIconFileId);

        // Common indexes for lookup and filtering scenarios
        builder.HasIndex(l => l.TwoLetterCode).HasDatabaseName("IX_Languages_TwoLetterCode");
        builder.HasIndex(l => l.NameInEnglish).HasDatabaseName("IX_Languages_NameInEnglish");
        builder.HasIndex(l => new { l.IsActive, l.IsDeleted }).HasDatabaseName("IX_Languages_IsActive_IsDeleted");

        // Enforce at the database level: among rows where IsActive = true AND IsDeleted = false,
        // only one row may have IsDefault = true.
        // This uses a filtered/partial unique index. The filter string is raw provider SQL
        // (PostgreSQL syntax — double-quoted identifiers, boolean literals, not SQL Server's
        // bracket-quoting and bit 1/0). EF Core includes the filter verbatim in the migration.
        builder.HasIndex(l => l.IsDefault)
               .HasDatabaseName("UX_Languages_Default_ActiveNotDeleted")
               .IsUnique()
               .HasFilter("\"IsDefault\" = true AND \"IsActive\" = true AND \"IsDeleted\" = false");
    }
}
