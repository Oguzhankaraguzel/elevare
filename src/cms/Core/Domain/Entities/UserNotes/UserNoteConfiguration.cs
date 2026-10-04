using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.UserNotes;

internal sealed class UserNoteConfiguration : BaseEntityConfiguration<UserNote>
{
    public override void Configure(EntityTypeBuilder<UserNote> builder)
    {
        base.Configure(builder);

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Content).IsRequired();
        builder.Property(n => n.Color).HasMaxLength(20);
        builder.Property(n => n.IsPinned).HasDefaultValue(false);
        builder.Property(n => n.IsArchived).HasDefaultValue(false);
        builder.Property(n => n.Type).HasDefaultValue(NoteType.Personal);
        builder.Property(n => n.Visibility).HasDefaultValue(NoteVisibility.Private);

        builder.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(n => n.UserId).HasDatabaseName("IX_UserNotes_UserId");
        builder.HasIndex(n => n.IsPinned).HasDatabaseName("IX_UserNotes_IsPinned");
        builder.HasIndex(n => n.IsArchived).HasDatabaseName("IX_UserNotes_IsArchived");
        builder.HasIndex(n => new { n.UserId, n.IsDeleted, n.IsArchived }).HasDatabaseName("IX_UserNotes_UserId_IsDeleted_IsArchived");
    }
}
