using WeddingBooking.Domain.Catalogue;

namespace WeddingBooking.Domain.Bookings;

/// <summary>
/// A line on one booking — copied from a package or added by hand. The price is
/// the booking's own copy; the total is never stored, it follows the guest count.
/// </summary>
public sealed class BookingLine
{
    private BookingLine() { }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public ProductGroup Group { get; private set; }
    public LinePricing Pricing { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public int? MinimumQuantity { get; private set; }
    public int SortOrder { get; private set; }

    /// <summary>Where the line came from, for the record. The line does not follow the package afterwards.</summary>
    public Guid? SourcePackageId { get; private set; }
    public string? SourcePackageName { get; private set; }

    public LineSpec Spec => new(Description, Group, Pricing, UnitPrice, Quantity, MinimumQuantity);

    internal static BookingLine Create(Guid bookingId, LineSpec spec, int sortOrder, Package? source = null)
    {
        var line = new BookingLine
        {
            Id = Guid.CreateVersion7(),
            BookingId = bookingId,
            SortOrder = sortOrder,
            SourcePackageId = source?.Id,
            SourcePackageName = source?.Name,
        };
        line.Apply(spec);
        return line;
    }

    internal void Apply(LineSpec spec)
    {
        var valid = spec.Validated();
        Description = valid.Description;
        Group = valid.Group;
        Pricing = valid.Pricing;
        UnitPrice = valid.UnitPrice;
        Quantity = valid.Quantity;
        MinimumQuantity = valid.MinimumQuantity;
    }
}
