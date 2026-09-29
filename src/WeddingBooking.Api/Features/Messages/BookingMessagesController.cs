using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Features.Bookings;
using WeddingBooking.Api.Features.Notes;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Messages;
using WeddingBooking.Domain.Notes;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Messages;

public sealed record DraftRequest(Guid TemplateId);

public sealed record MessageDraftResponse(
    Guid TemplateId,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject,
    string Body,
    // Placeholders the booking had no value for. They stay in the text so they are seen.
    IReadOnlyList<string> Missing,
    bool Simulated);

public sealed record SendRequest(
    [Required, MinLength(1)] IReadOnlyList<string> To,
    IReadOnlyList<string>? Cc,
    [Required, MaxLength(500)] string Subject,
    [Required, MaxLength(50_000)] string Body);

/// <summary>
/// A mail to the couple. The draft is merged from a template and the booking and
/// is only a suggestion; nothing is sent until someone has read the text and
/// pressed Send — and what was sent is written into the booking's history.
/// </summary>
[ApiController]
[Route("api/bookings/{id:guid}/messages")]
[ManagementOnly]
public sealed class BookingMessagesController(AppDbContext db, IMessageSender sender, CurrentUser currentUser, TimeProvider clock) : ControllerBase
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly TimeZoneInfo Denmark = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    [HttpPost("draft")]
    public async Task<ActionResult<MessageDraftResponse>> Draft(Guid id, DraftRequest request, CancellationToken ct)
    {
        var booking = await db.WithCustomer().AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        var template = await db.MessageTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TemplateId, ct);
        if (booking is null || template is null) return NotFound();

        var host = await HostAsync(booking, ct);
        var values = Values(booking, host);
        var subject = TemplateMerger.Merge(template.Subject, values);
        var body = TemplateMerger.Merge(template.Body, values);

        var to = booking.Customer.Contacts.OrderByDescending(c => c.IsPrimary).Select(c => c.Email).OfType<string>().ToList();
        var cc = template.CcViewingHost && host?.Email is { } hostEmail ? [hostEmail] : new List<string>();

        return new MessageDraftResponse(template.Id, to, cc, subject.Text, body.Text,
            subject.Missing.Union(body.Missing).ToList(), sender.IsSimulated);
    }

    [HttpPost("send")]
    public async Task<ActionResult<NoteResponse>> Send(Guid id, SendRequest request, CancellationToken ct)
    {
        if (!await db.Bookings.AnyAsync(b => b.Id == id, ct)) return NotFound();
        var to = Addresses(request.To);
        var cc = Addresses(request.Cc ?? []);
        if (to.Count == 0) throw new DomainException("Mailen skal have mindst én modtager.");

        var user = currentUser.User;
        await sender.SendAsync(new OutgoingMessage(user.Name, user.Email, to, cc, request.Subject.Trim(), request.Body), ct);

        var recipients = cc.Count > 0 ? $"Til: {string.Join(", ", to)} · CC: {string.Join(", ", cc)}" : $"Til: {string.Join(", ", to)}";
        var note = Note.SentMail(id, recipients, request.Subject.Trim(), request.Body, user.Id, user.Name, clock.GetUtcNow());
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);
        return NoteResponse.From(note);
    }

    private async Task<Domain.Users.User?> HostAsync(Booking booking, CancellationToken ct)
    {
        if (booking.ViewingHostId is { } hostId) return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == hostId, ct);
        // No host chosen: the event producer, as Bryan does the viewings.
        return await db.Users.AsNoTracking().Where(u => u.IsActive && u.Role == Domain.Users.Role.EventProducer).OrderBy(u => u.CreatedAt).FirstOrDefaultAsync(ct);
    }

    private Dictionary<string, string?> Values(Booking booking, Domain.Users.User? host)
    {
        var primary = booking.Customer.PrimaryContact;
        var day = booking.EventDate.ToString("dddd 'den' d. MMMM yyyy", Danish);
        string? viewing = booking.ViewingAt is { } at
            ? TimeZoneInfo.ConvertTime(at, Denmark).ToString("dddd 'den' d. MMMM 'kl.' HH.mm", Danish)
            : null;

        return new Dictionary<string, string?>
        {
            ["parret"] = booking.Customer.Name,
            ["fornavn"] = primary?.Name.Split(' ')[0],
            ["dato"] = day,
            ["gæster"] = booking.GuestCount.ToString(Danish),
            ["pris"] = booking.Lines.Count > 0 ? booking.Price().Total.ToString("N0", Danish) + " kr." : null,
            ["fremvisning"] = viewing,
            ["vært"] = host?.Name,
            ["vært_telefon"] = host?.Phone,
            ["afsender"] = currentUser.User.Name,
        };
    }

    private static List<string> Addresses(IEnumerable<string> addresses) =>
        addresses.Select(a => a.Trim()).Where(a => a.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(a => MailAddress.TryCreate(a, out var parsed) ? parsed.Address : throw new DomainException($"\"{a}\" er ikke en gyldig e-mail."))
            .ToList();
}
