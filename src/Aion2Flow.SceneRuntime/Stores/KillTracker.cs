using System.Runtime.InteropServices;

namespace Cloris.Aion2Flow.SceneRuntime.Stores;

// Who killed each NPC that died in sight: the player who took most of its HP off (the applier says whom each source's hits
// count for: a summon's for its owner, an NPC's for no one). A hit counts only up to the HP the NPC had left, so the killing
// blow's overkill doesn't, nor anything after its 0 HP update; the HP left comes from its HP updates, which come with every
// change. Hits and updates are walked in journal order once a flush is complete, as some hits are applied at the end of
// their batch, after its HP update (EffectiveHealingTracker does the same). In a minute of Abyss farming (19 Nov 2026) this
// gave each of the 78 NPCs that died in sight the killer a walk of the raw packets gave; the game's loot went by damage
// counted this way too (72 of 72).
// ponytail: a hit reaching the journal more than a flush late is capped against the HP after its batch; an NPC first seen
// hurt counts only what was dealt in sight; on a tie the first to have hit wins.
internal sealed class KillTracker
{
    // Hits no HP update followed within this long (the NPC went out of sight) are walked without one.
    private const long SettleMilliseconds = 1_000;

    private readonly Dictionary<int, NpcState> _npcs = [];
    private readonly HashSet<int> _active = [];
    private readonly List<int> _drained = [];
    private readonly List<Kill> _kills = [];
    private readonly Dictionary<int, long> _byKiller = [];
    private long _latestObservedAtMilliseconds;

    public void ObserveHit(int npcId, int sourceId, long amount, long ordinal, long observedAtMilliseconds) =>
        Observe(npcId, new Entry(ordinal, observedAtMilliseconds, sourceId, amount, IsHp: false));

    public void ObserveHp(int npcId, long hp, long ordinal, long observedAtMilliseconds) =>
        Observe(npcId, new Entry(ordinal, observedAtMilliseconds, 0, hp, IsHp: true));

    // The NPCs that died since the last call, each with its killer: whom killerOf names for a source (0: no one, an NPC whose
    // hits still take HP off). One no player hit isn't listed. Valid until the next call.
    public IReadOnlyList<Kill> Resolve(Func<int, int> killerOf)
    {
        _kills.Clear();
        var settleBefore = _latestObservedAtMilliseconds - SettleMilliseconds;
        foreach (var npcId in _active)
        {
            var npc = _npcs[npcId];
            var entries = npc.Entries;
            entries.Sort(static (left, right) => left.Ordinal.CompareTo(right.Ordinal));
            var walked = entries.FindLastIndex(static entry => entry.IsHp) + 1;
            while (walked < entries.Count && entries[walked].ObservedAtMilliseconds < settleBefore)
                walked++;

            for (var i = 0; i < walked; i++)
                Walk(npcId, npc, entries[i], killerOf);

            entries.RemoveRange(0, walked);
            if (entries.Count == 0)
                _drained.Add(npcId);
        }

        foreach (var npcId in _drained)
            _active.Remove(npcId);
        _drained.Clear();
        return _kills;
    }

    private void Walk(int npcId, NpcState npc, Entry entry, Func<int, int> killerOf)
    {
        if (!entry.IsHp)
        {
            if (npc.Dead)
                return;

            var counted = npc.Left is long left ? Math.Min(entry.Amount, left) : entry.Amount;
            npc.Left -= counted;
            if (counted > 0)
                CollectionsMarshal.GetValueRefOrAddDefault(npc.Damage ??= [], entry.SourceId, out _) += counted;
            return;
        }

        if (entry.Amount == 0)
        {
            if (!npc.Dead && npc.Damage is { Count: > 0 } damage && TopDealer(damage, killerOf) is > 0 and var killerId)
                _kills.Add(new Kill(npcId, killerId, entry.ObservedAtMilliseconds));
            npc.Damage?.Clear();
        }

        npc.Dead = entry.Amount == 0;
        npc.Left = entry.Amount;
    }

    // The killer with the most damage, their summons' with theirs; 0 when only NPCs hit.
    private int TopDealer(Dictionary<int, long> damage, Func<int, int> killerOf)
    {
        _byKiller.Clear();
        foreach (var (sourceId, amount) in damage)
        {
            if (killerOf(sourceId) is > 0 and var killerId)
                CollectionsMarshal.GetValueRefOrAddDefault(_byKiller, killerId, out _) += amount;
        }

        var (top, most) = (0, 0L);
        foreach (var (killerId, amount) in _byKiller)
        {
            if (amount > most)
                (top, most) = (killerId, amount);
        }

        return top;
    }

    private void Observe(int npcId, Entry entry)
    {
        if (!_npcs.TryGetValue(npcId, out var npc))
            _npcs[npcId] = npc = new NpcState();

        npc.Entries.Add(entry);
        _active.Add(npcId);
        _latestObservedAtMilliseconds = Math.Max(_latestObservedAtMilliseconds, entry.ObservedAtMilliseconds);
    }

    public readonly record struct Kill(int NpcId, int KillerId, long ObservedAtMilliseconds);

    // Amount: the hit's damage, or the HP an update reads.
    private readonly record struct Entry(long Ordinal, long ObservedAtMilliseconds, int SourceId, long Amount, bool IsHp);

    private sealed class NpcState
    {
        public long? Left;
        public bool Dead;
        public Dictionary<int, long>? Damage;
        public List<Entry> Entries { get; } = [];
    }
}
