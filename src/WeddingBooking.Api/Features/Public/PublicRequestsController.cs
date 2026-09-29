using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Features.Calendar;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Customers;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Public;

public sealed record PublicPerson(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, MaxLength(40)] string Phone)
{
    public PartnerDetails ToDetails() => new(FirstName, LastName, Email, Phone);
}

public sealed record ViewingRequest(
    [Required, MinLength(2), MaxLength(2)] IReadOnlyList<PublicPerson> People,
    DateTimeOffset Start,
    // The wedding date the couple has in mind.
    DateOnly WishedDate,
    [Range(1, Booking.MaxGuests)] int GuestCount,
    bool Consent,
    // Left empty by people; bots fill in every field.
    string? Website);

public sealed record WeddingRequest(
    [Required, MinLength(2), MaxLength(2)] IReadOnlyList<PublicPerson> People,
    DateOnly EventDate,
    [Range(1, Booking.MaxGuests)] int GuestCount,
    bool Consent,
    string? Website);

public sealed record FreeTimeResponse(DateTimeOffset Start, DateTimeOffset End);

public sealed record ReceiptResponse(string Reference, DateOnly EventDate, DateTimeOffset? ViewingAt);

/// <summary>
/// The forms the website links to: a couple books a viewing or asks for their date. Open without
/// login, so they show only free and taken — never whose — and everything they take in is a request
/// a person decides on. Nothing is mailed from here.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
[EnableRateLimiting(PublicPolicy)]
public sealed class PublicRequestsController(AppDbContext db, FreeTime freeTime, TimeProvider clock) : ControllerBase
{
    public const string PublicPolicy = "public-requests";

    // ponytail: one lock for every public request — they are a handful a day. Per-time locks if that ever changes.
    private const long RequestLock = 684_120;

    [HttpGet("viewing-times")]
    public async Task<IReadOnlyList<FreeTimeResponse>> ViewingTimes(CancellationToken ct) =>
        (await freeTime.ViewingSlotsAsync(null, 12, ct))
            .DistinctBy(s => s.Slot.Start).Take(12)
            .Select(s => new FreeTimeResponse(s.Slot.Start, s.Slot.End)).ToList();

    [HttpGet("wedding-dates")]
    public async Task<IReadOnlyList<DateOnly>> WeddingDates(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return await freeTime.WeddingDatesAsync(today.AddDays(30), today.AddYears(3), ct);
    }

    [HttpPost("requests/viewing")]
    public async Task<ActionResult<ReceiptResponse>> RequestViewing(ViewingRequest request, CancellationToken ct)
    {
        if (!request.Consent) return Refuse("Sæt kryds ved samtykke, så vi må gemme jeres oplysninger.");
        if (!string.IsNullOrEmpty(request.Website)) return Receipt(Guid.CreateVersion7(), request.WishedDate, request.Start);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({RequestLock})", ct);

        // Checked again inside the lock: the time the form showed may have been taken since.
        var start = request.Start.ToUniversalTime();
        var slot = (await freeTime.ViewingSlotsAsync(null, 500, ct)).FirstOrDefault(s => s.Slot.Start == start);
        if (slot is null) return Conflict(Problem("Tiden er lige blevet taget", "Vælg en anden tid."));

        var booking = await CreateAsync(request.People, request.WishedDate, request.GuestCount, start, slot.HostId, ct);
        await transaction.CommitAsync(ct);
        return Receipt(booking.Id, booking.EventDate, booking.ViewingAt);
    }

    [HttpPost("requests/wedding")]
    public async Task<ActionResult<ReceiptResponse>> RequestWedding(WeddingRequest request, CancellationToken ct)
    {
        if (!request.Consent) return Refuse("Sæt kryds ved samtykke, så vi må gemme jeres oplysninger.");
        if (!string.IsNullOrEmpty(request.Website)) return Receipt(Guid.CreateVersion7(), request.EventDate, null);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({RequestLock})", ct);

        if (!(await freeTime.WeddingDatesAsync(request.EventDate, request.EventDate, ct)).Contains(request.EventDate))
        {
            return Conflict(Problem("Datoen er ikke ledig", "Vælg en anden dato."));
        }

        var booking = await CreateAsync(request.People, request.EventDate, request.GuestCount, null, null, ct);
        await transaction.CommitAsync(ct);
        return Receipt(booking.Id, booking.EventDate, null);
    }

    private async Task<Booking> CreateAsync(IReadOnlyList<PublicPerson> people, DateOnly date, int guests, DateTimeOffset? viewingAt, Guid? hostId, CancellationToken ct)
    {
        var customer = Customer.Create(people[0].ToDetails(), people[1].ToDetails());
        var settings = await db.PricingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var booking = Booking.Request(customer, date, guests, settings?.Surcharge, viewingAt, hostId);
        db.Customers.Add(customer);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);
        return booking;
    }

    private ActionResult<ReceiptResponse> Receipt(Guid id, DateOnly date, DateTimeOffset? viewingAt) =>
        StatusCode(StatusCodes.Status201Created, new ReceiptResponse(id.ToString()[^6..].ToUpperInvariant(), date, viewingAt));

    private BadRequestObjectResult Refuse(string detail) => BadRequest(Problem("Kan ikke sendes", detail));

    private static ProblemDetails Problem(string title, string detail) => new() { Title = title, Detail = detail };
}
