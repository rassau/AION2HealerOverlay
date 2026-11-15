using System.Runtime.InteropServices;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.SceneRuntime.Projection;

public sealed class SceneCombatSnapshotAdapter(EntityStore entities, EntityVitalStore entityVitals, CombatStore combat, MechanicStore mechanics, ResourceStore resources, SceneBoundaryStore boundary, BossFocusStore? bossFocus = null, Guid encounterId = default)
{
    private const int SmallSetStackCapacity = 64;

    private readonly Dictionary<int, CombatantClassEvidence> _classEvidence = [];
    private readonly Dictionary<int, int> _resolvedCombatantIds = [];
    private readonly Dictionary<int, bool> _knownSummons = [];
    private readonly Dictionary<int, TargetInfo> _targetInfos = [];
    // Who each store event is from and to, beside the stores' 420-byte records: a combatant's detail picks its events by
    // these (WriteDetailEvents), so its scan over the scene doesn't load every record.
    private readonly EventHeaders<CombatEventRecord> _combatHeaders = new();
    private readonly EventHeaders<CombatMechanicEventRecord> _mechanicHeaders = new();
    private readonly EventHeaders<CombatResourceEventRecord> _resourceHeaders = new();
    private object? _classEvidenceJournal;
    private long _classEvidenceCombatRevision = -1;
    private long _classEvidenceSkillMapRevision = -1;
    private int _classEvidenceScannedEventCount;
    private long _resolveCacheIdentityRevision = -1;

    public long ReadModelRevision => SaturatingAdd(combat.Revision, mechanics.Revision, resources.Revision);

    public SceneCombatSnapshotAdapter(EntityStore entities, EntityVitalStore entityVitals, CombatStore combat, MechanicStore mechanics, ResourceStore resources, SceneBoundaryStore boundary)
        : this(entities, entityVitals, combat, mechanics, resources, boundary, null, default)
    {
    }

    public SceneCombatSnapshot CreateSnapshot(SceneKind kind = SceneKind.Standard) => CreateSnapshot(kind, new SceneCombatSnapshotBuilder());

    // A caller that makes snapshots over and over (playback) passes one builder: its dictionary is sized for every combatant
    // and lands on the large object heap once the scene is big. ToSnapshot copies, so nothing of the builder leaks out.
    internal SceneCombatSnapshot CreateSnapshot(SceneKind kind, SceneCombatSnapshotBuilder builder)
    {
        builder.Reset(encounterId, kind, combat.Combatants.Count + mechanics.Combatants.Count + (resources.Pairs.Count * 2), 0);
        BuildSnapshot(builder);
        return builder.ToSnapshot(ReadModelRevision);
    }

    internal void BuildSnapshot(SceneCombatSnapshotBuilder builder)
    {
        PrepareProjectionCaches();
        EnsureClassEvidence();
        builder.SetMap(boundary.CurrentMapId, boundary.CurrentMapInstanceId, boundary.SceneTransitionRevision);

        var targetDecision = BuildTargetDecisionFromCombatState();
        var now = ResolveSnapshotNow(targetDecision.LatestObservedAt);
        var trackingTargetId = ResolveTrackingTargetId(targetDecision.TrackingTargetId, now);
        var targetObservation = BuildTargetObservation(trackingTargetId);
        builder.SetTarget(targetObservation);

        var start = targetDecision.EncounterStartTime;
        var end = targetDecision.EncounterEndTime;
        if (start == end && start >= 0 && combat.EventSpan.Length > 0)
            ExpandSinglePointEncounterWindowFromRelevantRecovery(ref start, ref end);
        var encounterTime = end > start ? end - start : 0;
        var hasMaterializedActivity = combat.EventSpan.Length > 0 || mechanics.Events.Count > 0 || resources.Events.Count > 0;
        builder.SetEncounterWindow(start, end, encounterTime);

        if (hasMaterializedActivity)
            ApplyCombatState(builder);
        var totalDamage = 0L;
        foreach (var id in builder.CombatantIds)
        {
            ref var metrics = ref builder.GetExistingCombatant(id);
            metrics.CharacterClass = ResolveCharacterClass(id);
            metrics.IsVisiblePlayerCombatant = ShouldDisplayCombatant(id);
            if (metrics.CharacterClass is not null)
                totalDamage += metrics.DamageAmount;
        }

        foreach (var id in builder.CombatantIds)
        {
            ref var metrics = ref builder.GetExistingCombatant(id);
            metrics.DamagePerSecond = encounterTime > 0 ? (double)metrics.DamageAmount / encounterTime * 1000 : 0;
            metrics.HealingPerSecond = encounterTime > 0 ? (double)metrics.HealingAmount / encounterTime * 1000 : 0;
            metrics.EffectiveHealingPerSecond = encounterTime > 0 ? (double)metrics.EffectiveHealingAmount / encounterTime * 1000 : 0;
            metrics.UnhealedHealthSeconds = entityVitals.GetUnhealedHealthSeconds(id, start, end);
            metrics.DamageContribution = totalDamage > 0 ? (double)metrics.DamageAmount / totalDamage : 0;
        }

        builder.SetEncounter(EvaluateEncounter(trackingTargetId, hasMaterializedActivity, targetObservation, now));
    }

    public CombatDetailEventSet CreateDetailEvents(SceneCombatSnapshot snapshot, int combatantId)
        => CreateDetailEventsWithin(snapshot, combatantId, long.MinValue, long.MaxValue);

    // The detail events observed from one moment to another (both included), e.g. one boss fight: the time is tested
    // before an event is made.
    public CombatDetailEventSet CreateDetailEventsWithin(SceneCombatSnapshot snapshot, int combatantId, long fromMilliseconds, long toMilliseconds)
    {
        if (!CanProjectDetailCombatant(snapshot, combatantId))
            return CombatDetailEventSet.Empty;

        var writer = new ListDetailEventWriter();
        WriteDetailEvents(snapshot, combatantId, writer, CombatDetailProjectionScope.EncounterWindow, fromMilliseconds, toMilliseconds);
        return writer.ToEventSet();
    }

    // The players' metric detail events from one moment to another (both included), each list as
    // CreateDetailEventsWithin(snapshot, id, from, to).MetricEvents has it; all in one pass from the first storage segment
    // that reaches the moment, an event made once for its source's list and its target's. Players it can't project get none.
    public Dictionary<int, List<CombatMetricDetailEvent>> CreateMetricEventsWithin(SceneCombatSnapshot snapshot, IReadOnlyCollection<int> players, long fromMilliseconds, long toMilliseconds)
        => CreateMetricEventsWithin(snapshot, players, fromMilliseconds, toMilliseconds, since: null, out _, out _);

