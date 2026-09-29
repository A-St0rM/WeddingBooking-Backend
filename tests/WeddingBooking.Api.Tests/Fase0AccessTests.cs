using System.Net;
using WeddingBooking.Domain.Users;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase0AccessTests(ApiFactory api)
{
    [IntegrationFact]
    public async Task Bootstrap_manager_sees_their_name_and_role()
    {
        var me = await api.ClientFor(ApiFactory.ManagerEmail).GetJsonAsync("/api/me");
        Assert.Equal("EventManager", me["role"].Str());
    }

    [IntegrationFact]
    public async Task Signed_in_without_a_user_row_is_403_with_an_explanation()
    {
        var response = await api.ClientFor("stranger@test.dk").GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Ingen adgang", await response.Content.ReadAsStringAsync());
    }

    [IntegrationFact]
    public async Task No_token_is_401_and_health_is_open()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.CreateClient().GetAsync("/health")).StatusCode);
    }

    [IntegrationFact]
    public async Task A_role_without_the_right_gets_403_not_the_data()
    {
        var chef = await api.StaffAsync("chef0@test.dk", Role.Chef);
        Assert.Equal(HttpStatusCode.Forbidden, (await chef.GetAsync("/api/packages")).StatusCode);
    }
}
