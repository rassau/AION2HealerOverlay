using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.Protocol.Packets;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Observation;

using static Cloris.Aion2Flow.Tests.Capture.TransportTestData;

namespace Cloris.Aion2Flow.Tests.Capture;

// The game's kill message (0x048D) and the player's kill credit (0x8456), laid out as in a minute of Abyss farming
// (19 Nov 2026); the name is made up.
public sealed class KillPacketTests
{
    // NPC 52404 died, killed by player 4507 ("Abcd", server 2304) with skill 17041250.
    private const string KillMessage = "16048DB49903620704019B2300090441626364";
    // The same opcode with a 1-byte first number (a dungeon recording of July 2026): a nickname, no kill.
    private const string OtherMessage = "16048D060000000000009B2300090441626364";
    // An NPC that died with no one's hits seen: no killer.
    private const string KillerlessDeath = "1B048DB2EB02000000000000000000000000000000000000";
    // Players killed by players (PvP), laid out as in recordings of Jul and Nov 2026: player 865 (a 2-byte varint, where an
    // NPC's id has 3 bytes) killed by 15683 ("Abcd", server 1007), and player 9066 killed by 59 (a 1-byte id, server 2302).
    // Read at an NPC's offset they named 122 (15683 >> 7) and 1150 (the server's bytes).
    private const string PlayerKill = "15048DE1069AC72D00C37AEF030441626364";
    private const string PlayerKillBySmallId = "14048DEA46844ED7003BFE080441626364";
    // The kill credit counter 411100100 at 32367 and 32368 (Abyss), 311100100 at 68264 (dungeons and fields), and 240111200,
    // a tally of the Abyss that goes up with every credit too: left out, so a kill counts once.
    private const string Credit32367 = "18845601C4E380186F7E0000977EF3F5A101000007";
    private const string Credit32368 = "18845601C4E38018707E00005980F3F5A101000007";
    private const string DungeonCredit = "18845601C4028B12A80A0100A475F3F5A101000007";
    private const string OtherCounter = "1884560160CE4F0EE3180000977EF3F5A101000002";

    [Fact]
    public void The_Kill_Message_Names_The_Npc_That_Died_And_Its_Killer()
    {
        Assert.True(Packet048DNicknameParser.TryParse(Convert.FromHexString(KillMessage), out var kill));
        Assert.Equal((52_404, 4_507, "Abcd", (int?)2_304), (kill.KilledEntityId, kill.PlayerId, kill.Nickname, kill.OriginServerId));

        Assert.True(Packet048DNicknameParser.TryParse(Convert.FromHexString(OtherMessage), out var other));
        Assert.Equal((0, 4_507, "Abcd"), (other.KilledEntityId, other.PlayerId, other.Nickname));

        Assert.False(Packet048DNicknameParser.TryParse(Convert.FromHexString(KillerlessDeath), out _));
    }

    [Fact]
    public void A_Player_Killed_By_A_Player_Names_The_Killer_By_Their_Own_Id()
    {
        Assert.True(Packet048DNicknameParser.TryParse(Convert.FromHexString(PlayerKill), out var kill));
        Assert.Equal((0, 15_683, "Abcd", (int?)1_007), (kill.KilledEntityId, kill.PlayerId, kill.Nickname, kill.OriginServerId));

        Assert.True(Packet048DNicknameParser.TryParse(Convert.FromHexString(PlayerKillBySmallId), out var bySmallId));
        Assert.Equal((0, 59, "Abcd", (int?)2_302), (bySmallId.KilledEntityId, bySmallId.PlayerId, bySmallId.Nickname, bySmallId.OriginServerId));
    }

    // The player known by their id alone (0x4a36, an overlay started mid-session) is 122: a packet naming that id names the
    // player, so the misread above made the killer's name and server the player's, and the next start's remembered character.
    [Fact]
    public void A_Player_Kill_Leaves_The_Player_Known_By_Id_Alone_As_They_Were()
    {
        const long at = 1_795_127_735_716;
        var scene = new SceneLiveReadModel();
        using var processor = new PacketStreamProcessor(SceneSinkFactory.CreateForLive(scene)(), null);
        var connection = new TcpConnection(1, 2, 3, 4);
        Assert.True(processor.AppendAndProcess(BuildLengthPrefixedEnvelope(Convert.FromHexString("14008D93A101020100" + "76EEA20000000000")), in connection, at));

        processor.AppendAndProcess(BuildLengthPrefixedEnvelope(BuildFrame(0x4a, 0x36, [122])), in connection, at + 100);
        processor.AppendAndProcess(BuildLengthPrefixedEnvelope(Convert.FromHexString(PlayerKill)), in connection, at + 200);
        scene.CreateFrame();

        var registry = scene.Owner.MetadataRegistry;
        Assert.Equal(122, registry.LocalPlayerEntityId);
        Assert.Null(registry.LocalPlayerIdentity);
        Assert.True(registry.TryGetPcMetadata(15_683, out var killer));
        Assert.Equal(("Abcd", (int?)1_007), (killer.Nickname, killer.OriginServerId));
    }

    [Fact]
    public void Kill_Messages_And_Kill_Credits_Go_Into_The_Journal()
    {
        const long at = 1_795_127_735_716;
        var scene = new SceneLiveReadModel();
        using var processor = new PacketStreamProcessor(SceneSinkFactory.CreateForLive(scene)(), null);
        var connection = new TcpConnection(1, 2, 3, 4);
        // An HP update first, as the stream is only picked up at an opcode it knows.
        Assert.True(processor.AppendAndProcess(BuildLengthPrefixedEnvelope(Convert.FromHexString("14008D93A101020100" + "76EEA20000000000")), in connection, at));

        foreach (var frame in (string[])[KillMessage, OtherMessage, KillerlessDeath, Credit32367, Credit32368, DungeonCredit, OtherCounter])
            processor.AppendAndProcess(BuildLengthPrefixedEnvelope(Convert.FromHexString(frame)), in connection, at + 100);

        var states = new List<(int, int, long)>();
        var segment = scene.Owner.CreateLiveTimelineSegment();
        segment.ReadEntries(segment.CreateCursor(), 512, entries =>
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Domain == ObservedEventDomain.State && entry.State.StateCode is StateCodes.Kill or StateCodes.KillCredit)
                    states.Add((entry.State.StateCode, entry.State.EntityId, entry.State.Value0));
            }
        });

        Assert.Equal([(StateCodes.Kill, 52_404, 4_507L), (StateCodes.KillCredit, 0, 32_367L), (StateCodes.KillCredit, 0, 32_368L), (StateCodes.KillCredit, 0, 68_264L)], states);
    }
}
