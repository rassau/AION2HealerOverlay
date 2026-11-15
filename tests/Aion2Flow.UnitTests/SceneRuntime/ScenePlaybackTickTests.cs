using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Playback;
using Cloris.Aion2Flow.SceneRuntime.Projection;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

// What the playback window reads while it plays: frames, the event window and the timeline's samples.
public sealed class ScenePlaybackTickTests
{
    // The event window's index keeps its postings over identity changes that move no event, and rebuilds when one does:
    // 300's hits turn out to be its owner 100's, and 400, which 100 hit, turns out to be 500's summon. Read step by step it
    // must give what a session that builds the index in one go gives.
    [Theory]
    [InlineData(50)]
    [InlineData(150)]
    [InlineData(700)]
    public void EventIndex_StepByStepReadsMatchAFreshSessionAcrossLateSummonLinks(int stepMilliseconds)
    {
        var journal = new ObservedEventJournal();
        var sceneId = Guid.NewGuid();
        long ordinal = 0;
        AppendCombat(journal, sceneId, 300, 200, 50, ++ordinal, 1_000);
        AppendCombat(journal, sceneId, 100, 200, 70, ++ordinal, 1_100);
        AppendCombat(journal, sceneId, 100, 400, 90, ++ordinal, 1_200);
        AppendNickname(journal, sceneId, 100, "Healer", ++ordinal, 1_250);
        AppendCombat(journal, sceneId, 300, 200, 55, ++ordinal, 1_300);
        AppendSummon(journal, sceneId, 100, 300, ++ordinal, 1_500);
        AppendCombat(journal, sceneId, 300, 200, 60, ++ordinal, 1_600);
        AppendCombat(journal, sceneId, 100, 400, 95, ++ordinal, 1_700);
        AppendNickname(journal, sceneId, 600, "Tank", ++ordinal, 2_000);
        AppendCombat(journal, sceneId, 600, 200, 40, ++ordinal, 2_100);
        AppendSummon(journal, sceneId, 500, 400, ++ordinal, 2_500);
        AppendCombat(journal, sceneId, 100, 400, 99, ++ordinal, 2_600);
        AppendCombat(journal, sceneId, 400, 100, 33, ++ordinal, 2_650);
        AppendCombat(journal, sceneId, 100, 200, 80, ++ordinal, 3_000);
        AppendCombat(journal, sceneId, 300, 200, 65, ++ordinal, 3_100);
        journal.CompleteFlush(1);
        var payload = CreatePayload(journal, sceneId);
        List<ScenePlaybackEventScope> scopes = [ScenePlaybackEventScope.All];
        foreach (var id in new[] { 100, 200, 300, 400, 500, 600 })
        {
            scopes.Add(ScenePlaybackEventScope.ForCombatant(id));
            scopes.Add(ScenePlaybackEventScope.ForRelation(id, ScenePlaybackEventRelation.Outgoing));
            scopes.Add(ScenePlaybackEventScope.ForRelation(id, ScenePlaybackEventRelation.Incoming));
            scopes.Add(ScenePlaybackEventScope.ForCategory(id, ScenePlaybackEventRelation.Outgoing, CombatContributionCategory.Damage));
            scopes.Add(ScenePlaybackEventScope.ForCategory(id, ScenePlaybackEventRelation.Incoming, CombatContributionCategory.Damage));
        }

        var session = new ScenePlaybackSession(new ArchivedScenePlaybackSource(payload));
        var duration = session.Seek(0, TestContext.Current.CancellationToken).TimeRange.DurationMilliseconds;
        for (var position = 0L; position < duration + stepMilliseconds; position += stepMilliseconds)
        {
            var target = Math.Min(position, duration);
            session.AdvanceTo(target, TestContext.Current.CancellationToken);
            var fresh = new ScenePlaybackSession(new ArchivedScenePlaybackSource(payload));
            fresh.Seek(target, TestContext.Current.CancellationToken);
            foreach (var scope in scopes)
                Assert.Equal(ReadEvents(fresh, scope, duration), ReadEvents(session, scope, duration));
        }

        // The links did move events that were indexed before them: 300's first hit now counts as its owner's.
        Assert.Contains(ReadEvents(session, ScenePlaybackEventScope.All, duration), static marker => marker.PositionMilliseconds == 1_000 && marker.SourceEntityId == 100);
    }

