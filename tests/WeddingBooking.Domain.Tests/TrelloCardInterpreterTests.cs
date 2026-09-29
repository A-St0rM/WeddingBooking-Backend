using WeddingBooking.Domain.Readiness;
using WeddingBooking.Domain.Trello;

namespace WeddingBooking.Domain.Tests;

public sealed class TrelloCardInterpreterTests
{
    private static TrelloCard Card(string name, string desc = "", DateTimeOffset? due = null, bool closed = false,
        IReadOnlyList<TrelloChecklist>? checklists = null, Dictionary<string, string>? fields = null) =>
        new("c1", name, desc, due, "Bekræftet", closed, [], checklists ?? [], fields ?? new());

    [Theory]
    [InlineData("Bryllup 80 pax", null, null)]
    [InlineData("Tobias & Cecilie 80 pax 12.06.2027", "Tobias og Cecilie", "2027-06-12")]
    [InlineData("Sofie og Magnus - 55 gæster - 19. juni", "Sofie og Magnus", "2027-06-19")]
    [InlineData("Emma + Oliver 14/8 120 pax", "Emma og Oliver", "2027-08-14")]
    public void Reads_couple_date_and_guests_from_the_name(string name, string? couple, string? date)
    {
        var reading = TrelloCardInterpreter.Read(Card(name), 2027);

        Assert.Equal(couple, reading.CoupleName);
        Assert.Equal(date is null ? null : DateOnly.Parse(date), reading.EventDate);
        Assert.NotNull(reading.GuestCount);
    }

    [Fact]
    public void The_due_date_and_custom_fields_win_over_the_text()
    {
        var reading = TrelloCardInterpreter.Read(
            Card("Ida og Frederik", "ca. 70 pax", due: new DateTimeOffset(2027, 6, 12, 10, 0, 0, TimeSpan.Zero), fields: new() { ["Antal gæster"] = "73" }),
            2027);

        Assert.Equal(73, reading.GuestCount);
        Assert.Equal(new DateOnly(2027, 6, 12), reading.EventDate);
        Assert.True(reading.CanImport);
    }

    [Fact]
    public void A_card_without_date_or_guests_is_reported_with_the_reason()
    {
        Assert.Contains("dato", TrelloCardInterpreter.Read(Card("Bryllup 80 pax"), 2027).SkipReason);
        Assert.Contains("gæsteantal", TrelloCardInterpreter.Read(Card("Laura og William 27.05.2028"), 2028).SkipReason);
        Assert.Contains("arkiveret", TrelloCardInterpreter.Read(Card("Gammelt 80 pax 1.6.2027", closed: true), 2027).SkipReason);
        Assert.Contains("Skabelon", TrelloCardInterpreter.Read(Card("Skabelon bryllup 80 pax 1.6.2027"), 2027).SkipReason);
    }

    [Fact]
    public void Checklists_become_readiness_green_half_green_and_grey()
    {
        var reading = TrelloCardInterpreter.Read(Card("A og B 80 pax 1.6.2027", checklists:
        [
            new("Mad", [new("Menu", true), new("Allergier", true)]),
            new("Pynt og blomster", [new("Buketter", true), new("Borddækning", false)]),
            new("Kontakt", [new("Ring", false)]),
        ]), 2027);

        Assert.Equal(ReadinessStatus.Done, reading.Readiness[ReadinessCategory.Food]);
        Assert.Equal(ReadinessStatus.InProgress, reading.Readiness[ReadinessCategory.Decoration]);
        Assert.Equal(ReadinessStatus.NotStarted, reading.Readiness[ReadinessCategory.Contact]);
    }

    [Theory]
    [InlineData("Bryllup 26", 2026)]
    [InlineData("Bryllup 2028", 2028)]
    public void The_board_name_gives_the_year(string board, int year) => Assert.Equal(year, TrelloCardInterpreter.YearOf(board));
}
