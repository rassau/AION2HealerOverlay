using System.IO.Compression;
using Cloris.Aion2Flow.Capture.Diagnostics;
using Cloris.Aion2Flow.Resources.Catalog;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Tests.Protocol;

namespace Cloris.Aion2Flow.Tests.Capture;

// The fight graphs, a fight's details and best runs read only a pull's own events: for every pull of the recorded sessions,
// at several moments while it went on (the live scene) and from the history entries, they and the second-by-second values
// are those of the scene's whole events cut to the pull. AION2HEAL_FIGHT_GRAPH_RECORDINGS names more recordings to check
// (paths separated by ';'), e.g. long sessions that can't be checked in.
public sealed class FightGraphReplayTests
{
    [Fact]
    public void Pulls_Work_Out_The_Same_From_Their_Own_Events()
    {
        CombatResourceRegistry.SetGameResources(ResourceCatalog.Load(ResourceLanguage.English));
        var pulls = 0;
        foreach (var path in Directory.GetFiles(FixtureHelper.GetPath("logs")).Order(StringComparer.Ordinal))
            pulls += ComparePulls(path, moments: 3);
        TestContext.Current.TestOutputHelper?.WriteLine($"fixtures: {pulls} pulls compared");
        foreach (var path in (Environment.GetEnvironmentVariable("AION2HEAL_FIGHT_GRAPH_RECORDINGS") ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var compared = ComparePulls(path, moments: 12);
            TestContext.Current.TestOutputHelper?.WriteLine($"{Path.GetFileName(path)}: {compared} pulls compared");
            pulls += compared;
        }

        Assert.True(pulls > 0);
    }

    // The recording replayed up to each moment (an equal share of its lines more each time), every pull of the scene then
    // compared; at the end also every pull of its history entries. Returns the pulls compared.
    private static int ComparePulls(string path, int moments)
    {
        string[] lines;
        using (var reader = path.EndsWith(PacketRecording.Extension, StringComparison.OrdinalIgnoreCase)
                   ? new StreamReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress))
                   : File.OpenText(path))
        {
            lines = reader.ReadToEnd().Split('\n');
        }

