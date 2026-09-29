using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Users;

namespace WeddingBooking.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.Name).HasMaxLength(200).IsRequired();
        builder.Property(user => user.Phone).HasMaxLength(40);

        // Stored as text: a role is read straight out of the database often
        // enough that a number would be a nuisance.
        builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(40);

        builder.HasIndex(user => user.AuthUserId).IsUnique();
    }
}
