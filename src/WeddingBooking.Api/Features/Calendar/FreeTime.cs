using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Features.Payments;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Calendar;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Calendar;

public sealed record HostSlot(Interval Slot, Guid HostId, string HostName);

/// <summary>
/// What is free: viewing times per host and wedding dates. Shared by the calendar and the public
/// request forms, so the page Johan looks at and the form a couple fills in agree.
/// </summary>
public sealed class FreeTime(AppDbContext db, BookingFinance finance, TimeProvider clock)
{
    public static readonly TimeZoneInfo Denmark = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    public IQueryable<Booking> LiveBookings() =>
        db.Bookings.AsNoTracking().Include(b => b.Customer)
            .Where(b => b.Stage != SalesStage.Cancelled && b.Stage != SalesStage.Lost);

    /// <summary>
    /// The weddings that hold their day: signed, a date request still waiting (§11.12) and — for those who
    /// may see payments — the fee paid.
    /// </summary>
    public async Task<HashSet<Guid>> HoldsTheDayAsync(IReadOnlyCollection<Booking> bookings, bool seesPayments, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var held = bookings.Where(b => b.Stage == SalesStage.Signed || b.HoldsRequestedDate(now)).Select(b => b.Id).ToHashSet();
        if (!seesPayments || bookings.Count == 0) return held;
        var payments = await finance.ForManyAsync(bookings, ct);
        held.UnionWith(payments.Where(p => p.Value.ReservationFeePaid()).Select(p => p.Key));
        return held;
    }

    /// <summary>The free viewing times of every host with a week — or one — soonest first.</summary>
    public async Task<IReadOnlyList<HostSlot>> ViewingSlotsAsync(Guid? hostId, int countPerHost, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var weeks = await db.HostAvailability.AsNoTracking().Where(a => hostId == null || a.HostId == hostId).ToListAsync(ct);
        var hosts = await db.Users.AsNoTracking().Where(u => u.IsActive).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var horizonEnd = now.AddDays(120);
        var bookings = await LiveBookings()
            .Where(b => (b.ViewingAt >= now && b.ViewingAt < horizonEnd)
                || (b.EventDate >= DateOnly.FromDateTime(now.UtcDateTime) && b.EventDate <= DateOnly.FromDateTime(horizonEnd.UtcDateTime)))
            .ToListAsync(ct);
        var held = await HoldsTheDayAsync(bookings, seesPayments: true, ct);
        var blocks = await db.CalendarBlocks.AsNoTracking().Where(b => b.End > now).ToListAsync(ct);

        var slots = new List<HostSlot>();
        foreach (var week in weeks.GroupBy(a => a.HostId).Where(g => hosts.ContainsKey(g.Key)))
        {
            // Taken for this host: the estate's wedding days, blocks for the estate or for them, and their viewings.
            // A viewing without a chosen host takes the time from every host, so nothing is double-booked.
            var taken = bookings.Where(b => held.Contains(b.Id)).Select(b => new Interval(DayStart(b.EventDate), DayStart(b.EventDate.AddDays(1))))
                .Concat(blocks.Where(b => b.HostId is null || b.HostId == week.Key).Select(b => b.Interval))
                .Concat(bookings.Where(b => b.ViewingAt is not null && (b.ViewingHostId is null || b.ViewingHostId == week.Key))
                    .Select(b => new Interval(b.ViewingAt!.Value, b.ViewingAt.Value + AvailableTime.ViewingLength)));

            slots.AddRange(AvailableTime.NextViewings(now, week, taken, Denmark, countPerHost).Select(s => new HostSlot(s, week.Key, hosts[week.Key])));
        }

        return slots.OrderBy(s => s.Slot.Start).ToList();
    }

    public async Task<IReadOnlyList<DateOnly>> WeddingDatesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var bookings = await LiveBookings().Where(b => b.EventDate >= from && b.EventDate <= to).ToListAsync(ct);
        var held = await HoldsTheDayAsync(bookings, seesPayments: true, ct);
        return AvailableTime.WeddingDates(from, to, bookings.Where(b => held.Contains(b.Id)).Select(b => b.EventDate).ToHashSet()).ToList();
    }

    public static DateTimeOffset DayStart(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        // Npgsql stores and compares timestamptz in UTC only.
        return new DateTimeOffset(local, Denmark.GetUtcOffset(local)).ToUniversalTime();
    }
}
