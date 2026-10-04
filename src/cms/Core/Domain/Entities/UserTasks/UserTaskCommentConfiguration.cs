using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.UserTasks;

internal sealed class UserTaskCommentConfiguration : BaseEntityConfiguration<UserTaskComment>
{
    public override void Configure(EntityTypeBuilder<UserTaskComment> builder)
    {
        base.Configure(builder);

        builder.Property(c => c.Comment).HasMaxLength(2000).IsRequired();

        builder.HasOne(c => c.User)
               .WithMany()
               .HasForeignKey(c => c.UserId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(c => c.UserTaskId).HasDatabaseName("IX_UserTaskComments_UserTaskId");
        builder.HasIndex(c => c.UserId).HasDatabaseName("IX_UserTaskComments_UserId");
    }
}
