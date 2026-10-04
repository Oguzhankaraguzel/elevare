using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Logs;

internal sealed class AppLogConfiguration : IEntityTypeConfiguration<AppLog>
{
    public void Configure(EntityTypeBuilder<AppLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Message).HasMaxLength(1000).IsRequired();
        builder.Property(l => l.Path).HasMaxLength(500);
        builder.Property(l => l.UserAgent).HasMaxLength(500);

        // The log viewer always filters by date range and often by level/source.
        builder.HasIndex(l => l.CreatedAtUtc).HasDatabaseName("IX_AppLogs_CreatedAtUtc");
        builder.HasIndex(l => l.Level).HasDatabaseName("IX_AppLogs_Level");
        builder.HasIndex(l => l.Source).HasDatabaseName("IX_AppLogs_Source");
    }
}
