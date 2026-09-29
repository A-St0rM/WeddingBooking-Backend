namespace WeddingBooking.Domain.Bookings;

/// <summary>
/// How far the sale has come. This is stored; everything after it — date
/// secured, ready for the event, held — is derived when read.
/// </summary>
public enum SalesStage
{
    /// <summary>Came in from the website and waits for the event manager. Holds its time or date meanwhile.</summary>
    Requested,

    Inquiry,
    Viewing,
    QuoteSent,

    /// <summary>The signed agreement has come back. Set by uploading it, not by hand.</summary>
    Signed,

    Cancelled,
    Lost,
}

public static class SalesStages
{
    /// <summary>The stages someone may choose. Signed follows from the signed agreement.</summary>
    public static bool IsManual(this SalesStage stage) => stage is not (SalesStage.Signed or SalesStage.Requested);

    /// <summary>A booking that no longer holds its date.</summary>
    public static bool IsClosed(this SalesStage stage) => stage is SalesStage.Cancelled or SalesStage.Lost;
}

/// <summary>What the case is: a viewing that may become a wedding, or the wedding itself.</summary>
public enum BookingKind
{
    Viewing,
    Wedding,
}

/// <summary>Where a case came from.</summary>
public enum BookingOrigin
{
    Staff,
    Website,
    Trello,
}
