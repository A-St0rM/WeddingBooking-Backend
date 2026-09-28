using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Customers;
using WeddingBooking.Domain.Notes;
using WeddingBooking.Domain.Readiness;
using WeddingBooking.Domain.Trello;
using WeddingBooking.Infrastructure.Persistence;
using WeddingBooking.Integrations.Trello;

namespace WeddingBooking.Api.Features.Trello;

public enum CardAction
{
    Import,
    Imported,
    AlreadyImported,
    Skip,
}

public sealed record CardReport(
    string CardId,
    string CardName,
    string ListName,
    CardAction Action,
    string? Reason,
    string? CoupleName,
    DateOnly? EventDate,
    int? GuestCount,
    bool ExistingCustomer,
    IReadOnlyList<string> Labels,
    int ReadinessItems,
    Guid? BookingId);

public sealed record BoardReport(string Name, int? Year, IReadOnlyList<CardReport> Cards);

public sealed record ImportReport(bool DryRun, IReadOnlyList<BoardReport> Boards, int Cards, int Importable, int Imported, int AlreadyImported, int Skipped);

public sealed record TrelloStatusResponse(bool ApiConfigured);

/// <summary>
/// The one-time import of the wedding boards. A dry run first: every card is
/// either shown as importable or listed with the reason it cannot be. The real
/// run imports the same cards and can be repeated — a card already imported is
/// skipped. Trello itself is only read, never changed.
/// </summary>
[ApiController]
[Route("api/trello")]
[ManagementOnly]
public sealed class TrelloImportController(AppDbContext db, ITrelloGateway trello, CurrentUser currentUser, TimeProvider clock) : ControllerBase
{
    private const long MaxExportBytes = 50 * 1024 * 1024;

    [HttpGet("status")]
    public TrelloStatusResponse Status() => new(trello.IsConfigured);

    /// <summary>What an import would do. Nothing is saved.</summary>
    [HttpPost("preview"), RequestSizeLimit(MaxExportBytes)]
    public async Task<ImportReport> Preview([FromForm] List<IFormFile>? files, [FromForm] string? boardIds, CancellationToken ct) =>
        await RunAsync(await BoardsAsync(files, boardIds, ct), dryRun: true, ct);

    [HttpPost("import"), RequestSizeLimit(MaxExportBytes)]
    public async Task<ImportReport> Import([FromForm] List<IFormFile>? files, [FromForm] string? boardIds, CancellationToken ct) =>
        await RunAsync(await BoardsAsync(files, boardIds, ct), dryRun: false, ct);

