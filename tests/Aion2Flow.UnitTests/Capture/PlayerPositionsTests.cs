using System.Numerics;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.Protocol.Packets;
using Cloris.Aion2Flow.SceneRuntime;

namespace Cloris.Aion2Flow.Tests.Capture;

public sealed class PlayerPositionsTests
{
    // The game's x points east, y south, z up, in cm; the bearing is clockwise from north.
    [Theory]
    [InlineData(0, -1000, 0, 0)]
    [InlineData(1000, 0, 0, 90)]
    [InlineData(0, 1000, 0, 180)]
    [InlineData(-1000, 0, 0, 270)]
    [InlineData(1000, -1000, 0, 45)]
    public void Bearing_Points_North_Up_And_Clockwise(float east, float south, float up, double expectedBearing)
    {
        var me = new Vector3(5000, 5000, 100);

        var (meters, bearing, _) = PlayerPositions.Between(me, me + new Vector3(east, south, up));

        Assert.Equal(expectedBearing, bearing, 3);
        Assert.Equal(new Vector3(east, south, up).Length() / 100, meters, 3);
    }

    // A renewal of the local player's (891) aura from the Valkron run: the tail starts with the caster, ends with where they stood.
    [Fact]
    public void Aura_Renewal_Tells_Where_The_Caster_Stood()
    {
        var packet = Convert.FromHexString("2F2B38FB061335578F600AE2040000000000002E153DAEA1010000FB060FEFA7090102ACFA1347BA1E6247005B2147");

        Assert.True(Packet2B38Parser.TryParse(packet, out var parsed));
        Assert.Equal(891, parsed.CasterId);
        Assert.Equal(new Vector3(37882.67f, 57886.727f, 41307f), parsed.CasterPosition);
    }

    // A self-buff of the local player's (12742) from the Krao run: group 17, a 9-byte tail, the position still the last 12 bytes.
    [Fact]
    public void Self_Buff_Tells_Where_The_Caster_Stood()
    {
        var packet = Convert.FromHexString("2F2A38C663011105" + new string('0', 40) + "C66300" + "00B480BE" + "C6F9C90347000026C4");

        Assert.True(Packet2A38Parser.TryParse(packet, out var parsed));
        Assert.Equal(12742, parsed.EchoSourceId);
        Assert.Equal(new Vector3(-24384.352f, 33737.973f, -664f), parsed.CasterPosition);
    }

    // A party member (14372) in the second Vakron run: no party list came, so only their spawn (0x4536), which holds their
    // character id, ties them to their 0x1C92 positions; from then on their moves (0x1B37, 0x1A37, as sent) count too.
    [Fact]
    public void A_Spawn_Ties_A_Member_To_Their_Party_Positions_And_Their_Moves_Count_From_Then_On()
    {
        const long character = 648_518_346_341_583_375;
        const int member = 14_372;
        const long at = 1_794_172_260_000;
        PlayerPositions.UpdateCharacter(character, new Vector3(18_000, 19_000, 40_000), at);
        var spawn = new byte[64];
        BitConverter.TryWriteBytes(spawn.AsSpan(40), character);

        PlayerPositions.LinkSpawn(member, spawn, null, at + 500);

        Assert.True(PlayerPositions.TryGet(member, out var fix));
        Assert.Equal(new Vector3(18_000, 19_000, 40_000), fix.Position);

        var scene = new SceneLiveReadModel();
        using var processor = new PacketStreamProcessor(SceneSinkFactory.CreateForLive(scene)(), null);
        var connection = new TcpConnection(1, 2, 3, 4);
        // An HP update first, as the stream is only picked up at an opcode it knows.
        Assert.True(processor.AppendAndProcess(Envelope("14008D93A101020100" + "76EEA20000000000"), in connection, at + 900));
        Assert.True(processor.AppendAndProcess(Envelope("1A1B37A470050363BD9046B4FB9646AE2A1C4754D954D9"), in connection, at + 1_000));
        Assert.True(PlayerPositions.TryGet(member, out fix));
        Assert.Equal(new Vector3(18526.693359375f, 19325.8515625f, 39978.6796875f), fix.Position);
        Assert.True(processor.AppendAndProcess(Envelope("181A37A4700000476E90461EFD944600401C479A9E"), in connection, at + 1_600));
        Assert.True(PlayerPositions.TryGet(member, out fix));
        Assert.Equal(new Vector3(18487.138671875f, 19070.55859375f, 40_000), fix.Position);

        // Someone who isn't tied to a character: their moves aren't kept, and an old character (no 0x1C92 for a minute)
        // ties no one.
        PlayerPositions.UpdateMember(member + 1, new Vector3(1, 2, 3), at);
        PlayerPositions.LinkSpawn(member + 2, spawn, null, at + 60_001);
        Assert.False(PlayerPositions.TryGet(member + 1, out _));
        Assert.False(PlayerPositions.TryGet(member + 2, out _));
    }

