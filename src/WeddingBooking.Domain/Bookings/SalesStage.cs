namespace WeddingBooking.Domain.Bookings;

/// <summary>
/// How far the sale has come. This is stored; everything after it — date
/// secured, ready for the event, held — is derived when read.
/// </summary>
public enum SalesStage
{
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
    public static bool IsManual(this SalesStage stage) => stage != SalesStage.Signed;

    /// <summary>A booking that no longer holds its date.</summary>
    public static bool IsClosed(this SalesStage stage) => stage is SalesStage.Cancelled or SalesStage.Lost;
}
