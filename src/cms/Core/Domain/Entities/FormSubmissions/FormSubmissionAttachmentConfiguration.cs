using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.FormSubmissions;

internal sealed class FormSubmissionAttachmentConfiguration : IEntityTypeConfiguration<FormSubmissionAttachment>
{
    public void Configure(EntityTypeBuilder<FormSubmissionAttachment> builder)
    {
        builder.ToTable("FormSubmissionAttachments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.FieldName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.FileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Content).IsRequired();

        // Cascade: an attachment has no life of its own. Deleting a submission —
        // by hand, or by whatever retention comes to apply to submissions — takes
        // its files with it, with no job to remember to.
        builder.HasOne(a => a.FormSubmission)
            .WithMany(f => f.Attachments)
            .HasForeignKey(a => a.FormSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.FormSubmissionId).HasDatabaseName("IX_FormSubmissionAttachments_FormSubmissionId");
    }
}
