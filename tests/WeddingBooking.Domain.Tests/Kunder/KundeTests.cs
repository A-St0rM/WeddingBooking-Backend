using Shouldly;
using WeddingBooking.Domain.Kunder;

namespace WeddingBooking.Domain.Tests.Kunder;

public sealed class KundeTests
{
    private static Kunde EtBrudepar() =>
        Kunde.Create("Anna Nielsen & Mikkel Sørensen", "Slotsalléen 1", "4900", "Nakskov");

    [Fact]
    public void En_kunde_skal_have_et_navn()
    {
        Should.Throw<ArgumentException>(() => Kunde.Create("   "));
    }

    [Fact]
    public void Navn_og_adresse_trimmes()
    {
        var kunde = Kunde.Create("  Anna & Mikkel  ", "  Slotsalléen 1  ", "  4900 ", "  Nakskov ");

        kunde.Navn.ShouldBe("Anna & Mikkel");
        kunde.Adresse.ShouldBe("Slotsalléen 1");
        kunde.Postnr.ShouldBe("4900");
        kunde.By.ShouldBe("Nakskov");
    }

    [Fact]
    public void En_kunde_uden_kontaktpersoner_har_ingen_at_skrive_til()
    {
        EtBrudepar().PrimærKontakt.ShouldBeNull();
    }

    [Fact]
    public void Den_foerste_kontaktperson_bliver_primaer_af_sig_selv()
    {
        var kunde = EtBrudepar();

        var anna = kunde.TilføjKontaktperson("Anna Nielsen", "anna@example.dk", "12345678");

        anna.ErPrimær.ShouldBeTrue();
        kunde.PrimærKontakt.ShouldBe(anna);
    }

    [Fact]
    public void Den_anden_kontaktperson_bliver_ikke_primaer()
    {
        var kunde = EtBrudepar();
        kunde.TilføjKontaktperson("Anna Nielsen", "anna@example.dk", null);

        var mikkel = kunde.TilføjKontaktperson("Mikkel Sørensen", "mikkel@example.dk", null);

        mikkel.ErPrimær.ShouldBeFalse();
        kunde.Kontaktpersoner.Count.ShouldBe(2);
    }

    [Fact]
    public void Der_er_altid_hoejst_en_primaer_kontakt()
    {
        var kunde = EtBrudepar();
        var anna = kunde.TilføjKontaktperson("Anna Nielsen", null, null);
        var mikkel = kunde.TilføjKontaktperson("Mikkel Sørensen", null, null);

        kunde.GørPrimær(mikkel.Id);

        mikkel.ErPrimær.ShouldBeTrue();
        anna.ErPrimær.ShouldBeFalse();
        kunde.Kontaktpersoner.Count(k => k.ErPrimær).ShouldBe(1);
    }

    [Fact]
    public void En_kontaktperson_fra_en_anden_kunde_kan_ikke_goeres_primaer()
    {
        var kunde = EtBrudepar();
        kunde.TilføjKontaktperson("Anna Nielsen", null, null);

        Should.Throw<InvalidOperationException>(() => kunde.GørPrimær(Guid.CreateVersion7()));
    }

    [Fact]
    public void Fjernes_den_primaere_overtager_en_anden_rollen()
    {
        var kunde = EtBrudepar();
        var anna = kunde.TilføjKontaktperson("Anna Nielsen", null, null);
        var mikkel = kunde.TilføjKontaktperson("Mikkel Sørensen", null, null);

        kunde.FjernKontaktperson(anna.Id);

        kunde.Kontaktpersoner.ShouldHaveSingleItem();
        mikkel.ErPrimær.ShouldBeTrue("der skal altid være nogen at skrive til");
    }

    [Fact]
    public void En_kontaktperson_skal_have_et_navn()
    {
        Should.Throw<ArgumentException>(() => EtBrudepar().TilføjKontaktperson("  ", null, null));
    }

    [Fact]
    public void Tomme_kontaktoplysninger_bliver_til_ingenting_frem_for_tom_tekst()
    {
        var kontakt = EtBrudepar().TilføjKontaktperson("Anna", "   ", "");

        kontakt.Email.ShouldBeNull();
        kontakt.Telefon.ShouldBeNull();
    }
}
