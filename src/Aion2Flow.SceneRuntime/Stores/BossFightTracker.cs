using System.Runtime.InteropServices;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Projection;

namespace Cloris.Aion2Flow.SceneRuntime.Stores;

// How a training dummy pull runs: until the user pauses for the cut-off, or at most for a fixed length (0: none).
public readonly record struct TrainingDummyRules(long LengthMilliseconds, long CutoffMilliseconds)
{
    public static TrainingDummyRules Default { get; } = new(0, 5_000);
}

// Splits the scene into boss fights, one per pull: from the first hit on a boss or the boss's first aimed cast until the
// boss dies or heals back to full (a wipe or leash), or is left alone for five minutes. A fight keeps its own CombatStore,
// fed the scene's events while it runs, so its snapshot has every column of the scene's, counted over the fight. Its
// damage is what hit its boss.
// A training dummy can't die (it stops at 1 HP and is full again a moment later), so its pulls go by the user's own hits
// (their summons' too): from the first until they pause for the cut-off, or until the pull's length is up. Other players'
// hits neither start nor extend one.
internal sealed class BossFightTracker(EntityStore entities, EntityVitalStore entityVitals, SceneBoundaryStore boundary, RuntimeMetadataRegistry metadataRegistry)
{
    // The killing blow can reach the journal after the boss's 0 HP update (~170 ms seen); a damage-over-time tick can
    // land just after a reset. Neither starts a fight of its own.
    private const long LateHitMilliseconds = 1_000;
    // A pull starts the death count over unless another fight saw action this recently: a boss-kind add pulled mid-fight
    // keeps it, and those adds, some of which never end, stop counting once they've been quiet this long.
    private const long QuietMilliseconds = 60_000;
    // A boss without a hit or an aimed cast this long was left (the group walked on, out of sight in the open world): it
    // never dies or heals back to full where the overlay sees it. Its pull ends as a wipe at its last action.
    // ponytail: fixed 5 min, above the longest quiet seen in a pull that went on (228 s, a boss pulled along with another
    // waiting its turn); a boss quiet longer within one fight would split it into a failed attempt and the next. End on
    // the boss leaving sight instead (0x4736, not journaled) if that ever shows.
    private const long AbandonedMilliseconds = 300_000;

    private readonly List<Fight> _fights = [];
    private readonly List<int> _bossOrder = [];
    private readonly MechanicStore _noMechanics = new();
    // Who died how often (HP down to 0) while a fight was on, since the pull that started the current run of fights.
    private readonly Dictionary<int, int> _deaths = [];
    private readonly ResourceStore _noResources = new();
    // Whom the boss frame (0x2092) names, since when (credited up to there); ByCast: filled in from aimed casts.
    private int _bossTarget;
    private long _bossTargetSince;
    private bool _bossTargetByCast;

    public TrainingDummyRules DummyRules { get; set; } = TrainingDummyRules.Default;

