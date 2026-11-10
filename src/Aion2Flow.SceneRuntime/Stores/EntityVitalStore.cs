using System.Runtime.InteropServices;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.SceneRuntime.Stores;

public sealed class EntityVitalStore(int capacity = 0)
{
    private readonly Dictionary<int, EntityVitalState> _states = capacity > 0 ? new Dictionary<int, EntityVitalState>(capacity) : [];
    private readonly Dictionary<int, List<MissingHpSample>> _missingHp = [];
    private long _revision;

    public IReadOnlyDictionary<int, EntityVitalState> States => _states;

    public long Revision => _revision;

    public EntityVitalState Apply(ObservedEventEntry entry)
    {
        if (entry.Domain != ObservedEventDomain.EntityVital)
            throw new ArgumentException("The journal entry is not an entity-vital observation.", nameof(entry));

        return Apply(in entry.EntityVital, entry.ObservedAtMilliseconds, entry.Stamp.ObservationOrdinal);
    }

    public EntityVitalState Apply(
        in EntityVitalObservation observation,
        long observedAtMilliseconds,
        long observationOrdinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(observation.EntityId);
        ArgumentOutOfRangeException.ThrowIfNegative(observedAtMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegative(observationOrdinal);

        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(_states, observation.EntityId, out var exists);
        var maxHp = observation.MaxHp is > 0
            ? observation.MaxHp
            : exists
                ? state.MaxHp
                : null;
        state = new EntityVitalState(
            observation.EntityId,
            Math.Max(0, observation.CurrentHp),
            maxHp,
            observedAtMilliseconds,
            observationOrdinal,
            ++_revision);
        // Dead counts as nothing missing: 0 HP can't be healed, only revived.
        if (maxHp is long knownMaxHp)
            RecordMissingHp(observation.EntityId, observedAtMilliseconds, state.CurrentHp == 0 ? 0 : Math.Max(0, knownMaxHp - state.CurrentHp));
        return state;
    }

    public bool TryGet(int entityId, out EntityVitalState state) => _states.TryGetValue(entityId, out state);

    // HP an entity was missing while alive, times the seconds it was missing it, inside [start, end]. Zero until
    // its max HP is known: players only report it through party/force status, so it covers group members.
    public long GetUnhealedHealthSeconds(int entityId, long startMilliseconds, long endMilliseconds)
    {
        if (endMilliseconds <= startMilliseconds || !_missingHp.TryGetValue(entityId, out var samples))
            return 0;

        var total = 0L;
        for (var i = 0; i < samples.Count; i++)
        {
            var from = Math.Max(samples[i].ObservedAtMilliseconds, startMilliseconds);
            var to = i + 1 < samples.Count ? Math.Min(samples[i + 1].ObservedAtMilliseconds, endMilliseconds) : endMilliseconds;
            if (to > from)
                total += samples[i].MissingHp * (to - from);
        }

        return total / 1000;
    }

    private void RecordMissingHp(int entityId, long observedAtMilliseconds, long missingHp)
    {
        if (!_missingHp.TryGetValue(entityId, out var samples))
            _missingHp[entityId] = samples = [];
        else if (samples[^1].MissingHp == missingHp)
            return;
        else
            observedAtMilliseconds = Math.Max(observedAtMilliseconds, samples[^1].ObservedAtMilliseconds);

        samples.Add(new MissingHpSample(observedAtMilliseconds, missingHp));
    }

    public EntityVitalState[] CreateStateSnapshot()
    {
        if (_states.Count == 0)
            return [];

        var result = new EntityVitalState[_states.Count];
        var index = 0;
        foreach (var state in _states.Values)
            result[index++] = state;
        Array.Sort(result, static (left, right) => left.EntityId.CompareTo(right.EntityId));
        return result;
    }

    public void Clear()
    {
        _missingHp.Clear();
        if (_states.Count == 0)
            return;

        _states.Clear();
        _revision++;
    }

    private readonly record struct MissingHpSample(long ObservedAtMilliseconds, long MissingHp);
}

public readonly record struct EntityVitalState(
    int EntityId,
    long CurrentHp,
    long? MaxHp,
    long ObservedAtMilliseconds,
    long ObservationOrdinal,
    long Revision);
