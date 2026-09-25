using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Notes;

public enum NoteType
{
    Call,

    /// <summary>A mail — pasted in from the mailbox, or one sent from the system.</summary>
    Email,

    Internal,
}

/// <summary>
/// One entry in a booking's history: who spoke to the couple, when, about what.
/// Never edited or deleted — it is the record Johan goes back to in November.
/// </summary>
public sealed class Note
{
    private Note() { }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public NoteType Type { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public Guid? AuthorId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>For a mail sent from the system: who it went to.</summary>
    public string? Recipients { get; private set; }

    public string? Subject { get; private set; }

    public static Note Write(Guid bookingId, NoteType type, string text, Guid? authorId, string authorName, DateTimeOffset at)
    {
        var body = Common.Text.Required(text, "En note skal have en tekst.");
        if (body.Length > 50_000) throw new DomainException("Noten er for lang.");
        return new Note
        {
            Id = Guid.CreateVersion7(),
            BookingId = bookingId,
            Type = type,
            Text = body,
            AuthorId = authorId,
            AuthorName = authorName,
            CreatedAt = at,
        };
    }

    public static Note SentMail(Guid bookingId, string recipients, string subject, string body, Guid authorId, string authorName, DateTimeOffset at)
    {
        var note = Write(bookingId, NoteType.Email, body, authorId, authorName, at);
        note.Recipients = recipients;
        note.Subject = subject;
        return note;
    }
}
