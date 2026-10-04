using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.IntegrationSecrets;

internal sealed class IntegrationSecretConfiguration : BaseEntityConfiguration<IntegrationSecret>
{
    public override void Configure(EntityTypeBuilder<IntegrationSecret> builder)
    {
        base.Configure(builder);

        builder.ToTable("IntegrationSecrets");

        builder.Property(s => s.Key).HasMaxLength(200).IsRequired();
        builder.Property(s => s.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(500);
        builder.Property(s => s.DataType).HasMaxLength(50);

        builder.HasIndex(s => s.Key)
            .IsUnique()
            .HasDatabaseName("UX_IntegrationSecrets_Key")
            .HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(s => s.Category).HasDatabaseName("IX_IntegrationSecrets_Category");
    }
}
