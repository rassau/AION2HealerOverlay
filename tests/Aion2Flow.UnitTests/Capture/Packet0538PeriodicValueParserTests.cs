using Cloris.Aion2Flow.Protocol.Packets;

using static Cloris.Aion2Flow.Tests.Capture.TransportTestData;

namespace Cloris.Aion2Flow.Tests.Capture;

public sealed class Packet0538PeriodicValueParserTests
{
    // A frame of 126 bytes or more has a 2-byte length prefix (frame length = value + prefix length - 4): its last byte was
    // cut off. The tail here is no recognized one; only its length counts.
    [Theory]
    [InlineData(100)]
    [InlineData(130)]
    public void The_Whole_Frame_Is_Read_Whatever_Its_Length_Prefix(int tailLength)
    {
        // Target 133, mode 3, source 134, unknown 0, the effect reference, 100 damage.
        byte[] head = [0x85, 0x01, 0x03, 0x86, 0x01, 0x00, 0x01, 0x02, 0x03, 0x04, 0x64];

        Assert.True(Packet0538PeriodicValueParser.TryParse(BuildFrame(0x05, 0x38, [.. head, .. new byte[tailLength]]), out var parsed));
        Assert.Equal(133, parsed.TargetId);
        Assert.Equal(100, parsed.Damage);
        Assert.Equal(tailLength, parsed.TailLength);
    }
}
