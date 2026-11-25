using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime.Canonicalization;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.SceneRuntime.Stores;

public sealed class DomainEventApplier
{
    private readonly EntityStore _entities;
    private readonly SceneBoundaryStore _boundary;
    private readonly RuntimeMetadataRegistry _metadataRegistry;
    private readonly CombatStore _combat;
    private readonly MechanicStore _mechanics;
    private readonly ResourceStore _resources;
    private readonly ICombatOccurrenceObserver? _combatOccurrenceObserver;
    private readonly IAuraLifecycleObserver? _auraLifecycleObserver;
    private readonly CombatContributionPathResolver _contributionPathResolver = new(CombatContributionPath.ProductionFallback);
    private readonly SystemPeriodicRecoveryCanonicalizer _systemPeriodicRecovery = new();
    private readonly PeriodicPoolCanonicalizer _periodicPool = new();
    private readonly CompactDirectValueCanonicalizer _compactDirectValue = new();
    private readonly OwnerTargetSummonResourceCanonicalizer _ownerTargetSummonResource;
    private readonly CompactAvoidanceCanonicalizer _compactAvoidance = new();
    private readonly EntityVitalStore _entityVitals = new();
    private readonly EffectiveHealingTracker _effectiveHealing = new();
    private readonly AuraStore _auras = new();
    private readonly BossFocusStore _bossFocus;
    private readonly BossFightTracker _bossFights;
    private readonly StaggerAttributor _stagger = new();
    private readonly KillTracker _kills = new();
    private readonly Func<int, int> _killerOf;
    // The player's credited kills waiting for the overlay to know who the player is.
    private int _unassignedKillCredits;
    private readonly Func<int, bool> _bossFocusActivitySourcePredicate;

    public DomainEventApplier(EntityStore entities, SceneBoundaryStore boundary, RuntimeMetadataRegistry metadataRegistry, CombatStore combat, ICombatOccurrenceObserver? combatOccurrenceObserver = null, IAuraLifecycleObserver? auraLifecycleObserver = null)
    {
        _entities = entities;
        _boundary = boundary;
        _metadataRegistry = metadataRegistry;
        _combat = combat;
        _mechanics = new MechanicStore();
        _resources = new ResourceStore();
        _combatOccurrenceObserver = combatOccurrenceObserver;
        _auraLifecycleObserver = auraLifecycleObserver;
        _ownerTargetSummonResource = new OwnerTargetSummonResourceCanonicalizer(entities);
        _bossFocus = new BossFocusStore(entities, _entityVitals);
        _bossFights = new BossFightTracker(entities, _entityVitals, boundary, metadataRegistry);
        _bossFocusActivitySourcePredicate = IsBossFocusActivitySource;
        _killerOf = KillerOf;
    }

    public DomainEventApplier(EntityStore entities, SceneBoundaryStore boundary, CombatStore combat)
        : this(entities, boundary, new RuntimeMetadataRegistry(), combat)
    {
    }

    public DomainEventApplier(EntityStore entities, SceneBoundaryStore boundary, CombatStore combat, ICombatOccurrenceObserver combatOccurrenceObserver)
        : this(entities, boundary, new RuntimeMetadataRegistry(), combat, combatOccurrenceObserver)
    {
    }

    public DomainEventApplier(EntityStore entities, SceneBoundaryStore boundary, CombatStore combat, ISceneEventObserver sceneEventObserver)
        : this(entities, boundary, new RuntimeMetadataRegistry(), combat, sceneEventObserver, sceneEventObserver)
    {
    }

    public EntityStore Entities => _entities;
    public SceneBoundaryStore Boundary => _boundary;
    public RuntimeMetadataRegistry MetadataRegistry => _metadataRegistry;
    public CombatStore Combat => _combat;
    public MechanicStore Mechanics => _mechanics;
    public ResourceStore Resources => _resources;
    public EntityVitalStore EntityVitals => _entityVitals;
    public AuraStore Auras => _auras;
    public BossFocusStore BossFocus => _bossFocus;
    internal BossFightTracker BossFights => _bossFights;
    public bool TrackBossFocus { get; set; } = true;
    private CombatantStatisticsScope _combatantStatisticsScope = CombatantStatisticsScope.All;

    public CombatantStatisticsScope CombatantStatisticsScope
    {
        get => _combatantStatisticsScope;
        set
        {
            if (_combatantStatisticsScope == value)
                return;

            _combatantStatisticsScope = value;
            _bossFocus.ReconcileActivity(ResolveBossCombatActivityObservedAt);
        }
    }

    public void ApplyJournal(ObservedEventJournal journal)
    {
        var count = journal.Count;
        if (count == 0)
            return;

        _combat.EnsureCapacity(count);
        _mechanics.EnsureCapacity(count);
        _resources.EnsureCapacity(count);
        var cursor = journal.CreateCursor(0);
        while (true)
        {
            var result = journal.ReadEntries(cursor, 256, entries =>
            {
                for (var i = 0; i < entries.Count; i++)
                    ApplyEntry(entries[i]);
            });
            if (result.Count == 0)
                break;

            cursor = result.Cursor;
        }

        FlushPendingOutcomeSidecars();
        _effectiveHealing.Resolve(_combat, _bossFights);
        ResolveKills();
    }

