using System.Buffers.Binary;
using System.Collections.Frozen;
using Cloris.Aion2Flow.Resources.Generated;
using K4os.Compression.LZ4;

namespace Cloris.Aion2Flow.Resources.Catalog;

internal static partial class ResourcePackReader
{
    private enum SectionId : ushort
    {
        SkillDefinitions = 1,
        SkillBaseProjections = 2,
        SkillEffectOwners = 3,
        SkillSemanticRuntimeSkillIds = 4,
        SkillSemanticRuntimeSlots = 5,
        SkillSemanticRuntimeNodes = 6,
        SkillSemanticRuntimeNodeSlots = 7,
        NpcDefinitions = 8,
        SkillNames = 101,
        NpcCatalogNames = 102,
        MapNames = 103,
        ServerNames = 104
    }

    public static ResourceSharedCatalog LoadShared()
    {
        var payload = LoadPackPayload(ResourcePackManifest.SharedResourceName, ResourcePackDecoder.SharedPackKind, ResourcePackManifest.SharedUncompressedLength, ResourcePackManifest.SharedChecksum);
        var sections = ReadSections(payload);

        return new ResourceSharedCatalog(
            ReadSkillDefinitions(RequireSection(sections, SectionId.SkillDefinitions)),
            ReadSkillBaseProjections(RequireSection(sections, SectionId.SkillBaseProjections)),
            ReadEffectSkillIds(RequireSection(sections, SectionId.SkillEffectOwners)),
            ReadSkillSemanticRuntimeIndex(sections),
            ReadNpcDefinitions(RequireSection(sections, SectionId.NpcDefinitions)));
    }

    public static ResourceLocaleCatalog LoadLocale(string language)
    {
        var entry = ResourcePackManifest.GetLocale(language);
        var payload = LoadPackPayload(entry.ResourceName, ResourcePackDecoder.LocalePackKind, entry.UncompressedLength, entry.Checksum);
        var sections = ReadSections(payload);

        return new ResourceLocaleCatalog(
            language,
            ReadIntStringMap(RequireSection(sections, SectionId.SkillNames)),
            ReadIntStringMap(RequireSection(sections, SectionId.NpcCatalogNames)),
            ReadUIntStringMap(RequireSection(sections, SectionId.MapNames)),
            ReadServerNames(RequireSection(sections, SectionId.ServerNames)));
    }

    // A locale's map ids alone (see ResourceCatalog.LoadMapIds), without its skill and NPC names.
    public static FrozenSet<uint> LoadMapIds(string language)
    {
        var entry = ResourcePackManifest.GetLocale(language);
        var payload = LoadPackPayload(entry.ResourceName, ResourcePackDecoder.LocalePackKind, entry.UncompressedLength, entry.Checksum);
        return ReadUIntStringMap(RequireSection(ReadSections(payload), SectionId.MapNames)).Keys.ToFrozenSet();
    }

