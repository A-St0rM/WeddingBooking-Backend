using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase12PublicRequestTests(ApiFactory api)
{
    private HttpClient Manager => api.ClientFor(ApiFactory.ManagerEmail);
    private HttpClient Anonymous => api.CreateClient();

    private static object[] People(string a, string b) =>
    [
        new { firstName = a, lastName = "Hansen", email = $"{a.ToLowerInvariant()}@example.dk", phone = "20304050" },
        new { firstName = b, lastName = "Berg", email = $"{b.ToLowerInvariant()}@example.dk", phone = "30405060" },
    ];

    /// <summary>One host who shows round only on Wednesdays 9–10, so each Wednesday has exactly one free time.</summary>
    private async Task WednesdayHostAsync()
    {
        var users = (await Manager.GetJsonAsync("/api/users")).AsArray();
        var host = users.FirstOrDefault(u => u!["email"].Str() == "onsdag@test.dk")
            ?? await Manager.PostJsonAsync("/api/users", new { email = "onsdag@test.dk", name = "Onsdagsvært", role = "EventProducer", phone = (string?)null });
        await Manager.PutAsJsonAsyncOk($"/api/calendar/availability/{host!["id"].Str()}", new[] { new { day = "Wednesday", from = "09:00:00", to = "10:00:00" } });
    }

    private async Task<string> FreeWednesdayAsync() =>
        (await Anonymous.GetJsonAsync("/api/public/viewing-times")).AsArray()
            .Select(t => t!["start"].Str()).First(s => DateTimeOffset.Parse(s).DayOfWeek == DayOfWeek.Wednesday);

    [IntegrationFact]
    public async Task A_couple_books_a_viewing_without_login_and_it_waits_as_a_new_case_holding_its_time()
    {
        await WednesdayHostAsync();
        var start = await FreeWednesdayAsync();
        var outboxBefore = (await Manager.GetJsonAsync("/api/outbox")).AsArray().Count;

        var receipt = await (await Anonymous.PostAsJsonAsync("/api/public/requests/viewing",
            new { people = People("Nanna", "Rasmus"), start, wishedDate = "2028-06-17", guestCount = 85, consent = true, website = "" })).ReadAsync();
        Assert.Equal(6, receipt["reference"].Str().Length);

        var booking = (await Manager.GetJsonAsync("/api/bookings?search=nanna og rasmus")).AsArray().Single()!;
        Assert.Equal("Requested", booking["status"].Str());
        var detail = await Manager.GetJsonAsync($"/api/bookings/{booking["id"].Str()}");
        Assert.Equal("Website", detail["origin"].Str());
        Assert.Equal(["Nanna Hansen", "Rasmus Berg"], detail["customer"]!["contacts"]!.AsArray().Select(c => c!["fullName"].Str()));

        Assert.DoesNotContain((await Anonymous.GetJsonAsync("/api/public/viewing-times")).AsArray(), t => t!["start"].Str() == start);
        var day = DateTimeOffset.Parse(start).ToString("yyyy-MM-dd");
        Assert.Contains((await Manager.GetJsonAsync($"/api/calendar?from={day}&to={day}")).AsArray(), e => e!["kind"].Str() == "Viewing" && e["bookingId"].Str() == booking["id"].Str());

        // Nothing was mailed.
        Assert.Equal(outboxBefore, (await Manager.GetJsonAsync("/api/outbox")).AsArray().Count);

        var accepted = await Manager.PostJsonAsync($"/api/bookings/{booking["id"].Str()}/request/accept");
        Assert.Equal("Viewing", accepted["stage"].Str());
    }

    [IntegrationFact]
    public async Task Two_couples_pressing_at_once_cannot_both_get_the_same_time()
    {
        await WednesdayHostAsync();
        var start = await FreeWednesdayAsync();

        var attempts = await Task.WhenAll(
            Anonymous.PostAsJsonAsync("/api/public/requests/viewing", new { people = People("Samtidig", "Et"), start, wishedDate = "2028-06-24", guestCount = 70, consent = true, website = "" }),
            Anonymous.PostAsJsonAsync("/api/public/requests/viewing", new { people = People("Samtidig", "To"), start, wishedDate = "2028-06-24", guestCount = 70, consent = true, website = "" }));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], attempts.Select(a => a.StatusCode).Order());
    }

    [IntegrationFact]
    public async Task A_date_request_holds_its_date_until_it_is_declined()
    {
        var dates = (await Anonymous.GetJsonAsync("/api/public/wedding-dates")).AsArray().Select(d => d.Str()).ToList();
        var date = dates[^1];

        var made = await Anonymous.PostAsJsonAsync("/api/public/requests/wedding", new { people = People("Dato", "Par"), eventDate = date, guestCount = 100, consent = true, website = "" });
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        Assert.DoesNotContain((await Anonymous.GetJsonAsync("/api/public/wedding-dates")).AsArray(), d => d.Str() == date);

        var again = await Anonymous.PostAsJsonAsync("/api/public/requests/wedding", new { people = People("Andet", "Par"), eventDate = date, guestCount = 60, consent = true, website = "" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var booking = (await Manager.GetJsonAsync("/api/bookings?search=dato og par")).AsArray().Single()!;
        await Manager.PostJsonAsync($"/api/bookings/{booking["id"].Str()}/decline", new { reason = "Vi har et andet arrangement." });
        Assert.Contains((await Anonymous.GetJsonAsync("/api/public/wedding-dates")).AsArray(), d => d.Str() == date);
    }

    [IntegrationFact]
    public async Task A_bot_filling_the_hidden_field_gets_a_receipt_and_nothing_is_saved()
    {
        var bot = await Anonymous.PostAsJsonAsync("/api/public/requests/wedding",
            new { people = People("Robot", "Spam"), eventDate = "2028-06-10", guestCount = 50, consent = true, website = "http://spam.example" });

        Assert.Equal(HttpStatusCode.Created, bot.StatusCode);
        Assert.Empty((await Manager.GetJsonAsync("/api/bookings?search=robot og spam&includePast=true&includeClosed=true")).AsArray());
    }
}