    public DomainEventMaterialization ApplyEntry(ObservedEventEntry entry)
    {
        var observedAtMilliseconds = entry.ObservedAtMilliseconds;
        var auraLifecycle = default(AuraLifecycleTransition);
        switch (entry.Domain)
        {
            case ObservedEventDomain.Combat:
                ApplyCombat(entry, in entry.Combat);
                break;
            case ObservedEventDomain.State:
                ApplyState(entry, in entry.State);
                break;
            case ObservedEventDomain.EntityVital:
                var hpBefore = _entityVitals.TryGet(entry.EntityVital.EntityId, out var before) ? before.CurrentHp : long.MaxValue;
                var vital = _entityVitals.Apply(entry);
                if (hpBefore == 1 && vital.CurrentHp > 1)
                    RecognizeTrainingDummy(vital.EntityId);
                _bossFights.ObserveVital(in vital, hpBefore);
                // Party/force status re-sends HP on a ~500 ms beat and often still shows the HP from before a heal.
                if (entry.Opcode is not (0x1B92 or 0x2B96))
                {
                    _effectiveHealing.ObserveVital(vital.EntityId, vital.CurrentHp, entry.Stamp.ObservationOrdinal, observedAtMilliseconds);
                    if (IsNpc(vital.EntityId))
                        _kills.ObserveHp(vital.EntityId, vital.CurrentHp, entry.Stamp.ObservationOrdinal, observedAtMilliseconds);
                }
                if (vital.CurrentHp == 0)
                    _entities.ApplyBattleToggle(vital.EntityId, false);
                ApplyBossFocusNpcHp(vital.EntityId, vital.CurrentHp, vital.MaxHp ?? 0, observedAtMilliseconds);
                TryApplyBossCombatActivity(vital.EntityId);
                break;
            case ObservedEventDomain.Scene:
                ApplyScene(in entry.Scene);
                break;
            case ObservedEventDomain.Aura:
                auraLifecycle = _auras.Apply(entry);
                ObserveAuraLifecycle(entry, in auraLifecycle);
                ApplyAura(in entry.Aura);
                break;
            case ObservedEventDomain.Action:
                auraLifecycle = _auras.Apply(entry);
                ObserveAuraLifecycle(entry, in auraLifecycle);
                break;
        }

        return new DomainEventMaterialization(auraLifecycle);
    }

    public void CompleteFlush()
    {
        FlushPendingOutcomeSidecars();
        _effectiveHealing.Resolve(_combat, _bossFights);
        ResolveKills();
    }

    public void FlushPendingOutcomeSidecars()
    {
        foreach (var result in _compactDirectValue.FlushPending())
            ApplyStampedCombatResult(in result);

        foreach (var result in _compactAvoidance.FlushPending())
            ApplyStampedCombatResult(in result);
    }

    private void ApplyCombat(ObservedEventEntry entry, in CombatWireObservation combatObservation)
    {
        ObserveFight(entry.SourceEntityId, entry.TargetEntityId, in combatObservation);
        var stamp = entry.Stamp;
        var observedAtMilliseconds = stamp.OffsetTicks / TimeSpan.TicksPerMillisecond;
        if (entry.Opcode == 0x0238)
        {
            CountAggroCast(entry.SourceEntityId, in combatObservation, observedAtMilliseconds);
            var controlResults = _compactDirectValue.ObserveCompactControl0238(entry.SourceEntityId, in combatObservation);
            if (controlResults.Count > 0)
            {
                ApplyStampedCombatResults(controlResults);
            }

            return;
        }

        if (entry.Opcode == 0x0638)
        {
            ApplyStampedCombatResults(_compactDirectValue.ObserveCompactControl0638(entry.SourceEntityId, in combatObservation));
            return;
        }

        if (entry.Opcode == 0x0438)
        {
            var sidecarResults = _compactDirectValue.ObserveCompactValueSidecar0438(entry.SourceEntityId, entry.TargetEntityId, in combatObservation);
            if (sidecarResults.Count > 0)
            {
                ApplyStampedCombatResults(sidecarResults);
            }
        }

        if (entry.Opcode == 0x0438 &&
            _compactDirectValue.TryObserveCompactValue0438(entry.SourceEntityId, entry.TargetEntityId, in stamp, in combatObservation, observedAtMilliseconds, entry.Raw, out var compactResults))
        {
            if (compactResults.Count == 0)
                return;

            ApplyStampedCombatResults(compactResults);
            return;
        }

        var rawResults = entry.Opcode switch
        {
            0x0438 => _compactAvoidance.ObserveCompactValue0438(entry.SourceEntityId, entry.TargetEntityId, in stamp, in combatObservation, observedAtMilliseconds, entry.Raw),
            0x0238 => StampedCombatCanonicalizationBatch.Empty,
            0x0638 => StampedCombatCanonicalizationBatch.Empty,
            _ => _compactAvoidance.NormalizeCombat(entry.SourceEntityId, entry.TargetEntityId, in stamp, in combatObservation, observedAtMilliseconds, entry.Raw)
        };

        ApplyStampedCombatResults(rawResults);
    }

