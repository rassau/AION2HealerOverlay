using System.Buffers.Binary;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.Protocol.Packets;
using Cloris.Aion2Flow.SceneRuntime;

using static Cloris.Aion2Flow.Tests.Capture.TransportTestData;

namespace Cloris.Aion2Flow.Tests.Capture;

public sealed class MapArrivalTests
{
    // Arriving where 0x2136 sent the player: 0x2336 until the game update of 11 Nov 2026, 0x5196 since (back in the open
    // world after Draupnir, 11 Nov 2026; out of Draupnir, 9 Nov 2026), 0xf84e since the one of 18 Nov 2026 (made up: a
    // place, 2 bytes and a count where the game puts them; then one byte short). Without it no map change counted after
    // the first.
    [Theory]
    [InlineData("1E5196B4143E670348D05D29C73FA9B74500000100000000000000", true)]
    [InlineData("1E2336000000000000473441C8DA535D4800301C4604000000D627", true)]
    [InlineData("1EF84E00000000D20400E640468040B7C600E0AC43030000000000", true)]
    [InlineData("150140560400000000000000000000000100", false)]
    [InlineData("0B5196B4143E670348D05D29", false)]
    [InlineData("19F84E00000000D20400E640468040B7C600E0AC4303", false)]
    public void Arrival_Is_Known_By_Each_Opcode(string hex, bool arrival) =>
        Assert.Equal(arrival, Packet2336ArrivalParser.TryParse(Convert.FromHexString(hex)));

    // The backup for an arrival renamed once more (PacketStateHandler.ParseMapArrivalByPosition): a packet holding the
    // x, y, z a 0x2136 announced, at most 60 s after it. The spot is map 1010's from the replay fixture of 29 Jul 2026.
    private const long Announced = 1_794_600_000_000;
    private const uint X = 0xc7581d0a, Y = 0xc7b48eeb, Z = 0x46d7ce00;
    private static readonly byte[] Spot = Convert.FromHexString("0A1D58C7EB8EB4C700CED746");

    [Theory]
    [InlineData(0, true)]
    [InlineData(60_000, true)]
    [InlineData(60_001, false)]
    [InlineData(-1, false)]
    public void The_Backup_Waits_60_Seconds_For_The_Spot(long after, bool match) =>
        Assert.Equal(match, Armed().MatchArrivalBackup(Spot, Announced + after));

