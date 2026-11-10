using Cloris.Aion2Flow.SceneRuntime.Combat;

namespace Cloris.Aion2Flow.SceneRuntime.Stores;

// Credits each heal with the HP it actually restored. Player max HP never arrives on the wire, but the
// server sends a target's HP whenever it changes, so the HP gained between two updates (plus damage taken
// in between) is shared out over the heals in that interval, in journal order. A heal that is never
// followed by an update landed on a full target and restored nothing - even if that target's HP was never
// seen, since a target that never loses HP never gets an update.
// ponytail: a target the server sends no HP for at all reads as always full, and damage partly absorbed
// by a shield inside an interval can over-credit (capped at the heal amount); detect HP-less targets from
// hits without updates, and subtract absorbed amounts, if either ever matters.
internal sealed class EffectiveHealingTracker
{
    private const long UpdateWindowMilliseconds = 1_000;

    private readonly Dictionary<int, TargetState> _targets = [];
    private readonly HashSet<int> _active = [];
    private readonly List<int> _drained = [];
    private long _latestObservedAtMilliseconds;

    public void ObserveCombat(int targetId, in CombatContribution contribution, long ordinal, long observedAtMilliseconds, int eventIndex)
    {
        if (contribution.Metric == CombatMetricKind.Healing)
            Observe(targetId, new Entry(ordinal, observedAtMilliseconds, EntryKind.Heal, eventIndex, contribution.Amount));
        else if (contribution.Metric == CombatMetricKind.Damage)
            Observe(targetId, new Entry(ordinal, observedAtMilliseconds, EntryKind.Damage, eventIndex, contribution.Amount));
    }

    public void ObserveVital(int entityId, long currentHp, long ordinal, long observedAtMilliseconds) =>
        Observe(entityId, new Entry(ordinal, observedAtMilliseconds, EntryKind.Vital, -1, currentHp));

    public void Resolve(CombatStore combat, BossFightTracker? bossFights = null)
    {
        var expiredBefore = _latestObservedAtMilliseconds - UpdateWindowMilliseconds;
        foreach (var targetId in _active)
        {
            var state = _targets[targetId];
            var entries = state.Entries;
            entries.Sort(static (left, right) => left.Ordinal.CompareTo(right.Ordinal));

            var intervalStart = 0;
            var damageTaken = 0L;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Kind == EntryKind.Damage)
                {
                    damageTaken += entry.Amount;
                }
                else if (entry.Kind == EntryKind.Vital)
                {
                    var restored = state.Hp is long previousHp ? entry.Amount - previousHp + damageTaken : long.MaxValue;
                    Credit(combat, bossFights, entries, intervalStart, i, restored);
                    state.Hp = entry.Amount;
                    intervalStart = i + 1;
                    damageTaken = 0;
                }
            }

            // Heals with no HP update inside the window landed on a full target: settle them at zero.
            var settled = intervalStart;
            while (settled < entries.Count && entries[settled].ObservedAtMilliseconds < expiredBefore)
                settled++;

            entries.RemoveRange(0, settled);
            if (entries.Count == 0)
                _drained.Add(targetId);
        }

        foreach (var targetId in _drained)
            _active.Remove(targetId);
        _drained.Clear();
    }

    private void Observe(int targetId, in Entry entry)
    {
        if (targetId <= 0)
            return;

        if (!_targets.TryGetValue(targetId, out var state))
            _targets[targetId] = state = new TargetState();

        state.Entries.Add(entry);
        _active.Add(targetId);
        _latestObservedAtMilliseconds = Math.Max(_latestObservedAtMilliseconds, entry.ObservedAtMilliseconds);
    }

    private static void Credit(CombatStore combat, BossFightTracker? bossFights, List<Entry> entries, int start, int end, long restored)
    {
        for (var i = start; i < end; i++)
        {
            var entry = entries[i];
            if (entry.Kind != EntryKind.Heal)
                continue;

            var credited = Math.Clamp(restored, 0, entry.Amount);
            restored -= credited;
            if (credited > 0)
            {
                combat.CreditEffectiveHealing(entry.EventIndex, credited);
                bossFights?.CreditEffectiveHealing(entry.EventIndex, credited);
            }
        }
    }

    private enum EntryKind : byte
    {
        Heal,
        Damage,
        Vital
    }

    // EventIndex points at the combat event in the store; Amount is the heal or damage amount, or the observed HP for a vital.
    private readonly record struct Entry(long Ordinal, long ObservedAtMilliseconds, EntryKind Kind, int EventIndex, long Amount);

    private sealed class TargetState
    {
        public long? Hp { get; set; }

        public List<Entry> Entries { get; } = [];
    }
}