    private void ApplyStampedCombatResults(StampedCombatCanonicalizationBatch results)
    {
        if (results.Count == 0)
            return;

        foreach (var result in results)
            ApplyStampedCombatResult(in result);
    }

    private void ApplyStampedCombatResult(in StampedCombatCanonicalizationResult rawResult)
    {
        var observation = rawResult.Observation;
        var resultStamp = rawResult.Stamp;
        var result = new CombatCanonicalizationResult(rawResult.SourceId, rawResult.TargetId, observation, rawResult.Resolution);
        ApplyCanonicalizedCombatResult(in resultStamp, in result, rawResult.ObservedAtMilliseconds, rawResult.Raw);
    }

    private void ApplyCanonicalizedCombatResult(in TimelineStamp stamp, in CombatCanonicalizationResult result, long observedAtMilliseconds, RawPacketReference raw)
    {
        var resultObservation = result.Observation;
        var resultResolution = result.Resolution;
        var ownerTargetSummonResourceResult = _ownerTargetSummonResource
            .Normalize(result.SourceId, result.TargetId, in resultObservation)
            .Inherit(in resultResolution);
        var observation = ownerTargetSummonResourceResult.Observation;
        var ownerTargetResolution = ownerTargetSummonResourceResult.Resolution;
        var systemRecoveryResult = _systemPeriodicRecovery
            .Normalize(ownerTargetSummonResourceResult.SourceId, ownerTargetSummonResourceResult.TargetId, in observation)
            .Inherit(in ownerTargetResolution);
        var systemRecoveryObservation = systemRecoveryResult.Observation;
        var systemRecoveryResolution = systemRecoveryResult.Resolution;
        foreach (var normalized in _periodicPool.Normalize(systemRecoveryResult.SourceId, systemRecoveryResult.TargetId, in systemRecoveryObservation))
        {
            var final = normalized.Inherit(in systemRecoveryResolution);
            ApplyCombatResult(in final, observedAtMilliseconds, in stamp, raw);
        }
    }

    private void ApplyCombatResult(in CombatCanonicalizationResult result, long observedAtMilliseconds, in TimelineStamp stamp, RawPacketReference raw)
    {
        var occurrence = result.Resolution;
        var observation = result.Observation;
        var materialization = ResolveCombatOccurrence(result.SourceId, result.TargetId, in observation, in occurrence, observedAtMilliseconds, in stamp, raw);
        if (!materialization.IsAdmitted)
            return;

        ApplyMaterialization(
            result.SourceId,
            result.TargetId,
            in observation,
            in materialization,
            observedAtMilliseconds,
            stamp.ObservationOrdinal,
            raw,
            applyClassEvidence: true);

        MaterializeSecondaryContributions(in result, observedAtMilliseconds, in stamp, raw);
    }

    private void ApplyMaterialization(
        int sourceId,
        int targetId,
        in CombatWireObservation observation,
        in CombatOccurrenceMaterialization materialization,
        long observedAtMilliseconds,
        long sourceObservationOrdinal,
        RawPacketReference raw,
        bool applyClassEvidence)
    {
        if (!materialization.HasAny)
            return;

        if (materialization.Mechanic is { } mechanic)
            _mechanics.Apply(sourceId, targetId, in observation, in mechanic, observedAtMilliseconds, sourceObservationOrdinal, raw);

        if (materialization.Resource is { } resource)
            _resources.Apply(sourceId, targetId, in observation, in resource, observedAtMilliseconds, sourceObservationOrdinal, raw);

        if (materialization.Contribution is { } contribution)
        {
            if (applyClassEvidence)
            {
                _entities.ApplyCharacterClassEvidence(sourceId, in observation, in contribution);
                LinkUnnamedSummon(sourceId);
            }
            var eventIndex = _combat.ApplyCombat(sourceId, targetId, in observation, in contribution, observedAtMilliseconds, sourceObservationOrdinal, raw);
            _effectiveHealing.ObserveCombat(targetId, in contribution, sourceObservationOrdinal, observedAtMilliseconds, eventIndex);
            _bossFights.ObserveCombat(sourceId, targetId, in observation, in contribution, observedAtMilliseconds, sourceObservationOrdinal, raw, eventIndex);
            if (contribution.Metric == CombatMetricKind.Damage)
            {
                _stagger.ObserveHit(targetId, sourceId, observation.SkillCode, eventIndex, observedAtMilliseconds);
                if (IsNpc(targetId))
                    _kills.ObserveHit(targetId, sourceId, contribution.Amount, sourceObservationOrdinal, observedAtMilliseconds);
            }
        }

        ApplyBossCombatActivity(sourceId, targetId, observedAtMilliseconds);
    }