    // CreateMetricEventsWithin for a fight still going, worked out anew each second (the fight graphs): given where the last
    // read of the same fight stood (since), only the events from the first second of the fight that can have changed since
    // (firstSecond): the last read's last second, or the second of an event that came late or got a late credit. Nothing
    // read before can have changed otherwise: the store only grows, the whole fight lies inside the scene's window (an
    // event's fate doesn't depend on the snapshot then), and what decides an event's fate about its source and target (the
    // owner, summon, NPC) is checked for every one seen. Since is left unused when anything else differs, and a read
    // leaves a checkpoint only while the fight lies inside the scene's window.
    internal Dictionary<int, List<CombatMetricDetailEvent>> CreateMetricEventsWithin(SceneCombatSnapshot snapshot, IReadOnlyCollection<int> players, long fromMilliseconds,
        long toMilliseconds, FightEventsCheckpoint? since, out int firstSecond, out FightEventsCheckpoint? checkpoint)
    {
        var lists = new Dictionary<int, List<CombatMetricDetailEvent>>(players.Count);
        var projectable = new bool[players.Count];
        var index = 0;
        foreach (var id in players)
        {
            if (projectable[index++] = CanProjectDetailCombatant(snapshot, id))
                lists[id] = [];
        }

        firstSecond = 0;
        checkpoint = null;
        if (lists.Count == 0)
            return lists;

        PrepareProjectionCaches();
        var events = combat.EventSpan;
        var inSceneWindow = snapshot.EncounterStartTime <= fromMilliseconds && fromMilliseconds <= toMilliseconds && toMilliseconds <= snapshot.EncounterEndTime;
        if (inSceneWindow && since is not null && since.EncounterId == snapshot.EncounterId && since.From == fromMilliseconds && since.To <= toMilliseconds &&
            since.EventCount <= events.Length && since.Players.SequenceEqual(players) && since.Projectable.AsSpan().SequenceEqual(projectable) &&
            (since.IdentityRevision == entities.IdentityRevision || since.Facts.All(pair => FactsOf(pair.Key) == pair.Value)))
        {
            var earliest = long.MaxValue;
            foreach (ref readonly var record in events[Math.Min(since.EventCount, combat.FirstEventIndexCreditedAfter(since.Credits, combat.FirstEventIndexReaching(fromMilliseconds)))..])
            {
                if (record.ObservedAtMilliseconds >= fromMilliseconds && record.ObservedAtMilliseconds <= toMilliseconds)
                    earliest = Math.Min(earliest, record.ObservedAtMilliseconds);
            }

            var lastSecond = (int)Math.Max(1, (since.To - fromMilliseconds + 999) / 1000) - 1;
            firstSecond = earliest == long.MaxValue ? lastSecond : (int)Math.Min(lastSecond, (earliest - fromMilliseconds) / 1000);
        }

        var readFrom = fromMilliseconds + firstSecond * 1000L;
        var facts = firstSecond > 0 ? new Dictionary<int, EntityFacts>(since!.Facts) : [];
        foreach (ref readonly var record in events[combat.FirstEventIndexReaching(readFrom)..])
        {
            if (record.ObservedAtMilliseconds < readFrom || record.ObservedAtMilliseconds > toMilliseconds)
                continue;

            if (inSceneWindow)
            {
                if (!facts.ContainsKey(record.SourceId))
                    facts.Add(record.SourceId, FactsOf(record.SourceId));
                if (!facts.ContainsKey(record.TargetId))
                    facts.Add(record.TargetId, FactsOf(record.TargetId));
            }

            var sourceId = ResolveCombatantIdCached(record.SourceId);
            lists.TryGetValue(sourceId, out var sourceList);
            var targetList = record.TargetId != sourceId && lists.TryGetValue(record.TargetId, out var list) ? list : null;
            if (sourceList is null && targetList is null ||
                !TryCreateMetricDetailEventCached(snapshot, in record, sourceId, CombatDetailProjectionScope.EncounterWindow, out var detailEvent))
            {
                continue;
            }

            sourceList?.Add(detailEvent);
            targetList?.Add(detailEvent);
        }

        if (inSceneWindow)
            checkpoint = new FightEventsCheckpoint(snapshot.EncounterId, fromMilliseconds, toMilliseconds, [.. players], projectable, events.Length, combat.Credits, entities.IdentityRevision, facts);
        return lists;
    }

    // What decides the fate of an event about one of its two entities: whose it is (a summon's owner), and whether it is a
    // summon or an NPC (ShouldIncludeDetailEvent).
    private EntityFacts FactsOf(int entityId) => new(ResolveCombatantIdCached(entityId), IsKnownSummonCached(entityId), IsKnownNpcCombatant(entityId));

    internal CombatDetailWriteResult WriteDetailEvents(SceneCombatSnapshot snapshot, int combatantId, ICombatDetailEventWriter writer)
        => WriteDetailEvents(snapshot, combatantId, writer, CombatDetailProjectionScope.EncounterWindow);

