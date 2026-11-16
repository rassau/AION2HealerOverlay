using System.Globalization;
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

    // A recovery time in at most 4 characters: tenths below 10 s, whole seconds from there, 999 s at most; "–" for none.
    [Fact]
    public void Seconds_Fit_In_Four_Characters()
    {
        Assert.Equal(["–", "13s", "999s"], ((double[])[double.NaN, 13.4, 4_321]).Select(FightGraph.Seconds));
        Assert.Equal(5d.ToString("0.0", CultureInfo.CurrentCulture) + "s", FightGraph.Seconds(5));
        Assert.Equal(1.6.ToString("0.0", CultureInfo.CurrentCulture) + "s", FightGraph.Seconds(1.62));
        Assert.Equal([9.9, 10, 999, 0.1], ((double[])[9.94, 9.96, 1_234, 0.05]).Select(FightGraph.ShownSeconds));
        Assert.True(double.IsNaN(FightGraph.ShownSeconds(double.NaN)));
        Assert.Equal(["0.0's'", "0's'"], ((double[])[9.9, 10]).Select(value => (string?)FightGraph.SecondsFormatter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture)));
    }
}
