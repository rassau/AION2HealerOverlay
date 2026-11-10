using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Cloris.Aion2Flow.SceneRuntime.Combat;
namespace Cloris.Aion2Flow.SceneRuntime.Archive;

public sealed class EncounterArchiveService
{
    public const int MaxHistoryCount = 10;

    private readonly Lock _lock = new();
    private readonly List<ArchivedEncounterRecord> _history = [];
    private readonly Dictionary<Guid, ArchivedEncounterRecord> _historyByEncounterId = [];
    private ImmutableArray<ArchivedEncounterRecord> _historySnapshot = [];

    public event EventHandler? HistoryChanged;

    public IReadOnlyList<ArchivedEncounterRecord> History => _historySnapshot;

    public ArchivedEncounterRecord? Archive(SceneArchivePayload payload, string trigger, bool isAutomatic)
    {
        return AddArchiveRecord(payload, trigger, isAutomatic);
    }

    private ArchivedEncounterRecord? AddArchiveRecord(SceneArchivePayload scenePayload, string trigger, bool isAutomatic)
    {
        var archivedSnapshot = scenePayload.Snapshot;
        if (archivedSnapshot.EncounterTime <= 0 || archivedSnapshot.EncounterEndTime < archivedSnapshot.EncounterStartTime || archivedSnapshot.Combatants.Count == 0)
        {
            return null;
        }

        ArchivedEncounterRecord? record;
        bool historyChanged;
        lock (_lock)
        {
            if (_history.Count > 0 && IsEquivalent(_history[0].ScenePayload.Snapshot, archivedSnapshot))
            {
                return null;
            }

            record = new ArchivedEncounterRecord
            {
                ArchivedAt = scenePayload.SceneStarted.ToLocalTime(),
                Trigger = trigger,
                IsAutomatic = isAutomatic,
                ScenePayload = scenePayload
            };

            InsertLocked(record);
            TrimLocked();
            historyChanged = true;
        }

        if (historyChanged)
        {
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        return record;
    }

    // Imported entries go on top in file order, like new ones; entries already in the history are skipped.
    public int Import(IReadOnlyList<ArchivedEncounterRecord> records)
    {
        var added = 0;
        lock (_lock)
        {
            for (var i = records.Count - 1; i >= 0; i--)
            {
                if (_historyByEncounterId.ContainsKey(records[i].ScenePayload.Snapshot.EncounterId))
                    continue;

                InsertLocked(records[i]);
                added++;
            }

            TrimLocked();
        }

        if (added > 0)
            HistoryChanged?.Invoke(this, EventArgs.Empty);

        return added;
    }

    public void Rename(ArchivedEncounterRecord record, string? name)
    {
        lock (_lock)
            record.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(ArchivedEncounterRecord record)
    {
        lock (_lock)
        {
            if (!_history.Remove(record))
                return;

            _historyByEncounterId.Remove(record.ScenePayload.Snapshot.EncounterId);
            _historySnapshot = [.. _history];
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        lock (_lock)
        {
            if (_history.Count == 0)
                return;

            _history.Clear();
            _historyByEncounterId.Clear();
            _historySnapshot = [];
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void InsertLocked(ArchivedEncounterRecord record)
    {
        _history.Insert(0, record);
        _historyByEncounterId[record.ScenePayload.Snapshot.EncounterId] = record;
    }

    private void TrimLocked()
    {
        if (_history.Count > MaxHistoryCount)
        {
            for (var i = MaxHistoryCount; i < _history.Count; i++)
            {
                _historyByEncounterId.Remove(_history[i].ScenePayload.Snapshot.EncounterId);
            }

            _history.RemoveRange(MaxHistoryCount, _history.Count - MaxHistoryCount);
        }

        _historySnapshot = [.. _history];
    }

    public bool TryGetEncounter(Guid encounterId, [NotNullWhen(true)] out ArchivedEncounterRecord? record)
    {
        lock (_lock)
        {
            return _historyByEncounterId.TryGetValue(encounterId, out record);
        }
    }

    private static bool IsEquivalent(SceneCombatSnapshot left, SceneCombatSnapshot right)
    {
        return left.EncounterTime == right.EncounterTime &&
               left.EncounterId == right.EncounterId &&
               left.Encounter.TrackingTargetId == right.Encounter.TrackingTargetId &&
               (left.TargetObservation?.InstanceId ?? 0) == (right.TargetObservation?.InstanceId ?? 0) &&
               left.MapId == right.MapId &&
               left.MapInstanceId == right.MapInstanceId &&
               left.Combatants.Count == right.Combatants.Count &&
               SumDamage(left) == SumDamage(right);
    }

    private static double SumDamage(SceneCombatSnapshot snapshot)
    {
        var totalDamage = 0d;
        var combatants = snapshot.Combatants.AsSpan();
        foreach (ref readonly var entry in combatants)
        {
            totalDamage += entry.Metrics.DamageAmount;
        }

        return totalDamage;
    }
}
