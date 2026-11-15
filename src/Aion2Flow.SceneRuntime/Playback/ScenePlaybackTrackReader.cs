using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.SceneRuntime.Playback;

public static class ScenePlaybackTrackReader
{
    private static readonly ScenePlaybackTrack[] Tracks = Enum.GetValues<ScenePlaybackTrack>();
    private static readonly int TrackBucketCount = ResolveTrackBucketCount();

    public static ScenePlaybackTimelineSampledReadResult SampleTimeline(
        ReadOnlySpan<ScenePlaybackTrackMarker> markers,
        long startPositionMilliseconds,
        long endPositionMilliseconds,
        int maxMarkersPerTrack,
        int maxMarkersPerCombatantTrack,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMarkersPerTrack);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMarkersPerCombatantTrack);
        if (markers.IsEmpty || endPositionMilliseconds < startPositionMilliseconds)
            return ScenePlaybackTimelineSampledReadResult.Empty;

        var buckets = new SampleBucket[checked(TrackBucketCount * maxMarkersPerTrack)];
        var trackCounts = new int[TrackBucketCount];
        var combatants = new Dictionary<int, CombatantSampleAccumulator>();
        var windowDuration = Math.Max(1, endPositionMilliseconds - startPositionMilliseconds);
        for (var i = 0; i < markers.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ref readonly var marker = ref markers[i];
            if (marker.PositionMilliseconds < startPositionMilliseconds || marker.PositionMilliseconds > endPositionMilliseconds)
                continue;

            var trackIndex = ToTrackIndex(marker.Track);
            trackCounts[trackIndex]++;
            var ratio = (marker.PositionMilliseconds - startPositionMilliseconds) / (double)windowDuration;
            var bucketIndex = Math.Clamp((int)(ratio * maxMarkersPerTrack), 0, maxMarkersPerTrack - 1);
            buckets[trackIndex * maxMarkersPerTrack + bucketIndex].Add(in marker, i);

            AddCombatantSample(combatants, marker.SourceEntityId, in marker, i, startPositionMilliseconds, windowDuration, maxMarkersPerCombatantTrack);
            if (marker.TargetEntityId != marker.SourceEntityId)
                AddCombatantSample(combatants, marker.TargetEntityId, in marker, i, startPositionMilliseconds, windowDuration, maxMarkersPerCombatantTrack);
        }

        return new ScenePlaybackTimelineSampledReadResult(
            CreateSampledResult(buckets, trackCounts, maxMarkersPerTrack, markers),
            CreateCombatantSampledResult(combatants, markers));
    }

    private static void AddCombatantSample(Dictionary<int, CombatantSampleAccumulator> combatants, int combatantId, in ScenePlaybackTrackMarker marker, int markerIndex, long startPositionMilliseconds, long windowDuration, int maxMarkersPerCombatantTrack)
    {
        if (combatantId <= 0)
            return;

        if (!combatants.TryGetValue(combatantId, out var accumulator))
        {
            accumulator = new CombatantSampleAccumulator(maxMarkersPerCombatantTrack);
            combatants.Add(combatantId, accumulator);
        }

        var bucketIndex = Math.Clamp((int)((marker.PositionMilliseconds - startPositionMilliseconds) / (double)windowDuration * maxMarkersPerCombatantTrack), 0, maxMarkersPerCombatantTrack - 1);
        accumulator.Add(in marker, markerIndex, bucketIndex);
    }

    private static ScenePlaybackTrackSampledReadResult CreateSampledResult(SampleBucket[] buckets, int[] trackCounts, int maxMarkersPerTrack, ReadOnlySpan<ScenePlaybackTrackMarker> markers)
    {
        var samples = new List<ScenePlaybackTrackSample>(Math.Min(buckets.Length, 256));
        var counts = new List<ScenePlaybackTrackCount>(Tracks.Length);
        for (var trackOrdinal = 0; trackOrdinal < Tracks.Length; trackOrdinal++)
        {
            var track = Tracks[trackOrdinal];
            var trackIndex = ToTrackIndex(track);
            var count = trackCounts[trackIndex];
            if (count == 0)
                continue;

            counts.Add(new ScenePlaybackTrackCount(track, count));
            var offset = trackIndex * maxMarkersPerTrack;
            for (var bucketIndex = 0; bucketIndex < maxMarkersPerTrack; bucketIndex++)
            {
                ref readonly var bucket = ref buckets[offset + bucketIndex];
                if (bucket.Count > 0)
                    samples.Add(bucket.CreateSample(markers));
            }
        }

        return new ScenePlaybackTrackSampledReadResult(samples.ToArray(), counts.ToArray());
    }

    private static ScenePlaybackCombatantTrackSampledReadResult CreateCombatantSampledResult(Dictionary<int, CombatantSampleAccumulator> combatants, ReadOnlySpan<ScenePlaybackTrackMarker> markers)
    {
        if (combatants.Count == 0)
            return ScenePlaybackCombatantTrackSampledReadResult.Empty;

        var results = new List<ScenePlaybackCombatantTrackSamples>(combatants.Count);
        foreach (var (combatantId, accumulator) in combatants)
            results.Add(accumulator.CreateSamples(combatantId, markers));
        return new ScenePlaybackCombatantTrackSampledReadResult(results.ToArray());
    }

    // Keeps the index of its biggest marker in the sampled span (alive for the whole call), not a 168-byte copy of it: 24
    // bytes a bucket, so neither the global buckets nor an entity's go to the large object heap.
    private struct SampleBucket
    {
        private ulong _magnitude;
        private double _averagePosition;
        private int _markerIndex;

        public int Count { get; private set; }

        public void Add(in ScenePlaybackTrackMarker marker, int markerIndex)
        {
            Count++;
            _averagePosition += (marker.PositionMilliseconds - _averagePosition) / Count;
            var magnitude = AbsoluteMagnitude(marker.Amount);
            if (Count == 1 || magnitude > _magnitude)
            {
                _markerIndex = markerIndex;
                _magnitude = magnitude;
            }
        }

        public readonly ScenePlaybackTrackSample CreateSample(ReadOnlySpan<ScenePlaybackTrackMarker> markers)
            => new(markers[_markerIndex] with { PositionMilliseconds = (long)Math.Round(_averagePosition, MidpointRounding.AwayFromZero) }, Count);

        private static ulong AbsoluteMagnitude(long value)
            => value >= 0 ? (ulong)value : (ulong)(-(value + 1)) + 1;
    }

    // Buckets only for the tracks an entity has markers on, and result arrays of the exact size.
    private sealed class CombatantSampleAccumulator(int maxMarkersPerCombatantTrack)
    {
        private readonly SampleBucket[]?[] _trackBuckets = new SampleBucket[]?[TrackBucketCount];
        private readonly int[] _trackCounts = new int[TrackBucketCount];
        private readonly int _maxMarkersPerTrack = maxMarkersPerCombatantTrack;

        public void Add(in ScenePlaybackTrackMarker marker, int markerIndex, int bucketIndex)
        {
            var trackIndex = ToTrackIndex(marker.Track);
            _trackCounts[trackIndex]++;
            (_trackBuckets[trackIndex] ??= new SampleBucket[_maxMarkersPerTrack])[bucketIndex].Add(in marker, markerIndex);
        }

        public ScenePlaybackCombatantTrackSamples CreateSamples(int combatantId, ReadOnlySpan<ScenePlaybackTrackMarker> markers)
        {
            var sampleCount = 0;
            var trackCount = 0;
            for (var trackOrdinal = 0; trackOrdinal < Tracks.Length; trackOrdinal++)
            {
                var trackIndex = ToTrackIndex(Tracks[trackOrdinal]);
                if (_trackCounts[trackIndex] == 0)
                    continue;

                trackCount++;
                foreach (ref readonly var bucket in _trackBuckets[trackIndex].AsSpan())
                {
                    if (bucket.Count > 0)
                        sampleCount++;
                }
            }

            var samples = new ScenePlaybackTrackSample[sampleCount];
            var counts = new ScenePlaybackTrackCount[trackCount];
            sampleCount = 0;
            trackCount = 0;
            for (var trackOrdinal = 0; trackOrdinal < Tracks.Length; trackOrdinal++)
            {
                var track = Tracks[trackOrdinal];
                var trackIndex = ToTrackIndex(track);
                var count = _trackCounts[trackIndex];
                if (count == 0)
                    continue;

                counts[trackCount++] = new ScenePlaybackTrackCount(track, count);
                foreach (ref readonly var bucket in _trackBuckets[trackIndex].AsSpan())
                {
                    if (bucket.Count > 0)
                        samples[sampleCount++] = bucket.CreateSample(markers);
                }
            }

            return new ScenePlaybackCombatantTrackSamples(combatantId, samples, counts);
        }
    }

    private static int ToTrackIndex(ScenePlaybackTrack track)
    {
        var index = (int)track;
        if ((uint)index >= (uint)TrackBucketCount)
            throw new InvalidOperationException($"Unknown playback track: {track}.");

        return index;
    }

    private static int ResolveTrackBucketCount()
    {
        var max = -1;
        for (var i = 0; i < Tracks.Length; i++)
        {
            var index = (int)Tracks[i];
            if (index < 0)
                throw new InvalidOperationException($"Playback track cannot be negative: {Tracks[i]}.");

            max = Math.Max(max, index);
        }

        return checked(max + 1);
    }

}

