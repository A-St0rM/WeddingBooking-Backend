using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Customers;

namespace WeddingBooking.Domain.Bookings;

/// <summary>
/// One wedding. The guest count lives here and nowhere else: every price, every
/// instalment and the kitchen's cover count is derived from it when read.
/// </summary>
public sealed class Booking
{
    /// <summary>The barn seats 350.</summary>
    public const int MaxGuests = 350;

    private Booking() { }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;

    public DateOnly EventDate { get; private set; }
    public int GuestCount { get; private set; }
    public PaymentTerms PaymentTerms { get; private set; }
    public SalesStage Stage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Postgres' xmin. Two people changing the guest count at once get a conflict, not a silent overwrite.</summary>
    public uint Version { get; private set; }

    public static Booking Create(Customer customer, DateOnly eventDate, int guestCount, PaymentTerms terms = PaymentTerms.New)
    {
        var now = DateTimeOffset.UtcNow;
        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customer.Id,
            Customer = customer,
            Stage = SalesStage.Inquiry,
            PaymentTerms = terms,
            CreatedAt = now,
            UpdatedAt = now,
        };
        booking.Change(eventDate, guestCount, terms);
        return booking;
    }

    public void Change(DateOnly eventDate, int guestCount, PaymentTerms terms)
    {
        if (eventDate == default)
        {
            throw new DomainException("Bookingen skal have en dato.");
        }

        if (guestCount is < 1 or > MaxGuests)
        {
            throw new DomainException($"Gæsteantallet skal være mellem 1 og {MaxGuests}.");
        }

        EventDate = eventDate;
        GuestCount = guestCount;
        PaymentTerms = terms;
        Touch();
    }

    public void MoveTo(SalesStage stage)
    {
        if (!stage.IsManual())
        {
            throw new DomainException("En booking bliver underskrevet ved at uploade den underskrevne aftale.");
        }

        Stage = stage;
        Touch();
    }

    /// <summary>The signed agreement has come back.</summary>
    public void MarkSigned()
    {
        if (Stage.IsClosed())
        {
            throw new DomainException("En afbestilt eller tabt booking kan ikke underskrives.");
        }

        Stage = SalesStage.Signed;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
