using Cloris.Aion2Flow.Protocol.Readers;

namespace Cloris.Aion2Flow.Capture.Streams;

// The character's odyle energy as the game shows it, e.g. "120(+160)/840", for the meter's footer: what there is and, in
// brackets, what recharge items added. The game lists the character's resources at log-in (0x0b61: a count, then each
// resource) and sends a resource again when it changes (0x0c61: a byte, 01 for a recharge item, the resource, then how it
// changed). A resource is [mask][u32 id][u64 if mask & 0x01][varint if mask & 0x04][varint if mask & 0x08]; the odyle is
// 60,000,001 with both numbers. Read from three recordings (27 Nov 2026) and only looked at here: every frame goes on
// to the rest of the parser as before (PacketOpcodeDispatcher).
internal static class OdyleEnergy
{
    public const int ResourceId = 60_000_001;
    // ponytail: the game shows "/840" but sends no maximum (not with the resource, in the login list or among the
    // character's stats); a constant until a recording shows one.
    public const int Maximum = 840;
    private const byte KnownMaskBits = 0x01 | 0x04 | 0x08;

    // value << 32 | bracket; -1 while none came.
    private static long s_state = -1;

    public static (int Value, int Bracket)? Current => Volatile.Read(ref s_state) is var state and >= 0 ? ((int)(state >> 32), (int)state) : null;

    // From the dispatcher, for every frame.
    public static void Observe(byte opcode0, byte opcode1, ReadOnlySpan<byte> packet)
    {
        if (opcode1 != 0x61 || opcode0 is not (0x0b or 0x0c))
            return;

        if (opcode0 == 0x0b ? TryReadList(packet, out var value, out var bracket) : TryReadChange(packet, out value, out bracket))
            Volatile.Write(ref s_state, (long)value << 32 | (uint)bracket);
    }

    internal static void Clear() => Volatile.Write(ref s_state, -1);

    internal static bool TryReadList(ReadOnlySpan<byte> packet, out int value, out int bracket)
    {
        (value, bracket) = (0, 0);
        var reader = new PacketSpanReader(packet);
        if (!reader.TryReadVarInt(out _) || !reader.TryAdvance(2) || !reader.TryReadVarInt(out var count))
            return false;

        for (var i = 0; i < count; i++)
        {
            if (!TryReadResource(ref reader, out var id, out value, out bracket))
                return false;
            if (id == ResourceId)
                return true;
        }

        return false;
    }

    internal static bool TryReadChange(ReadOnlySpan<byte> packet, out int value, out int bracket)
    {
        (value, bracket) = (0, 0);
        var reader = new PacketSpanReader(packet);
        return reader.TryReadVarInt(out _) && reader.TryAdvance(3) && TryReadResource(ref reader, out var id, out value, out bracket) && id == ResourceId;
    }

    // A mask with bits not seen yet: its fields can't be skipped, so nothing after it is read.
    private static bool TryReadResource(ref PacketSpanReader reader, out int id, out int value, out int bracket)
    {
        (id, value, bracket) = (0, 0, 0);
        if (!reader.TryReadByte(out var mask) || (mask & ~KnownMaskBits) != 0 || !reader.TryReadUInt32Le(out id))
            return false;
        if ((mask & 0x01) != 0 && !reader.TryAdvance(8))
            return false;
        if ((mask & 0x04) != 0 && !reader.TryReadVarInt(out value))
            return false;
        return (mask & 0x08) == 0 || reader.TryReadVarInt(out bracket);
    }
}