        var pulls = 0;
        for (var moment = 1; moment <= moments; moment++)
        {
            var replay = PacketLogReplayService.Replay(new StringReader(string.Join('\n', lines, 0, lines.Length * moment / moments)), Path.GetFileName(path));
            var owner = replay.SceneOwner;
            var frame = owner.CreateFrame();
            var journal = owner.CreateLiveTimelineSegment().CreateBoundedSnapshot();
            foreach (var fight in frame.BossFights)
            {
                var (from, to) = (fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime);
                var players = BestRunRecorder.PlayersOf(fight);
                var expected = new Dictionary<int, CombatDetailDelta>();
                foreach (var id in players.Append(fight.BossId))
                {
                    var whole = owner.ReadLocked((_, _, _, _, _, _, adapter) => adapter.CreateDetailEvents(frame.Snapshot, id));
                    expected[id] = new CombatDetailDelta { MetricEvents = whole.MetricEvents, MechanicEvents = whole.MechanicEvents, ResourceEvents = whole.ResourceEvents }
                        .Within(from, to);
                    var detail = owner.CreateDetailDeltaWithin(frame.Snapshot, id, from, to);
                    Assert.Equal(expected[id].MetricEvents, detail.MetricEvents);
                    Assert.Equal(expected[id].MechanicEvents, detail.MechanicEvents);
                    Assert.Equal(expected[id].ResourceEvents, detail.ResourceEvents);
                }

                var events = owner.CreateMetricEventsWithin(frame.Snapshot, players, from, to);
                foreach (var id in players)
                    Assert.Equal(expected[id].MetricEvents, events.TryGetValue(id, out var list) ? list : []);

                var me = frame.MetadataRegistry?.LocalPlayerEntityId is int local && players.Contains(local) ? local : players.FirstOrDefault();
                // The refresh a moment earlier, whose journal checkpoint the next one takes up.
                var earlier = FightTimeline.Build(from, from + (to - from) / 2, fight.BossId, players, static _ => new FightEvents(static _ => []), journal, 0, true,
                    TestContext.Current.CancellationToken, null);
                Assert.NotNull(earlier.Checkpoint);
                foreach (var overhealing in (bool[])[true, false])
                {
                    var reference = FightTimeline.Build(from, to, fight.BossId, players, id => expected[id].MetricEvents, journal, me, overhealing, TestContext.Current.CancellationToken);
                    AssertSameTimeline(reference,
                        FightTimeline.Build(from, to, fight.BossId, players, _ => new FightEvents(id => events.TryGetValue(id, out var list) ? list : []), journal, me, overhealing,
                            TestContext.Current.CancellationToken, earlier));
                    AssertSameTimeline(reference,
                        FightTimeline.Build(from, to, fight.BossId, players, id => events.TryGetValue(id, out var list) ? list : [], journal, me, overhealing,
                            TestContext.Current.CancellationToken));

                    // Refreshes up to a later end each, every one taking up the one before (the fight graphs while the pull
                    // went on, but on the scene as it is now: hits after an end come in with the next).
                    FightTimeline? previous = null;
                    for (var step = 1; step <= 6; step++)
                    {
                        var end = step == 6 ? to : from + (to - from) * step / 6;
                        previous = FightTimeline.Build(from, end, fight.BossId, players, since => owner.CreateFightEvents(frame.Snapshot, players, from, end, since), journal, me,
                            overhealing, TestContext.Current.CancellationToken, previous);
                        AssertSameTimeline(FightTimeline.Build(from, end, fight.BossId, players, id => expected[id].MetricEvents.Where(e => e.ObservedAtMilliseconds <= end).ToList(),
                            journal, me, overhealing, TestContext.Current.CancellationToken), previous);
                    }
                }

                pulls++;
            }

            if (moment < moments)
                continue;

            foreach (var payload in replay.MapTransitionArchives.Append(owner.CreateArchivePayload()))
            {
                foreach (var fight in payload.BossFights)
                {
                    var (from, to) = (fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime);
                    var players = BestRunRecorder.PlayersOf(fight);
                    foreach (var id in players)
                        Assert.Equal(payload.CreateDetailDelta(id).Within(from, to).MetricEvents, payload.CreateMetricEventsWithin(id, from, to));

                    AssertSameTimeline(
                        FightTimeline.Build(from, to, fight.BossId, players, id => payload.CreateDetailDelta(id).Within(from, to).MetricEvents, payload.TimelineSegment,
                            players.FirstOrDefault(), cancellationToken: TestContext.Current.CancellationToken),
                        FightTimeline.Build(from, to, fight.BossId, players, id => payload.CreateMetricEventsWithin(id, from, to), payload.TimelineSegment,
                            players.FirstOrDefault(), cancellationToken: TestContext.Current.CancellationToken));
                    pulls++;
                }
            }
        }

        return pulls;
    }

    private static void AssertSameTimeline(FightTimeline expected, FightTimeline actual)
    {
        Assert.Equal(expected.Seconds, actual.Seconds);
        Assert.Equal(expected.Values.Keys.Order(), actual.Values.Keys.Order());
        foreach (var (player, values) in expected.Values)
        {
            for (var metric = 0; metric < values.Length; metric++)
                Assert.Equal(values[metric], actual.Values[player][metric]);
        }

        Assert.Equal(expected.Deaths, actual.Deaths);
        Assert.Equal(expected.Ping, actual.Ping);
        Assert.Equal(expected.Sources.Length, actual.Sources.Length);
        for (var metric = 0; metric < expected.Sources.Length; metric++)
        {
            for (var second = 0; second < expected.Sources[metric].Length; second++)
                Assert.Equal(expected.Sources[metric][second], actual.Sources[metric][second]);
        }
    }
}
