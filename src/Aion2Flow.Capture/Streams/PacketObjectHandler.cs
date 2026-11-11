using System.Buffers.Binary;
using System.Numerics;
using Cloris.Aion2Flow.Protocol.Readers;

namespace Cloris.Aion2Flow.Capture.Streams;

// Placed objects, read for the cube finder (see HiddenCubes) and otherwise left to the rest of the parser as before: the
// handlers say "not parsed". 0x3436: id (varint), kind (u16), code (u32), x, y, z (floats, cm), then heading, flags, the
// time it appeared and more. 0x3536: id, then its state, whose first byte is 4 once it's gone after use (1 while it's
// there). 0x058D: id, taken away.
internal static class PacketObjectHandler
{
    public static bool ParseSpawn3436Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (TryReadSpawn(packet, out var entityId, out var code, out var position))
            HiddenCubes.Saw(entityId, code, position, context.TimestampMilliseconds);
        return false;
    }

    public static bool ParseState3536Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (TryReadState(packet, out var entityId, out var state) && state == 4)
            HiddenCubes.Remove(entityId);
        return false;
    }

    public static bool ParseRemoved058DPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (TryReadRemoved(packet, out var entityId))
            HiddenCubes.Remove(entityId);
        return false;
    }

    internal static bool TryReadSpawn(ReadOnlySpan<byte> packet, out int entityId, out int code, out Vector3 position)
    {
        entityId = code = 0;
        position = default;
        var reader = new PacketSpanReader(packet);
        if (!reader.TryReadVarInt(out _) || !reader.TryAdvance(2) || !reader.TryReadVarInt(out entityId) ||
            !reader.TryReadUInt16Le(out _) || !reader.TryReadUInt32Le(out code) || reader.Remaining < 12)
            return false;

        var xyz = reader.RemainingSpan;
        position = new Vector3(BinaryPrimitives.ReadSingleLittleEndian(xyz), BinaryPrimitives.ReadSingleLittleEndian(xyz[4..]), BinaryPrimitives.ReadSingleLittleEndian(xyz[8..]));
        return entityId > 0 && float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z);
    }

    internal static bool TryReadState(ReadOnlySpan<byte> packet, out int entityId, out byte state)
    {
        entityId = 0;
        state = 0;
        var reader = new PacketSpanReader(packet);
        return reader.TryReadVarInt(out _) && reader.TryAdvance(2) && reader.TryReadVarInt(out entityId) && reader.TryReadByte(out state) && entityId > 0;
    }

    internal static bool TryReadRemoved(ReadOnlySpan<byte> packet, out int entityId)
    {
        entityId = 0;
        var reader = new PacketSpanReader(packet);
        return reader.TryReadVarInt(out _) && reader.TryAdvance(2) && reader.TryReadVarInt(out entityId) && reader.Remaining == 0 && entityId > 0;
    }
}
