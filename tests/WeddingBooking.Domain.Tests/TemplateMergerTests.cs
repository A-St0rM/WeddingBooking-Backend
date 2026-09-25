using WeddingBooking.Domain.Messages;

namespace WeddingBooking.Domain.Tests;

public sealed class TemplateMergerTests
{
    [Fact]
    public void Placeholders_are_filled_and_missing_ones_stay_visible()
    {
        var result = TemplateMerger.Merge(
            "Kære {{ parret }}, vi glæder os til {{fremvisning}}. Ring til {{vært}} på {{vært_telefon}}.",
            new Dictionary<string, string?> { ["parret"] = "Tobias og Cecilie", ["vært"] = "Bryan", ["vært_telefon"] = "22 33 44 55" });

        Assert.Equal("Kære Tobias og Cecilie, vi glæder os til {{fremvisning}}. Ring til Bryan på 22 33 44 55.", result.Text);
        Assert.Equal(["fremvisning"], result.Missing);
    }
}
