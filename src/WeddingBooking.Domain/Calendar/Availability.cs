using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Calendar;

public sealed record Interval(DateTimeOffset Start, DateTimeOffset End)
{
    public bool Overlaps(Interval other) => Start < other.End && other.Start < End;
}

/// <summary>A standing window in a host's week, in Danish time: "Tuesday 10–16".</summary>
public sealed class HostAvailability
{
    private HostAvailability() { }

    public Guid Id { get; private set; }
    public Guid HostId { get; private set; }
    public DayOfWeek Day { get; private set; }
    public TimeOnly From { get; private set; }
    public TimeOnly To { get; private set; }

    public static HostAvailability Create(Guid hostId, DayOfWeek day, TimeOnly from, TimeOnly to)
    {
        if (to <= from) throw new DomainException("Et tidsrum skal slutte efter, det begynder.");
        return new HostAvailability { Id = Guid.CreateVersion7(), HostId = hostId, Day = day, From = from, To = to };
    }
}

/// <summary>Time that cannot be booked: holidays, other events. Without a host it closes the whole estate.</summary>
public sealed class CalendarBlock
{
    private CalendarBlock() { }

    public Guid Id { get; private set; }
    public DateTimeOffset Start { get; private set; }
    public DateTimeOffset End { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid? HostId { get; private set; }

    public Interval Interval => new(Start, End);

    public static CalendarBlock Create(DateTimeOffset start, DateTimeOffset end, string reason, Guid? hostId)
    {
        if (end <= start) throw new DomainException("En blokering skal slutte efter, den begynder.");
        return new CalendarBlock { Id = Guid.CreateVersion7(), Start = start.ToUniversalTime(), End = end.ToUniversalTime(), Reason = Text.Required(reason, "Skriv, hvorfor tiden er blokeret."), HostId = hostId };
    }
}

/// <summary>Finding the time. Pure: the week, what is taken and the clock in — free times out.</summary>
public static class AvailableTime
{
    /// <summary>A viewing takes an hour (§11.10).</summary>
    public static readonly TimeSpan ViewingLength = TimeSpan.FromHours(1);

    /// <summary>The season: weekends in May, June, August and September. July is closed.</summary>
    public static bool InSeason(DateOnly date) => date.Month is 5 or 6 or 8 or 9;

    /// <summary>
    /// The next free viewing times: the host's week minus everything taken, whole hours from the
    /// start of each window, never in the past.
    /// </summary>
    public static IReadOnlyList<Interval> NextViewings(
        DateTimeOffset now, IEnumerable<HostAvailability> week, IEnumerable<Interval> taken, TimeZoneInfo zone,
        int count = 5, int horizonDays = 120)
    {
        var windows = week.ToList();
        var busy = taken.ToList();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var free = new List<Interval>();

        for (var offset = 0; offset < horizonDays && free.Count < count; offset++)
        {
            var day = today.AddDays(offset);
            foreach (var window in windows.Where(w => w.Day == day.DayOfWeek).OrderBy(w => w.From))
            {
                for (var start = window.From.ToTimeSpan(); start + ViewingLength <= window.To.ToTimeSpan() && free.Count < count; start += ViewingLength)
                {
                    var local = day.ToDateTime(TimeOnly.FromTimeSpan(start));
                    var slotStart = new DateTimeOffset(local, zone.GetUtcOffset(local));
                    var slot = new Interval(slotStart, slotStart + ViewingLength);
                    if (slot.Start > now && !busy.Any(slot.Overlaps)) free.Add(slot);
                }
            }
        }

        return free;
    }

    /// <summary>Saturdays in the season between two dates that no secured booking holds (§11.14).</summary>
    public static IEnumerable<DateOnly> WeddingDates(DateOnly from, DateOnly to, IReadOnlySet<DateOnly> secured)
    {
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (day.DayOfWeek == DayOfWeek.Saturday && InSeason(day) && !secured.Contains(day)) yield return day;
        }
    }
}
