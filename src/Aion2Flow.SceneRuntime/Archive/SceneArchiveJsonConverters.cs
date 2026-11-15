using System.Text.Json;
using System.Text.Json.Serialization;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.SceneRuntime.Archive;

// Reading a history file (SceneArchiveFile.ReadAsync): the timeline's headers and the raw packet references are most of
// it, and the generated metadata boxes every struct it reads (and, for the record structs, each constructor argument).
// These read the same JSON without that and skip unknown properties as the generated code does. Writing stays with the
// generated code, so files come out byte for byte as before. A property added to SceneArchiveTimelineEntry,
// ObservedEventHeader, TimelineStamp, RawPacketReference, PacketStructurePath or PacketStructureReference has to be
// read here too (SceneArchiveReadTests).
internal sealed class RawPacketReferenceJsonConverter : JsonConverter<RawPacketReference>
{
    public override RawPacketReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ReadRaw(ref reader);

    public override void Write(Utf8JsonWriter writer, RawPacketReference value, JsonSerializerOptions options) =>
        throw new NotSupportedException("History files are written by the generated serializer.");

    internal static RawPacketReference ReadRaw(ref Utf8JsonReader reader)
    {
        ExpectStartObject(ref reader);
        ushort opcode = 0;
        int payloadLength = 0;
        long captureSequence = 0;
        PacketStructurePath path = default;
        while (NextProperty(ref reader))
        {
            if (reader.ValueTextEquals("Opcode"u8)) { reader.Read(); opcode = reader.GetUInt16(); }
            else if (reader.ValueTextEquals("PayloadLength"u8)) { reader.Read(); payloadLength = reader.GetInt32(); }
            else if (reader.ValueTextEquals("CaptureSequence"u8)) { reader.Read(); captureSequence = reader.GetInt64(); }
            else if (reader.ValueTextEquals("StructurePath"u8)) { reader.Read(); path = ReadPath(ref reader); }
            else SkipValue(ref reader);
        }

        return new RawPacketReference(opcode, payloadLength, captureSequence, path);
    }

    private static PacketStructurePath ReadPath(ref Utf8JsonReader reader)
    {
        ExpectStartObject(ref reader);
        PacketStructureReference root = default, level1 = default, level2 = default, level3 = default, leaf = default;
        var depth = 0;
        while (NextProperty(ref reader))
        {
            if (reader.ValueTextEquals("Root"u8)) { reader.Read(); root = ReadReference(ref reader); }
            else if (reader.ValueTextEquals("Level1"u8)) { reader.Read(); level1 = ReadReference(ref reader); }
            else if (reader.ValueTextEquals("Level2"u8)) { reader.Read(); level2 = ReadReference(ref reader); }
            else if (reader.ValueTextEquals("Level3"u8)) { reader.Read(); level3 = ReadReference(ref reader); }
            else if (reader.ValueTextEquals("Leaf"u8)) { reader.Read(); leaf = ReadReference(ref reader); }
            else if (reader.ValueTextEquals("Depth"u8)) { reader.Read(); depth = reader.GetInt32(); }
            else SkipValue(ref reader);
        }

        return new PacketStructurePath(root, level1, level2, level3, leaf, depth);
    }

    private static PacketStructureReference ReadReference(ref Utf8JsonReader reader)
    {
        ExpectStartObject(ref reader);
        PacketStructureKind kind = 0;
        int scopeId = 0, parentScopeId = 0, depth = 0, siblingIndex = 0, offset = 0, length = 0, bodyOffset = 0, bodyLength = 0;
        while (NextProperty(ref reader))
        {
            if (reader.ValueTextEquals("Kind"u8)) { reader.Read(); kind = (PacketStructureKind)reader.GetByte(); }
            else if (reader.ValueTextEquals("ScopeId"u8)) { reader.Read(); scopeId = reader.GetInt32(); }
            else if (reader.ValueTextEquals("ParentScopeId"u8)) { reader.Read(); parentScopeId = reader.GetInt32(); }
            else if (reader.ValueTextEquals("Depth"u8)) { reader.Read(); depth = reader.GetInt32(); }
            else if (reader.ValueTextEquals("SiblingIndex"u8)) { reader.Read(); siblingIndex = reader.GetInt32(); }
            else if (reader.ValueTextEquals("Offset"u8)) { reader.Read(); offset = reader.GetInt32(); }
            else if (reader.ValueTextEquals("Length"u8)) { reader.Read(); length = reader.GetInt32(); }
            else if (reader.ValueTextEquals("BodyOffset"u8)) { reader.Read(); bodyOffset = reader.GetInt32(); }
            else if (reader.ValueTextEquals("BodyLength"u8)) { reader.Read(); bodyLength = reader.GetInt32(); }
            else SkipValue(ref reader);
        }

        return new PacketStructureReference(kind, scopeId, parentScopeId, depth, siblingIndex, offset, length, bodyOffset, bodyLength);
    }

