using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Media;

internal sealed class MediaFileConfiguration : BaseEntityConfiguration<MediaFile>
{
    public override void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        base.Configure(builder);

        builder.ToTable("MediaFiles");

        builder.Property(m => m.FileName).HasMaxLength(255).IsRequired();
        builder.Property(m => m.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(m => m.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(m => m.MimeType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.AltText).HasMaxLength(500);
        builder.Property(m => m.Title).HasMaxLength(255);
        builder.Property(m => m.FolderPath).HasMaxLength(500);

        builder.HasIndex(m => m.FilePath).HasDatabaseName("IX_MediaFiles_FilePath");
        builder.HasIndex(m => m.MediaType).HasDatabaseName("IX_MediaFiles_MediaType");
        builder.HasIndex(m => m.FolderPath).HasDatabaseName("IX_MediaFiles_FolderPath");

        builder.Property(m => m.Renditions).HasColumnType("jsonb");
    }
}
