using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Pricing;

/// <summary>
/// The small-wedding surcharge: a fixed amount on top when there are fewer
/// guests than the threshold, because the estate's start-up costs are the same
/// whatever the size. Copied onto each booking when it is created, like a price.
/// </summary>
public sealed record SurchargeRule(int Threshold, decimal Amount)
{
    public static readonly SurchargeRule None = new(0, 0);

    public bool AppliesTo(int guestCount) => Amount > 0 && guestCount < Threshold;

    public SurchargeRule Validated()
    {
        if (Threshold is < 0 or > 350) throw new DomainException("Grænsen skal være mellem 0 og 350 gæster.");
        if (Amount < 0) throw new DomainException("Tillægget kan ikke være negativt.");
        return this;
    }
}