    public void ObserveCombat(int sourceId, int targetId, in CombatWireObservation observation, in CombatContribution contribution, long observedAtMilliseconds, long sourceObservationOrdinal, RawPacketReference raw, int eventIndex)
    {
        var isDamage = contribution.Metric == CombatMetricKind.Damage;
        // Dummy pulls that are over by now end before this event, so it can't count in them (others' hits, the user's heals);
        // so do pulls of bosses left by now, and a hit on such a boss pulls it anew.
        EndDummyPulls(observedAtMilliseconds);
        EndAbandonedPulls(observedAtMilliseconds);
        // A boss beating on another boss-sized NPC isn't the group pulling it (seen: a boss hitting a 28 M HP NPC of its
        // own fight, which then stood in the history as a failed attempt).
        // ponytail: only a boss's hits are left out; should mobs beating on a boss-sized NPC ever show up as fights, ask
        // for a player (or a player's summon) as the attacker.
        if (isDamage && !IsBoss(sourceId))
        {
            if (!IsDummy(targetId))
                Engage(targetId, observedAtMilliseconds);
            else if (IsMine(sourceId))
                HitDummy(targetId, observedAtMilliseconds);
        }

        CreditBossTarget(observedAtMilliseconds);

        // By time, not arrival: some values are applied after later packets (flushed sidecars), and the killing blow can
        // trail the 0 HP update. Hits on adds go in too; the snapshot narrows damage to the boss, once it is certain who
        // is a player (names can arrive after the pull).
        foreach (var fight in _fights)
        {
            var hitsBoss = isDamage && targetId == fight.BossId;
            if (observedAtMilliseconds < fight.StartedAt ||
                fight.Outcome != BossFightOutcome.Ongoing &&
                observedAtMilliseconds - fight.EndedAt > (hitsBoss && fight.Outcome == BossFightOutcome.Killed ? LateHitMilliseconds : 0))
            {
                continue;
            }

            // A dummy pull's activity is the user's hits alone (HitDummy).
            if (fight.Outcome == BossFightOutcome.Ongoing && !fight.IsDummy && (sourceId == fight.BossId || targetId == fight.BossId))
                fight.LastActiveAt = Math.Max(fight.LastActiveAt, observedAtMilliseconds);
            var fightEventIndex = fight.Combat.ApplyCombat(sourceId, targetId, in observation, in contribution, observedAtMilliseconds, sourceObservationOrdinal, raw);
            if (contribution.Metric == CombatMetricKind.Healing)
                fight.HealingEvents[eventIndex] = fightEventIndex;
            fight.Version++;
        }
    }

    public void ObserveBossTarget(int targetId, long observedAtMilliseconds) => SetBossTarget(targetId, observedAtMilliseconds, byCast: false);

    private void SetBossTarget(int targetId, long observedAtMilliseconds, bool byCast)
    {
        CreditBossTarget(observedAtMilliseconds);
        _bossTarget = targetId;
        _bossTargetSince = observedAtMilliseconds;
        _bossTargetByCast = byCast;
    }

    // The time on the boss frame's target counts as their aggro in every pull going on: the frame doesn't say which
    // boss it means, and the one being fought must get it (boss-kind adds that never end are pulls too).
    // ponytail: a second boss pulled alongside shows the first one's targets; tell them apart by which battle-on the
    // frame followed if that ever matters.
    private void CreditBossTarget(long untilMilliseconds)
    {
        if (_bossTarget <= 0 || untilMilliseconds <= _bossTargetSince)
            return;

        foreach (var fight in _fights)
        {
            var from = Math.Max(_bossTargetSince, fight.StartedAt);
            if (fight.Outcome == BossFightOutcome.Ongoing && !fight.IsDummy && untilMilliseconds > from)
            {
                fight.Combat.AddAggroTime(_bossTarget, untilMilliseconds - from);
                fight.Version++;
            }
        }

        _bossTargetSince = untilMilliseconds;
    }

    // The time up to the end counts. A target filled in from casts ends with the fight; the frame clears its own.
    private void CreditBossTargetAtFightEnd(long endedAtMilliseconds)
    {
        CreditBossTarget(endedAtMilliseconds);
        if (_bossTargetByCast)
            _bossTarget = 0;
    }

    // Aimed casts of the boss itself: its aggro, as the scene counts it for every enemy.
    public void ObserveAggroCast(int sourceId, int targetId, long observedAtMilliseconds)
    {
        EndAbandonedPulls(observedAtMilliseconds);
        Engage(sourceId, observedAtMilliseconds);
        if (FindLatest(sourceId) is { Outcome: BossFightOutcome.Ongoing } fight)
        {
            fight.LastActiveAt = Math.Max(fight.LastActiveAt, observedAtMilliseconds);
            fight.Combat.CountAggroCast(sourceId, targetId);
            // The frame went blank while this boss still fights (another boss leaving the fight blanks it): its aimed
            // casts stand in until the frame names someone again. Open world bosses never fill the frame: casts there.
            if ((_bossTarget == 0 || _bossTargetByCast) && fight.Combat.AggroMilliseconds.Count > 0)
                SetBossTarget(targetId, observedAtMilliseconds, byCast: true);
            fight.Version++;
        }
    }

