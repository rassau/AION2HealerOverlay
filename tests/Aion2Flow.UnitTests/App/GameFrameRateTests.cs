using Cloris.Aion2Flow.Services;

namespace Cloris.Aion2Flow.Tests.App;

// The frame rate from the game's presents (here 600 ticks a second, so a frame every 10 ticks is 60 fps).
public sealed class GameFrameRateTests
{
    private const long Frequency = 600;

    // A steady 60 fps reads 60 in every window however it falls between the frames, the presents in any order.
    [Fact]
    public void A_Steady_Game_Reads_Its_Rate_In_Every_Window()
    {
        long[] presents = [.. Enumerable.Range(0, 200).Select(static frame => frame * 10L)];
        long[] shuffled = [.. presents.OrderBy(static at => (at * 7_919) % 1_009)];
        for (var start = 5L; start < 1_800; start += 37)
        {
            Assert.Equal(60, GameFrameRate.FramesPerSecond(presents, start, start + 60, 0, Frequency), 6);
            Assert.Equal(60, GameFrameRate.FramesPerSecond(shuffled, start, start + 60, 0, Frequency), 6);
        }
    }

    // 5 fps measured every 0.1 s: a window with its frame reads 5, and one between two frames no more than that.
    [Fact]
    public void A_Slow_Game_Reads_Its_Rate_Between_Its_Frames_Too()
    {
        long[] presents = [.. Enumerable.Range(0, 20).Select(static frame => frame * 120L)];
        var rate = 0d;
        for (var start = 0L; start < 2_000; start += 60)
        {
            rate = GameFrameRate.FramesPerSecond(presents, start, start + 60, rate, Frequency);
            if (start > 0)
                Assert.Equal(5, rate, 6);
        }
    }

    // 60 fps with slow frames (50 ticks) over the last 2 s, about 117 frame times: one is under 1 % of them and leaves the
    // 1 % low at 60, two pull it down to their rate; the presents in any order. A stall counts with its time so far.
    [Fact]
    public void The_One_Percent_Low_Goes_By_The_Slowest_Frames()
    {
        static long[] Frames(params int[] slow)
        {
            var (at, presents) = (0L, new List<long> { 0 });
            for (var frame = 1; frame <= 130; frame++)
                presents.Add(at += slow.Contains(frame) ? 50 : 10);
            return [.. presents.OrderBy(static t => (t * 7_919) % 1_009)];
        }

        Assert.Equal(60, GameFrameRate.OnePercentLow(Frames(60), 1_345 - 1_200, 1_345, Frequency), 6);
        Assert.Equal(12, GameFrameRate.OnePercentLow(Frames(60, 90), 1_385 - 1_200, 1_385, Frequency), 6);
        Assert.Equal(600 / 2_180d, GameFrameRate.OnePercentLow([0, 10, 20], 1_000, 2_200, Frequency), 6);
        Assert.Equal(0, GameFrameRate.OnePercentLow([], 0, 1_200, Frequency));
    }

    // A game that stops showing frames: at most one over the time since the last; none at all yet, 0. The first window
    // after the session starts, with no frame before it, counts from its start.
    [Fact]
    public void Without_Frames_The_Rate_Falls_Away()
    {
        long[] presents = [0, 10, 20];
        Assert.Equal(3, GameFrameRate.FramesPerSecond(presents, 100, 220, 60, Frequency), 6);
        Assert.Equal(0.5, GameFrameRate.FramesPerSecond(presents, 1_000, 1_220, 3, Frequency), 6);
        Assert.Equal(0, GameFrameRate.FramesPerSecond([], 0, 60, 60, Frequency));
        Assert.Equal(60, GameFrameRate.FramesPerSecond([110, 120, 130], 100, 160, 0, Frequency), 6);
    }
}
