using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Messages;
using WeddingBooking.Domain.Notes;

namespace WeddingBooking.Infrastructure.Persistence.Configurations;

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes");
        builder.HasKey(note => note.Id);
        builder.Property(note => note.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(note => note.AuthorName).HasMaxLength(200);
        builder.Property(note => note.Recipients).HasMaxLength(2000);
        builder.Property(note => note.Subject).HasMaxLength(500);
        builder.HasOne<Booking>().WithMany().HasForeignKey(note => note.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(note => new { note.BookingId, note.CreatedAt });
    }
}

internal sealed class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        builder.ToTable("message_templates");
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Name).HasMaxLength(200);
        builder.Property(template => template.Subject).HasMaxLength(500);
    }
}