    internal CombatDetailWriteResult WriteDetailEvents(SceneCombatSnapshot snapshot, int combatantId, ICombatDetailEventWriter writer, CombatDetailProjectionScope scope,
        long fromMilliseconds = long.MinValue, long toMilliseconds = long.MaxValue)
    {
        if (!CanProjectDetailCombatant(snapshot, combatantId))
            return default;

        PrepareProjectionCaches();

        var metricEventCount = 0;
        var mechanicEventCount = 0;
        var resourceEventCount = 0;
        var revision = 0L;
        // The headers first: the same test the TryCreate*DetailEventCached start with, without loading the record.
        var records = combat.EventSpan;
        var headers = _combatHeaders.Of(combat.Events, records, static (in CombatEventRecord record) => (record.SourceId, record.TargetId));
        for (var i = 0; i < headers.Length; i++)
        {
            if (!IsCombatantEvent(headers[i], combatantId) ||
                records[i].ObservedAtMilliseconds < fromMilliseconds || records[i].ObservedAtMilliseconds > toMilliseconds ||
                !TryCreateMetricDetailEventCached(snapshot, combatantId, in records[i], scope, out var detailEvent))
            {
                continue;
            }

            writer.AddMetric(in detailEvent);
            metricEventCount++;
            revision = Math.Max(revision, detailEvent.Revision);
        }

        var mechanicEvents = mechanics.EventSpan;
        headers = _mechanicHeaders.Of(mechanics.Events, mechanicEvents, static (in CombatMechanicEventRecord record) => (record.SourceId, record.TargetId));
        for (var i = 0; i < headers.Length; i++)
        {
            if (!IsCombatantEvent(headers[i], combatantId) ||
                mechanicEvents[i].ObservedAtMilliseconds < fromMilliseconds || mechanicEvents[i].ObservedAtMilliseconds > toMilliseconds ||
                !TryCreateMechanicDetailEventCached(snapshot, combatantId, in mechanicEvents[i], scope, out var detailEvent))
            {
                continue;
            }

            writer.AddMechanic(in detailEvent);
            mechanicEventCount++;
            revision = Math.Max(revision, detailEvent.Revision);
        }

        var resourceEvents = resources.EventSpan;
        headers = _resourceHeaders.Of(resources.Events, resourceEvents, static (in CombatResourceEventRecord record) => (record.SourceId, record.TargetId));
        for (var i = 0; i < headers.Length; i++)
        {
            if (!IsCombatantEvent(headers[i], combatantId) ||
                resourceEvents[i].ObservedAtMilliseconds < fromMilliseconds || resourceEvents[i].ObservedAtMilliseconds > toMilliseconds ||
                !TryCreateResourceDetailEventCached(snapshot, combatantId, in resourceEvents[i], scope, out var detailEvent))
            {
                continue;
            }

            writer.AddResource(in detailEvent);
            resourceEventCount++;
            revision = Math.Max(revision, detailEvent.Revision);
        }

        return new CombatDetailWriteResult(metricEventCount, mechanicEventCount, resourceEventCount, revision);
    }

    private bool IsCombatantEvent((int SourceId, int TargetId) header, int combatantId) =>
        header.TargetId == combatantId || ResolveCombatantIdCached(header.SourceId) == combatantId;

    internal bool TryCreateMetricDetailEvent(SceneCombatSnapshot snapshot, int combatantId, in CombatEventRecord record, out CombatMetricDetailEvent detailEvent)
    {
        PrepareProjectionCaches();
        return TryCreateMetricDetailEventCached(snapshot, combatantId, in record, CombatDetailProjectionScope.EncounterWindow, out detailEvent);
    }

    internal bool TryResolveMetricDetailEventSource(SceneCombatSnapshot snapshot, in CombatEventRecord record, out int sourceId)
    {
        PrepareProjectionCaches();
        return TryResolveMetricDetailEventSourceCached(snapshot, in record, CombatDetailProjectionScope.EncounterWindow, out sourceId);
    }

    private bool TryCreateMetricDetailEventCached(SceneCombatSnapshot snapshot, int combatantId, in CombatEventRecord record, CombatDetailProjectionScope scope, out CombatMetricDetailEvent detailEvent)
    {
        var eventSourceId = ResolveCombatantIdCached(record.SourceId);
        if (eventSourceId != combatantId && record.TargetId != combatantId)
        {
            detailEvent = default;
            return false;
        }

        return TryCreateMetricDetailEventCached(snapshot, in record, eventSourceId, scope, out detailEvent);
    }

    private bool TryCreateMetricDetailEventCached(SceneCombatSnapshot snapshot, in CombatEventRecord record, int eventSourceId, CombatDetailProjectionScope scope, out CombatMetricDetailEvent detailEvent)
    {
        if (!ShouldIncludeDetailEvent(in record, eventSourceId, record.TargetId, snapshot, scope))
        {
            detailEvent = default;
            return false;
        }

        detailEvent = CreateMetricDetailEvent(in record, eventSourceId);
        return true;
    }

    private bool TryResolveMetricDetailEventSourceCached(SceneCombatSnapshot snapshot, in CombatEventRecord record, CombatDetailProjectionScope scope, out int sourceId)
    {
        sourceId = ResolveCombatantIdCached(record.SourceId);
        if (ShouldIncludeDetailEvent(in record, sourceId, record.TargetId, snapshot, scope))
            return true;

        sourceId = 0;
        return false;
    }

    private static CombatMetricDetailEvent CreateMetricDetailEvent(in CombatEventRecord record, int sourceId)
        => new(
            CombatDetailFact.Create(
                record.Observation,
                sourceId,
                record.TargetId,
                ObservedAt(record),
                record.SourceObservationOrdinal,
                record.Revision,
                record.EventKey,
                record.Raw),
            record.Contribution)
        {
            EffectiveHealing = record.EffectiveHealing,
            Stagger = record.Stagger
        };

    private bool TryCreateMechanicDetailEventCached(
        SceneCombatSnapshot snapshot,
        int combatantId,
        in CombatMechanicEventRecord record,
        CombatDetailProjectionScope scope,
        out CombatMechanicDetailEvent detailEvent)
    {
        var sourceId = ResolveCombatantIdCached(record.SourceId);
        if (sourceId != combatantId && record.TargetId != combatantId ||
            scope == CombatDetailProjectionScope.EncounterWindow &&
            !IsWithinEncounterWindow(record.ObservedAtMilliseconds, snapshot.EncounterStartTime, snapshot.EncounterEndTime) ||
            IsSummonMechanicTargetCached(record.SourceId, record.TargetId, record.Mechanic))
        {
            detailEvent = default;
            return false;
        }

        detailEvent = new CombatMechanicDetailEvent(
            CombatDetailFact.Create(
                record.Observation,
                sourceId,
                record.TargetId,
                record.ObservedAtMilliseconds,
                record.SourceObservationOrdinal,
                record.Revision,
                record.EventKey,
                record.Raw),
            record.Mechanic);
        return true;
    }

    private bool TryCreateResourceDetailEventCached(
        SceneCombatSnapshot snapshot,
        int combatantId,
        in CombatResourceEventRecord record,
        CombatDetailProjectionScope scope,
        out CombatResourceDetailEvent detailEvent)
    {
        var sourceId = ResolveCombatantIdCached(record.SourceId);
        if (sourceId != combatantId && record.TargetId != combatantId ||
            scope == CombatDetailProjectionScope.EncounterWindow &&
            !IsWithinEncounterWindow(record.ObservedAtMilliseconds, snapshot.EncounterStartTime, snapshot.EncounterEndTime))
        {
            detailEvent = default;
            return false;
        }

        detailEvent = new CombatResourceDetailEvent(
            CombatDetailFact.Create(
                record.Observation,
                sourceId,
                record.TargetId,
                record.ObservedAtMilliseconds,
                record.SourceObservationOrdinal,
                record.Revision,
                record.EventKey,
                record.Raw),
            record.Resource);
        return true;
    }

