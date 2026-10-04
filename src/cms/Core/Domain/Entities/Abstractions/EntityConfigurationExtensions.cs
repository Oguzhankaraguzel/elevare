using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Abstractions;

/// <summary>
/// Composable EF configuration for the two concerns <see cref="BaseEntity"/> bundles
/// together — kept separate here so either could, in principle, be applied to an
/// entity that only needs one of them.
/// </summary>
internal static class EntityConfigurationExtensions
{
    public static void ConfigureAuditable<T>(this EntityTypeBuilder<T> builder) where T : class, IAuditable
    {
        builder.Property(e => e.CreateDate)
            .IsRequired()
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("(now() at time zone 'utc')");

        builder.Property(e => e.CreateUserId).IsRequired();

        builder.HasIndex(e => e.CreateDate);
        builder.HasIndex(e => e.CreateUserId);
        builder.HasIndex(e => e.UpdateUserId);

        builder.HasOne(e => e.CreateUser)
            .WithMany()
            .HasForeignKey(e => e.CreateUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.UpdateUser)
            .WithMany()
            .HasForeignKey(e => e.UpdateUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    public static void ConfigureSoftDeletable<T>(this EntityTypeBuilder<T> builder) where T : class, ISoftDeletable
    {
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasIndex(e => e.IsDeleted);
        builder.HasIndex(e => e.DeleteUserId);

        builder.HasOne(e => e.DeleteUser)
            .WithMany()
            .HasForeignKey(e => e.DeleteUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Global query filter to exclude soft-deleted entities (IsDeleted == true)
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
