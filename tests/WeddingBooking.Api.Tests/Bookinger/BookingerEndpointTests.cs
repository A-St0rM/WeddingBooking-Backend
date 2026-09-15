using System.Net;
using System.Net.Http.Json;
using Shouldly;
using WeddingBooking.Api.Contracts;
using WeddingBooking.Api.Tests.Fixtures;

namespace WeddingBooking.Api.Tests.Bookinger;

public sealed class BookingerEndpointTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [RequiresDatabaseFact]
    public async Task En_oprettet_booking_kan_findes_i_listen()
    {
        await api.ResetAsync();

        var oprettet = await OpretAsync(new OpretBookingRequest(
            "Anna Nielsen & Mikkel Sørensen", new DateOnly(2027, 8, 14), 94));

        var listen = await HentListenAsync();

        listen.ShouldHaveSingleItem();
        listen[0].Id.ShouldBe(oprettet.Id);
        listen[0].Kundenavn.ShouldBe("Anna Nielsen & Mikkel Sørensen");
        listen[0].Bryllupsdato.ShouldBe(new DateOnly(2027, 8, 14));
        listen[0].Gæsteantal.ShouldBe(94);
    }

    [RequiresDatabaseFact]
    public async Task En_booking_kan_oprettes_med_kun_et_navn()
    {
        await api.ResetAsync();

        var oprettet = await OpretAsync(new OpretBookingRequest("Anna & Mikkel", null, null));

        oprettet.Bryllupsdato.ShouldBeNull();
        oprettet.Gæsteantal.ShouldBeNull();
        (await HentListenAsync()).ShouldHaveSingleItem();
    }

    [RequiresDatabaseFact]
    public async Task Listen_viser_naermeste_bryllup_foerst_og_bookinger_uden_dato_sidst()
    {
        await api.ResetAsync();

        await OpretAsync(new OpretBookingRequest("Uden dato", null, null));
        await OpretAsync(new OpretBookingRequest("Sidst", new DateOnly(2027, 9, 4), 80));
        await OpretAsync(new OpretBookingRequest("Først", new DateOnly(2026, 5, 30), 110));

        var listen = await HentListenAsync();

        listen.Select(booking => booking.Kundenavn).ShouldBe(["Først", "Sidst", "Uden dato"]);
    }

    [RequiresDatabaseFact]
    public async Task Filtrering_paa_dato_finder_de_bookinger_der_allerede_ligger_der()
    {
        await api.ResetAsync();

        var optagetDato = new DateOnly(2027, 8, 14);
        await OpretAsync(new OpretBookingRequest("Allerede booket", optagetDato, 80));
        await OpretAsync(new OpretBookingRequest("En anden dag", new DateOnly(2027, 8, 21), 80));

        var paaDatoen = await HentListenAsync($"?bryllupsdato={optagetDato:yyyy-MM-dd}");

        paaDatoen.ShouldHaveSingleItem();
        paaDatoen[0].Kundenavn.ShouldBe("Allerede booket");
    }

    [RequiresDatabaseFact]
    public async Task En_booking_kan_hentes_enkeltvis_og_ukendte_id_er_giver_404()
    {
        await api.ResetAsync();

        var oprettet = await OpretAsync(new OpretBookingRequest("Anna & Mikkel", null, 80));

        var fundet = await api.Client.GetFromJsonAsync<BookingResponse>($"/api/bookinger/{oprettet.Id}");
        fundet!.Id.ShouldBe(oprettet.Id);

        var ukendt = await api.Client.GetAsync($"/api/bookinger/{Guid.NewGuid()}");
        ukendt.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <remarks>Runs without a database: rejected before anything is saved.</remarks>
    [Fact]
    public async Task Et_negativt_gaesteantal_afvises()
    {
        var svar = await api.Client.PostAsJsonAsync(
            "/api/bookinger", new OpretBookingRequest("Anna & Mikkel", null, -5));

        svar.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<BookingResponse> OpretAsync(OpretBookingRequest request)
    {
        var svar = await api.Client.PostAsJsonAsync("/api/bookinger", request);
        svar.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await svar.Content.ReadFromJsonAsync<BookingResponse>())!;
    }

    private async Task<IReadOnlyList<BookingResponse>> HentListenAsync(string query = "") =>
        (await api.Client.GetFromJsonAsync<List<BookingResponse>>($"/api/bookinger{query}"))!;
}