    public CombatSkillBreakdownSnapshot CreateSkillBreakdown(SceneCombatSnapshot snapshot, int combatantId)
    {
        if (combatantId <= 0 || snapshot.EncounterEndTime < snapshot.EncounterStartTime)
            return CombatSkillBreakdownSnapshot.Empty;

        PrepareProjectionCaches();
        var skills = new Dictionary<CombatEventKey, SkillMetrics>();
        ApplySkillBreakdownEvents(snapshot, combatantId, skills);

        return CombatSkillBreakdownSnapshot.From(skills);
    }

    private static bool IsSnapshotTarget(SceneCombatSnapshot snapshot, int entityId) =>
        snapshot.TargetObservation?.InstanceId == entityId || snapshot.Encounter.TrackingTargetId == entityId;

    private static long SaturatingAdd(long first, long second, long third)
    {
        var sum = first > long.MaxValue - second ? long.MaxValue : first + second;
        return sum > long.MaxValue - third ? long.MaxValue : sum + third;
    }

    private bool CanProjectDetailCombatant(SceneCombatSnapshot snapshot, int combatantId) =>
        combatantId > 0 &&
        snapshot.EncounterEndTime >= snapshot.EncounterStartTime &&
        (snapshot.Combatants.ContainsKey(combatantId) ||
         IsSnapshotTarget(snapshot, combatantId) ||
         CombatPairProjection.GetCombatant(combat, mechanics, resources, combatantId) is not null);

    public int ResolveDetailCombatantId(int entityId)
    {
        PrepareProjectionCaches();
        return ResolveCombatantIdCached(entityId);
    }

    internal CombatDetailProjectionVersion PrepareCurrentFrameEventProjection()
    {
        PrepareProjectionCaches();
        return new CombatDetailProjectionVersion(
            entities.IdentityRevision);
    }

    internal bool TryResolveCurrentFrameEventSourcePrepared(in CombatEventRecord record, out int sourceId)
    {
        sourceId = ResolveCombatantIdCached(record.SourceId);
        if (!IsSummonDamageTargetCached(in record))
            return true;

        sourceId = 0;
        return false;
    }

    internal bool TryResolveCurrentFrameEventSourcePrepared(in CombatMechanicEventRecord record, out int sourceId)
    {
        sourceId = ResolveCombatantIdCached(record.SourceId);
        if (!IsSummonMechanicTargetCached(record.SourceId, record.TargetId, record.Mechanic))
            return true;

        sourceId = 0;
        return false;
    }

    internal int ResolveCurrentFrameEventSourcePrepared(in CombatResourceEventRecord record) =>
        ResolveCombatantIdCached(record.SourceId);

    internal bool IsSummonDamageTarget(int sourceId, int targetId, long damage)
    {
        PrepareProjectionCaches();
        return IsSummonDamageTargetCached(sourceId, targetId, damage);
    }

    internal bool IsSummonMechanicTarget(int sourceId, int targetId, CombatMechanicOccurrence mechanic)
    {
        PrepareProjectionCaches();
        return IsSummonMechanicTargetCached(sourceId, targetId, mechanic);
    }

    // The fight graphs' take-up (CreateMetricEventsWithin) assumes that inside the scene's window this reads, besides the
    // event, only the EntityFacts of its source and target: add any new entity input to EntityFacts (FactsOf).
    private bool ShouldIncludeDetailEvent(in CombatEventRecord e, int sourceId, int targetId, SceneCombatSnapshot snapshot, CombatDetailProjectionScope scope)
    {
        if (e.ContributesHealing && IsNonPlayerHealTargetCached(targetId))
            return false;

        if (scope == CombatDetailProjectionScope.CurrentFrame)
            return !IsSummonDamageTargetCached(in e);

        if (IsWithinEncounterWindow(in e, snapshot.EncounterStartTime, snapshot.EncounterEndTime))
            return !IsSummonDamageTargetCached(in e);

        return IsRelevantRecoveryEvent(in e, sourceId, targetId, snapshot);
    }

    private TargetDecision BuildTargetDecisionFromCombatState()
    {
        _targetInfos.Clear();
        var latestObservedAt = 0L;
        foreach (var pair in combat.Pairs.Values)
        {
            var lastObserved = ObservedAt(pair);
            latestObservedAt = Math.Max(latestObservedAt, lastObserved);
            mechanics.TryGetPair(pair.SourceId, pair.TargetId, out var mechanicPair);
            if (!HasDamageActivity(pair.TotalDamage, mechanicPair))
                continue;

            var firstObserved = mechanicPair is null ? pair.FirstObserved : Math.Min(pair.FirstObserved, mechanicPair.FirstObserved);
            AddTargetInfo(pair.SourceId, pair.TargetId, pair.TotalDamage, firstObserved, Math.Max(lastObserved, mechanicPair?.LastObserved ?? 0));
        }

        foreach (var mechanicPair in mechanics.Pairs.Values)
        {
            latestObservedAt = Math.Max(latestObservedAt, mechanicPair.LastObserved);
            if (combat.Pairs.ContainsKey((mechanicPair.SourceId, mechanicPair.TargetId)) || !HasDamageActivity(0, mechanicPair))
                continue;

            AddTargetInfo(mechanicPair.SourceId, mechanicPair.TargetId, 0, mechanicPair.FirstObserved, mechanicPair.LastObserved);
        }

        foreach (var resourcePair in resources.Pairs.Values)
            latestObservedAt = Math.Max(latestObservedAt, resourcePair.LastObserved);

        if (_targetInfos.Count == 0)
            return BuildResourceOnlyTargetDecision(latestObservedAt);

        var start = long.MaxValue;
        var end = long.MinValue;
        var trackingTargetId = 0;
        var lastTargetObserved = long.MinValue;
        foreach (var (targetId, info) in _targetInfos)
        {
            start = Math.Min(start, info.FirstDamageAt);
            end = Math.Max(end, info.LastDamageAt);
            if (info.LastDamageAt > lastTargetObserved)
            {
                lastTargetObserved = info.LastDamageAt;
                trackingTargetId = targetId;
            }
        }

        return new TargetDecision(start, end, trackingTargetId, latestObservedAt);
    }