[Flags]
public enum ScenePlaybackCombatEventFlags : byte
{
    None = 0,
    Damage = 1 << 0,
    Healing = 1 << 1,
    Shield = 1 << 2
}

public readonly record struct ScenePlaybackAuraIdentity
{
    private ScenePlaybackAuraIdentity(ResourceEffectRef displayResourceEffectRef, int instanceSequenceId)
    {
        DisplayResourceEffectRef = displayResourceEffectRef;
        InstanceSequenceId = instanceSequenceId;
    }

    public ResourceEffectRef DisplayResourceEffectRef { get; }

    public int InstanceSequenceId { get; }

    public bool IsEmpty => DisplayResourceEffectRef.IsEmpty && InstanceSequenceId <= 0;

    public static ScenePlaybackAuraIdentity Create(ResourceEffectRef displayResourceEffectRef, int instanceSequenceId)
        => !displayResourceEffectRef.IsEmpty
            ? new ScenePlaybackAuraIdentity(displayResourceEffectRef, 0)
            : instanceSequenceId > 0
                ? new ScenePlaybackAuraIdentity(default, instanceSequenceId)
                : default;
}

public readonly record struct ScenePlaybackTrackMarker(
    ScenePlaybackTrack Track,
    long PositionMilliseconds,
    long OffsetMilliseconds,
    long ObservationOrdinal,
    int SourceEntityId,
    int TargetEntityId,
    CombatEventKey EventKey,
    ScenePlaybackCombatEventFlags CombatEventFlags,
    long Amount,
    long? CurrentHp,
    long? MaxHp,
    int ResultCode,
    AuraLifecycleEventKind LifecycleEventKind,
    int InstanceSequenceId,
    long DurationMilliseconds,
    ResourceEffectRef DisplayResourceEffectRef,
    AuraSemanticValue AuraSemantics)
{
    public int SkillCode => EventKey.SkillCode;
    public uint DisplayResourceEffectRefRaw => DisplayResourceEffectRef.RawId;
    public AuraDisposition AuraDisposition => AuraSemantics.Disposition;
    public AuraSemanticTrace AuraSemanticTrace => AuraSemantics.Trace;
    public ScenePlaybackAuraIdentity AuraIdentity => ScenePlaybackAuraIdentity.Create(DisplayResourceEffectRef, InstanceSequenceId);
}

