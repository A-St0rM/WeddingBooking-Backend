namespace WeddingBooking.Domain.Bookings;

/// <summary>The one word staff see. Derived, never stored.</summary>
public enum DisplayStatus
{
    Requested,
    Inquiry,
    Viewing,
    QuoteSent,
    Signed,
    DateSecured,
    ReadyForEvent,
    Held,
    AwaitingPostEventInvoice,
    PaymentOverdue,
    Cancelled,
    Lost,
}

/// <summary>What the status is worked out from. Payment facts come from the accounting system.</summary>
public sealed record StatusFacts(
    SalesStage Stage,
    DateOnly EventDate,
    DateOnly Today,
    bool ReservationFeePaid = false,
    bool AnyInstalmentOverdue = false,
    bool AllReadinessDone = false,
    bool PostEventOutstanding = false);

public static class BookingStatus
{
    /// <summary>The most pressing thing wins.</summary>
    public static DisplayStatus Derive(StatusFacts facts)
    {
        if (facts.Stage == SalesStage.Cancelled) return DisplayStatus.Cancelled;
        if (facts.Stage == SalesStage.Lost) return DisplayStatus.Lost;
        if (facts.Stage == SalesStage.Requested) return DisplayStatus.Requested;
        if (facts.AnyInstalmentOverdue) return DisplayStatus.PaymentOverdue;
        if (facts.EventDate < facts.Today)
        {
            return facts.PostEventOutstanding ? DisplayStatus.AwaitingPostEventInvoice : DisplayStatus.Held;
        }
        if (facts.AllReadinessDone) return DisplayStatus.ReadyForEvent;
        if (facts.ReservationFeePaid) return DisplayStatus.DateSecured;

        return facts.Stage switch
        {
            SalesStage.Viewing => DisplayStatus.Viewing,
            SalesStage.QuoteSent => DisplayStatus.QuoteSent,
            SalesStage.Signed => DisplayStatus.Signed,
            _ => DisplayStatus.Inquiry,
        };
    }
}
