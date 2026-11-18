using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.SceneRuntime.Twins;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

// History files are read with hand-written converters for the timeline's headers and raw packet references
// (SceneArchiveJsonConverters) and written by the generated serializer: whatever the writer writes of them has to come
// back, also from a file a newer version wrote with more properties, read in pieces as an import reads a file.
public sealed class SceneArchiveReadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Converters_Read_Every_Field_The_Writer_Writes(bool withUnknownProperties)
    {
        var seed = 1;
        // Every settable field set (a field added later is set too), and entries with parts left at their defaults.
        var entries = Enumerable.Range(0, 400).Select(_ => (SceneArchiveTimelineEntry)Fill(typeof(SceneArchiveTimelineEntry), ref seed)).ToList();
        var raw = (RawPacketReference)Fill(typeof(RawPacketReference), ref seed);
        entries.Add(default);
        entries.Add(new SceneArchiveTimelineEntry(ObservedEventDomain.Aura, default));
        entries.Add(new SceneArchiveTimelineEntry(ObservedEventDomain.Diagnostic, new ObservedEventHeader(Guid.Empty, new TimelineStamp(0, 7, 0), 0, 3, default)));
        entries.Add(new SceneArchiveTimelineEntry(default, new ObservedEventHeader(default, default, 0, 0, new RawPacketReference(5, 0, 0, new PacketStructureReference(PacketStructureKind.FrameBatchEntry, 0, 0, 2, 0, 0, 9, 0, 0)))));
        var model = new SceneArchiveEntryModel
        {
            Version = 1,
            CombatEvents = [new CombatEventRecord { Raw = raw }, default],
            MechanicEvents = [default(CombatMechanicEventRecord) with { Raw = raw }],
            ResourceEvents = [default(CombatResourceEventRecord) with { Raw = raw }],
            Timeline = new SceneArchiveTimelineModel { Entries = [.. entries] }
        };

        var json = JsonSerializer.Serialize(model, SceneArchiveJsonContext.Default.SceneArchiveEntryModel);
        if (withUnknownProperties)
        {
            json = json
                .Replace("{\"Domain\":", "{\"Next\":[1,{\"a\":null}],\"Domain\":")
                .Replace("\"Header\":{", "\"Header\":{\"Next\":{\"b\":[[],{}]},")
                .Replace("\"Stamp\":{", "\"Stamp\":{\"Next\":\"c\",")
                .Replace("\"Raw\":{", "\"Raw\":{\"Next\":{\"d\":{\"e\":[true]}},")
                .Replace("\"StructurePath\":{", "\"StructurePath\":{\"Next\":[{\"f\":1}],")
                .Replace("\"Leaf\":{", "\"Leaf\":{\"Next\":false,");
            Assert.Contains("\"Leaf\":{\"Next\"", json);
        }

        // A root array of entries, read the way SceneArchiveFile.ReadAsync does: streamed, so the reader only ever has
        // part of the file.
        var file = await Gzip("[" + json + "," + json + "]");
        foreach (var typeInfo in new[] { SceneArchiveFile.ReadTypeInfo, SceneArchiveJsonContext.Default.SceneArchiveEntryModel })
        {
            var read = await ReadAllAsync(file, typeInfo);
            Assert.Equal(2, read.Count);
            Assert.All(read, entry =>
            {
                Assert.Equal(model.Timeline!.Entries, entry.Timeline!.Entries);
                Assert.Equal(model.CombatEvents, entry.CombatEvents);
                Assert.Equal(model.MechanicEvents, entry.MechanicEvents);
                Assert.Equal(model.ResourceEvents, entry.ResourceEvents);
            });
        }

        // Without the converters both readers would still pass, and only the speed would be lost.
        Assert.Contains(SceneArchiveFile.ReadTypeInfo.Options.Converters, static c => c is RawPacketReferenceJsonConverter);
        Assert.Contains(SceneArchiveFile.ReadTypeInfo.Options.Converters, static c => c is SceneArchiveTimelineEntryJsonConverter);
    }

    // A file broken inside its first entry fails with the serializer's message, which an import shows: the generated
    // code's position, and a path that ends at the value a converter reads (the generated code names the property in it).
    [Theory]
    [InlineData("\"Raw\":{", "\"Raw\":null,\"x\":{", "$[0].CombatEvents[0].Raw")]
    [InlineData("\"Opcode\":", "\"Opcode\":1e9,\"x\":", "$[0].CombatEvents[0].Raw")]
    [InlineData("\"ParentScopeId\":", null, "$[0].CombatEvents[0].Raw")]
    [InlineData("\"Entries\":[", "\"Entries\":[null,", "$[0].Timeline.Entries[0]")]
    [InlineData("\"Header\":{", "\"Header\":5,\"x\":{", "$[0].Timeline.Entries[0]")]
    [InlineData("\"FlushId\":", null, "$[0].Timeline.Entries[0]")]
    public async Task A_File_Broken_In_Its_First_Entry_Fails_With_The_Generated_Position_And_The_Path_Down_To_The_Converted_Value(string at, string? replacement, string path)
    {
        var seed = 1;
        var model = new SceneArchiveEntryModel
        {
            Version = 1,
            CombatEvents = [new CombatEventRecord { Raw = (RawPacketReference)Fill(typeof(RawPacketReference), ref seed) }],
            Timeline = new SceneArchiveTimelineModel { Entries = [(SceneArchiveTimelineEntry)Fill(typeof(SceneArchiveTimelineEntry), ref seed)] }
        };
        var json = "[" + JsonSerializer.Serialize(model, SceneArchiveJsonContext.Default.SceneArchiveEntryModel) + "]";
        var index = json.IndexOf(at, StringComparison.Ordinal);
        // Malformed there, or cut off just after it.
        json = replacement is null ? json[..(index + at.Length + 1)] : json[..index] + replacement + json[(index + at.Length)..];
        var file = await Gzip(json);

        var generated = await Assert.ThrowsAsync<JsonException>(() => ReadAllAsync(file, SceneArchiveJsonContext.Default.SceneArchiveEntryModel));
        var converters = await Assert.ThrowsAsync<JsonException>(() => SceneArchiveFile.ReadAsync(new MemoryStream(file), TestContext.Current.CancellationToken));
        Assert.Equal(path, converters.Path);
        Assert.StartsWith(path, generated.Path);
        Assert.Equal((generated.LineNumber, generated.BytePositionInLine), (converters.LineNumber, converters.BytePositionInLine));
        Assert.EndsWith($" Path: {path} | LineNumber: {converters.LineNumber} | BytePositionInLine: {converters.BytePositionInLine}.", converters.Message);
    }

    // The passive levels of a spawn (StateCodes.SkillLevel) go into a history entry with the other states, and come back.
    [Fact]
    public async Task A_Spawns_Passive_Levels_Come_Back_From_The_File()
    {
        var scene = new SceneLiveReadModel();
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var spawn = new PacketObservationSource(1_794_600_000_000, 0, 0x4536, 0, 0, default);
        sink.AppendNickname(in spawn, 100, "Healer", originServerId: 2304);
        sink.AppendSkillLevel(in spawn, 100, 177_300_001, 16);

        using var file = new MemoryStream();
        await SceneArchiveFile.WriteAsync(file, [new ArchivedEncounterRecord { ScenePayload = scene.CreateArchivePayload() }], TestContext.Current.CancellationToken);
        var imported = Assert.Single(await SceneArchiveFile.ReadAsync(new MemoryStream(file.ToArray()), TestContext.Current.CancellationToken));

        Assert.Equal(16, SeenSkillLevels.Read(imported.ScenePayload.TimelineSegment, long.MaxValue, TestContext.Current.CancellationToken)[100][17_730_000]);
    }

    // A value of every settable property (init ones included) of a struct and the structs in it, each one different.
    private static object Fill(Type type, ref int seed)
    {
        var value = seed++;
        if (type.IsEnum)
            return Enum.ToObject(type, value % 100 + 1);
        if (type == typeof(Guid))
            return new Guid(value, 1, 2, [3, 4, 5, 6, 7, 8, 9, 10]);
        if (type == typeof(long))
            return value * 4_000_000_007L;
        if (type == typeof(int))
            return value * 7_919;
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return Fill(underlying, ref seed);
        if (type.IsPrimitive)
            return Convert.ChangeType(value % 100 + 1, type, System.Globalization.CultureInfo.InvariantCulture);

        var instance = Activator.CreateInstance(type)!;
        foreach (var property in type.GetProperties())
        {
            if (property.SetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0)
                property.SetValue(instance, Fill(property.PropertyType, ref seed));
        }

        // A type Fill can't set stays at its default, which the writer skips: a converter could drop it unnoticed.
        if (type.IsValueType && instance.Equals(Activator.CreateInstance(type)))
            throw new NotSupportedException($"Fill can't give {type} a value: add it here.");

        return instance;
    }

    private static async Task<byte[]> Gzip(string json)
    {
        using var file = new MemoryStream();
        await using (var gzip = new GZipStream(file, CompressionLevel.Fastest, leaveOpen: true))
            await gzip.WriteAsync(Encoding.UTF8.GetBytes(json), TestContext.Current.CancellationToken);
        return file.ToArray();
    }

    private static async Task<List<SceneArchiveEntryModel>> ReadAllAsync(byte[] file, JsonTypeInfo<SceneArchiveEntryModel> typeInfo)
    {
        await using var gzip = new GZipStream(new MemoryStream(file), CompressionMode.Decompress);
        var entries = new List<SceneArchiveEntryModel>();
        await foreach (var entry in JsonSerializer.DeserializeAsyncEnumerable(gzip, typeInfo, TestContext.Current.CancellationToken))
            entries.Add(entry!);
        return entries;
    }
}
