using Cloris.Aion2Flow.Protocol.Packets;

namespace Cloris.Aion2Flow.Tests.Capture;

public sealed class Packet2092BossTargetParserTests
{
    [Fact]
    public void Reads_Whom_The_Boss_Is_After()
    {
        // Urugugu Canyon, 2026-11-08: the boss turns to a party member (11454), and after the fight to no one.
        Assert.True(Packet2092BossTargetParser.TryParse(Convert.FromHexString("082092BE59"), out var target));
        Assert.Equal(11_454, target);

        Assert.True(Packet2092BossTargetParser.TryParse(Convert.FromHexString("07209200"), out var none));
        Assert.Equal(0, none);

        Assert.False(Packet2092BossTargetParser.TryParse(Convert.FromHexString("082192BE59"), out _));
    }
}
