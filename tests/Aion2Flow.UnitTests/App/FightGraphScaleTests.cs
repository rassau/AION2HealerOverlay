using Avalonia.Media;
using Cloris.Aion2Flow.Controls;

namespace Cloris.Aion2Flow.Tests.App;

// The fight graphs' scales: the overview in thousands keeps aggro, stagger and the recovery time on their own peak.
[Collection(AvaloniaTestCollection.Name)]
public sealed class FightGraphScaleTests
{
    // The amounts share the left scale, rounded up to a nice number; a curve on its own peak keeps out of it and goes up to
    // its own highest value, on a right axis that reads 0-100 %. As the overview was (and is at first): every curve on its
    // own peak, the left axis in %.
    [Fact]
    public void A_Curve_On_Its_Own_Peak_Keeps_Out_Of_The_Shared_Scale()
    {
        AvaloniaTestHost.Run(() =>
        {
            var damage = new FightGraphSeries("DPS", [100_000, 230_000], Colors.Blue, FightGraphStyle.Line, FightGraph.Compact);
            var aggro = new FightGraphSeries("Aggro", [0.2, 0.6], Colors.Green, FightGraphStyle.Line, static value => value.ToString("P0")) { RightAxis = true, OwnPeak = true };
            var thousands = new FightGraphModel(2, [damage, aggro], []);
            var graph = new FightGraph { Model = thousands };

            var (left, right) = graph.Scales(thousands);
            Assert.Equal((240_000d, 0.6, 240_000d), (left, graph.ScaleOf(aggro, thousands, left, right), graph.ScaleOf(damage, thousands, left, right)));
            Assert.Equal((FightGraph.Compact(240_000), "100%"), FightGraph.Labels(thousands, 4, left, right));
            Assert.Equal((FightGraph.Compact(120_000), "50%"), FightGraph.Labels(thousands, 2, left, right));

            var percent = new FightGraphModel(2, [damage, aggro with { RightAxis = false, OwnPeak = false }], []) { Normalized = true };
            graph.Model = percent;
            (left, right) = graph.Scales(percent);
            Assert.Equal(230_000d, graph.ScaleOf(damage, percent, left, right));
            Assert.Equal(("100%", (string?)null), FightGraph.Labels(percent, 4, left, right));
        });
    }
}
