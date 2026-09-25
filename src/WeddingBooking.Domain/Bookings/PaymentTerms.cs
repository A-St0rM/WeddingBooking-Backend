namespace WeddingBooking.Domain.Bookings;

/// <summary>
/// Which payment terms a booking was offered on. Both sets exist side by side in
/// the bookings already made, so every booking carries its own.
/// </summary>
public enum PaymentTerms
{
    /// <summary>The full amount 30 days before.</summary>
    Old,

    /// <summary>33 % six months before, the rest 30 days before. Standard from 2026.</summary>
    New,
}