    [Fact]
    public void A_Packet_Past_The_60_Seconds_Ends_The_Wait()
    {
        var state = Armed();

        Assert.False(state.MatchArrivalBackup([], Announced + 60_001));
        Assert.False(state.MatchArrivalBackup(Spot, Announced + 5_000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(13)]
    public void The_Spot_Counts_Anywhere_In_The_Body(int offset)
    {
        var body = new byte[25];
        Spot.CopyTo(body, offset);

        Assert.True(Armed().MatchArrivalBackup(body, Announced));
    }

    [Fact]
    public void Without_The_Whole_Spot_It_Waits_On()
    {
        var state = Armed();
        byte[] oneBitOff = [.. Spot];
        oneBitOff[^1] ^= 1;

        Assert.False(state.MatchArrivalBackup(oneBitOff, Announced));
        Assert.False(state.MatchArrivalBackup(Spot.AsSpan(1), Announced));
        Assert.True(state.MatchArrivalBackup(Spot, Announced + 1));
    }

    // Twelve zero bytes would turn up in almost any packet.
    [Fact]
    public void A_Spot_Of_Zeros_Arms_Nothing()
    {
        var state = new PacketPlayerGroupState();
        state.ArmArrivalBackup(0, 0, 0, Announced);

        Assert.False(state.MatchArrivalBackup(new byte[24], Announced));
    }

    [Fact]
    public void A_New_Announcement_Replaces_The_Spot_And_The_Time()
    {
        var state = Armed();
        state.ArmArrivalBackup(Z, Y, X, Announced + 3_000);

        Assert.False(state.MatchArrivalBackup(Spot, Announced + 3_000));
        Assert.True(state.MatchArrivalBackup([.. Spot[8..], .. Spot[4..8], .. Spot[..4]], Announced + 63_000));
    }

    // Through the frame parser: a 0x2136 to map 1010 arms the backup, the known arrival ends the wait.
    [Fact]
    public void The_Known_Arrival_Ends_The_Wait()
    {
        var group = new PacketPlayerGroupState();
        using var parser = new PacketFrameParser(SceneSinkFactory.CreateForLive(new SceneLiveReadModel())(), null, null, group);

        Assert.True(Parse(parser, Announcement()));
        Assert.True(group.MatchArrivalBackup(Spot, Announced));
        Assert.True(Parse(parser, BuildFrame(0x23, 0x36, [0, 0, 0, 0, 0, 0, .. Spot, 1, 0, 0, 0, 0, 0])));
        Assert.False(group.MatchArrivalBackup(Spot, Announced));
    }

    // A made-up 0x7ca5 holding the spot, which no case reads, is the arrival: alone or in an LZ4 container (5 of the 47
    // recorded arrivals came in one). It ends the wait, so another such frame can't switch the map once more, and the
    // log (app.log) names its code.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_Unread_Frame_Holding_The_Spot_Is_The_Arrival(bool inLz4Container)
    {
        var group = new PacketPlayerGroupState();
        using var parser = new PacketFrameParser(SceneSinkFactory.CreateForLive(new SceneLiveReadModel())(), null, null, group);
        var arrival = BuildFrame(0x7c, 0xa5, [0, 0, 0, 0, 0, 0, .. Spot, 1, 0, 0, 0, 0, 0]);
        List<string> logged = [];
        var sink = CaptureLog.Sink;
        CaptureLog.Sink = (level, message) =>
        {
            if (message.Contains("0x7ca5", StringComparison.Ordinal))
                logged.Add($"{level}: {message}");
        };
        try
        {
            Assert.True(Parse(parser, Announcement()));
            Assert.True(Parse(parser, inLz4Container ? BuildCompressedFrame(arrival) : arrival));
        }
        finally
        {
            CaptureLog.Sink = sink;
        }

        Assert.False(group.MatchArrivalBackup(Spot, Announced));
        Assert.Equal(
            "Warning: Map arrival found by the announced position in packet 0x7ca5: the game may have renamed the arrival packet (known: 2336, 5196, f84e).",
            Assert.Single(logged));
    }

    // Only frames as the stream brings them count. The parser tries the body of a frame it can't read (a 0x2a38 too
    // short for one) as a frame of its own, and in a packet that isn't whole frames (here a bad length byte first) it
    // looks at each offset for a frame ending with the transport pattern: each time a made-up 0x7ca5 holding the spot,
    // which isn't taken for the arrival.
    [Fact]
    public void A_Slice_Holding_The_Spot_Is_No_Arrival()
    {
        var group = new PacketPlayerGroupState();
        using var parser = new PacketFrameParser(SceneSinkFactory.CreateForLive(new SceneLiveReadModel())(), null, null, group);

        Assert.True(Parse(parser, Announcement()));
        Assert.False(Parse(parser, BuildFrame(0x2a, 0x38, BuildFrame(0x7c, 0xa5, Spot))));
        Assert.False(Parse(parser, [0x00, .. BuildFrame(0x7c, 0xa5, Spot), .. PacketTransportCodec.Pattern]));
        Assert.True(group.MatchArrivalBackup(Spot, Announced));
    }

    private static PacketPlayerGroupState Armed()
    {
        var state = new PacketPlayerGroupState();
        state.ArmArrivalBackup(X, Y, Z, Announced);
        return state;
    }

    // A teleport (2) to map 1010, to the spot.
    private static byte[] Announcement()
    {
        var body = new byte[38];
        body[0] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(4), 1_010);
        Spot.CopyTo(body, 16);
        body[32] = 2;
        return BuildFrame(0x21, 0x36, body);
    }

    private static bool Parse(PacketFrameParser parser, byte[] frame) =>
        parser.ParsePacketEntry(frame, new TcpConnection(1, 2, 3, 4), new PacketProcessingTimestamp(Announced, 0), 1);
}