    private void MaterializeSecondaryContributions(in CombatCanonicalizationResult result, long observedAtMilliseconds, in TimelineStamp stamp, RawPacketReference raw)
    {
        var observation = result.Observation;
        if (observation.DrainHealAmount > 0 && result.SourceId > 0 && result.SourceId != result.TargetId)
        {
            var drain = observation with
            {
                Damage = observation.DrainHealAmount,
                HitCount = 0,
                AttemptCount = 0,
                DrainHealAmount = 0,
                RegenerationAmount = 0,
                ResourceKind = CombatResourceKind.Unknown,
                Modifiers = DamageModifiers.None,
                PeriodicRelation = PeriodicEffectRelation.None,
                PeriodicMode = 0
            };
            var drainOccurrence = new CombatOccurrenceResolution(
                CombatPacketRule.DrainSecondary,
                CombatMaterializationKind.DrainSecondary,
                CombatAssociationKind.None,
                CombatSuppressionReason.None);
            var drainMaterialization = ResolveCombatOccurrence(result.SourceId, result.SourceId, in drain, in drainOccurrence, observedAtMilliseconds, in stamp, raw);
            ApplyMaterialization(result.SourceId, result.SourceId, in drain, in drainMaterialization, observedAtMilliseconds, stamp.ObservationOrdinal, raw, applyClassEvidence: false);
        }

        if (observation.RegenerationAmount <= 0 || result.TargetId <= 0 || IsSummon(result.TargetId))
            return;

        var regeneration = observation with
        {
            Damage = observation.RegenerationAmount,
            HitCount = 0,
            AttemptCount = 0,
            DrainHealAmount = 0,
            RegenerationAmount = 0,
            ResourceKind = CombatResourceKind.Unknown,
            Modifiers = DamageModifiers.None,
            PeriodicRelation = PeriodicEffectRelation.None,
            PeriodicMode = 0
        };
        var regenerationOccurrence = new CombatOccurrenceResolution(
            CombatPacketRule.RegenerationSecondary,
            CombatMaterializationKind.RegenerationSecondary,
            CombatAssociationKind.None,
            CombatSuppressionReason.None);
        var regenerationMaterialization = ResolveCombatOccurrence(result.TargetId, result.TargetId, in regeneration, in regenerationOccurrence, observedAtMilliseconds, in stamp, raw);
        ApplyMaterialization(result.TargetId, result.TargetId, in regeneration, in regenerationMaterialization, observedAtMilliseconds, stamp.ObservationOrdinal, raw, applyClassEvidence: false);
    }

    private CombatOccurrenceMaterialization ResolveCombatOccurrence(
        int sourceId,
        int targetId,
        in CombatWireObservation observation,
        in CombatOccurrenceResolution occurrence,
        long observedAtMilliseconds,
        in TimelineStamp stamp,
        RawPacketReference raw)
    {
        var materialization = CombatOccurrenceMaterializer.Resolve(sourceId, targetId, in observation, in occurrence, _contributionPathResolver);
        if (_combatOccurrenceObserver is { } observer)
        {
            var context = new CombatOccurrenceContext(
                sourceId,
                targetId,
                observation,
                occurrence,
                observedAtMilliseconds,
                stamp.ObservationOrdinal,
                stamp.FlushId,
                raw,
                materialization);
            observer.Observe(in context);
        }

        return materialization;
    }

    // A drop of the gauge goes to the hits that caused it; a break (max 0) empties what was left. A gauge seen for the
    // first time was full before that update.
    private void ApplyStaggerGauge(int entityId, long current, long max, long observedAtMilliseconds)
    {
        var known = _bossFocus.TryGetStaggerGauge(entityId, out var gauge);
        var broke = max == 0;
        if (broke)
            max = known ? gauge.Max : 0;

        var before = known && gauge.Max == max ? gauge.Current : max;
        if (before > current)
        {
            foreach (var credit in _stagger.Attribute(entityId, before - current, observedAtMilliseconds))
            {
                _combat.CreditStagger(credit.EventIndex, credit.Points);
                _bossFights.ObserveStagger(entityId, credit.SourceId, credit.Points, observedAtMilliseconds);
            }
        }

        if (broke)
            _bossFights.ObserveStaggerBreak(entityId);
        _bossFocus.ApplyStaggerGauge(entityId, current, max);
    }

    // Since a game patch (Nov 2026) some summons arrive without their owner (Elementalist spirits, Sorcerer Bittercold
    // Wind and Fire Wall, mostly at a pull), and their skills make them look like nameless players of the owner's class.
    // An unnamed entity whose skills show a class belongs to the only known player of that class. With two, only its spawn
    // could tell whose it is, and none came (the user was out of sight, at the Fire Temple's first pull): a summon of
    // nobody, which shows in no row (its damage goes unattributed).
    // ponytail: an unnamed stranger of a group member's class is taken for a summon too; link by spawn position if it matters.
    private void LinkUnnamedSummon(int sourceId)
    {
        if (!_entities.TryGet(sourceId, out var entity) ||
            entity.IsPlayer ||
            entity.OwnerEntityId.HasValue ||
            entity.Kind != NpcKind.Unknown ||
            entity.CharacterClass is not { } characterClass ||
            characterClass == CharacterClass.None ||
            _metadataRegistry.TryGetPcMetadata(sourceId, out _))
        {
            return;
        }

        var ownerId = 0;
        foreach (var (playerId, player) in _metadataRegistry.PcMetadataByEntityId)
        {
            var playerClass = player.CharacterClass ?? (_entities.TryGet(playerId, out var playerEntity) ? playerEntity.CharacterClass : null);
            if (playerClass != characterClass)
                continue;

            if (ownerId != 0)
            {
                _entities.ApplySummon(null, sourceId);
                return;
            }

            ownerId = playerId;
        }

        if (ownerId != 0)
            _entities.ApplySummon(ownerId, sourceId);
    }

