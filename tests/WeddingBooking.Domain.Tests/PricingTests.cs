using WeddingBooking.Domain.Catalogue;
using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Tests;

public sealed class PricingTests
{
    [Theory]
    //          guests  late night  expected total
    [InlineData(80,     false,      119_600)]      // 80 × 1.495
    [InlineData(94,     false,      140_530)]      // 94 × 1.495
    [InlineData(94,     true,       149_930)]      // 94 × (1.495 + 100)
    [InlineData(60,     false,       89_700)]      // at the threshold: no surcharge
    [InlineData(59,     false,       95_705)]      // 59 × 1.495 + 7.500
    [InlineData(55,     true,        95_225)]      // 55 × 1.595 + 7.500
    public void Total_follows_the_guest_count(int guests, bool lateNight, decimal expected)
    {
        var booking = TestData.Booking(guests);
        booking.AddPackage(TestData.BasePackage2026());
        if (lateNight) booking.AddLine(new LineSpec("Natmad", ProductGroup.LateNight, LinePricing.PerGuest, 100m));

        Assert.Equal(expected, booking.Price().Total);
    }

    [Fact]
    public void Changing_the_guest_count_once_changes_every_per_guest_line_and_the_total()
    {
        var booking = TestData.Booking(80);
        booking.AddPackage(TestData.BasePackage2026());
        booking.AddLine(new LineSpec("Spiritus og cocktailbar", ProductGroup.Bar, LinePricing.PerGuest, 155m));
        booking.AddLine(new LineSpec("Blomsterdekoration", ProductGroup.Other, LinePricing.Fixed, 4_000m));

        booking.Change(booking.EventDate, 94, booking.PaymentTerms);

        var price = booking.Price();
        Assert.Equal([94 * 1_495m, 94 * 155m, 4_000m], price.Lines.Select(line => line.Total));
        Assert.Equal(94 * 1_495m + 94 * 155m + 4_000m, price.Total);
    }

    [Fact]
    public void A_changed_package_price_leaves_existing_bookings_unchanged()
    {
        var package = TestData.BasePackage2026();
        var booking = TestData.Booking(80);
        booking.AddPackage(package);

        package.Update(package.Name, package.Kind, package.Year, null,
            [package.Lines[0].Spec with { UnitPrice = 1_650m }]);

        Assert.Equal(80 * 1_495m, booking.Price().Total);
    }

    [Fact]
    public void Fixed_line_counts_its_own_quantity_and_holds_its_minimum()
    {
        var friday = new LineSpec("Fredagsarrangement, let grill", ProductGroup.FridayEvent, LinePricing.Fixed, 250m, 35, MinimumQuantity: 30);
        var booking = TestData.Booking(94);
        var line = booking.AddLine(friday);

        Assert.Equal(35 * 250m, booking.Price().Total);
        Assert.Throws<DomainException>(() => booking.UpdateLine(line.Id, friday with { Quantity = 25 }));
    }

    [Fact]
    public void An_empty_booking_has_no_surcharge_to_add()
    {
        Assert.Equal(0m, TestData.Booking(40).Price().Total);
    }

    [Fact]
    public void Next_years_copy_raises_prices_to_whole_kroner()
    {
        var copy = TestData.BasePackage2026().CopyForYear(2027, 4m);

        Assert.Equal("Grundpakke 2027", copy.Name);
        Assert.Equal(1_555m, copy.Lines[0].UnitPrice); // 1.495 × 1,04 = 1.554,80
    }
}
