using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Customers;

namespace WeddingBooking.Domain.Tests;

public sealed class ConfirmAsWeddingTests
{
    private static Booking Viewing() =>
        Booking.Request(Customer.Create(new("Freja"), new("Oscar")), new(2028, 6, 17), 80, null, new DateTimeOffset(2027, 3, 2, 10, 0, 0, TimeSpan.Zero), null);

    [Fact]
    public void A_viewing_becomes_a_wedding_on_the_same_row_with_the_chosen_payer()
    {
        var booking = Viewing();
        var id = booking.Id;
        var oscar = booking.Customer.Partners.Last().Id;

        booking.ConfirmAsWedding(new(2028, 6, 24), 92, PaymentTerms.New, oscar);

        Assert.Equal((id, BookingKind.Wedding, SalesStage.Inquiry), (booking.Id, booking.Kind, booking.Stage));
        Assert.Equal((new DateOnly(2028, 6, 24), 92), (booking.EventDate, booking.GuestCount));
        Assert.Equal("Oscar", booking.Customer.Payer!.FirstName);
    }

    [Fact]
    public void A_wedding_is_not_confirmed_twice_and_a_declined_viewing_is_lost()
    {
        var booking = Viewing();
        booking.ConfirmAsWedding(new(2028, 6, 24), 92, PaymentTerms.New, booking.Customer.Payer!.Id);
        Assert.Throws<DomainException>(() => booking.ConfirmAsWedding(new(2028, 6, 24), 92, PaymentTerms.New, booking.Customer.Payer!.Id));

        var declined = Viewing();
        declined.Decline();
        Assert.Equal(SalesStage.Lost, declined.Stage);
        Assert.Throws<DomainException>(() => declined.Decline());
    }
}