    private TargetDecision BuildResourceOnlyTargetDecision(long latestObservedAt)
    {
        var start = long.MaxValue;
        var end = long.MinValue;
        var trackingTargetId = 0;
        var lastTargetObserved = long.MinValue;
        foreach (var pair in resources.Pairs.Values)
        {
            start = Math.Min(start, pair.FirstObserved);
            end = Math.Max(end, pair.LastObserved);
            var targetId = pair.TargetId > 0 ? pair.TargetId : ResolveCombatantIdCached(pair.SourceId);
            if (targetId > 0 && pair.LastObserved > lastTargetObserved)
            {
                trackingTargetId = targetId;
                lastTargetObserved = pair.LastObserved;
            }
        }

        return start == long.MaxValue
            ? new TargetDecision(0, 0, 0, latestObservedAt)
            : new TargetDecision(start, end, trackingTargetId, latestObservedAt);
    }

    private void AddTargetInfo(int sourceId, int targetId, long damage, long firstObserved, long lastObserved)
    {
        if (targetId <= 0 || IsKnownSummonCached(targetId) || IsSummonDamageTargetCached(sourceId, targetId, damage))
            return;

        ref var info = ref CollectionsMarshal.GetValueRefOrAddDefault(_targetInfos, targetId, out var exists);
        if (!exists)
            info = new TargetInfo(firstObserved, lastObserved);
        info.Add(firstObserved);
        info.Add(lastObserved);
    }

    private void ApplyCombatState(SceneCombatSnapshotBuilder builder)
    {
        foreach (var pair in combat.Pairs.Values)
        {
            var sourceId = ResolveCombatantIdCached(pair.SourceId);
            if (sourceId <= 0)
                continue;

            ref var metrics = ref builder.GetOrAddCombatant(sourceId);
            var healsPlayer = !IsNonPlayerHealTargetCached(pair.TargetId);
            metrics.ApplyCombatTotals(
                IsSummonDamageTargetCached(pair.SourceId, pair.TargetId, pair.TotalDamage) ? 0 : pair.TotalDamage,
                healsPlayer ? pair.TotalHealing : 0,
                healsPlayer ? pair.TotalEffectiveHealing : 0,
                healsPlayer ? pair.TotalPeriodicHealing : 0,
                pair.TotalDrainDamage,
                healsPlayer ? pair.TotalDrainHealing : 0,
                healsPlayer ? pair.TotalRegenerationHealing : 0,
                pair.TotalShield,
                pair.ShieldCount,
                pair.TotalShieldAbsorbed,
                pair.ShieldAbsorbedCount);
        }

        foreach (var pair in mechanics.Pairs.Values)
        {
            var sourceId = ResolveCombatantIdCached(pair.SourceId);
            if (sourceId > 0)
                _ = builder.GetOrAddCombatant(sourceId);
        }

        foreach (var pair in resources.Pairs.Values)
        {
            var sourceId = ResolveCombatantIdCached(pair.SourceId);
            if (sourceId > 0)
                _ = builder.GetOrAddCombatant(sourceId);
            if (pair.TargetId > 0 && pair.TargetId != sourceId)
                _ = builder.GetOrAddCombatant(pair.TargetId);
        }

        // Damage a combatant took in person; hits on their summons are not theirs.
        foreach (var pair in combat.Pairs.Values)
        {
            if (pair.TotalDamage > 0 &&
                builder.CombatantIds.Contains(pair.TargetId) &&
                !IsSummonDamageTargetCached(pair.SourceId, pair.TargetId, pair.TotalDamage))
            {
                builder.GetExistingCombatant(pair.TargetId).DamageTakenAmount += pair.TotalDamage;
            }
        }

        // A boss fight with the boss frame's target: the share of the time the boss was after them. Otherwise (the scene,
        // open world bosses, older histories) the share of the casts enemies aimed at them.
        if (combat.AggroMilliseconds.Count > 0)
        {
            var aggroMilliseconds = 0L;
            foreach (var milliseconds in combat.AggroMilliseconds.Values)
                aggroMilliseconds += milliseconds;
            foreach (var (targetId, milliseconds) in combat.AggroMilliseconds)
            {
                if (builder.CombatantIds.Contains(targetId))
                    builder.GetExistingCombatant(targetId).AggroShare += (double)milliseconds / aggroMilliseconds;
            }
        }
        else
        {
            var aggroCasts = 0;
            foreach (var casts in combat.AggroCasts.Values)
                aggroCasts += casts;
            foreach (var (key, casts) in combat.AggroCasts)
            {
                if (builder.CombatantIds.Contains(key.Target))
                    builder.GetExistingCombatant(key.Target).AggroShare += (double)casts / aggroCasts;
            }
        }

        var staggerPoints = 0d;
        foreach (var points in combat.StaggerPoints.Values)
            staggerPoints += points;
        foreach (var (sourceId, points) in combat.StaggerPoints)
        {
            var combatantId = ResolveCombatantIdCached(sourceId);
            if (builder.CombatantIds.Contains(combatantId))
                builder.GetExistingCombatant(combatantId).StaggerShare += points / staggerPoints;
        }
    }

    // Every source's evidence, whoever it turns out to be: who counts is decided when it's read (ResolveCharacterClass), so
    // an NPC spawn, a summon linked or a name doesn't rescan the scene.
    private void ProcessClassEvidenceEvents(EventRange<CombatEventRecord> events)
    {
        foreach (ref readonly var record in events)
        {
            if (record.SourceId <= 0)
                continue;

            var observation = record.Observation;
            var contribution = record.Contribution;
            if (!CombatantClassEvidence.TryCreate(in observation, in contribution, out var characterClass, out var score))
                continue;

            ref var evidence = ref CollectionsMarshal.GetValueRefOrAddDefault(_classEvidence, record.SourceId, out _);
            evidence.Add(characterClass, score);
        }
    }

