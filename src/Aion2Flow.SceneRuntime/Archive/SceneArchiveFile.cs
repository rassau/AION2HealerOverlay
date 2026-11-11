using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.SceneRuntime.Twins;

namespace Cloris.Aion2Flow.SceneRuntime.Archive;

// History entries saved as gzipped JSON, a root array with one item per entry. An item holds everything the overlay,
// the detail flyout and playback read from an archive, so an imported entry shows exactly what the original did, and
// the raw combat and journal data makes the file usable as a bug report.
public static class SceneArchiveFile
{
    public const string Extension = ".aion2heal";
    // Exports from before the program became Aion2Heal: the same format.
    public const string LegacyExtension = ".aion2flow";
    private const int FormatVersion = 1;

    public static async Task WriteAsync(Stream stream, IEnumerable<ArchivedEncounterRecord> records, CancellationToken cancellationToken = default)
    {
        await using var gzip = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);
        await JsonSerializer.SerializeAsync(gzip, records.Select(ToModel), SceneArchiveJsonContext.Default.Entries, cancellationToken);
    }

    // A file that ends mid-entry (the overlay closed while saving it) throws SceneArchiveCutOffException, which carries
    // the complete entries before the cut.
    public static async Task<List<ArchivedEncounterRecord>> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await using var gzip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
        var records = new List<ArchivedEncounterRecord>();
        try
        {
            await foreach (var model in JsonSerializer.DeserializeAsyncEnumerable(gzip, SceneArchiveJsonContext.Default.SceneArchiveEntryModel, cancellationToken))
            {
                if (model is not { Version: FormatVersion })
                    throw new InvalidDataException($"Not an Aion2Heal history file, or one written by a newer version (format {model?.Version}).");

                records.Add(FromModel(model));
            }
        }
        catch (JsonException ex) when (records.Count > 0)
        {
            throw new SceneArchiveCutOffException(records, ex);
        }

        return records;
    }

    private static SceneArchiveEntryModel ToModel(ArchivedEncounterRecord record)
    {
        var payload = record.ScenePayload;
        var identityScope = payload.IdentityScope;
        return new SceneArchiveEntryModel
        {
            Version = FormatVersion,
            Id = record.Id,
            Name = record.Name,
            ArchivedAt = record.ArchivedAt,
            Trigger = record.Trigger,
            IsAutomatic = record.IsAutomatic,
            Kind = payload.Kind,
            SceneStarted = payload.SceneStarted,
            Snapshot = ToModel(payload.Snapshot),
            CombatEvents = [.. payload.CombatEvents],
            MechanicEvents = [.. payload.MechanicEvents],
            ResourceEvents = [.. payload.ResourceEvents],
            PcMetadata = identityScope.PcMetadataSpan.ToArray(),
            NpcCodes = identityScope.NpcCodeSpan.ToArray(),
            MapCodes = identityScope.MapCodeSpan.ToArray(),
            Pairs = payload.Pairs.AsSpan().ToArray(),
            Combatants = payload.Combatants.AsSpan().ToArray(),
            Entities = payload.Entities.AsSpan().ToArray(),
            EntityVitals = payload.EntityVitals.AsSpan().ToArray(),
            Bosses = payload.Bosses.AsSpan().ToArray(),
            BossFights = Array.ConvertAll(payload.BossFights.AsSpan().ToArray(), static fight => new SceneArchiveBossFightModel
            {
                BossId = fight.BossId,
                BossNumber = fight.BossNumber,
                Attempt = fight.Attempt,
                Outcome = fight.Outcome,
                BossHp = fight.BossHp,
                BossMaxHp = fight.BossMaxHp,
                StaggerCount = fight.StaggerCount,
                Snapshot = ToModel(fight.Snapshot)
            }),
            MetricEventOrdinalsByCombatant = new(payload.MetricEventOrdinalsByCombatant),
            MechanicEventIndexesByCombatant = new(payload.MechanicEventIndexesByCombatant),
            ResourceEventIndexesByCombatant = new(payload.ResourceEventIndexesByCombatant),
            ProjectedMetricSources = new(payload.ProjectedMetricSources),
            ProjectedMechanicSources = new(payload.ProjectedMechanicSources),
            ProjectedResourceSources = new(payload.ProjectedResourceSources),
            Timeline = ToTimelineModel(payload.TimelineSegment),
            Twins = [.. record.Twins]
        };
    }

    private static ArchivedEncounterRecord FromModel(SceneArchiveEntryModel model)
    {
        var payload = SceneArchivePayload.Restore(
            FromModel(model.Snapshot),
            model.Kind,
            model.SceneStarted,
            FromTimelineModel(model.Timeline),
            Freeze(model.CombatEvents),
            Freeze(model.MechanicEvents),
            Freeze(model.ResourceEvents),
            new SceneIdentityScope(model.PcMetadata, model.NpcCodes, model.MapCodes),
            model.Pairs,
            model.Combatants,
            model.Entities,
            model.EntityVitals,
            model.Bosses,
            Array.ConvertAll(model.BossFights, static fight => new SceneBossFight(fight.BossId, fight.BossNumber, fight.Attempt, fight.Outcome, fight.BossHp, fight.BossMaxHp, FromModel(fight.Snapshot), fight.StaggerCount)),
            model.MetricEventOrdinalsByCombatant,
            model.MechanicEventIndexesByCombatant,
            model.ResourceEventIndexesByCombatant,
            model.ProjectedMetricSources,
            model.ProjectedMechanicSources,
            model.ProjectedResourceSources);
        return new ArchivedEncounterRecord
        {
            Id = model.Id,
            ArchivedAt = model.ArchivedAt,
            Trigger = model.Trigger,
            IsAutomatic = model.IsAutomatic,
            Name = model.Name,
            ScenePayload = payload,
            Twins = model.Twins
        };
    }

    private static SceneArchiveSnapshotModel ToModel(SceneCombatSnapshot snapshot) => new()
    {
        EncounterId = snapshot.EncounterId,
        Kind = snapshot.Kind,
        ReadModelRevision = snapshot.ReadModelRevision,
        SceneTransitionRevision = snapshot.SceneTransitionRevision,
        MapId = snapshot.MapId,
        MapInstanceId = snapshot.MapInstanceId,
        EncounterStartTime = snapshot.EncounterStartTime,
        EncounterEndTime = snapshot.EncounterEndTime,
        EncounterTime = snapshot.EncounterTime,
        Combatants = snapshot.Combatants.AsSpan().ToArray(),
        TargetObservation = snapshot.TargetObservation,
        Encounter = snapshot.Encounter,
        BossFocuses = snapshot.BossFocuses.AsSpan().ToArray(),
        BossNpcCodes = snapshot.BossNpcCodes.AsSpan().ToArray()
    };

    private static SceneCombatSnapshot FromModel(SceneArchiveSnapshotModel model) => new(
        model.EncounterId,
        model.Kind,
        model.ReadModelRevision,
        model.SceneTransitionRevision,
        model.MapId,
        model.MapInstanceId,
        model.EncounterStartTime,
        model.EncounterEndTime,
        model.EncounterTime,
        model.Combatants,
        model.TargetObservation,
        model.Encounter,
        model.BossFocuses,
        model.BossNpcCodes);

    private static EventSegment<T> Freeze<T>(T[] events) where T : struct
    {
        var journal = new EventJournal<T>(events.Length);
        foreach (ref readonly var record in events.AsSpan())
            journal.Append(in record);
        return journal.Freeze();
    }

    private static SceneArchiveTimelineModel? ToTimelineModel(SceneJournalSegment segment)
    {
        if (segment.IsEmpty)
            return null;

        var entries = new List<SceneArchiveTimelineEntry>();
        var combat = new List<CombatWireObservation>();
        var actions = new List<ActionObservation>();
        var states = new List<StateObservation>();
        var entityVitals = new List<EntityVitalObservation>();
        var auras = new List<AuraObservation>();
        var scenes = new List<SceneObservation>();
        var cursor = segment.CreateCursor();
        while (true)
        {
            var read = segment.ReadEntries(cursor, ObservedEventJournal.SegmentCapacity, batch =>
            {
                foreach (var entry in batch)
                {
                    entries.Add(new SceneArchiveTimelineEntry(entry.Domain, new ObservedEventHeader(entry.SceneSessionId, entry.Stamp, entry.SourceEntityId, entry.TargetEntityId, entry.Raw)));
                    switch (entry.Domain)
                    {
                        case ObservedEventDomain.Combat: combat.Add(entry.Combat); break;
                        case ObservedEventDomain.Action: actions.Add(entry.Action); break;
                        case ObservedEventDomain.State: states.Add(entry.State); break;
                        case ObservedEventDomain.EntityVital: entityVitals.Add(entry.EntityVital); break;
                        case ObservedEventDomain.Aura: auras.Add(entry.Aura); break;
                        case ObservedEventDomain.Scene: scenes.Add(entry.Scene); break;
                    }
                }
            });
            if (read.Count == 0)
                break;

            cursor = read.Cursor;
        }

        return new SceneArchiveTimelineModel
        {
            StartObservationOrdinal = segment.StartObservationOrdinal,
            EndObservationOrdinalExclusive = segment.CurrentEndObservationOrdinalExclusive,
            Entries = [.. entries],
            Combat = [.. combat],
            Actions = [.. actions],
            States = [.. states],
            EntityVitals = [.. entityVitals],
            Auras = [.. auras],
            Scenes = [.. scenes]
        };
    }

    private static SceneJournalSegment FromTimelineModel(SceneArchiveTimelineModel? timeline)
    {
        if (timeline is null)
            return SceneJournalSegment.Empty;

        var journal = ObservedEventJournal.StartingAt(timeline.Entries.Length > 0 ? timeline.Entries[0].Header.Stamp.ObservationOrdinal : timeline.StartObservationOrdinal);
        int combat = 0, action = 0, state = 0, entityVital = 0, aura = 0, scene = 0;
        foreach (ref readonly var entry in timeline.Entries.AsSpan())
        {
            var header = entry.Header;
            switch (entry.Domain)
            {
                case ObservedEventDomain.Combat: journal.Append(in header, in timeline.Combat[combat++]); break;
                case ObservedEventDomain.Action: journal.Append(in header, in timeline.Actions[action++]); break;
                case ObservedEventDomain.State: journal.Append(in header, in timeline.States[state++]); break;
                case ObservedEventDomain.EntityVital: journal.Append(in header, in timeline.EntityVitals[entityVital++]); break;
                case ObservedEventDomain.Aura: journal.Append(in header, in timeline.Auras[aura++]); break;
                case ObservedEventDomain.Scene: journal.Append(in header, in timeline.Scenes[scene++]); break;
                default: journal.AppendDiagnostic(in header); break;
            }
        }

        return new SceneJournalSegment(journal, timeline.StartObservationOrdinal, timeline.EndObservationOrdinalExclusive);
    }
}

