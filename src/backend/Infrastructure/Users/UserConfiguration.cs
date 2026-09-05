using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Users;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email).HasMaxLength(256);
        builder.Property(user => user.FirstName).HasMaxLength(100);
        builder.Property(user => user.LastName).HasMaxLength(100);

        // The identity key: the pair, never oid alone. The same person carries a different oid in
        // every tenant they belong to, so a unique index on oid by itself would be correct today
        // and a silent collision the day a second tenant appears.
        builder
            .HasIndex(user => new { user.EntraObjectId, user.EntraTenantId })
            .IsUnique();
    }
}
