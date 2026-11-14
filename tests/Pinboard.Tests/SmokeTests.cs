using Pinboard;
using Xunit;

namespace Pinboard.Tests;

public class SmokeTests
{
    [Fact]
    public void Large_size_is_360_by_360()
    {
        Assert.Equal(360, WidgetDefinition.Large.Width);
        Assert.Equal(360, WidgetDefinition.Large.Height);
    }
}
