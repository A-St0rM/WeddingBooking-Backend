using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Catalogue;

/// <summary>
/// What a priced line is, apart from where it lives — shared by a package's lines
/// and the lines copied onto a booking, so both follow the same rules.
/// </summary>
public sealed record LineSpec(
    string Description,
    ProductGroup Group,
    LinePricing Pricing,
    decimal UnitPrice,
    int Quantity = 1,
    int? MinimumQuantity = null)
{
    /// <summary>The spec, checked and tidied. Throws when a rule is broken.</summary>
    public LineSpec Validated()
    {
        var description = Text.Required(Description, "En linje skal have en beskrivelse.");
        if (UnitPrice < 0) throw new DomainException("En pris kan ikke være negativ.");
        if (decimal.Round(UnitPrice, 2) != UnitPrice) throw new DomainException("En pris har højst to decimaler.");
        if (MinimumQuantity is < 1) throw new DomainException("Et minimum skal være mindst 1.");

        // A per-guest line counts the booking's guests; its own quantity means nothing.
        var quantity = Pricing == LinePricing.PerGuest ? 1 : Quantity;
        if (quantity < 1) throw new DomainException("Antallet skal være mindst 1.");
        if (Pricing == LinePricing.Fixed && MinimumQuantity is { } minimum && quantity < minimum)
        {
            throw new DomainException($"{description} kræver mindst {minimum}.");
        }

        return this with { Description = description, Quantity = quantity };
    }
}
