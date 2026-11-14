using Pinboard;
using Xunit;

namespace Pinboard.Tests;

public class SystemStatsTests
{
    [Fact]
    public void Cpu_is_the_busy_share_between_two_samples() =>
        // kernel time includes idle time: 100 ticks passed, 75 of them idle.
        Assert.Equal(25, SystemStats.CpuUsage(new(idle: 1000, kernel: 1500, user: 500), new(idle: 1075, kernel: 1580, user: 520)), 6);

    [Fact]
    public void Cpu_is_zero_when_no_time_passed() =>
        Assert.Equal(0, SystemStats.CpuUsage(new(1, 2, 3), new(1, 2, 3)));

    [Fact]
    public void Memory_reading_is_a_percentage()
    {
        var m = SystemStats.ReadMemory();
        Assert.InRange(m.Percent, 0, 100);
        Assert.True(m.TotalGb > 0);
    }

    [Fact]
    public void Show_option_keeps_known_values_in_order() =>
        Assert.Equal(new[] { "cpu", "disk" }, SystemDefinition.Shown("disk,bogus,cpu"));

    [Fact]
    public void Empty_show_option_falls_back_to_cpu() =>
        Assert.Equal(new[] { "cpu" }, SystemDefinition.Shown(""));
}
