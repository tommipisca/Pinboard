using Pinboard;
using Xunit;

namespace Pinboard.Tests;

public class TimeProgressTests
{
    [Fact]
    public void Day_is_half_done_at_noon() =>
        Assert.Equal(0.5, TimeProgress.Fraction("day", new DateTime(2026, 11, 14, 12, 0, 0)), 6);

    [Fact]
    public void Week_starts_on_monday() =>
        Assert.Equal(0, TimeProgress.Fraction("week", new DateTime(2026, 11, 9, 0, 0, 0)), 6);

    [Fact]
    public void Week_is_half_done_on_thursday_at_noon() =>
        Assert.Equal(0.5, TimeProgress.Fraction("week", new DateTime(2026, 11, 12, 12, 0, 0)), 6);

    [Fact]
    public void Year_is_half_done_on_july_2_at_noon() =>
        Assert.Equal(0.5, TimeProgress.Fraction("year", new DateTime(2026, 7, 2, 12, 0, 0)), 3);

    [Fact]
    public void February_is_half_done_on_the_15th() =>
        Assert.Equal(0.5, TimeProgress.Fraction("month", new DateTime(2026, 2, 15, 0, 0, 0)), 6);

    [Fact]
    public void Week_label_uses_the_iso_week_number() =>
        Assert.Equal("Week 46", TimeProgress.Label("week", new DateTime(2026, 11, 14)));

    [Fact]
    public void Year_label_is_the_year() =>
        Assert.Equal("2026", TimeProgress.Label("year", new DateTime(2026, 11, 14)));

    [Fact]
    public void Month_label_is_the_month_name() =>
        Assert.Equal("November", TimeProgress.Label("month", new DateTime(2026, 11, 14)));
}