public sealed class SceneArchiveCutOffException(List<ArchivedEncounterRecord> records, Exception innerException)
    : IOException($"The file ends after {records.Count} complete entries.", innerException)
{
    public IReadOnlyList<ArchivedEncounterRecord> Records { get; } = records;
}

internal sealed class SceneArchiveEntryModel
{
    public int Version { get; set; }
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public DateTimeOffset ArchivedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public bool IsAutomatic { get; set; }
    public SceneKind Kind { get; set; }
    public DateTimeOffset SceneStarted { get; set; }
    public SceneArchiveSnapshotModel Snapshot { get; set; } = new();
    public CombatEventRecord[] CombatEvents { get; set; } = [];
    public CombatMechanicEventRecord[] MechanicEvents { get; set; } = [];
    public CombatResourceEventRecord[] ResourceEvents { get; set; } = [];
    public PcMetadataEntry[] PcMetadata { get; set; } = [];
    public NpcCodeEntry[] NpcCodes { get; set; } = [];
    public MapCodeEntry[] MapCodes { get; set; } = [];
    public DirectedPairSnapshot[] Pairs { get; set; } = [];
    public CombatantSummary[] Combatants { get; set; } = [];
    public SceneArchiveEntityIdentity[] Entities { get; set; } = [];
    public SceneArchiveEntityVital[] EntityVitals { get; set; } = [];
    public SceneArchiveBossFocus[] Bosses { get; set; } = [];
    public SceneArchiveBossFightModel[] BossFights { get; set; } = [];
    public Dictionary<int, long[]> MetricEventOrdinalsByCombatant { get; set; } = [];
    public Dictionary<int, int[]> MechanicEventIndexesByCombatant { get; set; } = [];
    public Dictionary<int, int[]> ResourceEventIndexesByCombatant { get; set; } = [];
    public Dictionary<long, int> ProjectedMetricSources { get; set; } = [];
    public Dictionary<int, int> ProjectedMechanicSources { get; set; } = [];
    public Dictionary<int, int> ProjectedResourceSources { get; set; } = [];
    public SceneArchiveTimelineModel? Timeline { get; set; }
    // Files from before twins have none.
    public DigitalTwin[] Twins { get; set; } = [];
}