    private static byte[] LoadPackPayload(string resourceName, byte expectedKind, int expectedUncompressedLength, ulong expectedChecksum)
    {
        using var stream = typeof(ResourceCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException($"Missing embedded resource pack: {resourceName}");
        using var reader = new BinaryReader(stream);
        var magic = reader.ReadUInt32();
        var version = reader.ReadUInt16();
        var kind = reader.ReadByte();
        _ = reader.ReadByte();
        var uncompressedLength = reader.ReadInt32();
        var checksum = reader.ReadUInt64();
        var compressedLength = reader.ReadInt32();
        if (magic != ResourcePackDecoder.PackMagic || version != ResourcePackDecoder.FormatVersion || kind != expectedKind || uncompressedLength != expectedUncompressedLength || checksum != expectedChecksum || compressedLength < 0)
        {
            throw new InvalidDataException($"Invalid resource pack header: {resourceName}");
        }

        var compressed = reader.ReadBytes(compressedLength);
        if (compressed.Length != compressedLength)
        {
            throw new InvalidDataException($"Truncated resource pack: {resourceName}");
        }

        var payload = new byte[uncompressedLength];
        var decodedLength = LZ4Codec.Decode(compressed, payload);
        if (decodedLength != uncompressedLength || ComputeChecksum(payload) != checksum)
        {
            throw new InvalidDataException($"Invalid resource pack payload: {resourceName}");
        }

        return payload;
    }

    private static Dictionary<SectionId, ResourcePackSection> ReadSections(byte[] payload)
    {
        ReadOnlySpan<byte> cursor = payload;
        var magic = ReadUInt32(ref cursor);
        var version = ReadUInt16(ref cursor);
        if (magic != ResourcePackDecoder.PayloadMagic || version != ResourcePackDecoder.FormatVersion)
        {
            throw new InvalidDataException("Invalid resource pack payload header.");
        }

        var sectionCount = ReadInt32(ref cursor);
        var headers = new SectionHeader[sectionCount];
        for (var i = 0; i < headers.Length; i++)
        {
            headers[i] = new SectionHeader((SectionId)ReadUInt16(ref cursor), ReadInt32(ref cursor), ReadInt32(ref cursor), ReadUInt64(ref cursor));
        }

        var sectionStart = payload.Length - cursor.Length;
        var result = new Dictionary<SectionId, ResourcePackSection>(sectionCount);
        var offset = sectionStart;
        for (var i = 0; i < headers.Length; i++)
        {
            var header = headers[i];
            if (header.Count < 0 || header.Length < 0 || payload.Length - offset < header.Length)
            {
                throw new InvalidDataException($"Invalid resource pack section length: {header.Id}");
            }

            // The section's own checksum isn't compared: the whole payload's (LoadPackPayload) already covers its bytes.
            var section = payload.AsMemory(offset, header.Length);
            offset += header.Length;
            result.Add(header.Id, new ResourcePackSection(header.Count, section));
        }

        if (offset != payload.Length)
        {
            throw new InvalidDataException("Resource pack contains trailing payload bytes.");
        }

        return result;
    }

    private static ResourcePackSection RequireSection(IReadOnlyDictionary<SectionId, ResourcePackSection> sections, SectionId sectionId)
        => sections.TryGetValue(sectionId, out var section) ? section : throw new InvalidDataException($"Missing resource pack section: {sectionId}");

    private static SkillDefinitionCatalog ReadSkillDefinitions(ResourcePackSection section)
    {
        var cursor = section.Payload.Span;
        var count = section.Count;
        var result = new SkillDefinitionCatalog(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(new SkillDefinition(
                ReadInt32(ref cursor),
                (SkillCategory)ReadByte(ref cursor),
                (SkillSourceType)ReadByte(ref cursor),
                ReadInt32(ref cursor)));
        }

        RequireFullyRead(cursor);
        return result;
    }

    private static FrozenDictionary<int, SkillBaseProjection> ReadSkillBaseProjections(ResourcePackSection section)
    {
        var cursor = section.Payload.Span;
        var count = section.Count;
        var result = new Dictionary<int, SkillBaseProjection>(count);
        for (var i = 0; i < count; i++)
        {
            var projection = new SkillBaseProjection(
                ReadInt32(ref cursor),
                ReadInt32(ref cursor));
            result.Add(projection.SkillCode, projection);
        }

        RequireFullyRead(cursor);
        return result.ToFrozenDictionary();
    }

    private static FrozenDictionary<uint, int> ReadEffectSkillIds(ResourcePackSection section)
    {
        var cursor = section.Payload.Span;
        var count = section.Count;
        var result = new Dictionary<uint, int>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(ReadUInt32(ref cursor), ReadInt32(ref cursor));
        }

        RequireFullyRead(cursor);
        return result.ToFrozenDictionary();
    }

    private static FrozenDictionary<int, NpcDefinition> ReadNpcDefinitions(ResourcePackSection section)
    {
        var cursor = section.Payload.Span;
        var count = section.Count;
        var result = new Dictionary<int, NpcDefinition>(count);
        for (var i = 0; i < count; i++)
        {
            var code = ReadInt32(ref cursor);
            var definition = new NpcDefinition(code, (NpcCatalogKind)ReadByte(ref cursor), (NpcHpDisplayScale)ReadByte(ref cursor));
            result.Add(definition.Code, definition);
        }

        RequireFullyRead(cursor);
        return result.ToFrozenDictionary();
    }

    // Not frozen: the snapshot looks each name up once while it's built and keeps only the names.
    private static Dictionary<int, string> ReadIntStringMap(ResourcePackSection section)
    {
        var cursor = section.Payload.Span;
        var count = section.Count;
        var result = new Dictionary<int, string>(count);
        // ponytail: equal names share one string (about 1.7k of 15.5k skill names and 4.9k of 12.6k NPC names differ).
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var key = ReadInt32(ref cursor);
            var name = ReadString(ref cursor);
            if (!names.Add(name))
                names.TryGetValue(name, out name!);
            result.Add(key, name);
        }

