using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Abstractions;

internal abstract class BaseEntityConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(e => e.Id);

        // IsActive isn't part of IAuditable/ISoftDeletable — it's a standalone flag,
        // not one of BaseEntity's two audit/soft-delete concerns — so it (and the
        // composite index spanning both concerns) stays configured directly here.
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => new { e.IsDeleted, e.IsActive });

        builder.ConfigureAuditable();
        builder.ConfigureSoftDeletable();
    }
}
