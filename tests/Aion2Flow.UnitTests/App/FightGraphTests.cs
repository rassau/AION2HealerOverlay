using Cloris.Aion2Flow.Controls;

namespace Cloris.Aion2Flow.Tests.App;

public sealed class FightGraphTests
{
    // "Smooth curves": a one-second spike spread over the seconds around it, the same on both sides (it stays where it was)
    // and with its total kept; a flat curve stays flat to its ends, and a gap stays a gap.
    [Fact]
    public void Smoothing_Spreads_A_Spike_Evenly_And_Keeps_Gaps()
    {
        var spike = new double[41];
        spike[20] = 1_000;
        var smoothed = FightGraph.Smoothed(spike);
        Assert.InRange(smoothed[20], 195, 205);
        Assert.Equal(20, Array.IndexOf(smoothed, smoothed.Max()));
        Assert.Equal(smoothed[17], smoothed[23], 9);
        Assert.Equal(1_000, smoothed.Sum(), 6);

        Assert.All(FightGraph.Smoothed([5, 5, 5, 5, 5]), static value => Assert.Equal(5, value, 9));
        var gap = FightGraph.Smoothed([10, double.NaN, 10]);
        Assert.True(double.IsNaN(gap[1]));
        Assert.Equal(10, gap[0], 9);
    }
}