    public void ObserveStagger(int bossId, int sourceId, double points, long observedAtMilliseconds)
    {
        if (FindLatest(bossId) is { Outcome: BossFightOutcome.Ongoing } fight && observedAtMilliseconds >= fight.StartedAt)
        {
            fight.Combat.AddStagger(sourceId, points);
            fight.Version++;
        }
    }

    public void ObserveStaggerBreak(int bossId)
    {
        if (FindLatest(bossId) is { Outcome: BossFightOutcome.Ongoing } fight)
        {
            fight.StaggerCount++;
            fight.Version++;
        }
    }

    // hpBefore: long.MaxValue while the entity's HP wasn't known.
    public void ObserveVital(in EntityVitalState vital, long hpBefore)
    {
        // HP comes in while nobody fights too (a party's ~500 ms beat), so it ends the pulls of bosses left by now; a left
        // boss seen again (back at full HP) then finds its pull over at its last action, not ended at this late update.
        EndAbandonedPulls(vital.ObservedAtMilliseconds);
        if (vital.CurrentHp == 0 && hpBefore is > 0 and < long.MaxValue && !IsBoss(vital.EntityId) && AnyFightActive(vital.ObservedAtMilliseconds))
            CollectionsMarshal.GetValueRefOrAddDefault(_deaths, vital.EntityId, out _)++;

        // A dummy's HP means nothing: it drops to 1 and is full again a moment later.
        if (!IsBoss(vital.EntityId) || IsDummy(vital.EntityId) || FindLatest(vital.EntityId) is not { Outcome: BossFightOutcome.Ongoing } fight)
            return;

        fight.Version++;
        fight.BossMaxHp = Math.Max(fight.BossMaxHp, vital.MaxHp ?? 0);
        if (vital.CurrentHp > hpBefore && vital.CurrentHp >= vital.MaxHp)
        {
            // BossHp keeps the HP the group left it at.
            CreditBossTargetAtFightEnd(vital.ObservedAtMilliseconds);
            fight.Outcome = BossFightOutcome.Reset;
            fight.EndedAt = vital.ObservedAtMilliseconds;
            return;
        }

        fight.BossHp = vital.CurrentHp;
        if (vital.CurrentHp == 0)
        {
            CreditBossTargetAtFightEnd(vital.ObservedAtMilliseconds);
            fight.Outcome = BossFightOutcome.Killed;
            fight.EndedAt = vital.ObservedAtMilliseconds;
        }
    }

    // How often they died in the current boss fight: shown from its pull until the next pull starts the count over.
    public int DeathsOf(int entityId) => _deaths.GetValueOrDefault(entityId);

    private bool AnyFightActive(long atMilliseconds)
    {
        foreach (var fight in _fights)
        {
            if (fight.Outcome == BossFightOutcome.Ongoing && atMilliseconds - fight.LastActiveAt <= QuietMilliseconds)
                return true;
        }

        return false;
    }

    public void CreditEffectiveHealing(int eventIndex, long amount)
    {
        foreach (var fight in _fights)
        {
            if (fight.HealingEvents.TryGetValue(eventIndex, out var fightEventIndex))
            {
                fight.Combat.CreditEffectiveHealing(fightEventIndex, amount);
                fight.Version++;
            }
        }
    }

    // ponytail: a fight is rebuilt only when its own events change, so a name or class learned after its last event
    // doesn't reach it; rebuild on entity identity changes too if that ever shows.
    // now: the scene's clock, which ends the dummy pulls that are over by then. Not the pulls of left bosses: this is the
    // wall clock, and set ahead of the capture clock the events go by (time sync) it would end every pull as it starts.
    public SceneBossFight[] CreateSnapshots(long nowMilliseconds)
    {
        EndDummyPulls(nowMilliseconds);
        var result = new SceneBossFight[_fights.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var fight = _fights[i];
            if (fight.Snapshot is null || fight.SnapshotVersion != fight.Version)
            {
                fight.Adapter ??= new SceneCombatSnapshotAdapter(entities, entityVitals, fight.Combat, _noMechanics, _noResources, boundary);
                fight.Snapshot = new SceneBossFight(fight.BossId, fight.BossNumber, fight.Attempt, fight.Outcome, fight.BossHp, fight.BossMaxHp, CountBossDamageOnly(fight, fight.Adapter), fight.StaggerCount,
                    fight.IsDummy, fight.LengthMilliseconds);
                fight.SnapshotVersion = fight.Version;
            }

            result[i] = fight.Snapshot;
        }

        return result;
    }

