using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Logs;

internal sealed class AuthEventConfiguration : IEntityTypeConfiguration<AuthEvent>
{
    public void Configure(EntityTypeBuilder<AuthEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserNameSnapshot).HasMaxLength(256).IsRequired();
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.UserAgent).HasMaxLength(500);

        // The viewer always filters/sorts by date, and often by user or event type.
        builder.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("IX_AuthEvents_CreatedAtUtc");
        builder.HasIndex(e => e.UserId).HasDatabaseName("IX_AuthEvents_UserId");
        builder.HasIndex(e => e.EventType).HasDatabaseName("IX_AuthEvents_EventType");
    }
}