    // The timeline's samples per track and per entity: the biggest marker of a bucket at the bucket's average position,
    // a magnitude tie keeping the first, long.MinValue as the biggest, a marker on the window's end in the last bucket,
    // an entity hit by itself counted once, ids of 0 and less left out, one entity on two tracks.
    [Fact]
    public void TimelineSampling_KeepsTheBiggestMarkerPerBucketAtTheAveragePosition()
    {
        ScenePlaybackTrackMarker[] markers =
        [
            Marker(0, ScenePlaybackTrack.Combat, 0, 1, 2, 10),
            Marker(1, ScenePlaybackTrack.Combat, 100, 1, 2, -30),
            Marker(2, ScenePlaybackTrack.Combat, 200, 2, 2, 30),
            Marker(3, ScenePlaybackTrack.Mechanic, 600, 0, 3, 0),
            Marker(4, ScenePlaybackTrack.Combat, 1_000, 3, -5, long.MinValue),
            Marker(5, ScenePlaybackTrack.Combat, 1_001, 1, 2, 999),
            Marker(6, ScenePlaybackTrack.EntityVital, 300, 1, 0, 5),
            Marker(7, ScenePlaybackTrack.Combat, 999, 1, 3, 7),
            Marker(8, ScenePlaybackTrack.Combat, 250, 1, 2, 40),
        ];

        var read = ScenePlaybackTrackReader.SampleTimeline(markers, 0, 1_000, 4, 2, TestContext.Current.CancellationToken);

        AssertSamples(markers, read.Global.Samples,
            (ScenePlaybackTrack.Combat, 1, 100, 3),
            (ScenePlaybackTrack.Combat, 8, 250, 1),
            (ScenePlaybackTrack.Combat, 4, 1_000, 2),
            (ScenePlaybackTrack.Mechanic, 3, 600, 1),
            (ScenePlaybackTrack.EntityVital, 6, 300, 1));
        Assert.Equal([new(ScenePlaybackTrack.Combat, 6), new(ScenePlaybackTrack.Mechanic, 1), new ScenePlaybackTrackCount(ScenePlaybackTrack.EntityVital, 1)], read.Global.TrackCounts);
        Assert.Equal([1, 2, 3], read.Combatants.Combatants.Select(static combatant => combatant.CombatantId));
        var first = read.Combatants.Combatants[0];
        AssertSamples(markers, first.Samples,
            (ScenePlaybackTrack.Combat, 8, 117, 3),
            (ScenePlaybackTrack.Combat, 7, 999, 1),
            (ScenePlaybackTrack.EntityVital, 6, 300, 1));
        Assert.Equal([new(ScenePlaybackTrack.Combat, 4), new ScenePlaybackTrackCount(ScenePlaybackTrack.EntityVital, 1)], first.TrackCounts);
        var second = read.Combatants.Combatants[1];
        AssertSamples(markers, second.Samples, (ScenePlaybackTrack.Combat, 8, 138, 4));
        Assert.Equal([new ScenePlaybackTrackCount(ScenePlaybackTrack.Combat, 4)], second.TrackCounts);
        var third = read.Combatants.Combatants[2];
        AssertSamples(markers, third.Samples,
            (ScenePlaybackTrack.Combat, 4, 1_000, 2),
            (ScenePlaybackTrack.Mechanic, 3, 600, 1));
        Assert.Equal([new(ScenePlaybackTrack.Combat, 2), new ScenePlaybackTrackCount(ScenePlaybackTrack.Mechanic, 1)], third.TrackCounts);

        var point = ScenePlaybackTrackReader.SampleTimeline(markers, 250, 250, 4, 2, TestContext.Current.CancellationToken);
        AssertSamples(markers, point.Global.Samples, (ScenePlaybackTrack.Combat, 8, 250, 1));
        Assert.Equal([1, 2], point.Combatants.Combatants.Select(static combatant => combatant.CombatantId));
        Assert.Equal(ScenePlaybackTimelineSampledReadResult.Empty, ScenePlaybackTrackReader.SampleTimeline(markers, 500, 400, 4, 2, TestContext.Current.CancellationToken));
    }

