using System.Runtime.InteropServices;

namespace Cloris.Aion2Flow.Services;

// The user's pings over the last hour as the meter measured them (the game's echo, one every 10 s), for the footer's 99th
// percentile and the ping graph. UI thread only.
public sealed class PingHistory
{
    public const long KeptMilliseconds = 3_600_000;
    // How far back the 99th percentile looks.
    public const long PercentileMilliseconds = 120_000;
    private readonly List<PingSample> _samples = [];

    // Oldest first.
    public IReadOnlyList<PingSample> Samples => _samples;

    public event Action? Changed;

    public void Add(long atMilliseconds, double milliseconds)
    {
        _samples.Add(new PingSample(atMilliseconds, milliseconds));
        var kept = _samples.FindIndex(sample => sample.At > atMilliseconds - KeptMilliseconds);
        if (kept > 0)
            _samples.RemoveRange(0, kept);
        Changed?.Invoke();
    }

    public double? Percentile99(long atMilliseconds) => Percentile99(CollectionsMarshal.AsSpan(_samples), atMilliseconds);

    // The ping that 99 % of the pings in the two minutes up to a moment stayed at or below (nearest rank), null without
    // any. With a ping every 10 s that's the highest of the last 12 or so.
    internal static double? Percentile99(ReadOnlySpan<PingSample> samples, long atMilliseconds)
    {
        // ponytail: one reused list instead of LINQ (the footer asks on every meter refresh, the graph for every ping it
        // shows). Every sample is looked at, as they need not be in order (the wall clock can step back); List.Sort orders
        // like Order() since pings are finite and never -0.
        var window = t_window ??= [];
        window.Clear();
        foreach (ref readonly var sample in samples)
        {
            if (sample.At > atMilliseconds - PercentileMilliseconds && sample.At <= atMilliseconds)
                window.Add(sample.Milliseconds);
        }

        if (window.Count == 0)
            return null;

        window.Sort();
        return window[(int)Math.Ceiling(0.99 * window.Count) - 1];
    }

    [ThreadStatic]
    private static List<double>? t_window;
}

public readonly record struct PingSample(long At, double Milliseconds);