    // Who fought whom, for the sides (RuntimeMetadataRegistry.IsHostile): a hit the game data calls damage (as the group
    // frame's shields go by) between two players the overlay knows by then, so by the players themselves; a summon's, or one
    // of an unnamed player taken for a summon (LinkUnnamedSummon), doesn't count.
    // ponytail: a hit before the overlay knows both as players is lost (an enemy named after their first hits counts from their
    // next); keep the hits between ids of no known kind and judge them when asked, should enemies go unmarked for it.
    private void ObserveFight(int sourceId, int targetId, in CombatWireObservation observation)
    {
        if (observation.Damage > 0 && sourceId != targetId &&
            _metadataRegistry.TryGetPcMetadata(targetId, out _) && _metadataRegistry.TryGetPcMetadata(sourceId, out _) &&
            CombatSemanticEvidenceResolver.Evaluate(in observation).Candidate is { Metric: CombatMetricKind.Damage })
            _metadataRegistry.ObserveFight(sourceId, targetId);
    }

    private bool IsSummon(int entityId) =>
        _entities.TryGet(entityId, out var entity) &&
        (entity.OwnerKind == EntityOwnerKind.Summon || entity.Kind == NpcKind.Summon);

    // An NPC as the kill columns count them: no player (by name or kind) and no player's summon (EntityStore.IsPlayersSummon,
    // as the meter has it). Only an enemy player (PvP) can make a kill of a player's summon whose owner went unseen and whose
    // code lies outside EntityStore.PlayersSummonCodes (KillerOf).
    private bool IsNpc(int entityId) =>
        entityId > 0 && !_metadataRegistry.TryGetPcMetadata(entityId, out _) &&
        !(_entities.TryGet(entityId, out var entity) && entity.IsPlayer) && !_entities.IsPlayersSummon(entityId, _metadataRegistry);

    private bool IsKnownNpc(int entityId) => _entities.IsKnownNpc(entityId, _metadataRegistry);

    // A summon's hits are its owner's, as the snapshot has it (SceneCombatSnapshotAdapter.ResolveCombatantIdCached).
    private int OwnerOf(int sourceId) =>
        _entities.TryGet(sourceId, out var source) && source.OwnerEntityId is int ownerId and > 0 ? ownerId : sourceId;

    // Whom a source's hits make the killer, decided at the kill (an owner linked after the hits counts): a player, a
    // summon's owner; no one (0) for an NPC, one of its adds or a summon of no known owner, whose hits only take HP off.
    private int KillerOf(int sourceId) => OwnerOf(sourceId) is > 0 and var killerId && !IsKnownNpc(killerId) ? killerId : 0;

    private void ResolveKills()
    {
        foreach (var kill in _kills.Resolve(_killerOf))
        {
            if (IsNpc(kill.NpcId))
                CountKills(kill.KillerId, new KillCounts(Kills: 1), kill.ObservedAtMilliseconds);
        }
    }

    // The player's credited kills, once the overlay knows who the player is: one started mid-session learns it within
    // seconds, and the credits before wait for it.
    private void AssignKillCredits(long observedAtMilliseconds)
    {
        if (_unassignedKillCredits > 0 && _metadataRegistry.LocalPlayerEntityId is > 0 and var me)
        {
            CountKills(me, new KillCounts(Credited: _unassignedKillCredits), observedAtMilliseconds);
            _unassignedKillCredits = 0;
        }
    }

    private void CountKills(int combatantId, KillCounts counts, long observedAtMilliseconds)
    {
        _combat.AddKillCounts(combatantId, counts);
        _bossFights.ObserveKillCounts(combatantId, counts, observedAtMilliseconds);
    }

    // A 0x0238 cast names its target in ChainId. Flag 2 marks the casts an enemy aims (attacks, targeted skills);
    // flag 0 are follow-ups, e.g. a mechanic's sub-casts that walk through every player.
    private void CountAggroCast(int sourceId, in CombatWireObservation cast, long observedAtMilliseconds)
    {
        if (cast.Flag == 2 &&
            _metadataRegistry.TryGetPcMetadata(cast.ChainId, out _) &&
            !_metadataRegistry.TryGetPcMetadata(sourceId, out _) &&
            !IsSummon(sourceId))
        {
            _combat.CountAggroCast(sourceId, cast.ChainId);
            _bossFights.ObserveAggroCast(sourceId, cast.ChainId, observedAtMilliseconds);
        }
    }

