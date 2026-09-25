using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using WeddingBooking.Domain.Users;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase5RolesTests(ApiFactory api)
{
    private HttpClient Manager => api.ClientFor(ApiFactory.ManagerEmail);

    private static readonly string[] MoneyWords = ["price", "unitprice", "total", "subtotal", "amount", "surcharge", "remaining", "paid"];

    private async Task<string> PricedBookingAsync(string name)
    {
        var pkg = (await Manager.GetJsonAsync("/api/packages?year=2026")).AsArray().First(p => p!["kind"].Str() == "Base")!;
        var booking = await Manager.PostJsonAsync("/api/bookings", new { customerName = name, eventDate = "2027-09-11", guestCount = 55 });
        await Manager.PostJsonAsync($"/api/bookings/{booking["id"].Str()}/packages", new { packageId = pkg["id"].Str() });
        return booking["id"].Str();
    }

    [IntegrationFact]
    public async Task The_chef_gets_answers_where_no_field_holds_a_price_or_an_amount()
    {
        var id = await PricedBookingAsync("Kok Par");
        var chef = await api.StaffAsync("kenneth5@test.dk", Role.Chef);

        var answers = new List<JsonNode>
        {
            await chef.GetJsonAsync($"/api/bookings/{id}"),
            await chef.GetJsonAsync("/api/bookings"),
            await chef.GetJsonAsync($"/api/bookings/{id}/readiness"),
        };
        var detail = answers[0];
        answers.Add(await chef.GetJsonAsync($"/api/customers/{detail["customer"]!["id"].Str()}"));

        foreach (var answer in answers)
        {
            var money = Money(answer).ToList();
            Assert.True(money.Count == 0, $"Pris i svaret til kokken: {string.Join(", ", money)}");
        }

        // The menu is there; the price is not.
        Assert.Contains("Treretters", detail["lines"]![0]!["description"].Str());
        Assert.Equal(HttpStatusCode.Forbidden, (await chef.GetAsync($"/api/bookings/{id}/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await chef.GetAsync($"/api/bookings/{id}/agreement")).StatusCode);
    }

    [IntegrationFact]
    public async Task All_items_done_is_ready_for_the_event_without_anyone_setting_it()
    {
        var id = await PricedBookingAsync("Klar Par");
        var readiness = await Manager.GetJsonAsync($"/api/bookings/{id}/readiness");
        Assert.False(readiness["readyForEvent"]!.GetValue<bool>());

        foreach (var item in readiness["items"]!.AsArray().Where(i => i!["category"].Str() != "PostEventInvoicing"))
        {
            readiness = await Manager.PutJsonAsync($"/api/bookings/{id}/readiness/{item!["id"].Str()}", new { status = "Done", notes = (string?)null, responsible = (string?)null });
        }

        Assert.True(readiness["readyForEvent"]!.GetValue<bool>());
        Assert.Equal("ReadyForEvent", (await Manager.GetJsonAsync($"/api/bookings/{id}"))["status"].Str());
    }

    [IntegrationFact]
    public async Task The_chef_changes_food_and_allergies_but_not_table_setting()
    {
        var id = await PricedBookingAsync("Allergi Par");
        var chef = await api.StaffAsync("kenneth5b@test.dk", Role.Chef);
        var items = (await chef.GetJsonAsync($"/api/bookings/{id}/readiness"))["items"]!.AsArray();
        var food = items.Single(i => i!["category"].Str() == "Food")!;
        var tables = items.Single(i => i!["category"].Str() == "TableSetting")!;
        Assert.True(food["canEdit"]!.GetValue<bool>());
        Assert.False(tables["canEdit"]!.GetValue<bool>());

        var changed = await chef.PutJsonAsync($"/api/bookings/{id}/readiness/{food["id"].Str()}", new { status = "InProgress", notes = "2 vegetarer, 1 glutenfri", responsible = "Kenneth" });
        Assert.Equal("2 vegetarer, 1 glutenfri", changed["items"]!.AsArray().Single(i => i!["category"].Str() == "Food")!["notes"].Str());

        var refused = await chef.PutAsJsonAsync($"/api/bookings/{id}/readiness/{tables["id"].Str()}", new { status = "Done", notes = (string?)null, responsible = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [IntegrationFact]
    public async Task A_user_created_by_the_manager_gets_in_and_a_deactivated_one_does_not()
    {
        var created = await Manager.PostJsonAsync("/api/users", new { email = "Bryan5@Test.dk", name = "Bryan", role = "EventProducer", phone = "22 33 44 55" });
        var bryan = api.ClientFor("bryan5@test.dk");
        Assert.Equal("EventProducer", (await bryan.GetJsonAsync("/api/me"))["role"].Str());

        await Manager.PutJsonAsync($"/api/users/{created["id"].Str()}", new { name = "Bryan", role = "EventProducer", phone = "22 33 44 55", isActive = false });
        Assert.Equal(HttpStatusCode.Forbidden, (await bryan.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bryan.GetAsync("/api/users")).StatusCode);
    }

    private static IEnumerable<string> Money(JsonNode? node, string path = "$") => node switch
    {
        JsonObject o => o.SelectMany(p =>
            (MoneyWords.Contains(p.Key.ToLowerInvariant()) && p.Value is not null ? [$"{path}.{p.Key}"] : Enumerable.Empty<string>())
                .Concat(Money(p.Value, $"{path}.{p.Key}"))),
        JsonArray a => a.SelectMany((item, i) => Money(item, $"{path}[{i}]")),
        _ => [],
    };
}
