using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase4AgreementTests(ApiFactory api)
{
    private HttpClient Manager => api.ClientFor(ApiFactory.ManagerEmail);

    [IntegrationFact]
    public async Task A_new_version_after_a_guest_change_keeps_the_old_one_and_a_signed_pdf_round_trips()
    {
        var pkg = (await Manager.GetJsonAsync("/api/packages?year=2026")).AsArray().First(p => p!["kind"].Str() == "Base")!;
        var booking = await Manager.PostJsonAsync("/api/bookings", new { customerName = "Aftale Par", eventDate = "2027-07-03", guestCount = 80 });
        var id = booking["id"].Str();
        booking = await Manager.PostJsonAsync($"/api/bookings/{id}/packages", new { packageId = pkg["id"].Str() });
        var agreement = $"/api/bookings/{id}/agreement";

        var first = await Manager.PostJsonAsync($"{agreement}/versions", new { clarification = "Morgenmad: rundstykker, kaffe og juice til alle gæster." });
        var v1 = first["versions"]![0]!["id"].Str();
        var pdf1 = await Manager.GetByteArrayAsync($"{agreement}/versions/{v1}/pdf");
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf1, 0, 5));

        var current = await Manager.GetJsonAsync($"/api/bookings/{id}");
        await Manager.PutJsonAsync($"/api/bookings/{id}", new { eventDate = "2027-07-03", guestCount = 94, paymentTerms = "New", version = current["version"]!.GetValue<uint>() });
        var second = await Manager.PostJsonAsync($"{agreement}/versions", new { clarification = first["clarification"].Str() });

        Assert.Equal(2, second["versions"]!.AsArray().Count);
        var pdf2 = await Manager.GetByteArrayAsync($"{agreement}/versions/{second["versions"]![0]!["id"].Str()}/pdf");
        Assert.NotEqual(pdf1, pdf2);
        Assert.Equal(pdf1, await Manager.GetByteArrayAsync($"{agreement}/versions/{v1}/pdf"));

        using var form = new MultipartFormDataContent();
        var signed = new ByteArrayContent(pdf2);
        signed.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(signed, "file", "underskrevet.pdf");
        var v2 = second["versions"]![0]!["id"].Str();
        var uploaded = await (await Manager.PostAsync($"{agreement}/versions/{v2}/signed", form)).ReadAsync();

        Assert.NotNull(uploaded["versions"]![0]!["signedAt"]);
        Assert.Equal(pdf2, await Manager.GetByteArrayAsync($"{agreement}/versions/{v2}/signed"));
        Assert.Equal("Signed", (await Manager.GetJsonAsync($"/api/bookings/{id}"))["stage"].Str());
    }

    [IntegrationFact]
    public async Task Something_that_is_not_a_pdf_is_refused()
    {
        var booking = await Manager.PostJsonAsync("/api/bookings", new { customerName = "Ikke PDF", eventDate = "2027-07-10", guestCount = 60 });
        var versions = await Manager.PostJsonAsync($"/api/bookings/{booking["id"].Str()}/agreement/versions", new { clarification = (string?)null });

        using var form = new MultipartFormDataContent { { new ByteArrayContent("hello"u8.ToArray()), "file", "fake.pdf" } };
        var response = await Manager.PostAsync($"/api/bookings/{booking["id"].Str()}/agreement/versions/{versions["versions"]![0]!["id"].Str()}/signed", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [IntegrationFact]
    public async Task Attaching_to_a_quote_is_refused_once_it_has_been_sent()
    {
        var pkg = (await Manager.GetJsonAsync("/api/packages?year=2026")).AsArray().First(p => p!["kind"].Str() == "Base")!;
        var booking = await Manager.PostJsonAsync("/api/bookings", new { customerName = "Bilag Par", eventDate = "2027-07-17", guestCount = 70 });
        var id = booking["id"].Str();
        await Manager.PostJsonAsync($"/api/bookings/{id}/packages", new { packageId = pkg["id"].Str() });
        var quote = await Manager.PostJsonAsync($"/api/bookings/{id}/payments/quote");
        var version = (await Manager.PostJsonAsync($"/api/bookings/{id}/agreement/versions", new { clarification = (string?)null }))["versions"]![0]!["id"].Str();

        Assert.Equal(HttpStatusCode.NoContent, (await Manager.PostAsync($"/api/bookings/{id}/agreement/versions/{version}/attach-to-quote", null)).StatusCode);

        await Manager.PostAsync($"/api/accounting-sim/documents/{quote["quote"]!["number"].Str()}/sent", null);
        Assert.Equal(HttpStatusCode.BadRequest, (await Manager.PostAsync($"/api/bookings/{id}/agreement/versions/{version}/attach-to-quote", null)).StatusCode);
    }
}
