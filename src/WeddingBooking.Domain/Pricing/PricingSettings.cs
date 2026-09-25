namespace WeddingBooking.Domain.Pricing;

/// <summary>
/// The estate's current pricing settings. One row. New bookings copy the
/// surcharge from here; existing bookings keep the one they were made with.
/// </summary>
public sealed class PricingSettings
{
    public const int SingletonId = 1;

    private PricingSettings() { }

    public int Id { get; private set; } = SingletonId;
    public int SmallWeddingThreshold { get; private set; }
    public decimal SmallWeddingSurcharge { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public SurchargeRule Surcharge => new(SmallWeddingThreshold, SmallWeddingSurcharge);

    public static PricingSettings Create(int threshold, decimal amount)
    {
        var settings = new PricingSettings();
        settings.Update(threshold, amount);
        return settings;
    }

    public void Update(int threshold, decimal amount)
    {
        var rule = new SurchargeRule(threshold, amount).Validated();
        SmallWeddingThreshold = rule.Threshold;
        SmallWeddingSurcharge = rule.Amount;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
