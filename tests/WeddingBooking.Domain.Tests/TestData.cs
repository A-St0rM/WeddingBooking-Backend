using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Catalogue;
using WeddingBooking.Domain.Customers;
using WeddingBooking.Domain.Pricing;

namespace WeddingBooking.Domain.Tests;

internal static class TestData
{
    public static readonly SurchargeRule Surcharge = new(60, 7_500m);

    public static Package BasePackage2026() => Package.Create("Grundpakke 2026", PackageKind.Base, 2026, null,
    [
        new LineSpec("Treretters menu med vand, vin og øl ad libitum", ProductGroup.DinnerAndParty, LinePricing.PerGuest, 1_495m),
    ]);

    public static Booking Booking(int guests, DateOnly? date = null, PaymentTerms terms = PaymentTerms.New) =>
        Bookings.Booking.Create(Customer.Create("Tobias og Cecilie"), date ?? new DateOnly(2027, 6, 12), guests, terms, Surcharge);
}
