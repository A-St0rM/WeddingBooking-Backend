using WeddingBooking.Domain.Trello;

namespace WeddingBooking.Domain.Tests;

public sealed class TrelloSyncRulesTests
{
    [Theory]
    //          system  trello  synced  →
    [InlineData(false,  null,   null,   CheckItemAction.AddToTrello)]
    [InlineData(true,   false,  null,   CheckItemAction.PushToTrello)]   // never synced: the system's state goes out
    [InlineData(true,   true,   null,   CheckItemAction.None)]
    [InlineData(true,   false,  false,  CheckItemAction.PushToTrello)]   // ticked in the system
    [InlineData(false,  true,   false,  CheckItemAction.TakeFromTrello)] // ticked in Trello
    [InlineData(true,   false,  true,   CheckItemAction.TakeFromTrello)] // unticked in Trello
    [InlineData(false,  false,  true,   CheckItemAction.None)]           // both unticked it
    [InlineData(true,   true,   true,   CheckItemAction.None)]
    public void Whoever_moved_from_the_agreed_state_changed_it(bool system, bool? trello, bool? synced, CheckItemAction expected) =>
        Assert.Equal(expected, TrelloSyncRules.Decide(system, trello, synced));

    [Fact]
    public void The_booking_writes_its_date_and_guests_in_a_block_and_the_pax_in_the_name()
    {
        var (name, description) = TrelloSyncRules.WithBooking("Bryllup 80 pax", "Husk blomster.", new DateOnly(2027, 6, 12), 94);

        Assert.Equal("Bryllup 94 pax", name);
        Assert.StartsWith("[bookingsystem]\nDato: lørdag den 12. juni 2027\nGæster: 94\n[/bookingsystem]", description);
        Assert.EndsWith("Husk blomster.", description);

        var (_, again) = TrelloSyncRules.WithBooking(name, description, new DateOnly(2027, 6, 12), 96);
        Assert.Contains("Gæster: 96", again);
        Assert.DoesNotContain("Gæster: 94", again);
        Assert.EndsWith("Husk blomster.", again);
    }
}