    // Most ticks at 60-144 Hz apply no entry: their frames share the snapshot and vitals of the frame before.
    [Fact]
    public void Frames_ShareTheSnapshotUntilAnEntryIsApplied()
    {
        var journal = new ObservedEventJournal();
        var sceneId = Guid.NewGuid();
        AppendCombat(journal, sceneId, 100, 200, 100, 1, 1_000);
        AppendEntityVital(journal, sceneId, 200, 30_000, 50_000, 2, 1_050);
        AppendCombat(journal, sceneId, 100, 200, 200, 3, 1_400);
        AppendCombat(journal, sceneId, 100, 200, 300, 4, 2_500);
        journal.CompleteFlush(1);
        var session = new ScenePlaybackSession(new ArchivedScenePlaybackSource(CreatePayload(journal, sceneId)));

        var first = session.Seek(1_100, TestContext.Current.CancellationToken);
        var idle = session.AdvanceTo(1_200, TestContext.Current.CancellationToken);
        var next = session.AdvanceTo(1_450, TestContext.Current.CancellationToken);

        Assert.Equal(1_200, idle.PositionMilliseconds);
        Assert.Same(first.Snapshot, idle.Snapshot);
        Assert.Same(first.EntityVitals, idle.EntityVitals);
        Assert.Equal(100, idle.CombatTotals.TotalDamage);
        Assert.NotSame(idle.Snapshot, next.Snapshot);
        Assert.Equal(300, next.CombatTotals.TotalDamage);
        Assert.Equal(300, next.Snapshot.Combatants[100].DamageAmount);
    }

    // A live playback tick must not refresh the live scene for its time range: that took the meter's pending changes, so
    // the meter skipped the frame that would have shown them.
    [Fact]
    public void LivePlaybackTick_LeavesTheMeterItsPendingChanges()
    {
        var scene = new SceneLiveReadModel();
        AppendLiveCombat(scene, 100, 200, 100, 1_000);
        using var source = scene.CreatePlaybackSource();
        var session = new ScenePlaybackSession(source);
        session.Seek(0, TestContext.Current.CancellationToken);
        scene.CreateFrame();

        AppendLiveCombat(scene, 100, 200, 200, 2_000);
        var frame = session.AdvanceTo(2_000, TestContext.Current.CancellationToken);

        Assert.Equal(2_000, frame.TimeRange.DurationMilliseconds);
        Assert.Equal(300, frame.CombatTotals.TotalDamage);
        Assert.True(scene.HasPendingProjectionChanges);
    }

    private static void AssertSamples(ScenePlaybackTrackMarker[] markers, IReadOnlyList<ScenePlaybackTrackSample> samples, params (ScenePlaybackTrack Track, int Marker, long Position, int Count)[] expected)
    {
        Assert.Equal(expected.Select(static sample => (sample.Track, (long)sample.Marker, sample.Position, sample.Count)),
            samples.Select(static sample => (sample.Marker.Track, sample.Marker.ObservationOrdinal, sample.Marker.PositionMilliseconds, sample.EventCount)));
        foreach (var sample in samples)
            Assert.Equal(markers[sample.Marker.ObservationOrdinal] with { PositionMilliseconds = sample.Marker.PositionMilliseconds }, sample.Marker);
    }

