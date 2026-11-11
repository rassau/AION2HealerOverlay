using System.Runtime.InteropServices;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Projection;

namespace Cloris.Aion2Flow.SceneRuntime.Stores;

// Splits the scene into boss fights, one per pull: from the first hit on a boss or the boss's first aimed cast until the
// boss dies or heals back to full (a wipe or leash). A fight keeps its own CombatStore, fed the scene's events while it
// runs, so its snapshot has every column of the scene's, counted over the fight. Its damage is what hit its boss.
internal sealed class BossFightTracker(EntityStore entities, EntityVitalStore entityVitals, SceneBoundaryStore boundary)
{
    // The killing blow can reach the journal after the boss's 0 HP update (~170 ms seen); a damage-over-time tick can
    // land just after a reset. Neither starts a fight of its own.
    private const long LateHitMilliseconds = 1_000;
    // A pull starts the death count over unless another fight saw action this recently: a boss-kind add pulled mid-fight
    // keeps it, and those adds, some of which never end, stop counting once they've been quiet this long.
    private const long QuietMilliseconds = 60_000;

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

    public void ObserveCombat(int sourceId, int targetId, in CombatWireObservation observation, in CombatContribution contribution, long observedAtMilliseconds, long sourceObservationOrdinal, RawPacketReference raw, int eventIndex)
    {
        var isDamage = contribution.Metric == CombatMetricKind.Damage;
        // A boss beating on another boss-sized NPC isn't the group pulling it (seen: a boss hitting a 28 M HP NPC of its
        // own fight, which then stood in the history as a failed attempt).
        // ponytail: only a boss's hits are left out; should mobs beating on a boss-sized NPC ever show up as fights, ask
        // for a player (or a player's summon) as the attacker.
        if (isDamage && !IsBoss(sourceId))
            Engage(targetId, observedAtMilliseconds);
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

            if (fight.Outcome == BossFightOutcome.Ongoing && (sourceId == fight.BossId || targetId == fight.BossId))
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
            if (fight.Outcome == BossFightOutcome.Ongoing && untilMilliseconds > from)
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
        if (vital.CurrentHp == 0 && hpBefore is > 0 and < long.MaxValue && !IsBoss(vital.EntityId) && AnyFightActive(vital.ObservedAtMilliseconds))
            CollectionsMarshal.GetValueRefOrAddDefault(_deaths, vital.EntityId, out _)++;

        if (!IsBoss(vital.EntityId) || FindLatest(vital.EntityId) is not { Outcome: BossFightOutcome.Ongoing } fight)
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
    public SceneBossFight[] CreateSnapshots()
    {
        var result = new SceneBossFight[_fights.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var fight = _fights[i];
            if (fight.Snapshot is null || fight.SnapshotVersion != fight.Version)
            {
                fight.Adapter ??= new SceneCombatSnapshotAdapter(entities, entityVitals, fight.Combat, _noMechanics, _noResources, boundary);
                fight.Snapshot = new SceneBossFight(fight.BossId, fight.BossNumber, fight.Attempt, fight.Outcome, fight.BossHp, fight.BossMaxHp, CountBossDamageOnly(fight, fight.Adapter), fight.StaggerCount);
                fight.SnapshotVersion = fight.Version;
            }

            result[i] = fight.Snapshot;
        }

        return result;
    }

    // Hits on adds count toward the fight's time, not its damage.
    private static SceneCombatSnapshot CountBossDamageOnly(Fight fight, SceneCombatSnapshotAdapter adapter)
    {
        var snapshot = adapter.CreateSnapshot();
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
                DamagePerSecond = snapshot.EncounterTime > 0 ? (double)damage / snapshot.EncounterTime * 1000 : 0
            };
            combatants[i] = combatants[i] with { Metrics = metrics };
        }

        return new SceneCombatSnapshot(snapshot.EncounterId, snapshot.Kind, snapshot.ReadModelRevision, snapshot.SceneTransitionRevision, snapshot.MapId, snapshot.MapInstanceId, snapshot.EncounterStartTime, snapshot.EncounterEndTime, snapshot.EncounterTime, combatants, snapshot.TargetObservation, snapshot.Encounter, [], []);
    }

    // A pull starts with the first hit or aimed cast after the last pull ended. A killed boss stays dead: hits that land
    // after its 0 HP go to the fight that killed it.
    private void Engage(int bossId, long observedAtMilliseconds)
    {
        if (!IsBoss(bossId))
            return;

        var latest = FindLatest(bossId);
        if (latest is { Outcome: BossFightOutcome.Ongoing or BossFightOutcome.Killed } ||
            latest is { Outcome: BossFightOutcome.Reset } && observedAtMilliseconds - latest.EndedAt <= LateHitMilliseconds)
        {
            return;
        }

        var number = _bossOrder.IndexOf(bossId) + 1;
        if (number == 0)
        {
            _bossOrder.Add(bossId);
            number = _bossOrder.Count;
        }

        if (!AnyFightActive(observedAtMilliseconds))
            _deaths.Clear();
        entityVitals.TryGet(bossId, out var vital);
        _fights.Add(new Fight(bossId, number, (latest?.Attempt ?? 0) + 1, observedAtMilliseconds)
        {
            BossHp = vital.CurrentHp,
            BossMaxHp = vital.MaxHp ?? 0
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

    private bool IsBoss(int entityId) =>
        entityId > 0 && entities.TryGet(entityId, out var entity) && BossModeFocusTargets.IsFocusTarget(entity.Kind);

    private sealed class Fight(int bossId, int bossNumber, int attempt, long startedAt)
    {
        public int BossId { get; } = bossId;
        public int BossNumber { get; } = bossNumber;
        public int Attempt { get; } = attempt;
        public long StartedAt { get; } = startedAt;
        // The last hit on the boss, by it, or its aimed cast.
        public long LastActiveAt { get; set; } = startedAt;
        public BossFightOutcome Outcome { get; set; }
        public long EndedAt { get; set; }
        public long BossHp { get; set; }
        public long BossMaxHp { get; set; }
        public int StaggerCount { get; set; }
        public CombatStore Combat { get; } = new();
        // Scene event index -> this fight's, for heals whose real amount is credited after the HP update.
        public Dictionary<int, int> HealingEvents { get; } = [];
        public long Version { get; set; }
        public long SnapshotVersion { get; set; } = -1;
        public SceneCombatSnapshotAdapter? Adapter { get; set; }
        public SceneBossFight? Snapshot { get; set; }
    }
}
