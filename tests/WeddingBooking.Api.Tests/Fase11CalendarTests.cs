using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase11CalendarTests(ApiFactory api)
{
    private HttpClient Manager => api.ClientFor(ApiFactory.ManagerEmail);

    [IntegrationFact]
    public async Task Five_free_times_that_overlap_nothing_and_a_new_viewing_or_block_takes_its_time_at_once()
    {
        var host = await Manager.PostJsonAsync("/api/users", new { email = "bryan11@test.dk", name = "Bryan 11", role = "EventProducer", phone = (string?)null });
        var hostId = host["id"].Str();
        var put = await Manager.PutAsJsonAsyncOk($"/api/calendar/availability/{hostId}", new[]
        {
            new { day = "Tuesday", from = "10:00:00", to = "13:00:00" },
            new { day = "Thursday", from = "14:00:00", to = "16:00:00" },
        });

        var slots = await SlotsAsync(hostId);
        Assert.Equal(5, slots.Count);
        await AssertNothingOverlapsAsync(slots);

        // A viewing on the first free time takes it.
        var booking = await Manager.PostJsonAsync("/api/bookings", new { partners = Json.Couple("Kalender og Par"), eventDate = "2028-06-03", guestCount = 70 });
        await Manager.PutJsonAsync($"/api/bookings/{booking["id"].Str()}/viewing", new { at = slots[0]["start"].Str(), hostId });
        var afterViewing = await SlotsAsync(hostId);
        Assert.DoesNotContain(afterViewing, s => s["start"].Str() == slots[0]["start"].Str());

        // A block over the next free time takes that too.
        var blocked = await Manager.PostAsJsonAsync("/api/calendar/blocks", new { start = afterViewing[0]["start"].Str(), end = afterViewing[0]["end"].Str(), reason = "Tandlæge", hostId = (string?)null });
        Assert.True(blocked.IsSuccessStatusCode, await blocked.Content.ReadAsStringAsync());
        var afterBlock = await SlotsAsync(hostId);
        Assert.DoesNotContain(afterBlock, s => s["start"].Str() == afterViewing[0]["start"].Str());
        await AssertNothingOverlapsAsync(afterBlock);

        var events = await EventsAsync(slots[0]["start"].Str()[..10], afterBlock[^1]["end"].Str()[..10]);
        Assert.Contains(events, e => e["kind"].Str() == "Viewing" && e["hostName"].Str() == "Bryan 11");
        Assert.Contains(events, e => e["kind"].Str() == "Block" && e["title"].Str() == "Tandlæge");
    }

    [IntegrationFact]
    public async Task Weddings_show_as_whole_days()
    {
        await Manager.PostJsonAsync("/api/bookings", new { partners = Json.Couple("Heldags og Par"), eventDate = "2028-08-12", guestCount = 90 });
        var wedding = (await EventsAsync("2028-08-01", "2028-08-31")).Single(e => e["title"].Str().StartsWith("Heldags og Par"));

        Assert.True(wedding["allDay"]!.GetValue<bool>());
        Assert.Equal("Wedding", wedding["kind"].Str());
        Assert.False(wedding["confirmed"]!.GetValue<bool>()); // an inquiry does not hold the day
    }

    private async Task<List<JsonNode>> SlotsAsync(string hostId) =>
        (await Manager.GetJsonAsync($"/api/calendar/slots?hostId={hostId}")).AsArray().Select(s => s!).ToList();

    private async Task<List<JsonNode>> EventsAsync(string from, string to) =>
        (await Manager.GetJsonAsync($"/api/calendar?from={from}&to={to}")).AsArray().Select(e => e!).ToList();

    private async Task AssertNothingOverlapsAsync(List<JsonNode> slots)
    {
        var events = await EventsAsync(slots[0]["start"].Str()[..10], slots[^1]["end"].Str()[..10]);
        foreach (var slot in slots)
        {
            var (start, end) = (DateTimeOffset.Parse(slot["start"].Str()), DateTimeOffset.Parse(slot["end"].Str()));
            Assert.DoesNotContain(events, e => e["kind"].Str() != "Wedding" || e["confirmed"]!.GetValue<bool>()
                ? DateTimeOffset.Parse(e["start"].Str()) < end && start < DateTimeOffset.Parse(e["end"].Str())
                : false);
        }
    }
}
