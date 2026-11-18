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

    private static byte[] Aura(uint effect, byte caster, byte level) =>
        [.. BitConverter.GetBytes(effect), 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x80, 0x75, 0xd5, 0x2a, 0xbb, 0x03, 0x00, 0x00, caster, level, 0x00];
}