    private void ExpandSinglePointEncounterWindowFromRelevantRecovery(ref long start, ref long end)
    {
        Span<int> relevantBuffer = stackalloc int[SmallSetStackCapacity];
        var relevant = new SmallIntSet(relevantBuffer);
        var events = combat.EventSpan;
        foreach (ref readonly var e in events)
        {
            if (!_targetInfos.ContainsKey(e.TargetId) || !e.ContributesDamage || IsSummonDamageTargetCached(in e))
                continue;

            relevant.Add(ResolveCombatantIdCached(e.SourceId));
            if (e.TargetId > 0)
                relevant.Add(e.TargetId);
        }

        if (relevant.Count == 0)
            return;

        var events2 = combat.EventSpan;
        foreach (ref readonly var e in events2)
        {
            var observedAt = ObservedAt(in e);
            if (IsWithinEncounterWindow(observedAt, start, end) || IsSummonDamageTargetCached(in e))
                continue;

            var sourceId = ResolveCombatantIdCached(e.SourceId);
            if (!IsRelevantRecoveryEvent(in e, sourceId, e.TargetId, ref relevant))
                continue;

            start = Math.Min(start, observedAt);
            end = Math.Max(end, observedAt);
        }
    }

    private void ApplySkillBreakdownEvents(SceneCombatSnapshot snapshot, int combatantId, Dictionary<CombatEventKey, SkillMetrics> skills)
    {
        Span<int> relevantBuffer = stackalloc int[SmallSetStackCapacity];
        var relevant = new SmallIntSet(relevantBuffer);
        var events = combat.EventSpan;
        foreach (ref readonly var e in events)
        {
            var observedAt = ObservedAt(in e);
            if (!IsWithinEncounterWindow(observedAt, snapshot.EncounterStartTime, snapshot.EncounterEndTime) || IsSummonDamageTargetCached(in e))
                continue;

            var sourceId = ResolveCombatantIdCached(e.SourceId);
            relevant.Add(sourceId);
            if (e.TargetId > 0)
                relevant.Add(e.TargetId);

            if (sourceId == combatantId)
                AddSkillEvent(skills, in e);
        }

        if (relevant.Count > 0)
        {
            var events2 = combat.EventSpan;
            foreach (ref readonly var e in events2)
            {
                var observedAt = ObservedAt(in e);
                if (IsWithinEncounterWindow(observedAt, snapshot.EncounterStartTime, snapshot.EncounterEndTime) || IsSummonDamageTargetCached(in e))
                    continue;

                var sourceId = ResolveCombatantIdCached(e.SourceId);
                if (sourceId != combatantId || !IsRelevantRecoveryEvent(in e, sourceId, e.TargetId, ref relevant))
                    continue;

                AddSkillEvent(skills, in e);
            }
        }

        var mechanicEvents = mechanics.Events;
        for (var i = 0; i < mechanicEvents.Count; i++)
        {
            var mechanicEvent = mechanicEvents[i];
            if (!IsWithinEncounterWindow(mechanicEvent.ObservedAtMilliseconds, snapshot.EncounterStartTime, snapshot.EncounterEndTime) ||
                IsSummonMechanicTargetCached(mechanicEvent.SourceId, mechanicEvent.TargetId, mechanicEvent.Mechanic) ||
                ResolveCombatantIdCached(mechanicEvent.SourceId) != combatantId)
            {
                continue;
            }

            AddSkillMechanicEvent(skills, in mechanicEvent);
        }
    }

    private static void AddSkillEvent(Dictionary<CombatEventKey, SkillMetrics> skills, in CombatEventRecord e)
    {
        var eventKey = e.EventKey;
        ref var metrics = ref CollectionsMarshal.GetValueRefOrAddDefault(skills, eventKey, out var exists);
        if (!exists)
        {
            metrics = new SkillMetrics(eventKey);
        }

        var contribution = e.Contribution;
        metrics.ProcessContribution(in contribution);
    }

    private static void AddSkillMechanicEvent(Dictionary<CombatEventKey, SkillMetrics> skills, in CombatMechanicEventRecord e)
    {
        var eventKey = e.EventKey;
        ref var metrics = ref CollectionsMarshal.GetValueRefOrAddDefault(skills, eventKey, out var exists);
        if (!exists)
            metrics = new SkillMetrics(eventKey);

        var mechanic = e.Mechanic;
        metrics.ProcessMechanic(in mechanic);
    }

    // Part of EntityFacts (FactsOf), which the fight graphs' take-up checks: a new entity input here belongs there too.
    private int ResolveCombatantIdCached(int combatantId)
    {
        if (combatantId <= 0)
            return combatantId;

        ref var resolved = ref CollectionsMarshal.GetValueRefOrAddDefault(_resolvedCombatantIds, combatantId, out var exists);
        if (exists)
            return resolved;

        if (entities.TryGet(combatantId, out var entity))
        {
            if (entity.OwnerEntityId is int ownerId)
            {
                resolved = ownerId;
                return resolved;
            }

            if (entity.Kind != NpcKind.Summon && IsExplicitNonSummon(entity))
            {
                resolved = combatantId;
                return resolved;
            }
        }

        resolved = combatantId;
        return resolved;
    }

    private bool IsSummonDamageTargetCached(in CombatEventRecord e)
    {
        return IsSummonDamageTargetCached(e.SourceId, e.TargetId, e.ContributesDamage ? e.Contribution.Amount : 0);
    }

    private bool IsSummonDamageTargetCached(int sourceId, int targetId, long damage)
    {
        if (targetId <= 0 || damage <= 0)
            return false;

        return IsSummonCombatTargetCached(sourceId, targetId);
    }

    private bool IsSummonMechanicTargetCached(
        int sourceId,
        int targetId,
        CombatMechanicOccurrence mechanic)
    {
        if (targetId <= 0 || !mechanic.HasFacts)
            return false;

        return IsSummonCombatTargetCached(sourceId, targetId);
    }

    private bool IsSummonCombatTargetCached(int sourceId, int targetId)
    {
        var sourceIsSummon = IsKnownSummonCached(sourceId);
        var targetIsSummon = IsKnownSummonCached(targetId);
        if (targetIsSummon)
            return true;

        return (sourceIsSummon || targetIsSummon) && ResolveCombatantIdCached(sourceId) == ResolveCombatantIdCached(targetId);
    }

    private bool IsKnownSummonCached(int entityId)
    {
        if (entityId <= 0)
            return false;

        ref var known = ref CollectionsMarshal.GetValueRefOrAddDefault(_knownSummons, entityId, out var exists);
        if (exists)
            return known;

        known = IsKnownSummonCore(entityId);
        return known;
    }

    private bool IsKnownSummonCore(int entityId)
    {
        return entities.TryGet(entityId, out var entity) &&
               (entity.OwnerKind == EntityOwnerKind.Summon || entity.Kind == NpcKind.Summon);
    }

