using Pinboard;
using Xunit;

namespace Pinboard.Tests;

public class CountdownTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 10, 0, 0);

    [Fact]
    public void Thirty_days_ahead()
    {
        var r = CountdownMath.Remaining(Now.AddDays(30), Now);
        Assert.Equal(30, r.Days);
        Assert.False(r.Past);
    }

    [Fact]
    public void Five_hours_ahead()
    {
        var r = CountdownMath.Remaining(Now.AddHours(5), Now);
        Assert.Equal(0, r.Days);
        Assert.Equal(5, r.Hours);
        Assert.Equal(0, r.Minutes);
    }

    [Fact]
    public void Target_now_is_today() => Assert.Equal("Today", CountdownMath.Headline(Now, Now));

    [Fact]
    public void Target_earlier_today_is_today() => Assert.Equal("Today", CountdownMath.Headline(Now.AddHours(-3), Now));

    [Fact]
    public void Three_days_ago() => Assert.Equal("3 days ago", CountdownMath.Headline(Now.AddDays(-3), Now));

    [Fact]
    public void One_day_ago() => Assert.Equal("1 day ago", CountdownMath.Headline(Now.AddDays(-1), Now));

    [Fact]
    public void Tomorrow() => Assert.Equal("Tomorrow", CountdownMath.Headline(new DateTime(2026, 11, 15, 8, 0, 0), Now));

    [Fact]
    public void In_thirty_days() => Assert.Equal("In 30 days", CountdownMath.Headline(Now.AddDays(30), Now));

    [Fact]
    public void Days_count_calendar_days_not_24_hour_blocks() =>
        Assert.Equal(1, CountdownMath.CalendarDays(new DateTime(2026, 11, 15, 1, 0, 0), new DateTime(2026, 11, 14, 23, 0, 0)));
}
