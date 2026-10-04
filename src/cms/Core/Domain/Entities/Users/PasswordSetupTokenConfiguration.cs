using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Users;

internal sealed class PasswordSetupTokenConfiguration : IEntityTypeConfiguration<PasswordSetupToken>
{
    public void Configure(EntityTypeBuilder<PasswordSetupToken> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();

        // Validating an incoming token looks it up by hash; listing a user's setup
        // status looks up their most recent row.
        builder.HasIndex(t => t.TokenHash).HasDatabaseName("IX_PasswordSetupTokens_TokenHash");
        builder.HasIndex(t => t.UserId).HasDatabaseName("IX_PasswordSetupTokens_UserId");
    }
}
