using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Domain.Notes;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Notes;

public sealed record NoteResponse(Guid Id, NoteType Type, string Text, string AuthorName, DateTimeOffset CreatedAt, string? Recipients, string? Subject)
{
    public static NoteResponse From(Note note) =>
        new(note.Id, note.Type, note.Text, note.AuthorName, note.CreatedAt, note.Recipients, note.Subject);
}

public sealed record WriteNoteRequest(NoteType Type, [Required, MaxLength(50_000)] string Text);

/// <summary>A booking's history — calls, mails pasted in, internal notes and every mail sent from here.</summary>
[ApiController]
[Route("api/bookings/{id:guid}/notes")]
public sealed class NotesController(AppDbContext db, CurrentUser currentUser, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NoteResponse>>> List(Guid id, CancellationToken ct)
    {
        if (!await db.Bookings.AnyAsync(b => b.Id == id, ct)) return NotFound();
        var notes = await db.Notes.AsNoTracking().Where(n => n.BookingId == id).OrderByDescending(n => n.CreatedAt).ToListAsync(ct);
        return notes.Select(NoteResponse.From).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<NoteResponse>> Write(Guid id, WriteNoteRequest request, CancellationToken ct)
    {
        if (!await db.Bookings.AnyAsync(b => b.Id == id, ct)) return NotFound();
        var user = currentUser.User;
        var note = Note.Write(id, request.Type, request.Text, user.Id, user.Name, clock.GetUtcNow());
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);
        return NoteResponse.From(note);
    }
}
