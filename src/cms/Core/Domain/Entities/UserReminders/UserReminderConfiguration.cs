using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.UserReminders;

internal sealed class UserReminderConfiguration : BaseEntityConfiguration<UserReminder>
{
    public override void Configure(EntityTypeBuilder<UserReminder> builder)
    {
        base.Configure(builder);

        builder.Property(r => r.Title).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Message).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.RemindAt).IsRequired();
        builder.Property(r => r.IsCompleted).HasDefaultValue(false);
        builder.Property(r => r.IsDismissed).HasDefaultValue(false);
        builder.Property(r => r.Channel).HasDefaultValue(ReminderChannel.InApp);

        builder.HasOne(r => r.User)
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(r => r.UserId).HasDatabaseName("IX_UserReminders_UserId");
        builder.HasIndex(r => r.RemindAt).HasDatabaseName("IX_UserReminders_RemindAt");
        builder.HasIndex(r => r.IsCompleted).HasDatabaseName("IX_UserReminders_IsCompleted");
        builder.HasIndex(r => new { r.IsCompleted, r.IsDismissed, r.RemindAt, r.IsDeleted })
               .HasDatabaseName("IX_UserReminders_Pending");
    }
}