internal sealed class SceneArchiveSnapshotModel
{
    public Guid EncounterId { get; set; }
    public SceneKind Kind { get; set; }
    public long ReadModelRevision { get; set; }
    public long SceneTransitionRevision { get; set; }
    public uint MapId { get; set; }
    public uint MapInstanceId { get; set; }
    public long EncounterStartTime { get; set; }
    public long EncounterEndTime { get; set; }
    public long EncounterTime { get; set; }
    public CombatantSnapshotEntry[] Combatants { get; set; } = [];
    public NpcRuntimeObservationSnapshot? TargetObservation { get; set; }
    public EncounterSummarySnapshot Encounter { get; set; }
    public SceneBossFocusSnapshot[] BossFocuses { get; set; } = [];
    public int[] BossNpcCodes { get; set; } = [];
}

internal sealed class SceneArchiveBossFightModel
{
    public int BossId { get; set; }
    public int BossNumber { get; set; }
    public int Attempt { get; set; }
    public BossFightOutcome Outcome { get; set; }
    public long BossHp { get; set; }
    public long BossMaxHp { get; set; }
    public int StaggerCount { get; set; }
    public SceneArchiveSnapshotModel Snapshot { get; set; } = new();
}

// The journal stores entries as headers plus one payload list per domain; the file keeps that layout, so each
// payload list is read in order as its domain comes up in Entries.
internal sealed class SceneArchiveTimelineModel
{
    public long StartObservationOrdinal { get; set; }
    public long EndObservationOrdinalExclusive { get; set; }
    public SceneArchiveTimelineEntry[] Entries { get; set; } = [];
    public CombatWireObservation[] Combat { get; set; } = [];
    public ActionObservation[] Actions { get; set; } = [];
    public StateObservation[] States { get; set; } = [];
    public EntityVitalObservation[] EntityVitals { get; set; } = [];
    public AuraObservation[] Auras { get; set; } = [];
    public SceneObservation[] Scenes { get; set; } = [];
}

internal readonly record struct SceneArchiveTimelineEntry(ObservedEventDomain Domain, ObservedEventHeader Header);

// Skipping defaults keeps the mostly-zero wire fields out of the file; IncludeFields covers the tuples on entities.
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    IgnoreReadOnlyProperties = true,
    IncludeFields = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(IEnumerable<SceneArchiveEntryModel>), TypeInfoPropertyName = "Entries")]
[JsonSerializable(typeof(SceneArchiveEntryModel))]
[JsonSerializable(typeof(DigitalTwin))]
internal sealed partial class SceneArchiveJsonContext : JsonSerializerContext
{
}
