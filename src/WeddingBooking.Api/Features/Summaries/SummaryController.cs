using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Common;
using WeddingBooking.Api.Features.Bookings;
using WeddingBooking.Api.Features.Payments;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Readiness;
using WeddingBooking.Domain.Summaries;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Summaries;

public sealed record SummaryResponse(
    string Source,
    CaseSummary Summary,
    // The booking's own data, shown next to the summary: the numbers come from here, not from the model.
    IReadOnlyList<string>? Facts,
    DateTimeOffset GeneratedAt);

public sealed record StandaloneSummaryRequest([Required, MaxLength(30_000)] string Text);

public sealed record BookingSummaryRequest([MaxLength(30_000)] string? ExtraText);

public sealed class SummaryOptions
{
    /// <summary>The standalone page needs no login. Off, it answers 404.</summary>
    public bool StandaloneEnabled { get; set; } = true;
}

/// <summary>
/// A summary of a case. It is shown, never stored: saving it as a note is a
/// separate step someone chooses.
/// </summary>
[ApiController]
public sealed class SummaryController(ISummariser summariser, TimeProvider clock) : ControllerBase
{
    public const string StandalonePolicy = "standalone-summary";
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    /// <summary>
    /// The standalone page: paste a mail thread, get a summary. Works with an
    /// OpenAI key alone — no login, no database. Rate limited per address, so it
    /// is not an open door to the estate's OpenAI account.
    /// </summary>
    [HttpPost("api/summary/standalone"), AllowAnonymous, EnableRateLimiting(StandalonePolicy)]
    public async Task<ActionResult<SummaryResponse>> Standalone(
        StandaloneSummaryRequest request, [FromServices] Microsoft.Extensions.Options.IOptions<SummaryOptions> options, CancellationToken ct)
    {
        if (!options.Value.StandaloneEnabled) return NotFound();
        var summary = await summariser.SummariseAsync(new SummaryInput(null, request.Text), ct);
        return new SummaryResponse(summariser.Source, summary, null, clock.GetUtcNow());
    }

    /// <summary>The summary of a booking, from its data, its history and any mail text pasted in.</summary>
    [HttpPost("api/bookings/{id:guid}/summary")]
    public async Task<ActionResult<SummaryResponse>> ForBooking(
        Guid id, BookingSummaryRequest request, [FromServices] AppDbContext db, [FromServices] CurrentUser currentUser,
        [FromServices] BookingFinance finance, CancellationToken ct)
    {
        var booking = await db.WithCustomer().AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();

        var notes = await db.Notes.AsNoTracking().Where(n => n.BookingId == id).OrderBy(n => n.CreatedAt).ToListAsync(ct);
        var text = string.Join("\n\n", notes.Select(n =>
            $"[{n.CreatedAt.ToString("d. MMMM yyyy", Danish)}, {n.AuthorName}, {(n.Recipients is not null ? "sendt mail " + n.Recipients : n.Type.ToString())}]\n{(n.Subject is not null ? n.Subject + "\n" : "")}{n.Text}"));
        if (!string.IsNullOrWhiteSpace(request.ExtraText)) text += $"\n\n[Indsat mailtekst]\n{request.ExtraText}";

        var facts = await FactsAsync(booking, currentUser.User.Role.SeesPrices() ? finance : null, ct);
        var summary = await summariser.SummariseAsync(new SummaryInput(facts, string.IsNullOrWhiteSpace(text) ? "Ingen noter endnu." : text), ct);
        return new SummaryResponse(summariser.Source, summary, facts, clock.GetUtcNow());
    }

    private async Task<IReadOnlyList<string>> FactsAsync(Booking booking, BookingFinance? finance, CancellationToken ct)
    {
        var payments = finance is null ? null : await finance.ForAsync(booking, ct);
        var status = BookingStatus.Derive(new StatusFacts(
            booking.Stage, booking.EventDate, Today.In(clock),
            ReservationFeePaid: payments?.ReservationFeePaid() ?? false,
            AnyInstalmentOverdue: payments?.AnyOverdue() ?? false,
            AllReadinessDone: ReadinessRules.AllDone(booking.ReadinessItems),
            PostEventOutstanding: ReadinessRules.PostEventOutstanding(booking.ReadinessItems)));

        var facts = new List<string>
        {
            $"Status: {StatusText(status)}",
            $"Dato: {booking.EventDate.ToString("dddd 'den' d. MMMM yyyy", Danish)}",
            $"Gæster: {booking.GuestCount}",
        };
        if (booking.Lines.Count > 0) facts.Add($"Indhold: {string.Join(", ", booking.OrderedLines.Select(l => l.Description))}");
        if (payments is not null)
        {
            if (booking.Lines.Count > 0) facts.Add($"Pris i alt: {payments.Total.ToString("N0", Danish)} kr.");
            facts.Add("Betaling: " + string.Join("; ", payments.Instalments.Select(i => $"{InstalmentText(i.Kind)} {StateText(i.State)}")));
        }
        var ready = booking.ReadinessItems.Where(i => i.Category != ReadinessCategory.PostEventInvoicing).ToList();
        facts.Add($"Klargøring: {ready.Count(i => i.Status == ReadinessStatus.Done)} af {ready.Count} færdige");
        return facts;
    }

    private static string StatusText(DisplayStatus status) => status switch
    {
        DisplayStatus.Inquiry => "Forespørgsel",
        DisplayStatus.Viewing => "Fremvisning aftalt",
        DisplayStatus.QuoteSent => "Tilbud sendt",
        DisplayStatus.Signed => "Underskrevet",
        DisplayStatus.DateSecured => "Dato sikret",
        DisplayStatus.ReadyForEvent => "Klar til afholdelse",
        DisplayStatus.Held => "Afholdt",
        DisplayStatus.AwaitingPostEventInvoice => "Afventer efterfakturering",
        DisplayStatus.PaymentOverdue => "Betaling forfalden",
        DisplayStatus.Cancelled => "Afbestilt",
        _ => "Tabt",
    };

    private static string InstalmentText(Domain.Payments.InstalmentKind kind) => kind switch
    {
        Domain.Payments.InstalmentKind.ReservationFee => "reservationsgebyr",
        Domain.Payments.InstalmentKind.Instalment => "33 %-rate",
        _ => "restbetaling",
    };

    private static string StateText(InstalmentState state) => state switch
    {
        InstalmentState.Paid => "betalt",
        InstalmentState.Overdue => "forfalden",
        InstalmentState.Booked => "faktureret",
        InstalmentState.Draft => "kladde",
        _ => "ikke faktureret",
    };
}
