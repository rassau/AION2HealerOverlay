using System.Buffers.Binary;
using Cloris.Aion2Flow.Protocol.Readers;

namespace Cloris.Aion2Flow.Protocol.Packets;

internal readonly record struct Packet008DRemainHp(int NpcId, long? CurrentHp, long? MaxHp);

// Entity stat update: a mask (bit 0: int32 stats like mana, only sent for the local player; bit 1: int64
// stats), then per block a count of (stat id, value) entries. Int64 stat 0 is current HP, 7 is max HP.
internal static class Packet008DRemainHpParser
{
    private const int CurrentHpStat = 0;
    private const int MaxHpStat = 7;

    public static bool TryParse(ReadOnlySpan<byte> packet, out Packet008DRemainHp result)
    {
        result = default;

        var reader = new PacketSpanReader(packet);
        if (!reader.TryReadVarInt(out _)) return false;
        if (reader.Remaining < 2) return false;
        if (packet[reader.Offset] != 0x00 || packet[reader.Offset + 1] != 0x8d) return false;
        if (!reader.TryAdvance(2)) return false;

        if (!reader.TryReadVarInt(out var npcId)) return false;
        if (npcId == 0) return false;
        if (!reader.TryReadVarInt(out var mask) || mask is < 1 or > 3) return false;

        if ((mask & 1) != 0)
        {
            if (!reader.TryReadVarInt(out var count)) return false;
            for (var i = 0; i < count; i++)
            {
                if (!reader.TryReadVarInt(out _) || !reader.TryAdvance(sizeof(int))) return false;
            }
        }

        long? currentHp = null;
        long? maxHp = null;
        if ((mask & 2) != 0)
        {
            if (!reader.TryReadVarInt(out var count)) return false;
            for (var i = 0; i < count; i++)
            {
                if (!reader.TryReadVarInt(out var stat) || reader.Remaining < sizeof(long)) return false;
                var value = BinaryPrimitives.ReadInt64LittleEndian(packet.Slice(reader.Offset, sizeof(long)));
                if (!reader.TryAdvance(sizeof(long))) return false;
                if (stat == CurrentHpStat)
                    currentHp = value;
                else if (stat == MaxHpStat)
                    maxHp = value;
            }
        }

        if (reader.Remaining != 0) return false;

        result = new Packet008DRemainHp(npcId, currentHp, maxHp);
        return true;
    }
}