        RequireFullyRead(cursor);
        return result;
    }

    private static FrozenDictionary<uint, string> ReadUIntStringMap(ResourcePackSection section)
    {
        var cursor = section.Payload.Span;
        var count = section.Count;
        var result = new Dictionary<uint, string>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(ReadUInt32(ref cursor), ReadString(ref cursor));
        }

        RequireFullyRead(cursor);
        return result.ToFrozenDictionary();
    }

    private static FrozenDictionary<int, ServerNameEntry> ReadServerNames(ResourcePackSection section)
    {
        var cursor = section.Payload.Span;
        var count = section.Count;
        var result = new Dictionary<int, ServerNameEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = new ServerNameEntry(ReadInt32(ref cursor), ReadString(ref cursor), ReadString(ref cursor));
            result.Add(entry.Code, entry);
        }

        RequireFullyRead(cursor);
        return result.ToFrozenDictionary();
    }

    private static string ReadString(ref ReadOnlySpan<byte> cursor)
    {
        var length = ReadInt32(ref cursor);
        if (length < 0)
        {
            throw new InvalidDataException("Unexpected null string in resource pack.");
        }

        return ReadStringBody(ref cursor, length);
    }

    private static string ReadStringBody(ref ReadOnlySpan<byte> cursor, int length)
    {
        if (length == 0)
        {
            return string.Empty;
        }

        if (cursor.Length < length)
        {
            throw new InvalidDataException("Truncated string in resource pack.");
        }

        // Unmasked on the stack (the longest name in today's packs is 112 bytes), not in an array per string.
        Span<byte> bytes = length <= 256 ? stackalloc byte[256] : new byte[length];
        bytes = bytes[..length];
        cursor[..length].CopyTo(bytes);
        cursor = cursor[length..];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] ^= ResourcePackDecoder.StringMask;
        }

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static byte ReadByte(ref ReadOnlySpan<byte> cursor)
    {
        if (cursor.IsEmpty)
        {
            throw new InvalidDataException("Unexpected end of resource pack.");
        }

        var value = cursor[0];
        cursor = cursor[1..];
        return value;
    }

    private static ushort ReadUInt16(ref ReadOnlySpan<byte> cursor)
    {
        if (cursor.Length < sizeof(ushort))
        {
            throw new InvalidDataException("Unexpected end of resource pack.");
        }

        var value = BinaryPrimitives.ReadUInt16LittleEndian(cursor);
        cursor = cursor[sizeof(ushort)..];
        return value;
    }

    private static int ReadInt32(ref ReadOnlySpan<byte> cursor)
    {
        if (cursor.Length < sizeof(int))
        {
            throw new InvalidDataException("Unexpected end of resource pack.");
        }

        var value = BinaryPrimitives.ReadInt32LittleEndian(cursor);
        cursor = cursor[sizeof(int)..];
        return value;
    }

    private static uint ReadUInt32(ref ReadOnlySpan<byte> cursor)
    {
        if (cursor.Length < sizeof(uint))
        {
            throw new InvalidDataException("Unexpected end of resource pack.");
        }

        var value = BinaryPrimitives.ReadUInt32LittleEndian(cursor);
        cursor = cursor[sizeof(uint)..];
        return value;
    }

    private static ulong ReadUInt64(ref ReadOnlySpan<byte> cursor)
    {
        if (cursor.Length < sizeof(ulong))
        {
            throw new InvalidDataException("Unexpected end of resource pack.");
        }

        var value = BinaryPrimitives.ReadUInt64LittleEndian(cursor);
        cursor = cursor[sizeof(ulong)..];
        return value;
    }

    private static void RequireFullyRead(ReadOnlySpan<byte> cursor)
    {
        if (!cursor.IsEmpty)
        {
            throw new InvalidDataException("Resource pack section contains trailing bytes.");
        }
    }

    internal static ulong ComputeChecksum(ReadOnlySpan<byte> bytes)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        for (var i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= prime;
        }

        return hash;
    }

    private readonly record struct ResourcePackSection(int Count, ReadOnlyMemory<byte> Payload);
    private readonly record struct SectionHeader(SectionId Id, int Count, int Length, ulong Checksum);
}
