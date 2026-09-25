using System.Net;
using System.Net.Http.Json;
using WeddingBooking.Domain.Users;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase6CommunicationTests(ApiFactory api)
{
    private HttpClient Manager => api.ClientFor(ApiFactory.ManagerEmail);

    [IntegrationFact]
    public async Task The_viewing_confirmation_has_the_host_on_cc_and_their_phone_in_the_text_and_is_logged_when_sent()
    {
        var bryan = await Manager.PostJsonAsync("/api/users", new { email = "bryan6@test.dk", name = "Bryan", role = "EventProducer", phone = "22 33 44 55" });
        var booking = await Manager.PostJsonAsync("/api/bookings", new { customerName = "Mail Par", eventDate = "2027-10-02", guestCount = 70 });
        var id = booking["id"].Str();
        await Manager.PostJsonAsync($"/api/customers/{booking["customer"]!["id"].Str()}/contacts", new { name = "Tobias", email = "tobias6@example.dk", phone = (string?)null });
        var viewing = await Manager.PutJsonAsync($"/api/bookings/{id}/viewing", new { at = "2027-03-02T13:00:00Z", hostId = bryan["id"].Str() });
        Assert.Equal("Viewing", viewing["stage"].Str());

        var templates = (await Manager.GetJsonAsync("/api/message-templates")).AsArray();
        var confirmation = templates.Single(t => t!["ccViewingHost"]!.GetValue<bool>())!;
        var draft = await Manager.PostJsonAsync($"/api/bookings/{id}/messages/draft", new { templateId = confirmation["id"].Str() });

        Assert.Equal(["bryan6@test.dk"], draft["cc"]!.AsArray().Select(c => c.Str()));
        Assert.Contains("22 33 44 55", draft["body"].Str());
        Assert.Contains("kl. 14.00", draft["body"].Str()); // 13:00 UTC is 14.00 in Denmark in March
        Assert.Empty(draft["missing"]!.AsArray());

        // Nothing is sent by making a draft.
        Assert.DoesNotContain((await Manager.GetJsonAsync("/api/outbox")).AsArray(), m => m!["to"].Str().Contains("tobias6"));

        var edited = draft["body"].Str() + "\n\nPS: Tag gode sko på — vi går en tur i parken.";
        var sent = await Manager.PostJsonAsync($"/api/bookings/{id}/messages/send",
            new { to = draft["to"]!.AsArray().Select(t => t.Str()), cc = draft["cc"]!.AsArray().Select(c => c.Str()), subject = draft["subject"].Str(), body = edited });

        var history = (await Manager.GetJsonAsync($"/api/bookings/{id}/notes")).AsArray();
        var logged = Assert.Single(history)!;
        Assert.Equal("Email", logged["type"].Str());
        Assert.Contains("tobias6@example.dk", logged["recipients"].Str());
        Assert.Contains("bryan6@test.dk", logged["recipients"].Str());
        Assert.Contains("gode sko", logged["text"].Str());
        Assert.NotNull(logged["createdAt"]);

        var outbox = (await Manager.GetJsonAsync("/api/outbox")).AsArray();
        Assert.Contains(outbox, m => m!["to"].Str() == "tobias6@example.dk" && m["body"].Str().Contains("gode sko"));
    }

    [IntegrationFact]
    public async Task Staff_write_notes_and_only_management_sends_mail()
    {
        var booking = await Manager.PostJsonAsync("/api/bookings", new { customerName = "Note Par", eventDate = "2027-10-09", guestCount = 70 });
        var id = booking["id"].Str();
        var producer = await api.StaffAsync("bryan6b@test.dk", Role.EventProducer);

        var note = await producer.PostJsonAsync($"/api/bookings/{id}/notes", new { type = "Call", text = "Ringede: vil vende tilbage efter nytår om morgenmad." });
        Assert.Equal("bryan6b", note["authorName"].Str());

        var refused = await producer.PostAsJsonAsync($"/api/bookings/{id}/messages/send", new { to = new[] { "x@example.dk" }, cc = Array.Empty<string>(), subject = "Hej", body = "Hej" });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }
}
