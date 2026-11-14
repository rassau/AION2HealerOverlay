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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
