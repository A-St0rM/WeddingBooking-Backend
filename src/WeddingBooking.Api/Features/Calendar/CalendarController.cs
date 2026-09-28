using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Features.Payments;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Calendar;
using WeddingBooking.Domain.Common;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Calendar;

public enum CalendarEventKind
{
    Wedding,
    Viewing,
    Block,
}

public sealed record CalendarEventResponse(
    CalendarEventKind Kind,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool AllDay,
    string Title,
    Guid? BookingId,
    Guid? BlockId,
    string? HostName,
    // A wedding that holds its day: signed or with the fee paid. The others are still inquiries.
    bool Confirmed);

public sealed record SlotResponse(DateTimeOffset Start, DateTimeOffset End, Guid HostId, string HostName);

public sealed record WindowDto(DayOfWeek Day, TimeOnly From, TimeOnly To);

public sealed record HostWeekResponse(Guid HostId, string HostName, IReadOnlyList<WindowDto> Windows);

public sealed record BlockRequest(DateTimeOffset Start, DateTimeOffset End, [Required, MaxLength(200)] string Reason, Guid? HostId);

/// <summary>
/// The estate's calendar: weddings (the whole day), viewings with their host, and blocked time —
/// and the free viewing times that follow from each host's week.
/// </summary>
[ApiController]
[Route("api/calendar")]
public sealed class CalendarController(AppDbContext db, FreeTime freeTime, CurrentUser currentUser) : ControllerBase
{

    [HttpGet]
    public async Task<IReadOnlyList<CalendarEventResponse>> Events([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        if (to < from || to.DayNumber - from.DayNumber > 400) throw new DomainException("Vælg højst et år ad gangen.");
        var start = FreeTime.DayStart(from);
        var end = FreeTime.DayStart(to.AddDays(1));

        var bookings = await freeTime.LiveBookings()
            .Where(b => (b.EventDate >= from && b.EventDate <= to) || (b.ViewingAt >= start && b.ViewingAt < end))
            .ToListAsync(ct);
        var held = await freeTime.HoldsTheDayAsync(bookings, currentUser.User.Role.SeesPrices(), ct);
        var hosts = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var blocks = await db.CalendarBlocks.AsNoTracking().Where(b => b.Start < end && b.End > start).ToListAsync(ct);

        var events = new List<CalendarEventResponse>();
        foreach (var booking in bookings)
        {
            if (booking.EventDate >= from && booking.EventDate <= to)
            {
                events.Add(new(CalendarEventKind.Wedding, FreeTime.DayStart(booking.EventDate), FreeTime.DayStart(booking.EventDate.AddDays(1)), true,
                    $"{booking.Customer.Name} · {booking.GuestCount} gæster", booking.Id, null, null, held.Contains(booking.Id)));
            }
            if (booking.ViewingAt is { } at && at >= start && at < end)
            {
                events.Add(new(CalendarEventKind.Viewing, at, at + AvailableTime.ViewingLength, false,
                    $"Fremvisning: {booking.Customer.Name}", booking.Id, null,
                    booking.ViewingHostId is { } h ? hosts.GetValueOrDefault(h) : null, true));
            }
        }
        events.AddRange(blocks.Select(b => new CalendarEventResponse(CalendarEventKind.Block, b.Start, b.End, false, b.Reason, null, b.Id,
            b.HostId is { } h ? hosts.GetValueOrDefault(h) : null, true)));

        return events.OrderBy(e => e.Start).ToList();
    }

    /// <summary>The next free viewing times — for one host, or for everyone with a week set.</summary>
    [HttpGet("slots"), ManagementOnly]
    public async Task<IReadOnlyList<SlotResponse>> Slots([FromQuery] Guid? hostId, [FromQuery, Range(1, 20)] int count = 5, CancellationToken ct = default) =>
        (await freeTime.ViewingSlotsAsync(hostId, count, ct)).Take(count)
            .Select(s => new SlotResponse(s.Slot.Start, s.Slot.End, s.HostId, s.HostName)).ToList();

    /// <summary>Free wedding dates: Saturdays in the season that no secured booking holds.</summary>
    [HttpGet("wedding-dates")]
    public async Task<IReadOnlyList<DateOnly>> WeddingDates([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        if (to < from || to.DayNumber - from.DayNumber > 800) throw new DomainException("Vælg højst to år ad gangen.");
        return await freeTime.WeddingDatesAsync(from, to, ct);
    }

    [HttpGet("availability"), ManagementOnly]
    public async Task<IReadOnlyList<HostWeekResponse>> Availability(CancellationToken ct)
    {
        var windows = await db.HostAvailability.AsNoTracking().ToListAsync(ct);
        var hosts = await db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Name).ToListAsync(ct);
        return hosts.Select(h => new HostWeekResponse(h.Id, h.Name,
                windows.Where(w => w.HostId == h.Id).OrderBy(w => ((int)w.Day + 6) % 7).ThenBy(w => w.From).Select(w => new WindowDto(w.Day, w.From, w.To)).ToList()))
            .ToList();
    }

    /// <summary>Replaces a host's week.</summary>
    [HttpPut("availability/{hostId:guid}"), ManagementOnly]
    public async Task<IActionResult> SetAvailability(Guid hostId, IReadOnlyList<WindowDto> windows, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == hostId && u.IsActive, ct)) return NotFound();
        db.HostAvailability.RemoveRange(db.HostAvailability.Where(a => a.HostId == hostId));
        db.HostAvailability.AddRange(windows.Select(w => HostAvailability.Create(hostId, w.Day, w.From, w.To)));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("blocks"), ManagementOnly]
    public async Task<IActionResult> Block(BlockRequest request, CancellationToken ct)
    {
        db.CalendarBlocks.Add(CalendarBlock.Create(request.Start, request.End, request.Reason, request.HostId));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("blocks/{id:guid}"), ManagementOnly]
    public async Task<IActionResult> Unblock(Guid id, CancellationToken ct) =>
        await db.CalendarBlocks.Where(b => b.Id == id).ExecuteDeleteAsync(ct) == 0 ? NotFound() : NoContent();
}
