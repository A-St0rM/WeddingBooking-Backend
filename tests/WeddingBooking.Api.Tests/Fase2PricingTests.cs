using System.Text.Json.Nodes;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase2PricingTests(ApiFactory api)
{
    private HttpClient Manager => api.ClientFor(ApiFactory.ManagerEmail);

    private async Task<JsonNode> BaseAsync() =>
        (await Manager.GetJsonAsync("/api/packages?year=2026")).AsArray().First(p => p!["kind"].Str() == "Base")!;

    private async Task<JsonNode> BookingWithBaseAsync(string name, int guests, string packageId)
    {
        var booking = await Manager.PostJsonAsync("/api/bookings", new { customerName = name, eventDate = "2027-09-04", guestCount = guests });
        return await Manager.PostJsonAsync($"/api/bookings/{booking["id"].Str()}/packages", new { packageId });
    }

    [IntegrationFact]
    public async Task Eighty_to_ninety_four_changes_every_per_guest_line_and_the_total_in_one_edit()
    {
        var pkg = await BaseAsync();
        var booking = await BookingWithBaseAsync("Pris 80", 80, pkg["id"].Str());
        Assert.Equal(80 * 1495m, booking["price"]!["total"].Dec());

        var changed = await Manager.PutJsonAsync($"/api/bookings/{booking["id"].Str()}",
            new { eventDate = "2027-09-04", guestCount = 94, paymentTerms = "New", version = booking["version"]!.GetValue<uint>() });

        Assert.Equal(94, changed["price"]!["lines"]![0]!["units"]!.GetValue<int>());
        Assert.Equal(94 * 1495m, changed["price"]!["total"].Dec());
    }

    [IntegrationFact]
    public async Task A_changed_package_price_leaves_existing_bookings_unchanged()
    {
        var created = await Manager.PostJsonAsync("/api/packages", new
        {
            name = "Testpakke", kind = "Base", year = 2031, description = (string?)null,
            lines = new[] { new { description = "Menu", group = "DinnerAndParty", pricing = "PerGuest", unitPrice = 1000m, quantity = 1, minimumQuantity = (int?)null } },
        });
        var booking = await BookingWithBaseAsync("Pris fast", 70, created["id"].Str());

        await Manager.PutJsonAsync($"/api/packages/{created["id"].Str()}", new
        {
            name = "Testpakke", kind = "Base", year = 2031, description = (string?)null,
            lines = new[] { new { description = "Menu", group = "DinnerAndParty", pricing = "PerGuest", unitPrice = 1200m, quantity = 1, minimumQuantity = (int?)null } },
        });

        var after = await Manager.GetJsonAsync($"/api/bookings/{booking["id"].Str()}");
        Assert.Equal(70 * 1000m, after["price"]!["total"].Dec());
    }

    [IntegrationFact]
    public async Task Fifty_five_guests_show_the_surcharge_and_sixty_do_not()
    {
        var pkg = await BaseAsync();
        var small = await BookingWithBaseAsync("Lille", 55, pkg["id"].Str());
        var sixty = await BookingWithBaseAsync("Tres", 60, pkg["id"].Str());

        Assert.True(small["price"]!["surcharge"]!["applies"]!.GetValue<bool>());
        Assert.Equal(55 * 1495m + small["price"]!["surcharge"]!["amount"].Dec(), small["price"]!["total"].Dec());
        Assert.False(sixty["price"]!["surcharge"]!["applies"]!.GetValue<bool>());
        Assert.Equal(60 * 1495m, sixty["price"]!["total"].Dec());
    }
}
