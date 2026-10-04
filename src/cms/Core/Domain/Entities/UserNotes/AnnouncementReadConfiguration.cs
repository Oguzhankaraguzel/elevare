using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.UserNotes;

internal sealed class AnnouncementReadConfiguration : IEntityTypeConfiguration<AnnouncementRead>
{
    public void Configure(EntityTypeBuilder<AnnouncementRead> builder)
    {
        builder.HasKey(r => r.Id);

        // One marker per person per announcement — the unread count is a COUNT of
        // announcements without a row here, so a duplicate would not corrupt it, but
        // the unique index keeps a double-click from quietly growing the table.
        builder.HasIndex(r => new { r.UserNoteId, r.UserId }).IsUnique();

        builder.HasOne(r => r.UserNote)
            .WithMany()
            .HasForeignKey(r => r.UserNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
