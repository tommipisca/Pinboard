using Pinboard;
using Xunit;

namespace Pinboard.Tests;

public class WorldClockTests
{
    [Fact]
    public void Unknown_zones_are_skipped()
    {
        var cities = WorldClocks.Parse("""[{"zone":"Asia/Tokyo","city":"Tokyo"},{"zone":"Nowhere/Fake","city":"X"}]""");
        Assert.Single(cities);
        Assert.Equal("Tokyo", cities[0].Name);
    }

    [Fact]
    public void Broken_option_falls_back_to_the_defaults() =>
        Assert.True(WorldClocks.Parse("not json").Count >= 2);

    [Fact]
    public void At_most_four_cities_are_kept()
    {
        string json = """[{"zone":"Asia/Tokyo","city":"A"},{"zone":"Asia/Tokyo","city":"B"},{"zone":"Asia/Tokyo","city":"C"},{"zone":"Asia/Tokyo","city":"D"},{"zone":"Asia/Tokyo","city":"E"}]""";
        Assert.Equal(4, WorldClocks.Parse(json).Count);
    }

    [Fact]
    public void Serialized_cities_parse_back()
    {
        var cities = new[] { new WorldClocks.City("Asia/Tokyo", "Tokyo"), new WorldClocks.City("Europe/London", "London") };
        Assert.Equal(cities, WorldClocks.Parse(WorldClocks.Serialize(cities)));
    }

    [Fact]
    public void Later_calendar_day_is_tomorrow() =>
        Assert.Equal("Tomorrow", WorldClocks.DayLabel(new DateTime(2026, 11, 15, 1, 0, 0), new DateTime(2026, 11, 14, 23, 0, 0)));

    [Fact]
    public void Earlier_calendar_day_is_yesterday() =>
        Assert.Equal("Yesterday", WorldClocks.DayLabel(new DateTime(2026, 11, 13, 22, 0, 0), new DateTime(2026, 11, 14, 8, 0, 0)));

    [Theory]
    [InlineData(8, 0, "+8h")]
    [InlineData(-5, 0, "−5h")]
    [InlineData(5, 30, "+5:30h")]
    [InlineData(0, 0, "+0h")]
    public void Offsets_are_short(int hours, int minutes, string expected) =>
        Assert.Equal(expected, WorldClocks.FormatOffset(new TimeSpan(hours, hours < 0 ? -minutes : minutes, 0)));
}