    // Hits on adds count toward the fight's time, not its damage. A dummy pull runs from the user's first hit to their
    // last (or to its length): the pause before it was seen to be over isn't part of it.
    private static SceneCombatSnapshot CountBossDamageOnly(Fight fight, SceneCombatSnapshotAdapter adapter)
    {
        var snapshot = adapter.CreateSnapshot();
        var (start, end, time) = (snapshot.EncounterStartTime, snapshot.EncounterEndTime, snapshot.EncounterTime);
        if (fight.IsDummy)
        {
            (start, end) = (fight.StartedAt, fight.Outcome == BossFightOutcome.Ongoing ? Math.Min(fight.LastActiveAt, fight.LengthEnd) : fight.EndedAt);
            time = Math.Max(0, end - start);
        }

        var bossDamage = new Dictionary<int, long>();
        foreach (var pair in fight.Combat.Pairs.Values)
        {
            if (pair.TargetId == fight.BossId && pair.TotalDamage > 0)
                CollectionsMarshal.GetValueRefOrAddDefault(bossDamage, adapter.ResolveDetailCombatantId(pair.SourceId), out _) += pair.TotalDamage;
        }

        var combatants = snapshot.Combatants.AsSpan().ToArray();
        for (var i = 0; i < combatants.Length; i++)
        {
            var damage = bossDamage.GetValueOrDefault(combatants[i].Id);
            var metrics = combatants[i].Metrics with
            {
                DamageAmount = damage,
                DamagePerSecond = PerSecond(damage, time),
                HealingPerSecond = fight.IsDummy ? PerSecond(combatants[i].Metrics.HealingAmount, time) : combatants[i].Metrics.HealingPerSecond,
                EffectiveHealingPerSecond = fight.IsDummy ? PerSecond(combatants[i].Metrics.EffectiveHealingAmount, time) : combatants[i].Metrics.EffectiveHealingPerSecond,
            };
            combatants[i] = combatants[i] with { Metrics = metrics };
        }

        return new SceneCombatSnapshot(snapshot.EncounterId, snapshot.Kind, snapshot.ReadModelRevision, snapshot.SceneTransitionRevision, snapshot.MapId, snapshot.MapInstanceId, start, end, time, combatants, snapshot.TargetObservation, snapshot.Encounter, [], []);
    }

    private static double PerSecond(long amount, long milliseconds) => milliseconds > 0 ? (double)amount / milliseconds * 1000 : 0;

    // A pull starts with the first hit or aimed cast after the last pull ended. A killed boss stays dead: hits that land
    // after its 0 HP go to the fight that killed it.
    private void Engage(int bossId, long observedAtMilliseconds)
    {
        if (!IsBoss(bossId) || IsDummy(bossId))
            return;

        var latest = FindLatest(bossId);
        if (latest is { Outcome: BossFightOutcome.Ongoing or BossFightOutcome.Killed } ||
            latest is { Outcome: BossFightOutcome.Reset } && observedAtMilliseconds - latest.EndedAt <= LateHitMilliseconds)
        {
            return;
        }

        entityVitals.TryGet(bossId, out var vital);
        Start(bossId, latest, observedAtMilliseconds, vital.CurrentHp, vital.MaxHp ?? 0);
    }

