using Cloris.Aion2Flow.Protocol.Readers;

namespace Cloris.Aion2Flow.Protocol.Packets;

// Max is null when the gauge broke: the boss is staggered.
internal readonly record struct Packet05E0StaggerGauge(int NpcId, int Current, int? Max);

// A boss's stagger (groggy) gauge, sent on every change: [npc] 03 01 [max u32] [current u32] 02 as stagger skills
// wear it down, and [npc] 00 03 02 when it runs out. The boss is then staggered for ~5 s and the gauge refills.
internal static class Packet05E0StaggerGaugeParser
{
    public static bool TryParse(ReadOnlySpan<byte> packet, out Packet05E0StaggerGauge result)
    {
        result = default;

        var reader = new PacketSpanReader(packet);
        if (!reader.TryReadVarInt(out _)) return false;
        if (reader.Remaining < 2) return false;
        if (packet[reader.Offset] != 0x05 || packet[reader.Offset + 1] != 0xe0) return false;
        if (!reader.TryAdvance(2)) return false;
        if (!reader.TryReadVarInt(out var npcId) || npcId <= 0) return false;
        if (!reader.TryReadByte(out var mode)) return false;

        if (mode == 0x00)
        {
            result = new Packet05E0StaggerGauge(npcId, 0, null);
            return true;
        }

        if (mode != 0x03 ||
            !reader.TryAdvance(1) ||
            !reader.TryReadUInt32Le(out var max) ||
            !reader.TryReadUInt32Le(out var current) ||
            max <= 0 ||
            current < 0 ||
            current > max)
        {
            return false;
        }

        result = new Packet05E0StaggerGauge(npcId, current, max);
        return true;
    }
}
