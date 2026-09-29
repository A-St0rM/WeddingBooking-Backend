using WeddingBooking.Domain.Catalogue;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Customers;
using WeddingBooking.Domain.Pricing;

namespace WeddingBooking.Domain.Bookings;

/// <summary>
/// One wedding. The guest count lives here and nowhere else: every price, every
/// instalment and the kitchen's cover count is derived from it when read.
/// </summary>
public sealed class Booking
{
    /// <summary>The barn seats 350.</summary>
    public const int MaxGuests = 350;

    private readonly List<BookingLine> _lines = [];

    private Booking() { }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;

    public DateOnly EventDate { get; private set; }
    public int GuestCount { get; private set; }
    public PaymentTerms PaymentTerms { get; private set; }
    public SalesStage Stage { get; private set; }

    public IReadOnlyList<BookingLine> Lines => _lines;

    /// <summary>The small-wedding surcharge as it stood when the booking was made.</summary>
    public SurchargeRule Surcharge { get; private set; } = SurchargeRule.None;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Postgres' xmin. Two people changing the guest count at once get a conflict, not a silent overwrite.</summary>
    public uint Version { get; private set; }

    public static Booking Create(
        Customer customer, DateOnly eventDate, int guestCount, PaymentTerms terms = PaymentTerms.New, SurchargeRule? surcharge = null)
    {
        var now = DateTimeOffset.UtcNow;
        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customer.Id,
            Customer = customer,
            Stage = SalesStage.Inquiry,
            PaymentTerms = terms,
            Surcharge = (surcharge ?? SurchargeRule.None).Validated(),
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

    /// <summary>What the booking costs right now. Worked out, never stored.</summary>
    public PriceBreakdown Price() =>
        PriceCalculator.Calculate(GuestCount, OrderedLines.Select(line => (line.Id, line.Spec)), Surcharge);

    public IEnumerable<BookingLine> OrderedLines => _lines.OrderBy(line => line.SortOrder);

    /// <summary>Copies the package's lines and prices onto the booking. Later changes to the package do not reach them.</summary>
    public IReadOnlyList<BookingLine> AddPackage(Package package)
    {
        if (!package.IsActive) throw new DomainException("Pakken er udgået og kan ikke lægges på en booking.");
        var added = package.Lines.OrderBy(line => line.SortOrder)
            .Select(line => BookingLine.Create(Id, line.Spec, NextSortOrder(), package))
            .Select(line => { _lines.Add(line); return line; })
            .ToList();
        Touch();
        return added;
    }

    public BookingLine AddLine(LineSpec spec)
    {
        var line = BookingLine.Create(Id, spec, NextSortOrder());
        _lines.Add(line);
        Touch();
        return line;
    }

    public void UpdateLine(Guid lineId, LineSpec spec)
    {
        FindLine(lineId).Apply(spec);
        Touch();
    }

    public void RemoveLine(Guid lineId)
    {
        _lines.Remove(FindLine(lineId));
        Touch();
    }

    private int NextSortOrder() => _lines.Count == 0 ? 0 : _lines.Max(line => line.SortOrder) + 1;

    private BookingLine FindLine(Guid lineId) =>
        _lines.FirstOrDefault(line => line.Id == lineId) ?? throw new DomainException("Linjen findes ikke på bookingen.");

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