    // A player in a field group (7453): no 0x1C92 in the open world, and they stood still, so no moves either; their spawn
    // says where they stand (40 m from us, where our healing aura just reached them), and that holds until they leave sight.
    [Fact]
    public void A_Player_In_Sight_Stands_Where_Last_Seen_Until_They_Leave_Sight()
    {
        const int player = 7_453;
        const long at = 1_794_250_000_000;
        var scene = new SceneLiveReadModel();
        using var processor = new PacketStreamProcessor(SceneSinkFactory.CreateForLive(scene)(), null);
        var connection = new TcpConnection(1, 2, 3, 4);
        Assert.True(processor.AppendAndProcess(Envelope("14008D93A101020100" + "76EEA20000000000"), in connection, at));

        // Their spawn (the name made up), cut after the position and heading.
        Assert.True(processor.AppendAndProcess(Envelope("2E45369D3A03A0A0010708506C61796572303114000000020280" + "92CFBAFB47E514C8C700E2A246FCDD9543"), in connection, at + 100));
        Assert.True(PlayerPositions.TryGet(player, out var fix));
        Assert.Equal(new Vector3(128885.6171875f, -102441.7890625f, 20849f), fix.Position);
        Assert.True(fix.InSight);
        Assert.False(PlayerPositions.IsOutOfSight(player));

        Assert.True(processor.AppendAndProcess(Envelope("1A1B379D3A050363BD9046B4FB9646AE2A1C4754D954D9"), in connection, at + 600_000));
        Assert.True(PlayerPositions.TryGet(player, out fix));
        Assert.Equal(new Vector3(18526.693359375f, 19325.8515625f, 39978.6796875f), fix.Position);
        Assert.True(fix.InSight);

        // Out of sight (too far): nothing left of them, and their moves aren't kept until they come back into sight.
        Assert.True(processor.AppendAndProcess(Envelope("0A47369D3A0100"), in connection, at + 601_000));
        Assert.False(PlayerPositions.TryGet(player, out _));
        Assert.True(PlayerPositions.IsOutOfSight(player));
        PlayerPositions.UpdateMember(player, new Vector3(1, 2, 3), at + 602_000);
        Assert.False(PlayerPositions.TryGet(player, out _));

        PlayerPositions.LinkSpawn(player, [], new Vector3(4, 5, 6), at + 603_000);
        Assert.True(PlayerPositions.TryGet(player, out fix));
        Assert.Equal(new Vector3(4, 5, 6), fix.Position);
        Assert.False(PlayerPositions.IsOutOfSight(player));
    }

