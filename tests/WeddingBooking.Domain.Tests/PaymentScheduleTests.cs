using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Payments;

namespace WeddingBooking.Domain.Tests;

public sealed class PaymentScheduleTests
{
    private static readonly DateOnly Wedding = new(2027, 6, 12);
    private static readonly DateOnly Booked = new(2026, 9, 25);

    [Theory]
    //          terms              total     fee     33 %     final
    [InlineData(PaymentTerms.New, 119_600, 25_000, 39_468,  55_132)]  // 80 × 1.495
    [InlineData(PaymentTerms.New, 140_530, 25_000, 46_375,  69_155)]  // 94 × 1.495; 46.374,9 rounds up
    [InlineData(PaymentTerms.Old, 119_600, 25_000,      0,  94_600)]
    [InlineData(PaymentTerms.New,  20_000, 20_000,      0,       0)]  // less than the fee: the fee is all there is
    [InlineData(PaymentTerms.New,       0,      0,      0,       0)]  // no price yet
    public void Instalments_are_the_rule(PaymentTerms terms, decimal total, decimal fee, decimal share, decimal final)
    {
        var schedule = PaymentSchedule.For(terms, total, Wedding, Booked);

        Assert.Equal(fee, Amount(schedule, InstalmentKind.ReservationFee));
        Assert.Equal(share, Amount(schedule, InstalmentKind.Instalment));
        Assert.Equal(final, Amount(schedule, InstalmentKind.FinalPayment));
    }

    [Fact]
    public void Instalments_add_up_to_the_total_for_every_guest_count_and_both_terms()
    {
        foreach (var terms in Enum.GetValues<PaymentTerms>())
        {
            for (var guests = 1; guests <= 350; guests++)
            {
                var booking = TestData.Booking(guests, Wedding, terms);
                booking.AddPackage(TestData.BasePackage2026());
                var total = booking.Price().Total;

                var schedule = PaymentSchedule.For(terms, total, Wedding, Booked);

                Assert.Equal(total, schedule.Sum(payment => payment.Amount));
                Assert.All(schedule, payment => Assert.True(payment.Amount >= 0, $"{terms} {guests}: {payment}"));
            }
        }
    }

    [Fact]
    public void A_later_guest_change_lands_on_the_final_payment_once_the_33_percent_is_invoiced()
    {
        var invoiced = new Dictionary<InstalmentKind, decimal>
        {
            [InstalmentKind.ReservationFee] = 25_000,
            [InstalmentKind.Instalment] = 39_468, // invoiced at 80 guests
        };

        var schedule = PaymentSchedule.For(PaymentTerms.New, 140_530, Wedding, Booked, invoiced: invoiced); // now 94

        Assert.Equal(39_468, Amount(schedule, InstalmentKind.Instalment));
        Assert.Equal(140_530 - 25_000 - 39_468, Amount(schedule, InstalmentKind.FinalPayment));
        Assert.Equal(140_530, schedule.Sum(payment => payment.Amount));
    }

    [Fact]
    public void Due_dates_follow_the_wedding()
    {
        var schedule = PaymentSchedule.For(PaymentTerms.New, 119_600, Wedding, Booked);

        Assert.Equal(Booked.AddDays(8), Due(schedule, InstalmentKind.ReservationFee));
        Assert.Equal(new DateOnly(2026, 12, 12), Due(schedule, InstalmentKind.Instalment));
        Assert.Equal(new DateOnly(2027, 5, 13), Due(schedule, InstalmentKind.FinalPayment));
    }

    [Fact]
    public void Booked_less_than_six_months_ahead_the_33_percent_falls_due_at_signing()
    {
        var booked = new DateOnly(2027, 3, 1);
        var signed = new DateOnly(2027, 3, 10);

        Assert.Equal(booked, Due(PaymentSchedule.For(PaymentTerms.New, 119_600, Wedding, booked), InstalmentKind.Instalment));
        Assert.Equal(signed, Due(PaymentSchedule.For(PaymentTerms.New, 119_600, Wedding, booked, signed), InstalmentKind.Instalment));
    }

    private static decimal Amount(IReadOnlyList<ScheduledPayment> schedule, InstalmentKind kind) =>
        schedule.FirstOrDefault(payment => payment.Kind == kind)?.Amount ?? 0;

    private static DateOnly Due(IReadOnlyList<ScheduledPayment> schedule, InstalmentKind kind) =>
        schedule.Single(payment => payment.Kind == kind).DueDate;
}
