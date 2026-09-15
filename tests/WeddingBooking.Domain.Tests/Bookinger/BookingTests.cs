using Shouldly;
using WeddingBooking.Domain.Bookinger;

namespace WeddingBooking.Domain.Tests.Bookinger;

/// <summary>The narrow seam: the domain as plain functions, no database, no HTTP.</summary>
public sealed class BookingTests
{
    [Fact]
    public void En_booking_kan_oprettes_helt_uden_oplysninger()
    {
        var booking = Booking.Create(null, null, null);

        booking.Id.ShouldNotBe(Guid.Empty);
        booking.Kundenavn.ShouldBeNull();
        booking.Bryllupsdato.ShouldBeNull();
        booking.Gæsteantal.ShouldBeNull();
    }

    [Theory]
    [InlineData("  Anna & Mikkel  ", "Anna & Mikkel")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Kundenavnnavnet_trimmes_og_tomme_navne_bliver_til_ingenting(string? input, string? forventet)
    {
        Booking.Create(input, null, null).Kundenavn.ShouldBe(forventet);
    }

    [Fact]
    public void Et_ukendt_gaesteantal_er_tilladt()
    {
        Booking.Create("Anna & Mikkel", new DateOnly(2027, 8, 14), null).Gæsteantal.ShouldBeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Et_negativt_gaesteantal_afvises(int gæsteantal)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Booking.Create("Anna & Mikkel", null, gæsteantal));
    }

    [Fact]
    public void Nul_gaester_er_ikke_en_fejl()
    {
        Booking.Create("Anna & Mikkel", null, 0).Gæsteantal.ShouldBe(0);
    }

}