    // The user's hit on a training dummy: it goes on their pull of it, or starts one. Hits after a pull's length was up
    // start no new one until they paused for the cut-off.
    private void HitDummy(int dummyId, long observedAtMilliseconds)
    {
        var latest = FindLatest(dummyId);
        var cutoff = DummyRules.CutoffMilliseconds;
        if (latest is { Outcome: BossFightOutcome.Ongoing } && !EndDummyPull(latest, observedAtMilliseconds) ||
            latest is { Outcome: BossFightOutcome.TimeUp } && observedAtMilliseconds - latest.LastActiveAt <= cutoff)
        {
            latest.LastActiveAt = Math.Max(latest.LastActiveAt, observedAtMilliseconds);
            return;
        }

        // A hit that reached the journal late (a flushed sidecar, seen 20 s late in a replay: a summon's hits): one from
        // inside a pull that ended belongs to it (the events loop adds it), one from within the cut-off after its end takes
        // it up again.
        if (latest is { Outcome: BossFightOutcome.Stopped } && observedAtMilliseconds - latest.LastActiveAt <= cutoff)
        {
            if (observedAtMilliseconds > latest.EndedAt)
                (latest.Outcome, latest.LastActiveAt) = (BossFightOutcome.Ongoing, observedAtMilliseconds);
            return;
        }

        Start(dummyId, latest, observedAtMilliseconds, 0, 0, isDummy: true);
    }

    // The scene's clock (or an event's time) says the dummy pulls whose cut-off or length has passed are over; it waits a
    // moment longer, as hits can reach the journal late.
    private void EndDummyPulls(long nowMilliseconds)
    {
        foreach (var fight in _fights)
        {
            if (fight.IsDummy && fight.Outcome == BossFightOutcome.Ongoing)
                EndDummyPull(fight, nowMilliseconds - LateHitMilliseconds);
        }
    }

    // A dummy pull is over at `at` when its length is up while the user was still at it (no pause of the cut-off before),
    // or when they paused for the cut-off: it ends with their last hit then.
    private bool EndDummyPull(Fight fight, long at)
    {
        var cutoff = DummyRules.CutoffMilliseconds;
        if (at >= fight.LengthEnd && fight.LastActiveAt >= fight.LengthEnd - cutoff)
            (fight.Outcome, fight.EndedAt) = (BossFightOutcome.TimeUp, fight.LengthEnd);
        else if (at - fight.LastActiveAt > cutoff)
            (fight.Outcome, fight.EndedAt) = (BossFightOutcome.Stopped, fight.LastActiveAt);
        else
            return false;

        TrimAfterEnd(fight);
        fight.Version++;
        return true;
    }

    // The boss of a pull still going was left: the pull ends with its last action (see AbandonedMilliseconds).
    private void EndAbandonedPulls(long nowMilliseconds)
    {
        foreach (var fight in _fights)
        {
            if (fight.Outcome == BossFightOutcome.Ongoing && !fight.IsDummy && nowMilliseconds - fight.LastActiveAt > AbandonedMilliseconds)
            {
                (fight.Outcome, fight.EndedAt) = (BossFightOutcome.Reset, fight.LastActiveAt);
                TrimAfterEnd(fight);
                fight.Version++;
            }
        }
    }

    // What reached a pull after its end (a dummy's while it waited for the cut-off: others' hits on the dummy, the user's
    // heals; a left boss's while it waited to be seen as left) leaves it again: its store is made anew from the events up
    // to its end. The boss's aimed casts all come before; the boss frame's time can't be told apart by when, so it stays.
    private static void TrimAfterEnd(Fight fight)
    {
        var events = fight.Combat.Events;
        var kept = new int[events.Count];
        var trimmed = new CombatStore(trackChanges: false);
        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            kept[i] = -1;
            if (e.ObservedAtMilliseconds > fight.EndedAt)
                continue;

            kept[i] = trimmed.ApplyCombat(e.SourceId, e.TargetId, e.Observation, e.Contribution, e.ObservedAtMilliseconds, e.SourceObservationOrdinal, e.Raw);
            if (e.EffectiveHealing > 0)
                trimmed.CreditEffectiveHealing(kept[i], e.EffectiveHealing);
        }