    // One real move of each kind from the transcendence run and the Vakron runs, entity changed to a party member's (3403): each
    // kind has its position in its own place (see ParseMovePacket), the flags byte deciding for 0x1B37 to 0x2937.
    [Theory]
    [InlineData("181A37CB1A00007E9E77C6753C95468005C747A2FA", -15847.623046875f, 19102.228515625f, 101899.0f)]
    [InlineData("1A1B37CB1A0503939859C61C6D8F464A2DC74779077907", -13926.1435546875f, 18358.5546875f, 101978.578125f)]
    [InlineData("1A1C37CB1A043F1B6F468CB6B6C580FBB5479DC99DC901", 15302.8115234375f, -5846.818359375f, 93175.0f)]
    [InlineData("241E37CB1A14A63E65C6C2EA90464A2DC747CAF5009E5144F39D56C300007A4403", -14671.662109375f, 18549.37890625f, 101978.578125f)]
    [InlineData("241F37CB1A020F8C70462E2C03C6BD31B647F8460000000000000000AE4A864401", 15395.0146484375f, -8395.044921875f, 93283.4765625f)]
    [InlineData("242337CB1A02862603C6B176A5469F76C94788EFC3214F44BB437AC33E231EC301", -8393.630859375f, 21179.345703125f, 103149.2421875f)]
    [InlineData("242437CB1A015DB1E5C594F7A14632D7C8477EEF388B8A44D4B1EDC3582EBEC301", -7350.17041015625f, 20731.7890625f, 102830.390625f)]
    [InlineData("242937CB1A0303AC3A9846E99E07C6B60EB6478B4204448A2C06C4A3BF69C1B5DF", 19485.3359375f, -8679.7275390625f, 93213.421875f)]
    [InlineData("3B2E37CB1A736524F400017E9E77C6753C9546B903C74737C87D3F2E8006BE0000000000A27A47B82900002056C34297C0ABC10000000001", -15847.623046875f, 19102.228515625f, 101895.4453125f)]
    [InlineData("252F37CB1A0078DF75C671079546971AC747D8077A3FCCD75BBE0000000000A27A47", -15735.8671875f, 19075.720703125f, 101941.1796875f)]
    [InlineData("263437CB1A1EDD2346687EF54670DF1647A08149C53028C6C46050DA422A8D03000001", 10487.279296875f, 31423.203125f, 38623.4375f)]
    [InlineData("253C37CB1A03B0B7524662E794C580ECB5471C9BB542000001A0ABBD00CBAC164A72", 13485.921875f, -4764.9228515625f, 93145.0f)]
    public void Every_Kind_Of_Move_Says_Where_They_Are(string frameHex, float x, float y, float z)
    {
        const int member = 3_403;
        const long at = 1_794_300_000_000;
        PlayerPositions.LinkSpawn(member, [], new Vector3(1, 2, 3), at);
        var scene = new SceneLiveReadModel();
        using var processor = new PacketStreamProcessor(SceneSinkFactory.CreateForLive(scene)(), null);
        var connection = new TcpConnection(1, 2, 3, 4);
        Assert.True(processor.AppendAndProcess(Envelope("14008D93A101020100" + "76EEA20000000000"), in connection, at));

        Assert.True(processor.AppendAndProcess(Envelope(frameHex), in connection, at + 100));

        Assert.True(PlayerPositions.TryGet(member, out var fix));
        Assert.Equal(new Vector3(x, y, z), fix.Position);
    }

    private static byte[] Envelope(string frameHex)
    {
        var frame = Convert.FromHexString(frameHex);
        var envelope = new byte[PacketTransportCodec.LengthPrefixedHeaderLength + frame.Length];
        BitConverter.TryWriteBytes(envelope, frame.Length);
        frame.CopyTo(envelope, PacketTransportCodec.LengthPrefixedHeaderLength);
        return envelope;
    }

    [Fact]
    public void Rise_Is_How_Much_Higher_In_Meters()
    {
        var (_, _, rise) = PlayerPositions.Between(new Vector3(0, 0, 100), new Vector3(0, 0, -400));

        Assert.Equal(-5, rise, 3);
    }
}
