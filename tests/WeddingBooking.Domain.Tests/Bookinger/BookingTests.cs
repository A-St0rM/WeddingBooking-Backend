using Shouldly;
using WeddingBooking.Domain.Bookinger;

namespace WeddingBooking.Domain.Tests.Bookinger;

/// <summary>The narrow seam: the domain as plain functions, no database, no HTTP.</summary>
public sealed class BookingTests
{
    private static readonly Guid Brudeparret = Guid.CreateVersion7();
    private static readonly Guid Forældrene = Guid.CreateVersion7();

    [Fact]
    public void En_booking_kan_oprettes_uden_dato_og_uden_gaesteantal()
    {
        var booking = Booking.Create(Brudeparret, null, null);

        booking.Id.ShouldNotBe(Guid.Empty);
        booking.Bryllupsdato.ShouldBeNull();
        booking.Gæsteantal.ShouldBeNull();
    }

    [Fact]
    public void En_booking_skal_hoere_til_en_kunde()
    {
        Should.Throw<ArgumentException>(() => Booking.Create(Guid.Empty, null, null));
    }

    [Fact]
    public void Betaleren_er_som_udgangspunkt_brudeparret_selv()
    {
        var booking = Booking.Create(Brudeparret, null, null);

        booking.BetalerKundeId.ShouldBe(Brudeparret);
        booking.HarSærskiltBetaler.ShouldBeFalse();
    }

    [Fact]
    public void Betaleren_kan_saettes_til_en_anden_naar_foraeldrene_betaler()
    {
        var booking = Booking.Create(Brudeparret, null, null);

        booking.SætBetaler(Forældrene);

        booking.BetalerKundeId.ShouldBe(Forældrene);
        booking.HarSærskiltBetaler.ShouldBeTrue();
        booking.KundeId.ShouldBe(Brudeparret, "bookingen hører stadig til brudeparret");
    }

    [Fact]
    public void Betaleren_kan_gives_tilbage_til_brudeparret()
    {
        var booking = Booking.Create(Brudeparret, null, null);
        booking.SætBetaler(Forældrene);

        booking.NulstilBetaler();

        booking.BetalerKundeId.ShouldBe(Brudeparret);
        booking.HarSærskiltBetaler.ShouldBeFalse();
    }

    [Fact]
    public void Et_ukendt_gaesteantal_er_tilladt()
    {
        Booking.Create(Brudeparret, new DateOnly(2027, 8, 14), null).Gæsteantal.ShouldBeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Et_negativt_gaesteantal_afvises(int gæsteantal)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Booking.Create(Brudeparret, null, gæsteantal));
        Should.Throw<ArgumentOutOfRangeException>(
            () => Booking.Create(Brudeparret, null, null).SætGæsteantal(gæsteantal));
    }

    [Fact]
    public void Nul_gaester_er_ikke_en_fejl()
    {
        Booking.Create(Brudeparret, null, 0).Gæsteantal.ShouldBe(0);
    }
}
