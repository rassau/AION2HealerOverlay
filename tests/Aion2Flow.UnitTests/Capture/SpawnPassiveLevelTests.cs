using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Playback;
using Cloris.Aion2Flow.SceneRuntime.Twins;

using static Cloris.Aion2Flow.Tests.Capture.TransportTestData;

namespace Cloris.Aion2Flow.Tests.Capture;

// A player coming into sight (0x4536) lists their lasting auras, all ten passives among them, each with its level: the
// made-up spawn of PlayerPositionsTests, here for player 6802 (varint 92 35, another made-up name), with two of their
// passives appended.
public sealed class SpawnPassiveLevelTests
{
    private const int Player = 6_802;

    [Fact]
    public void A_Spawns_Passive_Levels_Go_Into_The_Journal()
    {
        var scene = Spawn(withPassives: true);
        var segment = scene.Owner.CreateLiveTimelineSegment();
        var states = new List<(int, long, long)>();
        segment.ReadEntries(segment.CreateCursor(), 512, entries =>
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Domain == ObservedEventDomain.State && entry.State.StateCode == StateCodes.SkillLevel)
                    states.Add((entry.State.EntityId, entry.State.Value0, entry.State.Value1));
            }
        });

        Assert.Equal([(Player, 177_300_001L, 16L), (Player, 177_100_001L, 12L)], states);
        Assert.Equal(new Dictionary<int, int> { [17_730_000] = 16, [17_710_000] = 12 }, SeenSkillLevels.Read(segment, long.MaxValue, TestContext.Current.CancellationToken)[Player]);
        // The projection takes no entity from them, and playback, as with positions, no State event.
        var without = Spawn(withPassives: false);
        Assert.Equal(EntityIds(without), EntityIds(scene));
        Assert.Equal(StateEvents(without), StateEvents(scene));
    }

    private static SceneLiveReadModel Spawn(bool withPassives)
    {
        const long at = 1_794_250_000_000;
        var scene = new SceneLiveReadModel();
        using var processor = new PacketStreamProcessor(SceneSinkFactory.CreateForLive(scene)(), null);
        var connection = new TcpConnection(1, 2, 3, 4);
        // An HP update first, as the stream is only picked up at an opcode it knows.
        Assert.True(processor.AppendAndProcess(BuildLengthPrefixedEnvelope(Convert.FromHexString("14008D93A101020100" + "76EEA20000000000")), in connection, at));

        // Entity, name, class, faction, then kind, flags, a byte, position and heading.
        byte[] spawn = [0x92, 0x35, 0x03, 0xa0, 0xa0, 0x01, 0x07, 0x08, .. "Player02"u8, 0x14, 0x00, 0x00, 0x00, 0x02, 0x02, 0x80, 0x92, .. Convert.FromHexString("CFBAFB47E514C8C700E2A246FCDD9543")];
        if (withPassives)
            spawn = [.. spawn, .. Passive(177_300_001, 16), .. Passive(177_100_001, 12)];
        Assert.True(processor.AppendAndProcess(BuildLengthPrefixedEnvelope(BuildFrame(0x45, 0x36, spawn)), in connection, at + 100));
        return scene;
    }

    private static byte[] Passive(uint effect, byte level) =>
        [.. BitConverter.GetBytes(effect), 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x80, 0x75, 0xd5, 0x2a, 0xbb, 0x03, 0x00, 0x00, 0x92, 0x35, level, 0x00];

    private static int[] EntityIds(SceneLiveReadModel scene)
    {
        scene.CreateFrame();
        return [.. scene.Owner.Entities.Entities.Keys.Order()];
    }

    private static int StateEvents(SceneLiveReadModel scene)
    {
        var segment = scene.Owner.CreateLiveTimelineSegment();
        var markers = ScenePlaybackTrackIndex.Build(segment, TestContext.Current.CancellationToken).ReadWindow(0, long.MaxValue, segment.CurrentEndObservationOrdinalExclusive, 100);
        return markers.AsSpan().ToArray().Count(static marker => marker.Track == ScenePlaybackTrack.State);
    }
}