    // The errors below carry no message: the serializer then writes its own, as the generated code's errors have it
    // ("The JSON value could not be converted to ...", the path and the position), which an import shows.
    internal static void ExpectStartObject(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();
    }

    // Moves to the next property's name; false at the object's end.
    internal static bool NextProperty(ref Utf8JsonReader reader)
    {
        if (!reader.Read())
            throw new JsonException();

        return reader.TokenType switch
        {
            JsonTokenType.PropertyName => true,
            JsonTokenType.EndObject => false,
            _ => throw new JsonException()
        };
    }

    // An unknown property's value (a newer version's). Skip() throws on the partial buffer of a streamed read even though
    // the serializer has the whole value buffered for a converter; TrySkip doesn't.
    internal static void SkipValue(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || !reader.TrySkip())
            throw new JsonException();
    }
}

internal sealed class SceneArchiveTimelineEntryJsonConverter : JsonConverter<SceneArchiveTimelineEntry>
{
    public override SceneArchiveTimelineEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        RawPacketReferenceJsonConverter.ExpectStartObject(ref reader);
        ObservedEventDomain domain = 0;
        ObservedEventHeader header = default;
        while (RawPacketReferenceJsonConverter.NextProperty(ref reader))
        {
            if (reader.ValueTextEquals("Domain"u8)) { reader.Read(); domain = (ObservedEventDomain)reader.GetByte(); }
            else if (reader.ValueTextEquals("Header"u8)) { reader.Read(); header = ReadHeader(ref reader); }
            else RawPacketReferenceJsonConverter.SkipValue(ref reader);
        }

        return new SceneArchiveTimelineEntry(domain, header);
    }

    public override void Write(Utf8JsonWriter writer, SceneArchiveTimelineEntry value, JsonSerializerOptions options) =>
        throw new NotSupportedException("History files are written by the generated serializer.");

    private static ObservedEventHeader ReadHeader(ref Utf8JsonReader reader)
    {
        RawPacketReferenceJsonConverter.ExpectStartObject(ref reader);
        var sceneSessionId = Guid.Empty;
        TimelineStamp stamp = default;
        int sourceEntityId = 0, targetEntityId = 0;
        RawPacketReference raw = default;
        while (RawPacketReferenceJsonConverter.NextProperty(ref reader))
        {
            if (reader.ValueTextEquals("SceneSessionId"u8)) { reader.Read(); sceneSessionId = reader.GetGuid(); }
            else if (reader.ValueTextEquals("Stamp"u8)) { reader.Read(); stamp = ReadStamp(ref reader); }
            else if (reader.ValueTextEquals("SourceEntityId"u8)) { reader.Read(); sourceEntityId = reader.GetInt32(); }
            else if (reader.ValueTextEquals("TargetEntityId"u8)) { reader.Read(); targetEntityId = reader.GetInt32(); }
            else if (reader.ValueTextEquals("Raw"u8)) { reader.Read(); raw = RawPacketReferenceJsonConverter.ReadRaw(ref reader); }
            else RawPacketReferenceJsonConverter.SkipValue(ref reader);
        }

        return new ObservedEventHeader(sceneSessionId, stamp, sourceEntityId, targetEntityId, raw);
    }

    private static TimelineStamp ReadStamp(ref Utf8JsonReader reader)
    {
        RawPacketReferenceJsonConverter.ExpectStartObject(ref reader);
        long offsetTicks = 0, observationOrdinal = 0, flushId = 0;
        while (RawPacketReferenceJsonConverter.NextProperty(ref reader))
        {
            if (reader.ValueTextEquals("OffsetTicks"u8)) { reader.Read(); offsetTicks = reader.GetInt64(); }
            else if (reader.ValueTextEquals("ObservationOrdinal"u8)) { reader.Read(); observationOrdinal = reader.GetInt64(); }
            else if (reader.ValueTextEquals("FlushId"u8)) { reader.Read(); flushId = reader.GetInt64(); }
            else RawPacketReferenceJsonConverter.SkipValue(ref reader);
        }

        return new TimelineStamp(offsetTicks, observationOrdinal, flushId);
    }
}
