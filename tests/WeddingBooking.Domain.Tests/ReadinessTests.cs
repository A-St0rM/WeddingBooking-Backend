using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Readiness;
using WeddingBooking.Domain.Users;

namespace WeddingBooking.Domain.Tests;

public sealed class ReadinessTests
{
    private static readonly User Manager = User.Create("johan@test.dk", "Johan", Role.EventManager);
    private static readonly User Chef = User.Create("kenneth@test.dk", "Kenneth", Role.Chef);

    [Fact]
    public void Ready_for_the_event_follows_when_every_item_before_the_day_is_done()
    {
        var booking = TestData.Booking(80);
        Assert.False(ReadinessRules.AllDone(booking.ReadinessItems));

        foreach (var item in booking.ReadinessItems.Where(i => i.Category != ReadinessCategory.PostEventInvoicing))
        {
            booking.UpdateReadiness(item.Id, ReadinessStatus.Done, null, null, Manager);
        }

        Assert.True(ReadinessRules.AllDone(booking.ReadinessItems));
    }

    [Fact]
    public void The_chef_may_change_food_and_not_table_setting()
    {
        var booking = TestData.Booking(80);
        var food = booking.ReadinessItems.Single(i => i.Category == ReadinessCategory.Food);
        var tables = booking.ReadinessItems.Single(i => i.Category == ReadinessCategory.TableSetting);

        booking.UpdateReadiness(food.Id, ReadinessStatus.InProgress, "2 vegetarer, 1 glutenfri", null, Chef);
        Assert.Equal("2 vegetarer, 1 glutenfri", food.Notes);
        Assert.Throws<ForbiddenException>(() => booking.UpdateReadiness(tables.Id, ReadinessStatus.Done, null, null, Chef));
    }

    [Theory]
    [InlineData(Role.EventManager, ReadinessCategory.Food, true)]
    [InlineData(Role.Owner, ReadinessCategory.PostEventInvoicing, true)]
    [InlineData(Role.Chef, ReadinessCategory.Food, true)]
    [InlineData(Role.Chef, ReadinessCategory.Decoration, false)]
    [InlineData(Role.EventProducer, ReadinessCategory.TableSetting, true)]
    [InlineData(Role.EventProducer, ReadinessCategory.Decoration, true)]
    [InlineData(Role.EventProducer, ReadinessCategory.Food, false)]
    [InlineData(Role.Bookkeeper, ReadinessCategory.PostEventInvoicing, true)]
    [InlineData(Role.Bookkeeper, ReadinessCategory.Contact, false)]
    public void Each_role_changes_its_own_part(Role role, ReadinessCategory category, bool allowed) =>
        Assert.Equal(allowed, ReadinessRules.CanEdit(role, category));
}