    private async Task<List<TrelloBoard>> BoardsAsync(List<IFormFile>? files, string? boardIds, CancellationToken ct)
    {
        var boards = new List<TrelloBoard>();
        foreach (var file in files ?? [])
        {
            using var reader = new StreamReader(file.OpenReadStream());
            try
            {
                boards.Add(TrelloJson.Parse(await reader.ReadToEndAsync(ct)));
            }
            catch (FormatException error)
            {
                throw new DomainException($"{file.FileName}: {error.Message}");
            }
        }

        foreach (var id in (boardIds ?? "").Split([',', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            boards.Add(await trello.GetBoardAsync(id, ct));
        }

        if (boards.Count == 0) throw new DomainException("Vælg mindst én Trello-eksport, eller skriv et bord-id.");
        return boards;
    }

    private async Task<ImportReport> RunAsync(List<TrelloBoard> boards, bool dryRun, CancellationToken ct)
    {
        var cardIds = boards.SelectMany(b => b.Cards).Select(c => c.Id).ToList();
        var imported = await db.Bookings.Where(b => b.TrelloCardId != null && cardIds.Contains(b.TrelloCardId))
            .ToDictionaryAsync(b => b.TrelloCardId!, b => b.Id, ct);
        var customers = await db.Customers.ToListAsync(ct);
        var settings = await db.PricingSettings.AsNoTracking().FirstOrDefaultAsync(ct);

        var reports = new List<BoardReport>();
        foreach (var board in boards)
        {
            var year = TrelloCardInterpreter.YearOf(board.Name);
            var cards = new List<CardReport>();
            foreach (var card in board.Cards)
            {
                var reading = TrelloCardInterpreter.Read(card, year);
                var existing = reading.CoupleName is { } couple
                    ? customers.FirstOrDefault(c => string.Equals(c.Name, couple, StringComparison.OrdinalIgnoreCase))
                    : null;

                if (imported.TryGetValue(card.Id, out var bookingId))
                {
                    cards.Add(Report(card, reading, CardAction.AlreadyImported, "Kortet er allerede importeret.", existing is not null, bookingId));
                    continue;
                }
                if (!reading.CanImport)
                {
                    cards.Add(Report(card, reading, CardAction.Skip, reading.SkipReason, existing is not null, null));
                    continue;
                }
                if (dryRun)
                {
                    cards.Add(Report(card, reading, CardAction.Import, null, existing is not null, null));
                    continue;
                }

                var customer = existing ?? NewCustomer(reading);
                if (existing is null)
                {
                    db.Customers.Add(customer);
                    customers.Add(customer);
                }

                var booking = Booking.Create(customer, reading.EventDate!.Value, reading.GuestCount!.Value, PaymentTerms.New, settings?.Surcharge);
                booking.RecordTrelloOrigin(card.Id);
                if (IsConfirmed(card.ListName)) booking.MarkSigned(clock.GetUtcNow());
                foreach (var (category, status) in reading.Readiness) booking.ImportReadiness(category, status);
                db.Bookings.Add(booking);
                db.Notes.Add(Note.Write(booking.Id, NoteType.Internal, ImportNote(board, card, reading), currentUser.User.Id, currentUser.User.Name, clock.GetUtcNow()));

                await db.SaveChangesAsync(ct);
                imported[card.Id] = booking.Id;
                cards.Add(Report(card, reading, CardAction.Imported, null, existing is not null, booking.Id));
            }
            reports.Add(new BoardReport(board.Name, year, cards));
        }

        var all = reports.SelectMany(r => r.Cards).ToList();
        return new ImportReport(
            dryRun,
            reports,
            all.Count,
            all.Count(c => c.Action == CardAction.Import),
            all.Count(c => c.Action == CardAction.Imported),
            all.Count(c => c.Action == CardAction.AlreadyImported),
            all.Count(c => c.Action == CardAction.Skip));
    }

    private static CardReport Report(TrelloCard card, CardReading reading, CardAction action, string? reason, bool existingCustomer, Guid? bookingId) =>
        new(card.Id, card.Name, card.ListName, action, reason, reading.CoupleName, reading.EventDate, reading.GuestCount,
            existingCustomer, reading.Labels, reading.Readiness.Count, bookingId);

    /// <summary>A card in a list that says the booking is confirmed or signed.</summary>
    private static bool IsConfirmed(string listName) =>
        new[] { "bekræft", "underskrev", "booket", "afholdt", "klar" }.Any(w => listName.Contains(w, StringComparison.OrdinalIgnoreCase));

    /// <summary>The couple from the card: the two names, the e-mails in the order they appear, the first phone number on the first.</summary>
    private static Customer NewCustomer(CardReading reading)
    {
        var (first, second) = Customer.SplitCouple(reading.CoupleName)!.Value;
        return Customer.Create(
            new PartnerDetails(first, null, reading.Emails.ElementAtOrDefault(0), reading.Phones.ElementAtOrDefault(0)),
            new PartnerDetails(second, null, reading.Emails.ElementAtOrDefault(1), reading.Phones.ElementAtOrDefault(1)));
    }

    private static string ImportNote(TrelloBoard board, TrelloCard card, CardReading reading)
    {
        var lines = new List<string> { $"Importeret fra Trello: \"{card.Name}\" på bordet {board.Name}, listen {card.ListName}." };
        if (reading.Labels.Count > 0) lines.Add($"Labels i Trello: {string.Join(", ", reading.Labels)}. Betalingsstatus læses nu fra regnskabet, ikke fra labels.");
        if (!string.IsNullOrWhiteSpace(card.Description)) lines.Add($"Kortets beskrivelse:\n{card.Description.Trim()}");
        foreach (var checklist in card.Checklists)
        {
            lines.Add($"{checklist.Name}: {string.Join(", ", checklist.Items.Select(i => (i.Complete ? "✓ " : "○ ") + i.Name))}");
        }
        return string.Join("\n\n", lines);
    }
}
