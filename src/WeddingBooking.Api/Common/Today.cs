namespace WeddingBooking.Api.Common;

internal static class Today
{
    private static readonly TimeZoneInfo Denmark = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    /// <summary>Today on the estate. "Overdue" and "held" turn over at Danish midnight, not UTC's.</summary>
    public static DateOnly In(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Denmark).DateTime);
}
