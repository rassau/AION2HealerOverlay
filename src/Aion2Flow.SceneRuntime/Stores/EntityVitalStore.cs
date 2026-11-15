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

        var span = CollectionsMarshal.AsSpan(samples);
        return (MissingHpUntil(span, endMilliseconds) - MissingHpUntil(span, startMilliseconds)) / 1000;
    }

    // HP·ms missing from the first sample up to a moment: the sum kept with the last sample at or before it, plus that
    // sample's since. Several samples can share a moment (RecordMissingHp moves late ones up): the last one counts on.
    private static long MissingHpUntil(ReadOnlySpan<MissingHpSample> samples, long atMilliseconds)
    {
        var (low, high) = (0, samples.Length);
        while (low < high)
        {
            var middle = (low + high) >>> 1;
            if (samples[middle].ObservedAtMilliseconds <= atMilliseconds)
                low = middle + 1;
            else
                high = middle;
        }

        if (low == 0)
            return 0;

        ref readonly var sample = ref samples[low - 1];
        return sample.MissingHpBefore + sample.MissingHp * (atMilliseconds - sample.ObservedAtMilliseconds);
    }

    private void RecordMissingHp(int entityId, long observedAtMilliseconds, long missingHp)
    {
        var missingHpBefore = 0L;
        if (!_missingHp.TryGetValue(entityId, out var samples))
            _missingHp[entityId] = samples = [];
        else if (samples[^1].MissingHp == missingHp)
            return;
        else
        {
            var last = samples[^1];
            observedAtMilliseconds = Math.Max(observedAtMilliseconds, last.ObservedAtMilliseconds);
            missingHpBefore = last.MissingHpBefore + last.MissingHp * (observedAtMilliseconds - last.ObservedAtMilliseconds);
        }

        samples.Add(new MissingHpSample(observedAtMilliseconds, missingHp, missingHpBefore));
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

    // MissingHpBefore: HP·ms missing from the entity's first sample up to this one, so a query needs no walk over them all.
    private readonly record struct MissingHpSample(long ObservedAtMilliseconds, long MissingHp, long MissingHpBefore);
}

public readonly record struct EntityVitalState(
    int EntityId,
    long CurrentHp,
    long? MaxHp,
    long ObservedAtMilliseconds,
    long ObservationOrdinal,
    long Revision);
