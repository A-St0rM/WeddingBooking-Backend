using WeddingBooking.Domain.Bookings;

namespace WeddingBooking.Domain.Tests;

public sealed class DisplayStatusTests
{
    private static readonly DateOnly Today = new(2027, 1, 10);
    private static readonly DateOnly Upcoming = new(2027, 6, 12);
    private static readonly DateOnly Past = new(2026, 9, 5);

    public static TheoryData<StatusFacts, DisplayStatus> Cases => new()
    {
        { new(SalesStage.Inquiry, Upcoming, Today), DisplayStatus.Inquiry },
        { new(SalesStage.Viewing, Upcoming, Today), DisplayStatus.Viewing },
        { new(SalesStage.QuoteSent, Upcoming, Today), DisplayStatus.QuoteSent },
        { new(SalesStage.Signed, Upcoming, Today), DisplayStatus.Signed },
        { new(SalesStage.QuoteSent, Upcoming, Today, ReservationFeePaid: true), DisplayStatus.DateSecured },
        { new(SalesStage.Signed, Upcoming, Today, ReservationFeePaid: true, AllReadinessDone: true), DisplayStatus.ReadyForEvent },
        { new(SalesStage.Signed, Past, Today, ReservationFeePaid: true), DisplayStatus.Held },
        { new(SalesStage.Signed, Past, Today, PostEventOutstanding: true), DisplayStatus.AwaitingPostEventInvoice },
        // Overdue beats everything but a closed booking.
        { new(SalesStage.Signed, Upcoming, Today, ReservationFeePaid: true, AnyInstalmentOverdue: true, AllReadinessDone: true), DisplayStatus.PaymentOverdue },
        { new(SalesStage.Cancelled, Upcoming, Today, AnyInstalmentOverdue: true), DisplayStatus.Cancelled },
        { new(SalesStage.Lost, Past, Today), DisplayStatus.Lost },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_most_pressing_status_wins(StatusFacts facts, DisplayStatus expected) =>
        Assert.Equal(expected, BookingStatus.Derive(facts));
}
