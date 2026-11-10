using Cloris.Aion2Flow.Protocol.Packets;

namespace Cloris.Aion2Flow.Tests.Capture;

public sealed class Packet008DRemainHpParserTests
{
    [Theory]
    [InlineData("13008D9C110201002B87000000000000", 2_204, 34_603L, null)]
    [InlineData("13008D9C110201076987000000000000", 2_204, null, 34_665L)]
    [InlineData("1C008DE779020200DE5B00000000000007DE5B000000000000", 15_591, 23_518L, 23_518L)]
    [InlineData("19008DDD10030101260F000001007D61000000000000", 2_141, 24_957L, null)]
    [InlineData("0F008DDD1001010308460200", 2_141, null, null)]
    public void Reads_CurrentAndMaxHp_FromStatBlocks(string hex, int entityId, long? currentHp, long? maxHp)
    {
        Assert.True(Packet008DRemainHpParser.TryParse(Convert.FromHexString(hex), out var parsed));
        Assert.Equal(entityId, parsed.NpcId);
        Assert.Equal(currentHp, parsed.CurrentHp);
        Assert.Equal(maxHp, parsed.MaxHp);
    }

    [Fact]
    public void Rejects_Frames_That_Do_Not_End_With_The_Stat_Blocks()
    {
        Assert.False(Packet008DRemainHpParser.TryParse(Convert.FromHexString("14008D9C110201002B8700000000000000"), out _));
    }
}