    private void ObserveAuraLifecycle(ObservedEventEntry entry, in AuraLifecycleTransition transition)
    {
        if (_auraLifecycleObserver is not { } observer)
            return;

        var stamp = entry.Stamp;
        var context = entry.Domain == ObservedEventDomain.Aura
            ? new AuraLifecycleObservationContext(
                AuraLifecycleSourceKind.Aura,
                entry.SourceEntityId,
                entry.TargetEntityId,
                entry.Aura,
                default,
                transition,
                entry.ObservedAtMilliseconds,
                stamp.ObservationOrdinal,
                stamp.FlushId,
                entry.Raw)
            : new AuraLifecycleObservationContext(
                AuraLifecycleSourceKind.Action,
                entry.SourceEntityId,
                entry.TargetEntityId,
                default,
                entry.Action,
                transition,
                entry.ObservedAtMilliseconds,
                stamp.ObservationOrdinal,
                stamp.FlushId,
                entry.Raw);
        observer.Observe(in context);
    }

    private void ApplyAura(in AuraObservation aura)
    {
        if (aura.Kind == AuraObservationKind.Result && aura.EntityId > 0 && aura.InstanceSequenceId > 0)
            _entities.ApplyNpc2C38State(aura.EntityId, aura.InstanceSequenceId, aura.ResultCode);
    }

    private void ApplyScene(in SceneObservation scene)
    {
        _boundary.ApplySceneObservation(in scene);

        switch (scene.Kind)
        {
            case SceneObservationKind.CurrentMap:
            case SceneObservationKind.DestinationMapArrival:
            case SceneObservationKind.MapEventRegistered:
                _metadataRegistry.UpsertMapCode(_boundary.CurrentMapInstanceId, _boundary.CurrentMapId);
                break;
            case SceneObservationKind.MapContextStarted:
            case SceneObservationKind.TransportStreamActivated:
                _compactDirectValue.ResetPendingAssociations();
                _metadataRegistry.UpsertMapCode(_boundary.CurrentMapInstanceId, _boundary.CurrentMapId);
                break;
        }
    }

    private void ApplyState(ObservedEventEntry entry, in StateObservation state)
    {
        if (entry.TargetEntityId != 0 && state.EntityId == entry.TargetEntityId && entry.SourceEntityId != entry.TargetEntityId)
        {
            _entities.ApplySummon(entry.SourceEntityId, entry.TargetEntityId);
            ReconcileBossFocusActivity();
            return;
        }

        if (state.StateCode == StateCodes.PlayerIdentity)
        {
            if (entry.SourceEntityId > 0)
            {
                var nickname = state.Text ?? string.Empty;
                var metadataClass = state.CharacterClass is CharacterClass.None ? null : state.CharacterClass;
                if (!string.IsNullOrWhiteSpace(nickname))
                    _entities.ApplyNickname(entry.SourceEntityId, nickname);
                else
                    _entities.ApplyPlayerIdentity(entry.SourceEntityId);
                if (metadataClass is { } characterClass)
                    _entities.ApplyMetadataCharacterClass(entry.SourceEntityId, characterClass);
                // A name (or being the user) shows a player taken for a summon before they were known (LinkUnnamedSummon).
                if (state.IsLocalPlayer || !string.IsNullOrWhiteSpace(nickname))
                    _entities.ClearSummon(entry.SourceEntityId);

                var legionName = state.LegionName ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(nickname) || metadataClass is not null || state.IsLocalPlayer || state.OriginServerId is not null || !string.IsNullOrWhiteSpace(legionName))
                    _metadataRegistry.UpsertPcMetadata(entry.SourceEntityId, nickname, state.Faction, metadataClass, state.IsLocalPlayer, state.OriginServerId, legionName);
                AssignKillCredits(entry.ObservedAtMilliseconds);
            }
            ReconcileBossFocusActivity();
            return;
        }

        if (state.StateCode == StateCodes.PlayerGroupMembership)
        {
            if (entry.SourceEntityId > 0 && state.GroupMembership.IsKnown)
            {
                // Party and force status name only players: one taken for a summon (LinkUnnamedSummon) is a player after all.
                _entities.ClearSummon(entry.SourceEntityId);
                _metadataRegistry.UpsertPlayerGroupMembership(entry.SourceEntityId, state.GroupMembership);
            }
            else if (state.GroupMembership.IsKnown && state.OriginServerId is > 0 && !string.IsNullOrWhiteSpace(state.Text))
            {
                _metadataRegistry.UpsertPlayerGroupProfile(state.OriginServerId.Value, state.Text, state.GroupMembership);
            }
            ReconcileBossFocusActivity();
            return;
        }

        if (state.StateCode is StateCodes.Cooldown4738 or StateCodes.CooldownStart0238 or StateCodes.CooldownCharge2238 or StateCodes.Ping or StateCodes.SkillLevel)
            return;

        if (state.StateCode == StateCodes.BossTarget)
        {
            _bossFights.ObserveBossTarget(state.EntityId, entry.Stamp.OffsetTicks / TimeSpan.TicksPerMillisecond);
            return;
        }

        if (state.StateCode == StateCodes.LocalizedNpcName)
        {
            return;
        }

        if (state.StateCode == StateCodes.StaggerGauge)
        {
            ApplyStaggerGauge(state.EntityId, state.Value0, state.Value1, entry.ObservedAtMilliseconds);
            return;
        }

