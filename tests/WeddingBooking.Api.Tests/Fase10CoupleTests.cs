using System.Net;
using System.Text.Json.Nodes;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase10CoupleTests(ApiFactory api)
{
    private HttpClient Manager => api.ClientFor(ApiFactory.ManagerEmail);

    [IntegrationFact]
    public async Task The_payer_is_the_accounting_customer_and_cannot_change_once_an_invoice_is_booked()
    {
        var pkg = (await Manager.GetJsonAsync("/api/packages?year=2026")).AsArray().First(p => p!["kind"].Str() == "Base")!;
        var booking = await Manager.PostJsonAsync("/api/bookings", new { partners = Json.Couple("Johanne og Viktor"), eventDate = "2027-09-18", guestCount = 80 });
        var id = booking["id"].Str();
        var customer = $"/api/customers/{booking["customer"]!["id"].Str()}";
        var viktor = booking["customer"]!["contacts"]!.AsArray().Single(c => c!["firstName"].Str() == "Viktor")!["id"].Str();
        await Manager.PostJsonAsync($"/api/bookings/{id}/packages", new { packageId = pkg["id"].Str() });

        // Viktor pays: his full name and the address are what the accounting system gets.
        await Manager.PutJsonAsync($"{customer}/contacts/{viktor}", new { firstName = "Viktor", lastName = "Holm", email = "viktor@example.dk", phone = (string?)null });
        await Manager.PutJsonAsync(customer, new { address = "Algade 1", postalCode = "4930", city = "Maribo" });
        await Manager.PostJsonAsync($"{customer}/contacts/{viktor}/payer");
        var quote = await Manager.PostJsonAsync($"/api/bookings/{id}/payments/quote");
        Assert.Equal("Viktor Holm", Document(await Manager.GetJsonAsync("/api/accounting-sim/documents"), quote["quote"]!["number"].Str())["customerName"].Str());

        var invoiced = await Manager.PostJsonAsync($"/api/bookings/{id}/payments/invoices", new { kind = "ReservationFee" });
        var number = invoiced["instalments"]!.AsArray().Single(i => i!["kind"].Str() == "ReservationFee")!["invoice"]!["number"].Str();
        await Manager.PostJsonAsync($"/api/bookings/{id}/payments/invoices/{number}/book");

        var johanne = booking["customer"]!["contacts"]!.AsArray().Single(c => c!["firstName"].Str() == "Johanne")!["id"].Str();
        Assert.Equal(HttpStatusCode.BadRequest, (await Manager.PostAsync($"{customer}/contacts/{johanne}/payer", null)).StatusCode);
    }

    [IntegrationFact]
    public async Task Customers_made_as_two_people_are_not_in_the_report()
    {
        await Manager.PostJsonAsync("/api/bookings", new { partners = Json.Couple("Rapport og Par"), eventDate = "2027-09-25", guestCount = 60 });
        Assert.DoesNotContain((await Manager.GetJsonAsync("/api/customers/incomplete")).AsArray(), c => c!["name"].Str() == "Rapport og Par");
    }

    private static JsonNode Document(JsonNode documents, string number) =>
        documents.AsArray().Single(d => d!["number"].Str() == number)!;
}