public readonly record struct ScenePlaybackTrackSample(ScenePlaybackTrackMarker Marker, int EventCount);

public readonly record struct ScenePlaybackTrackCount(ScenePlaybackTrack Track, int Count);

public readonly record struct ScenePlaybackTrackSampledReadResult(IReadOnlyList<ScenePlaybackTrackSample> Samples, IReadOnlyList<ScenePlaybackTrackCount> TrackCounts)
{
    public static ScenePlaybackTrackSampledReadResult Empty { get; } = new([], []);
}

public readonly record struct ScenePlaybackCombatantTrackSamples(int CombatantId, IReadOnlyList<ScenePlaybackTrackSample> Samples, IReadOnlyList<ScenePlaybackTrackCount> TrackCounts);

public readonly record struct ScenePlaybackCombatantTrackSampledReadResult(IReadOnlyList<ScenePlaybackCombatantTrackSamples> Combatants)
{
    public static ScenePlaybackCombatantTrackSampledReadResult Empty { get; } = new([]);
}

public readonly record struct ScenePlaybackTimelineSampledReadResult(
    ScenePlaybackTrackSampledReadResult Global,
    ScenePlaybackCombatantTrackSampledReadResult Combatants)
{
    public static ScenePlaybackTimelineSampledReadResult Empty { get; } = new(
        ScenePlaybackTrackSampledReadResult.Empty,
        ScenePlaybackCombatantTrackSampledReadResult.Empty);
}
