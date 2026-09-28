using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase3PaymentTests(ApiFactory api)
{
    private HttpClient Manager => api.ClientFor(ApiFactory.ManagerEmail);

    [IntegrationFact]
    public async Task Quote_then_fee_paid_is_date_secured_then_33_percent_and_final_payment()
    {
        var pkg = (await Manager.GetJsonAsync("/api/packages?year=2026")).AsArray().First(p => p!["kind"].Str() == "Base")!;
        var booking = await Manager.PostJsonAsync("/api/bookings", new { partners = Json.Couple("Hele forløbet"), eventDate = "2027-08-21", guestCount = 94 });
        var id = booking["id"].Str();
        await Manager.PostJsonAsync($"/api/bookings/{id}/packages", new { packageId = pkg["id"].Str() });
        var payments = $"/api/bookings/{id}/payments";

        var quoted = await Manager.PostJsonAsync($"{payments}/quote");
        Assert.Equal("Draft", quoted["quote"]!["status"].Str());
        Assert.Equal(94 * 1495m, quoted["quote"]!["amount"].Dec());

        foreach (var kind in new[] { "ReservationFee", "Instalment", "FinalPayment" })
        {
            var invoiced = await Manager.PostJsonAsync($"{payments}/invoices", new { kind });
            var invoice = Instalment(invoiced, kind)["invoice"]!;
            await Manager.PostJsonAsync($"{payments}/invoices/{invoice["number"].Str()}/book");

            var pay = await Manager.PostAsJsonAsync($"/api/accounting-sim/documents/{invoice["number"].Str()}/payments", new { amount = invoice["amount"].Dec(), paidOn = (string?)null });
            Assert.True(pay.IsSuccessStatusCode, await pay.Content.ReadAsStringAsync());

            if (kind == "ReservationFee")
            {
                var afterFee = await Manager.GetJsonAsync($"/api/bookings/{id}");
                Assert.Equal("DateSecured", afterFee["status"].Str());
            }
        }

        var done = await Manager.GetJsonAsync(payments);
        Assert.All(done["instalments"]!.AsArray(), i => Assert.Equal("Paid", i!["state"].Str()));
        Assert.Equal(done["total"].Dec(), done["instalments"]!.AsArray().Sum(i => i!["amount"].Dec()));
        Assert.Equal(25_000m, Instalment(done, "ReservationFee")["amount"].Dec());
    }

    [IntegrationFact]
    public async Task Invoicing_the_same_instalment_twice_gives_one_invoice()
    {
        var pkg = (await Manager.GetJsonAsync("/api/packages?year=2026")).AsArray().First(p => p!["kind"].Str() == "Base")!;
        var booking = await Manager.PostJsonAsync("/api/bookings", new { partners = Json.Couple("To gange"), eventDate = "2027-08-28", guestCount = 80 });
        var id = booking["id"].Str();
        await Manager.PostJsonAsync($"/api/bookings/{id}/packages", new { packageId = pkg["id"].Str() });

        var first = await Manager.PostJsonAsync($"/api/bookings/{id}/payments/invoices", new { kind = "ReservationFee" });
        var second = await Manager.PostJsonAsync($"/api/bookings/{id}/payments/invoices", new { kind = "ReservationFee" });

        Assert.Equal(Instalment(first, "ReservationFee")["invoice"]!["number"].Str(), Instalment(second, "ReservationFee")["invoice"]!["number"].Str());
    }

    [IntegrationFact]
    public async Task Booking_tables_hold_no_payment_status_and_no_amount_only_references()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var columns = await db.Database.SqlQueryRaw<string>(
            "SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'app' AND table_name IN ('bookings', 'accounting_references', 'customers')")
            .ToListAsync();

        Assert.DoesNotContain(columns, c => c.Contains("status") || c.Contains("paid") || c.Contains("remaining") || c == "amount" || c.Contains("total"));
        Assert.Contains("number", columns);
    }

    private static JsonNode Instalment(JsonNode payments, string kind) =>
        payments["instalments"]!.AsArray().Single(i => i!["kind"].Str() == kind)!;
}