    private static bool IsExplicitNonSummon(EntityRecord entity) =>
        entity.IsPlayer ||
        entity.NpcCode.HasValue ||
        entity.Kind is NpcKind.Monster or NpcKind.Boss or NpcKind.Friendly or NpcKind.TrainingDummy;

    private bool IsKnownNpcCombatant(int entityId) =>
        entities.TryGet(entityId, out var entity) && (entity.NpcCode.HasValue || entity.Kind is NpcKind.Monster or NpcKind.Boss or NpcKind.Friendly or NpcKind.Summon or NpcKind.TrainingDummy);

    // Healing only counts when it lands on a player: a spirit refilled on summon, or an NPC healed, is not.
    // Part of EntityFacts (FactsOf), which the fight graphs' take-up checks: a new entity input here belongs there too.
    private bool IsNonPlayerHealTargetCached(int targetId) =>
        IsKnownSummonCached(targetId) || IsKnownNpcCombatant(targetId);

    private bool ShouldDisplayCombatant(int entityId)
    {
        if (!entities.TryGet(entityId, out var entity))
            return true;

        if (entity.NpcCode.HasValue)
            return false;

        return entity.Kind is not (NpcKind.Monster or NpcKind.Boss or NpcKind.Friendly or NpcKind.Summon or NpcKind.TrainingDummy);
    }

    private static bool IsWithinEncounterWindow(in CombatEventRecord e, long start, long end) =>
        IsWithinEncounterWindow(ObservedAt(e), start, end);

    private static bool IsWithinEncounterWindow(long observedAt, long start, long end) =>
        observedAt >= start && observedAt <= end;

    private static long ObservedAt(in CombatEventRecord e) =>
        e.ObservedAtMilliseconds;

    private static long ObservedAt(CombatPairRecord pair) =>
        pair.LastObserved;

    private static bool HasDamageActivity(long damage, CombatMechanicPairRecord? mechanic) =>
        damage > 0 ||
        mechanic is not null &&
        (mechanic.HitCount > 0 ||
         mechanic.AttemptCount > 0 ||
         mechanic.EvadeCount > 0 ||
         mechanic.InvincibleCount > 0 ||
         mechanic.MultiHitCount > 0);

    private static bool IsRelevantRecoveryEvent(in CombatEventRecord e, int sourceId, int targetId, ref SmallIntSet relevant)
    {
        if (e.Contribution.Amount <= 0 || (!relevant.Contains(sourceId) && !relevant.Contains(targetId)))
            return false;

        return IsRecoveryEvent(in e);
    }

    private static bool IsRelevantRecoveryEvent(in CombatEventRecord e, int sourceId, int targetId, SceneCombatSnapshot snapshot)
    {
        if (e.Contribution.Amount <= 0)
            return false;

        var targetObservationId = snapshot.TargetObservation?.InstanceId;
        if (!snapshot.Combatants.ContainsKey(sourceId) &&
            !snapshot.Combatants.ContainsKey(targetId) &&
            targetObservationId != sourceId &&
            targetObservationId != targetId)
        {
            return false;
        }

        return IsRecoveryEvent(in e);
    }

    private static bool IsRecoveryEvent(in CombatEventRecord e)
    {
        return e.Contribution.Metric is CombatMetricKind.Healing or CombatMetricKind.ShieldGranted or CombatMetricKind.ShieldAbsorbed;
    }

    private int ResolveTrackingTargetId(int trackingTargetId, long nowMilliseconds)
    {
        if (trackingTargetId > 0)
            return trackingTargetId;

        return bossFocus is not null && bossFocus.TryGetObservedBoss(nowMilliseconds, 10_000, out var boss) ? boss.InstanceId : 0;
    }

    private CharacterClass? ResolveCharacterClass(int entityId)
    {
        if (IsKnownNpcCombatant(entityId))
            return null;

        if (entities.TryGet(entityId, out var entity) && entity.CharacterClass is { } characterClass)
            return characterClass;

        // A source's evidence counts while it is no one's summon and no NPC (checked above). These read only identity that
        // EntityStore changes with a new IdentityRevision, and the caches follow it (PrepareProjectionCaches).
        return _classEvidence.TryGetValue(entityId, out var evidence) && ResolveCombatantIdCached(entityId) == entityId && !IsKnownSummonCached(entityId)
            ? evidence.Resolve()
            : null;
    }

    private void PrepareProjectionCaches()
    {
        EnsureResolveCaches();
    }

    private void EnsureClassEvidence()
    {
        var combatRevision = combat.Revision;
        var skillMapRevision = CombatResourceRegistry.SkillMapRevision;
        var journal = combat.Events;
        var events = combat.EventSpan;
        // A cleared store starts a new journal, which may have grown past the old count and revision by now.
        var rebuildFromStart = !ReferenceEquals(_classEvidenceJournal, journal) ||
                               combatRevision < _classEvidenceCombatRevision ||
                               _classEvidenceSkillMapRevision != skillMapRevision ||
                               _classEvidenceScannedEventCount > events.Length;

        if (rebuildFromStart)
        {
            _classEvidence.Clear();
            _classEvidenceScannedEventCount = 0;
        }

        if (_classEvidenceScannedEventCount < events.Length)
        {
            ProcessClassEvidenceEvents(events[_classEvidenceScannedEventCount..]);
            _classEvidenceScannedEventCount = events.Length;
        }

        _classEvidenceJournal = journal;
        _classEvidenceCombatRevision = combatRevision;
        _classEvidenceSkillMapRevision = skillMapRevision;
    }

    private void EnsureResolveCaches()
    {
        if (_resolveCacheIdentityRevision == entities.IdentityRevision)
            return;

        _resolvedCombatantIds.Clear();
        _knownSummons.Clear();
        _resolveCacheIdentityRevision = entities.IdentityRevision;
    }

