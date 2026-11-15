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
}
