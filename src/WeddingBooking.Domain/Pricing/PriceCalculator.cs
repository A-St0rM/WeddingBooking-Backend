using WeddingBooking.Domain.Catalogue;

namespace WeddingBooking.Domain.Pricing;

public sealed record PricedLine(Guid LineId, decimal UnitPrice, int Units, decimal Total);

public sealed record PriceBreakdown(IReadOnlyList<PricedLine> Lines, decimal Subtotal, decimal Surcharge, decimal Total);

/// <summary>
/// What a booking costs, worked out from its guest count and its lines every time
/// it is read. Nothing here is stored: a stored total is how 80 and 94 guests
/// come to exist side by side.
/// </summary>
public static class PriceCalculator
{
    public static int Units(LinePricing pricing, int quantity, int guestCount) =>
        pricing == LinePricing.PerGuest ? guestCount : quantity;

    public static PriceBreakdown Calculate(int guestCount, IEnumerable<(Guid Id, LineSpec Spec)> lines, SurchargeRule surcharge)
    {
        var priced = lines
            .Select(line =>
            {
                var units = Units(line.Spec.Pricing, line.Spec.Quantity, guestCount);
                return new PricedLine(line.Id, line.Spec.UnitPrice, units, line.Spec.UnitPrice * units);
            })
            .ToList();

        var subtotal = priced.Sum(line => line.Total);
        // The surcharge goes on top of the price; a booking with nothing on it yet has no price to go on top of.
        var extra = priced.Count > 0 && surcharge.AppliesTo(guestCount) ? surcharge.Amount : 0m;
        return new PriceBreakdown(priced, subtotal, extra, subtotal + extra);
    }
}