        // The game's kill message names who landed the killing blow. It counts for an NPC the overlay saw die (it comes
        // ~50 ms after the 0 HP update), which also keeps out other messages of its opcode.
        if (state.StateCode == StateCodes.Kill)
        {
            if (state.Value0 is > 0 and <= int.MaxValue && IsNpc(state.EntityId) &&
                _entityVitals.TryGet(state.EntityId, out var killed) && killed.CurrentHp == 0)
                CountKills((int)state.Value0, new KillCounts(LastHits: 1), entry.ObservedAtMilliseconds);
            return;
        }

        // The game's count of the player's credited kills: a frame each time it goes up, mostly by one, with the count before
        // it (Value1, -1: the first the capture saw; it sees the frames a waiting boss scene leaves out too). More is frames
        // the capture missed, the same value a repeat; a jump down or far up (seen once, July 2026) counts one, as does the
        // first frame.
        // ponytail: "far" is more than 10; steps of 2-3 seen.
        if (state.StateCode == StateCodes.KillCredit)
        {
            var step = state.Value0 - state.Value1;
            _unassignedKillCredits += state.Value1 < 0 ? 1 : step == 0 ? 0 : step is > 0 and <= 10 ? (int)step : 1;
            AssignKillCredits(entry.ObservedAtMilliseconds);
            return;
        }

        if (state.StateCode == StateCodes.NpcKind)
        {
            var kind = state.Value0 is >= int.MinValue and <= int.MaxValue && Enum.IsDefined((NpcKind)(int)state.Value0) ? (NpcKind)(int)state.Value0 : NpcKind.Unknown;
            _entities.ApplyNpcKind(state.EntityId, kind);
            ApplyBossFocusNpcKind(state.EntityId, kind, entry.Stamp.OffsetTicks / TimeSpan.TicksPerMillisecond);
            TryApplyBossCombatActivity(state.EntityId);
            return;
        }

        if (state.StateCode == StateCodes.NpcBattle)
        {
            var isActive = state.Value0 != 0 && CanNpcBattleActivate(state.EntityId);
            _entities.ApplyBattleToggle(state.EntityId, isActive);
            if (TrackBossFocus && CombatantStatisticsScope == CombatantStatisticsScope.All)
                _bossFocus.ApplyBattle(
                    state.EntityId,
                    isActive,
                    entry.Stamp.OffsetTicks / TimeSpan.TicksPerMillisecond);
            return;
        }

        if (state.StateCode == StateCodes.NpcBattleToggle)
        {
            var isActive = !_entities.GetOrAdd(state.EntityId).NpcCombatActive && CanNpcBattleActivate(state.EntityId);
            _entities.ApplyBattleToggle(state.EntityId, isActive);
            if (TrackBossFocus && CombatantStatisticsScope == CombatantStatisticsScope.All)
                _bossFocus.ApplyBattleToggle(
                    state.EntityId,
                    isActive,
                    entry.Stamp.OffsetTicks / TimeSpan.TicksPerMillisecond);
            return;
        }

        if (state.StateCode is >= 2_000_000 and <= 2_999_999)
        {
            _entities.ApplyNpcCode(state.EntityId, state.StateCode);
            _metadataRegistry.UpsertNpcCode(state.EntityId, state.StateCode);
            _bossFights.ObserveNpcCode(state.EntityId, state.StateCode, entry.ObservedAtMilliseconds);
            return;
        }

        if (state.StateCode == 2136)
        {
            _entities.ApplyNpc2136State(state.EntityId, state.Value0, state.Value1);
            return;
        }

        if (state.StateCode == 140)
        {
            _entities.ApplyNpc0140Value(state.EntityId, state.Value0);
            return;
        }

        if (state.StateCode == 240)
        {
            _entities.ApplyNpc0240Value(state.EntityId, state.Value0);
            return;
        }

        if (state.StateCode == 4636)
        {
            _entities.ApplyNpc4636State(state.EntityId, checked((byte)state.Value0), checked((byte)state.Value1));
            return;
        }