    private static ScenePlaybackTrackMarker Marker(long ordinal, ScenePlaybackTrack track, long position, int sourceId, int targetId, long amount)
        => new(track, position, position, ordinal, sourceId, targetId, default, ScenePlaybackCombatEventFlags.Damage, amount, null, null, 0, default, 0, 0, default, default);

    private static ScenePlaybackEventMarker[] ReadEvents(ScenePlaybackSession session, ScenePlaybackEventScope scope, long endPositionMilliseconds)
    {
        var buffer = new ScenePlaybackEventMarker[96];
        var read = session.CopyLatestMaterializedEvents(scope, 0, endPositionMilliseconds, buffer);
        return buffer.AsSpan(0, read.Count).ToArray();
    }

    private static SceneArchivePayload CreatePayload(ObservedEventJournal journal, Guid sceneId)
    {
        var owner = new SceneReadModelOwner(journal, sceneId, DateTimeOffset.Now);
        owner.CreateSnapshot();
        return owner.CreateArchivePayload();
    }

    private static void AppendCombat(ObservedEventJournal journal, Guid sceneId, int sourceId, int targetId, int damage, long ordinal, long observedAt)
    {
        var header = CreateHeader(sceneId, sourceId, targetId, ordinal, observedAt, new RawPacketReference(0x0438, 0, ordinal));
        var observation = new CombatWireObservation { SkillCode = 11000010, Damage = damage, HitCount = 1, AttemptCount = 1 };
        journal.Append(in header, in observation);
    }

    private static void AppendNickname(ObservedEventJournal journal, Guid sceneId, int entityId, string nickname, long ordinal, long observedAt)
    {
        var header = CreateHeader(sceneId, entityId, 0, ordinal, observedAt, new RawPacketReference(0, 0, ordinal));
        var observation = new StateObservation(entityId, StateCodes.PlayerIdentity, 0, 0, 0, nickname);
        journal.Append(in header, in observation);
    }

    // As JournalingRuntimeObservationSink.AppendSummon writes it.
    private static void AppendSummon(ObservedEventJournal journal, Guid sceneId, int ownerId, int summonId, long ordinal, long observedAt)
    {
        var header = CreateHeader(sceneId, ownerId, summonId, ordinal, observedAt, new RawPacketReference(0, 0, ordinal));
        var observation = new StateObservation(summonId, 0, ownerId, 0, 0, null);
        journal.Append(in header, in observation);
    }

    private static void AppendEntityVital(ObservedEventJournal journal, Guid sceneId, int entityId, long currentHp, long? maxHp, long ordinal, long observedAt)
    {
        var header = CreateHeader(sceneId, entityId, 0, ordinal, observedAt, new RawPacketReference(0, 0, ordinal));
        var observation = new EntityVitalObservation(entityId, currentHp, maxHp);
        journal.Append(in header, in observation);
    }

    private static void AppendLiveCombat(SceneLiveReadModel scene, int sourceId, int targetId, int damage, long observedAt)
    {
        var flushId = scene.NextFlushId();
        var stamp = scene.Clock.CreateStamp(scene.SessionStarted.ToUnixTimeMilliseconds() + observedAt, flushId);
        var header = new ObservedEventHeader(scene.SessionId, stamp, sourceId, targetId, new RawPacketReference(0x0438, 0, stamp.ObservationOrdinal));
        var observation = new CombatWireObservation { SkillCode = 11000010, Damage = damage, HitCount = 1, AttemptCount = 1 };
        scene.Journal.Append(in header, in observation);
        scene.Journal.CompleteFlush(flushId);
    }

    private static ObservedEventHeader CreateHeader(Guid sceneId, int sourceId, int targetId, long ordinal, long observedAt, RawPacketReference raw)
        => new(
            sceneId,
            new TimelineStamp { OffsetTicks = observedAt * TimeSpan.TicksPerMillisecond, ObservationOrdinal = ordinal - 1, FlushId = 1 },
            sourceId,
            targetId,
            raw);
}
