using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase13ConfirmTests(ApiFactory api)
{
    private HttpClient Manager => api.ClientFor(ApiFactory.ManagerEmail);
    private HttpClient Anonymous => api.CreateClient();

    /// <summary>A viewing booked from the website on a Friday — a day only this host shows round — and accepted.</summary>
    private async Task<(JsonNode Booking, string Start)> AcceptedViewingAsync(string first, string second)
    {
        var users = (await Manager.GetJsonAsync("/api/users")).AsArray();
        var host = users.FirstOrDefault(u => u!["email"].Str() == "fredag@test.dk")
            ?? await Manager.PostJsonAsync("/api/users", new { email = "fredag@test.dk", name = "Fredagsvært", role = "EventProducer", phone = (string?)null });
        await Manager.PutAsJsonAsyncOk($"/api/calendar/availability/{host!["id"].Str()}", new[] { new { day = "Friday", from = "09:00:00", to = "11:00:00" } });

        var start = (await Anonymous.GetJsonAsync("/api/public/viewing-times")).AsArray()
            .Select(t => t!["start"].Str()).First(s => DateTimeOffset.Parse(s).DayOfWeek == DayOfWeek.Friday);
        var people = new object[]
        {
            new { firstName = first, lastName = "Holm", email = $"{first.ToLowerInvariant()}@example.dk", phone = "20304050" },
            new { firstName = second, lastName = "Lund", email = $"{second.ToLowerInvariant()}@example.dk", phone = "30405060" },
        };
        await (await Anonymous.PostAsJsonAsync("/api/public/requests/viewing",
            new { people, start, wishedDate = "2028-08-19", guestCount = 70, consent = true, website = "" })).ReadAsync();

        var summary = (await Manager.GetJsonAsync($"/api/bookings?search={first} og {second}")).AsArray().Single()!;
        var booking = await Manager.PostJsonAsync($"/api/bookings/{summary["id"].Str()}/request/accept");
        return (booking, start);
    }

    [IntegrationFact]
    public async Task A_viewing_confirmed_as_a_booking_keeps_its_customer_people_and_history_and_goes_on_as_any_booking()
    {
        var (viewing, _) = await AcceptedViewingAsync("Alma", "Viggo");
        var id = viewing["id"].Str();
        Assert.Equal("Viewing", viewing["kind"].Str());
        await Manager.PostJsonAsync($"/api/bookings/{id}/notes", new { type = "Call", text = "Fremvisningen gik godt; de vil gerne have 25. august." });
        var viggo = viewing["customer"]!["contacts"]!.AsArray().Single(c => c!["firstName"].Str() == "Viggo")!["id"].Str();

        var wedding = await Manager.PostJsonAsync($"/api/bookings/{id}/confirm-as-wedding",
            new { eventDate = "2028-08-26", guestCount = 96, paymentTerms = "New", payerId = viggo });

        Assert.Equal((id, "Wedding", "Inquiry"), (wedding["id"].Str(), wedding["kind"].Str(), wedding["stage"].Str()));
        Assert.Equal(viewing["customer"]!["id"].Str(), wedding["customer"]!["id"].Str());
        Assert.Equal(["Alma Holm", "Viggo Lund"], wedding["customer"]!["contacts"]!.AsArray().Select(c => c!["fullName"].Str()));
        Assert.Equal("Viggo", wedding["customer"]!["contacts"]!.AsArray().Single(c => c!["isPayer"]!.GetValue<bool>())!["firstName"].Str());
        Assert.Contains((await Manager.GetJsonAsync($"/api/bookings/{id}/notes")).AsArray(), n => n!["text"].Str().Contains("gik godt"));
        Assert.Single((await Manager.GetJsonAsync($"/api/customers/{wedding["customer"]!["id"].Str()}"))["bookings"]!.AsArray());

        // From here on it is a booking like any other.
        var pkg = (await Manager.GetJsonAsync("/api/packages?year=2026")).AsArray().First(p => p!["kind"].Str() == "Base")!;
        var priced = await Manager.PostJsonAsync($"/api/bookings/{id}/packages", new { packageId = pkg["id"].Str() });
        Assert.Equal(96 * 1495m, priced["price"]!["total"].Dec());
        var quote = await Manager.PostJsonAsync($"/api/bookings/{id}/payments/quote");
        Assert.Equal("Viggo Lund", (await Manager.GetJsonAsync("/api/accounting-sim/documents")).AsArray()
            .Single(d => d!["number"].Str() == quote["quote"]!["number"].Str())!["customerName"].Str());
        var invoiced = await Manager.PostJsonAsync($"/api/bookings/{id}/payments/invoices", new { kind = "ReservationFee" });
        Assert.Equal(25_000m, invoiced["instalments"]!.AsArray().Single(i => i!["kind"].Str() == "ReservationFee")!["invoice"]!["amount"].Dec());
        var agreement = await Manager.PostJsonAsync($"/api/bookings/{id}/agreement/versions", new { clarification = (string?)null });
        Assert.Single(agreement["versions"]!.AsArray());
    }

    [IntegrationFact]
    public async Task A_viewing_the_couple_says_no_to_frees_its_time_and_is_lost_with_the_reason()
    {
        var (viewing, start) = await AcceptedViewingAsync("Nej", "Tak");
        var id = viewing["id"].Str();
        Assert.DoesNotContain((await Anonymous.GetJsonAsync("/api/public/viewing-times")).AsArray(), t => t!["start"].Str() == start);

        var lost = await Manager.PostJsonAsync($"/api/bookings/{id}/decline", new { reason = "De har valgt et sted tættere på København." });

        Assert.Equal("Lost", lost["stage"].Str());
        Assert.Contains((await Manager.GetJsonAsync($"/api/bookings/{id}/notes")).AsArray(), n => n!["text"].Str().Contains("tættere på København"));
        Assert.Contains((await Anonymous.GetJsonAsync("/api/public/viewing-times")).AsArray(), t => t!["start"].Str() == start);
        var day = DateTimeOffset.Parse(start).ToString("yyyy-MM-dd");
        Assert.DoesNotContain((await Manager.GetJsonAsync($"/api/calendar?from={day}&to={day}")).AsArray(), e => e!["bookingId"]?.ToString() == id);
    }
}
