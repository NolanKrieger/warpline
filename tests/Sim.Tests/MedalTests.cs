using Warpline.Sim;

namespace Warpline.Tests;

public class MedalTests
{
    [Theory]
    [InlineData(1000, Medal.Dev)]
    [InlineData(999, Medal.Dev)]
    [InlineData(1150, Medal.Gold)]
    [InlineData(1151, Medal.Silver)]
    [InlineData(1400, Medal.Silver)]
    [InlineData(1401, Medal.Bronze)]
    [InlineData(0, Medal.None)]
    public void ThresholdsFollowDevTime(int ticks, Medal expected) => Assert.Equal(expected, Medals.For(ticks, 1000));

    [Fact]
    public void FormatsTimes() => Assert.Equal("1:02.500", Runner.FormatTime(7500));
}
