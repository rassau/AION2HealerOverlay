using Cloris.Aion2Flow.Protocol.Packets;

namespace Cloris.Aion2Flow.Tests.Capture;

public sealed class MapArrivalTests
{
    // Arriving where 0x2136 sent the player: 0x2336 until the game update of 11 Nov 2026, 0x5196 since (back in the open
    // world after Draupnir, 11 Nov 2026; out of Draupnir, 9 Nov 2026). Without it no map change counted after the first.
    [Theory]
    [InlineData("1E5196B4143E670348D05D29C73FA9B74500000100000000000000", true)]
    [InlineData("1E2336000000000000473441C8DA535D4800301C4604000000D627", true)]
    [InlineData("150140560400000000000000000000000100", false)]
    [InlineData("0B5196B4143E670348D05D29", false)]
    public void Arrival_Is_Known_By_Either_Opcode(string hex, bool arrival) =>
        Assert.Equal(arrival, Packet2336ArrivalParser.TryParse(Convert.FromHexString(hex)));
}