    private NpcRuntimeObservationSnapshot? BuildTargetObservation(int targetId)
    {
        if (targetId <= 0)
            return null;

        long? value2136 = null;
        long? sequence2136 = null;
        long? value0140 = null;
        long? value0240 = null;
        byte? state4636Value0 = null;
        byte? state4636Value1 = null;
        int? sequence2C38 = null;
        int? result2C38 = null;
        long? hp = null;
        bool? battleToggledOn = null;

        if (entities.TryGet(targetId, out var entity))
        {
            value2136 = entity.Value2136;
            sequence2136 = entity.Sequence2136;
            value0140 = entity.Value0140;
            value0240 = entity.Value0240;
            state4636Value0 = entity.State4636?.State0;
            state4636Value1 = entity.State4636?.State1;
            sequence2C38 = entity.Latest2C38?.SequenceId;
            result2C38 = entity.Latest2C38?.ResultCode;
            battleToggledOn = entity.NpcCombatActive;
        }

        if (entityVitals.TryGet(targetId, out var vital))
            hp = vital.CurrentHp;

        var mutableObservation = new NpcRuntimeObservation
        {
            InstanceId = targetId,
            Value2136 = value2136,
            Sequence2136 = sequence2136,
            Value0140 = value0140,
            Value0240 = value0240,
            State4636Value0 = state4636Value0,
            State4636Value1 = state4636Value1,
            Sequence2C38 = sequence2C38,
            Result2C38 = result2C38,
            Hp = hp,
            BattleToggledOn = battleToggledOn
        };
        var phaseHint = NpcRuntimeObservationInterpreter.InferPhaseHint(mutableObservation);

        return new NpcRuntimeObservationSnapshot(
            targetId,
            value2136,
            sequence2136,
            value0140,
            value0240,
            state4636Value0,
            state4636Value1,
            sequence2C38,
            result2C38,
            hp,
            battleToggledOn,
            phaseHint);
    }

    private long ResolveSnapshotNow(long latestObservedAt)
    {
        var now = latestObservedAt;

        if (combat.Pairs.Count == 0 && bossFocus is not null)
        {
            var snapshots = bossFocus.GetObservedBosses(0, long.MaxValue);
            for (var i = 0; i < snapshots.Count; i++)
                now = Math.Max(now, snapshots[i].LastObservedAtMilliseconds);
        }

        return now;
    }

    private EncounterSummarySnapshot EvaluateEncounter(int targetId, bool hasMaterializedActivity, NpcRuntimeObservationSnapshot? observation, long nowMilliseconds)
    {
        if (targetId <= 0 && bossFocus is not null && bossFocus.TryGetObservedBoss(nowMilliseconds, 10_000, out var boss))
        {
            targetId = boss.InstanceId;
            observation = BuildTargetObservation(targetId);
        }

        if (targetId <= 0)
        {
            return new EncounterSummarySnapshot(
                TrackingTargetId: 0,
                PhaseHint: NpcRuntimePhaseHint.Unknown,
                IsActive: false,
                ShouldArchive: false,
                Reason: "no-target");
        }

        return new EncounterSummarySnapshot(
            TrackingTargetId: targetId,
            PhaseHint: observation?.PhaseHint ?? NpcRuntimePhaseHint.Unknown,
            IsActive: hasMaterializedActivity || observation?.BattleToggledOn == true || observation?.Hp.HasValue == true,
            ShouldArchive: false,
            Reason: hasMaterializedActivity ? "scene-combat" : observation?.BattleToggledOn == true ? "battle-toggle" : observation?.Hp.HasValue == true ? "hp-observed" : "insufficient-signal");
    }

    private readonly record struct TargetDecision(long EncounterStartTime, long EncounterEndTime, int TrackingTargetId, long LatestObservedAt);

    private struct TargetInfo(long firstDamageAt, long lastDamageAt)
    {
        public long FirstDamageAt { get; private set; } = firstDamageAt;
        public long LastDamageAt { get; private set; } = lastDamageAt;

        public void Add(long observedAt)
        {
            FirstDamageAt = Math.Min(FirstDamageAt, observedAt);
            LastDamageAt = Math.Max(LastDamageAt, observedAt);
        }
    }

    private ref struct SmallIntSet
    {
        private Span<int> _buffer;
        private HashSet<int>? _overflow;
        private int _count;

        public SmallIntSet(Span<int> buffer)
        {
            _buffer = buffer;
            _overflow = null;
            _count = 0;
        }

        public readonly int Count => _overflow?.Count ?? _count;

        public bool Add(int value)
        {
            if (value <= 0)
                return false;

            if (_overflow is { } overflow)
                return overflow.Add(value);

            for (var i = 0; i < _count; i++)
            {
                if (_buffer[i] == value)
                    return false;
            }

            if (_count < _buffer.Length)
            {
                _buffer[_count++] = value;
                return true;
            }

            _overflow = new(_buffer.Length * 2);
            for (var i = 0; i < _count; i++)
            {
                _overflow.Add(_buffer[i]);
            }

            return _overflow.Add(value);
        }

        public readonly bool Contains(int value)
        {
            if (value <= 0)
                return false;

            if (_overflow is { } overflow)
                return overflow.Contains(value);

            for (var i = 0; i < _count; i++)
            {
                if (_buffer[i] == value)
                    return true;
            }

            return false;
        }
    }

    private sealed class ListDetailEventWriter : ICombatDetailEventWriter
    {
        private readonly List<CombatMetricDetailEvent> _metricEvents = [];
        private readonly List<CombatMechanicDetailEvent> _mechanicEvents = [];
        private readonly List<CombatResourceDetailEvent> _resourceEvents = [];

        public void Clear()
        {
            _metricEvents.Clear();
            _mechanicEvents.Clear();
            _resourceEvents.Clear();
        }

        public void AddMetric(in CombatMetricDetailEvent detailEvent) => _metricEvents.Add(detailEvent);

        public void AddMechanic(in CombatMechanicDetailEvent detailEvent) => _mechanicEvents.Add(detailEvent);

        public void AddResource(in CombatResourceDetailEvent detailEvent) => _resourceEvents.Add(detailEvent);

        public CombatDetailEventSet ToEventSet() => new(_metricEvents, _mechanicEvents, _resourceEvents);
    }

    private delegate (int SourceId, int TargetId) HeaderOf<T>(in T record) where T : struct;

    // A store's events' headers, added as the store grows; a store cleared starts a new journal, and they start over.
    private sealed class EventHeaders<T> where T : struct
    {
        private readonly List<(int SourceId, int TargetId)> _headers = [];
        private object? _journal;

        public ReadOnlySpan<(int SourceId, int TargetId)> Of(IReadOnlyList<T> journal, EventRange<T> events, HeaderOf<T> header)
        {
            if (!ReferenceEquals(_journal, journal))
            {
                _headers.Clear();
                _journal = journal;
            }

            for (var i = _headers.Count; i < events.Length; i++)
                _headers.Add(header(in events[i]));
            return CollectionsMarshal.AsSpan(_headers);
        }
    }
}
