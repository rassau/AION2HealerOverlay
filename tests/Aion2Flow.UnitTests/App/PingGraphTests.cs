using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.Services;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class PingGraphTests
{
    // The game measures a ping every 10 s: the 99th percentile of two minutes is the highest of the last 12 there, and an
    // hour is kept. With many pings it's the nearest rank: the 198th lowest of 200.
    [Fact]
    public void The_Footers_Percentile_Is_What_99_Percent_Of_Two_Minutes_Pings_Stayed_At_Or_Below()
    {
        var history = new PingHistory();
        Assert.Null(history.Percentile99(0));
        for (var i = 0; i <= 400; i++)
            history.Add(i * 10_000L, i == 382 ? 300 : i == 394 ? 80 : 20 + i % 3);

        Assert.Equal((360, 410_000L), (history.Samples.Count, history.Samples[0].At));
        Assert.Equal(80, history.Percentile99(4_000_000));
        Assert.Equal(300, history.Percentile99(3_900_000));
        Assert.Null(history.Percentile99(4_200_000));

        var busy = new PingHistory();
        for (var i = 1; i <= 200; i++)
            busy.Add(i * 500L, i);
        Assert.Equal(198, busy.Percentile99(100_000));
    }

    // Without LINQ the percentile picks what Where/Order did, in whatever order the pings are (the wall clock can step
    // back between two), with ties and windows of over 100 pings.
    [Fact]
    public void The_Percentile_Picks_What_The_Ordered_Window_Did()
    {
        var random = new Random(4711);
        for (var round = 0; round < 300; round++)
        {
            var samples = new List<PingSample>();
            var at = 1_000_000L;
            for (var i = random.Next(0, 300); i > 0; i--)
            {
                at += random.Next(-30_000, 20_000);
                samples.Add(new PingSample(at, random.Next(0, 4) == 0 ? 50 : random.NextDouble() * 400));
            }

            for (var query = 0; query < 20; query++)
            {
                var moment = at + random.Next(-200_000, 50_000);
                var window = samples
                    .Where(sample => sample.At > moment - PingHistory.PercentileMilliseconds && sample.At <= moment)
                    .Select(static sample => sample.Value)
                    .Order()
                    .ToArray();
                Assert.Equal(window.Length == 0 ? null : window[(int)Math.Ceiling(0.99 * window.Length) - 1], PingHistory.Percentile99(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(samples), moment));
            }
        }
    }

    // Ten minutes at a time, from the first ping's to now: moved back it stays there, past now it follows the pings again.
    [Fact]
    public void The_Graph_Moves_Along_The_Hour_Kept()
    {
        AvaloniaTestHost.Run(() =>
        {
            var now = DateTimeOffset.FromUnixTimeMilliseconds(4_000_000);
            var history = new PingHistory();
            for (var at = 1_000_000L; at <= now.ToUnixTimeMilliseconds(); at += 10_000)
                history.Add(at, 20);
            var graph = new PingGraph { History = history, Clock = new FixedTimeProvider(now) };

            Assert.Equal(4_000_000, graph.ViewEnd);
            graph.MoveTo(3_000_000);
            Assert.Equal(3_000_000, graph.ViewEnd);
            graph.MoveTo(0);
            Assert.Equal(1_000_000 + PingGraph.SpanMilliseconds, graph.ViewEnd);
            graph.MoveTo(5_000_000);
            Assert.Equal(4_000_000, graph.ViewEnd);
        });
    }

    // The game's frame rate, ten minutes kept: two minutes at a time, drawn with its 1 % low.
    [Fact]
    public void The_Frame_Rate_Graph_Moves_Along_The_Ten_Minutes_Kept()
    {
        AvaloniaTestHost.Run(() =>
        {
            var now = DateTimeOffset.FromUnixTimeMilliseconds(4_000_000);
            var history = new PingHistory(600_000);
            for (var at = 3_000_000L; at <= now.ToUnixTimeMilliseconds(); at += 250)
                history.Add(at, 55 + at % 7, 40 + at % 5);
            Assert.Equal(3_400_250L, history.Samples[0].At);

            var graph = new PingGraph { History = history, ShowsFrameRate = true, Clock = new FixedTimeProvider(now) };
            graph.MoveTo(0);
            Assert.Equal(3_400_250L + PingGraph.FrameRateSpanMilliseconds, graph.ViewEnd);
            graph.MoveTo(3_900_000);
            graph.Measure(new Avalonia.Size(320, 140));
            graph.Arrange(new Avalonia.Rect(0, 0, 320, 140));
            using var target = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(320, 140));
            target.Render(graph);
        });
    }

    // Smooth curve: a spike is averaged into the values around it (FightGraph.Smoothed) and a gap stops the average;
    // hovering keeps each value's own. Off, the lines are the values.
    [Fact]
    public void A_Smooth_Curve_Averages_Up_To_A_Gap()
    {
        AvaloniaTestHost.Run(() =>
        {
            var now = DateTimeOffset.FromUnixTimeMilliseconds(4_000_000);
            var history = new PingHistory(600_000);
            for (var at = 3_900_000L; at <= 3_950_000; at += 250)
                history.Add(at, at == 3_925_000 ? 120 : 60, 50);
            for (var at = 3_960_000L; at <= 4_000_000; at += 250)
                history.Add(at, 90, 50);

            var graph = new PingGraph { History = history, ShowsFrameRate = true, Smooth = true, Clock = new FixedTimeProvider(now) };
            var shown = graph.Shown(3_880_000, 4_000_000);
            var spike = shown.Single(static item => item.Sample.At == 3_925_000);
            Assert.Equal(120, spike.Sample.Value);
            Assert.InRange(spike.Line, 60.1, 119.9);
            Assert.Equal(50, spike.PercentileLine, 9);
            Assert.Equal(60, shown.Single(static item => item.Sample.At == 3_950_000).Line, 9);
            Assert.Equal(90, shown.Single(static item => item.Sample.At == 3_960_000).Line, 9);

            graph.Smooth = false;
            Assert.All(graph.Shown(3_880_000, 4_000_000), static item => Assert.Equal(item.Sample.Value, item.Line));
        });
    }

    // The wifi and monitor icons: each opens and closes its own graph, the other stays as it is; after the meter's folding
    // closed both, a click opens its graph alone. (Graph, other, popup open.)
    [Fact]
    public void Each_Icon_Opens_And_Closes_Its_Own_Graph()
    {
        Assert.Equal((true, false, true), Cloris.Aion2Flow.Views.MainWindow.Toggled(open: false, graph: false, other: false));
        Assert.Equal((true, false, true), Cloris.Aion2Flow.Views.MainWindow.Toggled(open: false, graph: true, other: true));
        Assert.Equal((true, true, true), Cloris.Aion2Flow.Views.MainWindow.Toggled(open: true, graph: false, other: true));
        Assert.Equal((false, true, true), Cloris.Aion2Flow.Views.MainWindow.Toggled(open: true, graph: true, other: true));
        Assert.Equal((false, false, false), Cloris.Aion2Flow.Views.MainWindow.Toggled(open: true, graph: true, other: false));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
