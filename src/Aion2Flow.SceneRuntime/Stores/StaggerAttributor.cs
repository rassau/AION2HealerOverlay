using System.Runtime.InteropServices;

namespace Cloris.Aion2Flow.SceneRuntime.Stores;

// Who wore down a stagger gauge. Hits don't say how much stagger they dealt, but the gauge drops right after them, in
// the same server tick. A drop goes to the hits on that target in the 30 ms before it: all of it when one player made
// them. Otherwise each skill weighs what it took off when it was the only hit before a drop (a skill's stagger is
// fixed; some sometimes deal 1.5x), and what known skills leave unexplained is split evenly between the players whose
// skills aren't known yet. A player's share is split between their hits the same way.
internal sealed class StaggerAttributor
{
    private const long WindowMilliseconds = 30;

    private readonly Dictionary<int, List<Hit>> _recentHits = [];
    private readonly Dictionary<int, Dictionary<long, int>> _dropsBySkill = [];
    private readonly Dictionary<int, double> _known = [];
    private readonly HashSet<int> _unknown = [];
    private readonly Dictionary<int, double> _credits = [];
    private readonly List<StaggerCredit> _hitCredits = [];

    public void ObserveHit(int targetId, int sourceId, int skillCode, int eventIndex, long observedAtMilliseconds)
    {
        if (!_recentHits.TryGetValue(targetId, out var hits))
            _recentHits[targetId] = hits = [];

        Prune(hits, observedAtMilliseconds);
        hits.Add(new Hit(sourceId, skillCode, eventIndex, observedAtMilliseconds));
    }

    // Points per hit for a drop of the target's gauge; empty when no hit came just before it. Valid until the next call.
    public IReadOnlyList<StaggerCredit> Attribute(int targetId, long drop, long observedAtMilliseconds)
    {
        _hitCredits.Clear();
        if (!_recentHits.TryGetValue(targetId, out var hits))
            return _hitCredits;

        Prune(hits, observedAtMilliseconds);
        if (hits.Count == 0)
            return _hitCredits;

        if (hits.Count == 1)
            Learn(hits[0].SkillCode, drop);

        CreditSources(hits, drop);
        foreach (var (sourceId, points) in _credits)
            CreditHits(hits, sourceId, points);
        return _hitCredits;
    }

    private void CreditSources(List<Hit> hits, long drop)
    {
        _credits.Clear();
        var onePlayer = true;
        foreach (var hit in hits)
            onePlayer &= hit.SourceId == hits[0].SourceId;
        if (onePlayer)
        {
            _credits[hits[0].SourceId] = drop;
            return;
        }

        _known.Clear();
        _unknown.Clear();
        var knownTotal = 0d;
        foreach (var hit in hits)
        {
            if (TryGetSkillStagger(hit.SkillCode, out var value))
            {
                CollectionsMarshal.GetValueRefOrAddDefault(_known, hit.SourceId, out _) += value;
                knownTotal += value;
            }
            else
            {
                _unknown.Add(hit.SourceId);
            }
        }

        if (_unknown.Count == 0 || knownTotal >= drop)
        {
            foreach (var (sourceId, value) in _known)
                _credits[sourceId] = drop * value / knownTotal;
            return;
        }

        foreach (var (sourceId, value) in _known)
            _credits[sourceId] = value;
        foreach (var sourceId in _unknown)
            CollectionsMarshal.GetValueRefOrAddDefault(_credits, sourceId, out _) += (drop - knownTotal) / _unknown.Count;
    }

    // Hits of known skills get their value (scaled down when they explain more than the player's points); the rest is
    // split evenly between the player's hits of skills not known yet.
    private void CreditHits(List<Hit> hits, int sourceId, double points)
    {
        var knownTotal = 0d;
        var unknownCount = 0;
        foreach (var hit in hits)
        {
            if (hit.SourceId != sourceId)
                continue;

            if (TryGetSkillStagger(hit.SkillCode, out var value))
                knownTotal += value;
            else
                unknownCount++;
        }

        var scale = unknownCount == 0 || knownTotal > points ? points / knownTotal : 1;
        var rest = unknownCount > 0 ? Math.Max(0, points - knownTotal) / unknownCount : 0;
        foreach (var hit in hits)
        {
            if (hit.SourceId != sourceId)
                continue;

            var credit = TryGetSkillStagger(hit.SkillCode, out var value) ? value * scale : rest;
            if (credit > 0)
                _hitCredits.Add(new StaggerCredit(hit.EventIndex, sourceId, credit));
        }
    }

    private void Learn(int skillCode, long drop)
    {
        if (!_dropsBySkill.TryGetValue(skillCode, out var drops))
            _dropsBySkill[skillCode] = drops = [];

        CollectionsMarshal.GetValueRefOrAddDefault(drops, drop, out _)++;
    }

    // The drop a skill caused most often when it was the only hit before one; the smaller on a tie (the 1.5x is rarer).
    private bool TryGetSkillStagger(int skillCode, out long value)
    {
        value = 0;
        if (!_dropsBySkill.TryGetValue(skillCode, out var drops))
            return false;

        var best = 0;
        foreach (var (drop, count) in drops)
        {
            if (count > best || count == best && drop < value)
            {
                best = count;
                value = drop;
            }
        }

        return best > 0;
    }

    private static void Prune(List<Hit> hits, long now)
    {
        var stale = 0;
        while (stale < hits.Count && now - hits[stale].ObservedAtMilliseconds > WindowMilliseconds)
            stale++;

        hits.RemoveRange(0, stale);
    }

    private readonly record struct Hit(int SourceId, int SkillCode, int EventIndex, long ObservedAtMilliseconds);
}

// Stagger points one hit (an event of the scene's CombatStore) wore down.
internal readonly record struct StaggerCredit(int EventIndex, int SourceId, double Points);
