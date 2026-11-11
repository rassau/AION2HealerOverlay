using Cloris.Aion2Flow.Protocol.Readers;

namespace Cloris.Aion2Flow.Protocol.Packets;

// The player arrived where 0x2136 sent them (map change, teleport, respawn), with that place's x, y, z. It was 0x2336
// (6 zero bytes, x, y, z, a count, 2 bytes) until the game update of 11 Nov 2026 made it 0x5196 (2 bytes, x, y, z,
// 10 bytes); both are 24 bytes after the opcode.
internal static class Packet2336ArrivalParser
{
    public static bool TryParse(ReadOnlySpan<byte> packet)
    {
        var reader = new PacketSpanReader(packet);
        if (!reader.TryReadVarInt(out _)) return false;
        if (reader.Remaining < 2) return false;
        if ((packet[reader.Offset], packet[reader.Offset + 1]) is not ((0x23, 0x36) or (0x51, 0x96))) return false;
        reader.TryAdvance(2);

        return reader.Remaining >= 20;
    }
}