        if (Array.IndexOf(kept, -1) < 0)
            return;

        foreach (var (sourceId, points) in fight.Combat.StaggerPoints)
            trimmed.AddStagger(sourceId, points);
        foreach (var ((sourceId, targetId), casts) in fight.Combat.AggroCasts)
        {
            for (var i = 0; i < casts; i++)
                trimmed.CountAggroCast(sourceId, targetId);
        }

        foreach (var (targetId, milliseconds) in fight.Combat.AggroMilliseconds)
            trimmed.AddAggroTime(targetId, milliseconds);
        foreach (var (eventIndex, fightEventIndex) in fight.HealingEvents.ToArray())
        {
            if (kept[fightEventIndex] < 0)
                fight.HealingEvents.Remove(eventIndex);
            else
                fight.HealingEvents[eventIndex] = kept[fightEventIndex];
        }

        fight.Combat = trimmed;
        fight.Adapter = null;
    }

    private void Start(int bossId, Fight? latest, long observedAtMilliseconds, long bossHp, long bossMaxHp, bool isDummy = false)
    {
        var number = _bossOrder.IndexOf(bossId) + 1;
        if (number == 0)
        {
            _bossOrder.Add(bossId);
            number = _bossOrder.Count;
        }

        if (!AnyFightActive(observedAtMilliseconds))
            _deaths.Clear();
        _fights.Add(new Fight(bossId, number, (latest?.Attempt ?? 0) + 1, observedAtMilliseconds)
        {
            BossHp = bossHp,
            BossMaxHp = bossMaxHp,
            IsDummy = isDummy,
            LengthMilliseconds = isDummy ? DummyRules.LengthMilliseconds : 0,
        });
    }

    private Fight? FindLatest(int bossId)
    {
        for (var i = _fights.Count - 1; i >= 0; i--)
        {
            if (_fights[i].BossId == bossId)
                return _fights[i];
        }

        return null;
    }

    // A boss or a training dummy (see BossModeFocusTargets).
    private bool IsBoss(int entityId) =>
        entityId > 0 && entities.TryGet(entityId, out var entity) && BossModeFocusTargets.IsFocusTarget(entity.Kind);

    private bool IsDummy(int entityId) =>
        entityId > 0 && entities.TryGet(entityId, out var entity) && entity.Kind == NpcKind.TrainingDummy;

    // The user or their summon.
    private bool IsMine(int sourceId)
    {
        var me = metadataRegistry.LocalPlayerEntityId;
        return me > 0 && (sourceId == me || entities.TryGet(sourceId, out var source) && source.OwnerEntityId == me);
    }

    private sealed class Fight(int bossId, int bossNumber, int attempt, long startedAt)
    {
        public int BossId { get; } = bossId;
        public int BossNumber { get; } = bossNumber;
        public int Attempt { get; } = attempt;
        public long StartedAt { get; } = startedAt;
        // The last hit on the boss, by it, or its aimed cast; on a dummy the user's last hit (also after its length).
        public long LastActiveAt { get; set; } = startedAt;
        public bool IsDummy { get; init; }
        // A dummy pull's length (0: until the user stops), and when it's up.
        public long LengthMilliseconds { get; init; }
        public long LengthEnd => LengthMilliseconds > 0 ? StartedAt + LengthMilliseconds : long.MaxValue;
        public BossFightOutcome Outcome { get; set; }
        public long EndedAt { get; set; }
        public long BossHp { get; set; }
        public long BossMaxHp { get; set; }
        public int StaggerCount { get; set; }
        public CombatStore Combat { get; set; } = new(trackChanges: false);
        // Scene event index -> this fight's, for heals whose real amount is credited after the HP update.
        public Dictionary<int, int> HealingEvents { get; } = [];
        public long Version { get; set; }
        public long SnapshotVersion { get; set; } = -1;
        public SceneCombatSnapshotAdapter? Adapter { get; set; }
        public SceneBossFight? Snapshot { get; set; }
    }
}
