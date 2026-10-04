using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.FormSubmissions;

internal sealed class FormSubmissionConfiguration : IEntityTypeConfiguration<FormSubmission>
{
    public void Configure(EntityTypeBuilder<FormSubmission> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FormName).HasMaxLength(200);
        builder.Property(f => f.FieldsJson).IsRequired();

        // Restrict, not Cascade: pages are only ever soft-deleted, so this FK
        // never actually blocks a delete in practice.
        builder.HasOne(f => f.PageInfo)
            .WithMany()
            .HasForeignKey(f => f.PageInfoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(f => f.ReplyToEmail).HasMaxLength(256);
        builder.Property(f => f.ReplySubject).HasMaxLength(300);

        builder.HasOne(f => f.RepliedByUser)
            .WithMany()
            .HasForeignKey(f => f.RepliedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.PageInfoId).HasDatabaseName("IX_FormSubmissions_PageInfoId");
        builder.HasIndex(f => f.SubmittedAtUtc).HasDatabaseName("IX_FormSubmissions_SubmittedAtUtc");
    }
}
