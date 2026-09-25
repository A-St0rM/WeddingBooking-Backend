using WeddingBooking.Domain.Bookings;

namespace WeddingBooking.Domain.Payments;

public enum InstalmentKind
{
    /// <summary>25.000 kr. at booking. Secures the date; deducted from the final payment; kept on cancellation.</summary>
    ReservationFee,

    /// <summary>33 % of the price, six months before. New terms only.</summary>
    Instalment,

    /// <summary>The rest, 30 days before, so everything is paid before the party starts.</summary>
    FinalPayment,
}

public sealed record ScheduledPayment(InstalmentKind Kind, decimal Amount, DateOnly DueDate);

/// <summary>
/// When a couple pays what. One function of the terms, the price and the dates,
/// worked out every time it is read, so the instalments always add up to the
/// price — for every guest count, both sets of terms and after any change.
/// </summary>
public static class PaymentSchedule
{
    public const decimal ReservationFee = 25_000m;
    public const decimal InstalmentShare = 0.33m;
    public const int InstalmentMonthsBefore = 6;
    public const int FinalPaymentDaysBefore = 30;

    /// <summary>A reservation fee invoice is due this many days after booking.</summary>
    public const int ReservationFeeDueDays = 8;

    /// <param name="terms">Which terms the booking was offered on.</param>
    /// <param name="total">The booking's price, from <see cref="Pricing.PriceCalculator"/>.</param>
    /// <param name="eventDate">The wedding day.</param>
    /// <param name="bookedOn">The day the booking was made.</param>
    /// <param name="signedOn">
    /// The day the agreement was signed, if it has been. When the wedding is less than
    /// six months away at booking, the 33 % instalment falls due at signing.
    /// </param>
    /// <param name="invoiced">
    /// Amounts already invoiced, read from the accounting system. An invoice is not
    /// changed after the fact; the final payment absorbs the difference instead, so
    /// the plan still adds up to the price when the guest count changes later.
    /// </param>
    public static IReadOnlyList<ScheduledPayment> For(
        PaymentTerms terms,
        decimal total,
        DateOnly eventDate,
        DateOnly bookedOn,
        DateOnly? signedOn = null,
        IReadOnlyDictionary<InstalmentKind, decimal>? invoiced = null)
    {
        invoiced ??= new Dictionary<InstalmentKind, decimal>();
        var finalDue = eventDate.AddDays(-FinalPaymentDaysBefore);

        var fee = invoiced.TryGetValue(InstalmentKind.ReservationFee, out var invoicedFee)
            ? invoicedFee
            : Math.Min(ReservationFee, Math.Max(total, 0));
        var feeDue = Earliest(bookedOn.AddDays(ReservationFeeDueDays), finalDue);

        var schedule = new List<ScheduledPayment> { new(InstalmentKind.ReservationFee, fee, feeDue) };

        if (terms == PaymentTerms.New)
        {
            var share = invoiced.TryGetValue(InstalmentKind.Instalment, out var invoicedShare)
                ? invoicedShare
                : Math.Clamp(decimal.Round(total * InstalmentShare, 0, MidpointRounding.AwayFromZero), 0, Math.Max(total - fee, 0));

            var sixMonthsBefore = eventDate.AddMonths(-InstalmentMonthsBefore);
            var shareDue = sixMonthsBefore >= bookedOn ? sixMonthsBefore : signedOn ?? bookedOn;
            schedule.Add(new(InstalmentKind.Instalment, share, Earliest(shareDue, finalDue)));
        }

        // The remainder, whatever has gone before. Negative only if the price fell
        // below what was already invoiced — then it is money to pay back.
        var final = total - schedule.Sum(payment => payment.Amount);
        schedule.Add(new(InstalmentKind.FinalPayment, final, finalDue));
        return schedule;
    }

    private static DateOnly Earliest(DateOnly a, DateOnly b) => a < b ? a : b;
}
