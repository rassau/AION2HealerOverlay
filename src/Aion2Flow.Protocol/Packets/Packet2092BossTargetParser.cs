using Cloris.Aion2Flow.Protocol.Readers;

namespace Cloris.Aion2Flow.Protocol.Packets;

// 0x2092: whom the boss is after, as the game's boss frame shows it beside the boss's HP: [target entity varint], 0 for
// no one. Comes on the pull, on every switch and (0) when a boss leaves the fight; it doesn't say which boss.
internal static class Packet2092BossTargetParser
{
    public static bool TryParse(ReadOnlySpan<byte> packet, out int targetId)
    {
        targetId = 0;
        var reader = new PacketSpanReader(packet);
        if (!reader.TryReadVarInt(out _)) return false;
        if (reader.Remaining < 2) return false;
        if (packet[reader.Offset] != 0x20 || packet[reader.Offset + 1] != 0x92) return false;
        if (!reader.TryAdvance(2)) return false;
        return reader.TryReadVarInt(out targetId) && targetId >= 0;
    }
}
