using System.Net;
using System.Net.Http.Json;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase1BookingTests(ApiFactory api)
{
    private HttpClient Manager => api.ClientFor(ApiFactory.ManagerEmail);

    [IntegrationFact]
    public async Task A_booking_from_name_date_and_guests_and_a_returning_customer_on_the_same_customer()
    {
        var first = await Manager.PostJsonAsync("/api/bookings", new { partners = Json.Couple("Tobias og Cecilie Fase1"), eventDate = "2027-06-12", guestCount = 80 });
        Assert.Equal("Inquiry", first["stage"].Str());

        var found = (await Manager.GetJsonAsync("/api/customers?search=tobias og cec")).AsArray();
        var customer = Assert.Single(found);
        Assert.Equal(first["customer"]!["id"].Str(), customer!["id"].Str());

        var second = await Manager.PostJsonAsync("/api/bookings", new { customerId = customer["id"].Str(), eventDate = "2029-08-18", guestCount = 60 });
        Assert.Equal(customer["id"].Str(), second["customer"]!["id"].Str());

        var detail = await Manager.GetJsonAsync($"/api/customers/{customer["id"].Str()}");
        Assert.Equal(2, detail["bookings"]!.AsArray().Count);
    }

    [IntegrationFact]
    public async Task Two_bookings_on_one_date_warn_but_both_save()
    {
        await Manager.PostJsonAsync("/api/bookings", new { partners = Json.Couple("Par A"), eventDate = "2028-05-20", guestCount = 70 });
        var second = await Manager.PostJsonAsync("/api/bookings", new { partners = Json.Couple("Par B"), eventDate = "2028-05-20", guestCount = 90 });

        Assert.StartsWith("Par A", Assert.Single(second["sameDateBookings"]!.AsArray())!["customerName"].Str());
    }

    [IntegrationFact]
    public async Task Saving_over_someone_elses_change_is_409()
    {
        var booking = await Manager.PostJsonAsync("/api/bookings", new { partners = Json.Couple("Samtidige"), eventDate = "2028-06-03", guestCount = 70 });
        var url = $"/api/bookings/{booking["id"].Str()}";
        var version = booking["version"]!.GetValue<uint>();

        await Manager.PutJsonAsync(url, new { eventDate = "2028-06-03", guestCount = 72, paymentTerms = "New", version });
        var stale = await Manager.PutAsJsonAsync(url, new { eventDate = "2028-06-03", guestCount = 74, paymentTerms = "New", version });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [IntegrationFact]
    public async Task A_new_booking_needs_both_people()
    {
        var one = await Manager.PostAsJsonAsync("/api/bookings", new
        {
            partners = new[] { new { firstName = "Tobias", lastName = (string?)null, email = (string?)null, phone = (string?)null } },
            eventDate = "2028-06-10", guestCount = 70,
        });
        Assert.Equal(HttpStatusCode.BadRequest, one.StatusCode);

        var two = await Manager.PostJsonAsync("/api/bookings", new { partners = Json.Couple("Theo og Clara Fase1b"), eventDate = "2028-06-10", guestCount = 70 });
        var people = two["customer"]!["contacts"]!.AsArray();
        Assert.Equal(["Theo", "Clara Fase1b"], people.Select(p => p!["firstName"].Str()));
        Assert.Single(people, p => p!["isPayer"]!.GetValue<bool>());
    }
}
