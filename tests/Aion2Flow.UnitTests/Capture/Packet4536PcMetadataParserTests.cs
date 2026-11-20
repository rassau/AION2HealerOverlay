using Cloris.Aion2Flow.Protocol.Packets;

using static Cloris.Aion2Flow.Tests.Capture.TransportTestData;

namespace Cloris.Aion2Flow.Tests.Capture;

public sealed class Packet4536PcMetadataParserTests
{
    // A spawn (made-up name) that ends right after its class code: TryParse threw ArgumentOutOfRangeException for it, on
    // the capture thread too, where candidate streams are classified.
    [Fact]
    public void A_Spawn_Cut_Right_After_The_Class_Code_Is_Rejected()
    {
        byte[] body = [0x01, 0x07, 0x04, .. "Abcd"u8, 0x05, 0x00, 0x00, 0x00];

        Assert.False(Packet4536PcMetadataParser.TryParse(BuildFrame(0x45, 0x36, body), out _));

        // With the faction byte it parses as before.
        Assert.True(Packet4536PcMetadataParser.TryParse(BuildFrame(0x45, 0x36, [.. body, 0x01]), out var spawn));
        Assert.Equal(1, spawn.EntityId);
        Assert.Equal("Abcd", spawn.Nickname);
        Assert.Equal(5, spawn.ClassCode);
        Assert.Equal(1, spawn.FactionCode);
    }

    // A spawn's lasting auras (made-up bytes): only the spawned player's (100) at level 2 or more count, not another
    // caster's (101) at 18 nor their own at 1.
    [Fact]
    public void A_Spawn_Gives_The_Players_Own_Passive_Levels()
    {
        byte[] packet = [0x01, 0x02, .. Aura(177_300_001, caster: 100, level: 16), .. Aura(177_200_001, caster: 101, level: 18), .. Aura(177_100_001, caster: 100, level: 1)];
        var into = new (uint Effect, int Level)[16];

        Assert.Equal(1, Packet4536PcMetadataParser.ReadPassiveLevels(packet, 100, into));
        Assert.Equal((177_300_001u, 16), into[0]);
        Assert.Equal(0, Packet4536PcMetadataParser.ReadPassiveLevels([0x01, 0x02, 0x03], 100, into));

        // A full `into` ends the scan.
        byte[] two = [.. Aura(177_300_001, caster: 100, level: 16), .. Aura(177_100_001, caster: 100, level: 12)];
        Assert.Equal(2, Packet4536PcMetadataParser.ReadPassiveLevels(two, 100, into));
        Assert.Equal(1, Packet4536PcMetadataParser.ReadPassiveLevels(two, 100, into.AsSpan(0, 1)));
    }

    // Where a player stands as they come into sight (made-up name, real headers): kind, flags and a byte, then x, y, z and
    // the heading. In the Abyss and the PvP boss fights a byte (1 or 2) comes before the flags; a spawn whose position
    // makes no sense (a layout not seen yet) gives none.
    [Theory]
    [InlineData("0280D2")]
    [InlineData("0200D2")]
    [InlineData("0188D2")]
    [InlineData("0201C0D6")]
    [InlineData("020288D2")]
    [InlineData("010140D6")]
    public void A_Spawn_Says_Where_The_Player_Stands(string header)
    {
        var spawn = Spawn(header, x: -27_599.76f, y: 36_944.22f, z: 35_235f, heading: 256.5f);

        Assert.True(Packet4536PcMetadataParser.TryParse(BuildFrame(0x45, 0x36, spawn), out var parsed));
        Assert.Equal(new System.Numerics.Vector3(-27_599.76f, 36_944.22f, 35_235f), parsed.Position);
    }

    [Theory]
    [InlineData(-2.0735963e28f, 256.5f)]
    [InlineData(35_235f, 1.0e32f)]
    [InlineData(35_235f, -90f)]
    [InlineData(float.NaN, 256.5f)]
    public void A_Spawn_Whose_Position_Makes_No_Sense_Gives_None(float z, float heading)
    {
        Assert.True(Packet4536PcMetadataParser.TryParse(BuildFrame(0x45, 0x36, Spawn("0201C0D6", -27_599.76f, 36_944.22f, z, heading)), out var parsed));
        Assert.Null(parsed.Position);
    }

    // A spawn in a PvP area whose flags (0x88) carry the header extension, 12 bytes more, here ending in a negative float:
    // read with the PvP byte as the flags, the extension wasn't skipped and the walk stepped over the server id
    // (made-up server 1001, legion "Wxyz").
    [Fact]
    public void A_Pvp_Spawn_Gives_The_Players_Server_And_Legion()
    {
        byte[] spawn = [.. Spawn("020288D2", -27_599.76f, 36_944.22f, 35_235f, 256.5f),
            .. BitConverter.GetBytes(0f), .. BitConverter.GetBytes(0f), .. BitConverter.GetBytes(-1f),
            0xE9, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xE9, 0x03, 0x04, .. "Wxyz"u8, 0x01, 0x00, 0x00, 0x00];

        Assert.True(Packet4536PcMetadataParser.TryParse(BuildFrame(0x45, 0x36, spawn), out var parsed));
        Assert.Equal(1001, parsed.OriginServerId);
        Assert.Equal("Wxyz", parsed.LegionName);
    }

    // A spawn cut in its header, also right after the PvP byte: no position, and nothing thrown on the capture thread.
    [Theory]
    [InlineData("02")]
    [InlineData("0201")]
    [InlineData("0201C0D6CFBAFB47")]
    public void A_Spawn_Cut_In_Its_Header_Gives_No_Position(string header)
    {
        byte[] spawn = [0x01, 0x07, 0x04, .. "Abcd"u8, 0x05, 0x00, 0x00, 0x00, 0x02, .. Convert.FromHexString(header)];

        Assert.True(Packet4536PcMetadataParser.TryParse(BuildFrame(0x45, 0x36, spawn), out var parsed));
        Assert.Null(parsed.Position);
    }

    // Entity 1, "Abcd", class 5, faction 2, then the header and the position.
    private static byte[] Spawn(string header, float x, float y, float z, float heading) =>
        [0x01, 0x07, 0x04, .. "Abcd"u8, 0x05, 0x00, 0x00, 0x00, 0x02, .. Convert.FromHexString(header),
            .. BitConverter.GetBytes(x), .. BitConverter.GetBytes(y), .. BitConverter.GetBytes(z), .. BitConverter.GetBytes(heading)];

    private static byte[] Aura(uint effect, byte caster, byte level) =>
        [.. BitConverter.GetBytes(effect), 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x80, 0x75, 0xd5, 0x2a, 0xbb, 0x03, 0x00, 0x00, caster, level, 0x00];
}
