using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.Protocol.Packets;

namespace Cloris.Aion2Flow.Tests.Capture;

public sealed class Packet0238CompactControlParserTests
{
    [Fact]
    public void LongTail_ParsesPacketCooldownMilliseconds()
    {
        var body = Convert.FromHexString("AA1000C47704010802A2BC01256731C3F4DAE6C79567D5C700CEBF46E46601809601");

        Assert.True(Packet0238CompactControlParser.TryParse(BuildPacket(body), out var parsed));
        Assert.Equal(2_090, parsed.SourceId);
        Assert.Equal(17_070_020u, parsed.BodyCodeRaw);
        Assert.Equal(19_200, parsed.CooldownMilliseconds);
    }

    [Fact]
    public void ShortTail_ParsesPacketCooldownMilliseconds()
    {
        var body = Convert.FromHexString("AA1000E4A709011800AA10B38AA042E46601C025");

        Assert.True(Packet0238CompactControlParser.TryParse(BuildPacket(body), out var parsed));
        Assert.Equal(17_410_020u, parsed.BodyCodeRaw);
        Assert.Equal(4_800, parsed.CooldownMilliseconds);
    }

    [Fact]
    public void ZeroCooldown_IsPreserved()
    {
        var body = Convert.FromHexString("AA1000E5A709011900AA10B38AA042E4660100");

        Assert.True(Packet0238CompactControlParser.TryParse(BuildPacket(body), out var parsed));
        Assert.Equal(17_410_021u, parsed.BodyCodeRaw);
        Assert.Equal(0, parsed.CooldownMilliseconds);
    }

    [Fact]
    public void UnknownTailState_DoesNotInventCooldown()
    {
        var body = Convert.FromHexString("AA1000E4A709011800AA10B38AA042E46603C025");

        Assert.True(Packet0238CompactControlParser.TryParse(BuildPacket(body), out var parsed));
        Assert.Null(parsed.CooldownMilliseconds);
    }

    // A frame of 126 bytes or more has a 2-byte length prefix (frame length = value + prefix length - 4): such frames, an
    // NPC's cast on 7 or more targets, were rejected.
    [Theory]
    [InlineData(121)]
    [InlineData(122)]
    [InlineData(200)]
    public void LongFrame_WithTwoByteLengthPrefix_Parses(int bodyLength)
    {
        var head = Convert.FromHexString("AA1000E4A709011800AA10");
        var body = new byte[bodyLength];
        head.CopyTo(body, 0);

        Assert.True(Packet0238CompactControlParser.TryParse(BuildPacket(body), out var parsed));
        Assert.Equal(2_090, parsed.SourceId);
        Assert.Equal(17_410_020u, parsed.BodyCodeRaw);
        Assert.Equal(bodyLength - head.Length, parsed.TailLength);
    }

    private static byte[] BuildPacket(ReadOnlySpan<byte> body)
    {
        Span<byte> prefix = stackalloc byte[5];
        Assert.True(PacketTransportCodec.TryWriteVarInt(body.Length + 6, prefix, out var prefixLength));
        var packet = new byte[prefixLength + sizeof(ushort) + body.Length];
        prefix[..prefixLength].CopyTo(packet);
        packet[prefixLength] = 0x02;
        packet[prefixLength + 1] = 0x38;
        body.CopyTo(packet.AsSpan(prefixLength + sizeof(ushort)));
        return packet;
    }
}