        _ = _entities.GetOrAdd(state.EntityId);
    }

    private bool CanNpcBattleActivate(int instanceId) => !_entityVitals.TryGet(instanceId, out var vital) || vital.CurrentHp != 0;

    private void ApplyBossFocusNpcKind(int instanceId, NpcKind kind, long observedAtMilliseconds)
    {
        if (!TrackBossFocus)
            return;

        if (CombatantStatisticsScope == CombatantStatisticsScope.All)
            _bossFocus.ApplyNpcKind(instanceId, kind, observedAtMilliseconds);
        else
            _bossFocus.ApplyNpcKindState(instanceId, kind, observedAtMilliseconds);
    }

    private void ApplyBossFocusNpcHp(int instanceId, long hp, long maxHp, long observedAtMilliseconds)
    {
        if (!TrackBossFocus)
            return;

        if (CombatantStatisticsScope == CombatantStatisticsScope.All)
            _bossFocus.ApplyNpcHp(instanceId, hp, maxHp, observedAtMilliseconds);
        else
            _bossFocus.ApplyNpcHpState(instanceId, hp, maxHp);
    }

    private void TryApplyBossCombatActivity(int instanceId)
    {
        if (!TrackBossFocus)
            return;

        if (!IsEligibleBossFocusTarget(instanceId) ||
            ResolveBossCombatActivityObservedAt(instanceId) is not long activityObservedAtMilliseconds)
        {
            return;
        }

        _bossFocus.ApplyCombatActivity(instanceId, activityObservedAtMilliseconds, activityObservedAtMilliseconds);
    }

    private long? ResolveBossCombatActivityObservedAt(int instanceId)
    {
        if (!IsFocusTargetInstance(instanceId))
            return null;

        var hasActivity = _combat.TryGetLastCombatActivityObservedAt(instanceId, _bossFocusActivitySourcePredicate, out var activityObservedAtMilliseconds);
        if (_mechanics.TryGetLastCombatActivityObservedAt(instanceId, _bossFocusActivitySourcePredicate, out var mechanicActivityObservedAtMilliseconds))
        {
            activityObservedAtMilliseconds = hasActivity
                ? Math.Max(activityObservedAtMilliseconds, mechanicActivityObservedAtMilliseconds)
                : mechanicActivityObservedAtMilliseconds;
            hasActivity = true;
        }

        return hasActivity ? activityObservedAtMilliseconds : null;
    }

    private void ReconcileBossFocusActivity() => _bossFocus.ReconcileActivity(ResolveBossCombatActivityObservedAt);

    private void ApplyBossCombatActivity(int sourceId, int targetId, long observedAtMilliseconds)
    {
        if (!TrackBossFocus)
            return;

        if (IsEligibleBossFocusTarget(sourceId) && IsBossFocusActivitySource(targetId))
        {
            _bossFocus.ApplyCombatActivity(sourceId, observedAtMilliseconds, observedAtMilliseconds);
        }

        if (IsEligibleBossFocusTarget(targetId) && IsBossFocusActivitySource(sourceId))
        {
            _bossFocus.ApplyCombatActivity(targetId, observedAtMilliseconds, observedAtMilliseconds);
        }
    }

    // An NPC back up from exactly 1 HP is a training dummy: one can't die, it stops at 1 HP and is full again a moment later
    // (0.1-0.55 s seen). The game data names them (TrainingDummy), but only from their spawn: this is for one that spawned
    // before the overlay saw it. NPCs the game data knows, players and summons keep what they are.
    // ponytail: a single refill decides; ask for two if an NPC healed from exactly 1 HP ever shows up as a dummy.
    private void RecognizeTrainingDummy(int entityId)
    {
        if ((!_entities.TryGet(entityId, out var entity) ||
             entity is { Kind: NpcKind.Unknown, NpcCode: null, IsPlayer: false, OwnerEntityId: null, CharacterClass: null or CharacterClass.None }) &&
            !_metadataRegistry.TryGetPcMetadata(entityId, out _))
        {
            _entities.ApplyNpcKind(entityId, NpcKind.TrainingDummy);
        }
    }

    internal bool IsBossFocusActivitySource(int instanceId)
    {
        if (instanceId <= 0)
            return false;

        var currentId = instanceId;
        for (var depth = 0; depth < 4; depth++)
        {
            if (_metadataRegistry.TryGetPcMetadata(currentId, out var metadata))
                return IsInBossFocusActivityScope(in metadata);

            if (!_entities.TryGet(currentId, out var entity))
                return CombatantStatisticsScope == CombatantStatisticsScope.All;

            if (entity.IsPlayer || entity.CharacterClass is not null and not CharacterClass.None)
                return CombatantStatisticsScope == CombatantStatisticsScope.All;

            // As IsSummon: a summon keeps its owner when a later kind (an NPC kind state) overwrote Summon.
            if ((entity.Kind == NpcKind.Summon || entity.OwnerKind == EntityOwnerKind.Summon) && entity.OwnerEntityId is int ownerId && ownerId > 0 && ownerId != currentId)
            {
                currentId = ownerId;
                continue;
            }

            return CombatantStatisticsScope == CombatantStatisticsScope.All && entity.Kind == NpcKind.Unknown && entity.NpcCode is null;
        }

        return false;
    }

    private bool IsFocusTargetInstance(int instanceId) =>
        instanceId > 0 &&
        _entities.TryGet(instanceId, out var entity) &&
        BossModeFocusTargets.IsFocusTarget(entity.Kind);

    private bool IsEligibleBossFocusTarget(int instanceId) =>
        IsFocusTargetInstance(instanceId) &&
        (!_entityVitals.TryGet(instanceId, out var vital) || vital.CurrentHp != 0);

    private bool IsInBossFocusActivityScope(in PcMetadata metadata) =>
        CombatantStatisticsScope switch
        {
            CombatantStatisticsScope.All => true,
            CombatantStatisticsScope.Self => metadata.IsLocalPlayer,
            CombatantStatisticsScope.Party => metadata.IsLocalPlayer || metadata.GroupRelation == PlayerGroupRelation.PartyMember,
            CombatantStatisticsScope.Force => metadata.IsLocalPlayer || metadata.GroupRelation is PlayerGroupRelation.PartyMember or PlayerGroupRelation.ForceMember,
            _ => false
        };
}

public readonly record struct DomainEventMaterialization(AuraLifecycleTransition AuraLifecycle);
