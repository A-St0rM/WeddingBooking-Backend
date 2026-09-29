using WeddingBooking.Domain.Calendar;

namespace WeddingBooking.Domain.Tests;

public sealed class AvailableTimeTests
{
    private static readonly TimeZoneInfo Denmark = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    private static readonly Guid Bryan = Guid.NewGuid();
    private static readonly HostAvailability[] Week =
    [
        HostAvailability.Create(Bryan, DayOfWeek.Tuesday, new(10, 0), new(13, 0)),
        HostAvailability.Create(Bryan, DayOfWeek.Thursday, new(14, 0), new(16, 0)),
    ];

    // Monday 1 March 2027, 09.00 in Denmark (UTC+1).
    private static readonly DateTimeOffset Monday = new(2027, 3, 1, 9, 0, 0, TimeSpan.FromHours(1));

    private static DateTimeOffset At(int day, int hour) => new(2027, 3, day, hour, 0, 0, TimeSpan.FromHours(1));

    [Fact]
    public void The_next_five_times_follow_the_week_in_whole_hours()
    {
        var free = AvailableTime.NextViewings(Monday, Week, [], Denmark);

        Assert.Equal([At(2, 10), At(2, 11), At(2, 12), At(4, 14), At(4, 15)], free.Select(s => s.Start));
    }

    [Fact]
    public void Nothing_offered_overlaps_what_is_taken()
    {
        var taken = new Interval[]
        {
            new(At(2, 11), At(2, 12)),                 // a viewing
            new(At(4, 0), At(5, 0)),                   // a blocked day
        };

        var free = AvailableTime.NextViewings(Monday, Week, taken, Denmark);

        Assert.Equal([At(2, 10), At(2, 12), At(9, 10), At(9, 11), At(9, 12)], free.Select(s => s.Start));
        Assert.DoesNotContain(free, slot => taken.Any(slot.Overlaps));
    }

    [Fact]
    public void A_time_already_passed_today_is_not_offered()
    {
        var tuesdayNoon = new DateTimeOffset(2027, 3, 2, 11, 30, 0, TimeSpan.FromHours(1));
        Assert.Equal(At(2, 12), AvailableTime.NextViewings(tuesdayNoon, Week, [], Denmark)[0].Start);
    }

    [Fact]
    public void Wedding_dates_are_saturdays_in_the_season_that_nobody_has_secured()
    {
        var dates = AvailableTime.WeddingDates(new(2027, 6, 20), new(2027, 8, 10), new HashSet<DateOnly> { new(2027, 8, 7) }).ToList();

        Assert.Equal([new DateOnly(2027, 6, 26)], dates); // July is closed, 7 August is taken
    }
}
