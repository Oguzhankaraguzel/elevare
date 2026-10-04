using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.UserTasks;

internal sealed class UserTaskConfiguration : BaseEntityConfiguration<UserTask>
{
    public override void Configure(EntityTypeBuilder<UserTask> builder)
    {
        base.Configure(builder);

        builder.Property(t => t.Title).HasMaxLength(300).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(2000).IsRequired();
        builder.Property(t => t.Status).HasDefaultValue(UserTaskStatus.New);
        builder.Property(t => t.Priority).HasDefaultValue(UserTaskPriority.Medium);
        builder.Property(t => t.IsRead).HasDefaultValue(false);
        builder.Property(t => t.IsArchived).HasDefaultValue(false);
        builder.Property(t => t.RelatedEntityType).HasMaxLength(100);

        builder.HasOne(t => t.AssignedToUser)
               .WithMany()
               .HasForeignKey(t => t.AssignedToUserId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(t => t.AssignedByUser)
               .WithMany()
               .HasForeignKey(t => t.AssignedByUserId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(t => t.ParentTask)
               .WithMany(t => t.SubTasks)
               .HasForeignKey(t => t.ParentTaskId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(t => t.Comments)
               .WithOne(c => c.UserTask)
               .HasForeignKey(c => c.UserTaskId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.AssignedToUserId).HasDatabaseName("IX_UserTasks_AssignedToUserId");
        builder.HasIndex(t => t.AssignedByUserId).HasDatabaseName("IX_UserTasks_AssignedByUserId");
        builder.HasIndex(t => t.Status).HasDatabaseName("IX_UserTasks_Status");
        builder.HasIndex(t => t.Priority).HasDatabaseName("IX_UserTasks_Priority");
        builder.HasIndex(t => t.DueDate).HasDatabaseName("IX_UserTasks_DueDate");
        builder.HasIndex(t => new { t.AssignedToUserId, t.Status, t.IsDeleted })
               .HasDatabaseName("IX_UserTasks_AssignedTo_Status_IsDeleted");
    }
}
