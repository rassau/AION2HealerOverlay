using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.SceneRuntime.Projection;

internal sealed class SceneProjectionState
{
    // How far past the latest entry the clock may take the running time (RunningEnd).
    private const long RunningClockLeadMilliseconds = 15_000;

    private SceneCombatSnapshotAdapter? _adapter;
    private CombatantStatisticsScope _combatantStatisticsScope = CombatantStatisticsScope.All;
    private TrainingDummyRules _trainingDummyRules = TrainingDummyRules.Default;

    public static SceneProjectionState Create(
        Guid encounterId,
        ICombatOccurrenceObserver? combatOccurrenceObserver = null,
        IAuraLifecycleObserver? auraLifecycleObserver = null)
        => new(
            encounterId,
            new EntityStore(),
            new SceneBoundaryStore(),
            new RuntimeMetadataRegistry(),
            new CombatStore(),
            combatOccurrenceObserver,
            auraLifecycleObserver);

    public SceneProjectionState(
        Guid encounterId,
        EntityStore entities,
        SceneBoundaryStore boundary,
        RuntimeMetadataRegistry metadataRegistry,
        CombatStore combat,
        ICombatOccurrenceObserver? combatOccurrenceObserver = null,
        IAuraLifecycleObserver? auraLifecycleObserver = null)
    {
        EncounterId = encounterId;
        Entities = entities;
        Boundary = boundary;
        MetadataRegistry = metadataRegistry;
        Combat = combat;
        CombatOccurrenceObserver = combatOccurrenceObserver;
        AuraLifecycleObserver = auraLifecycleObserver;
        Applier = CreateApplier(trackBossFocus: true);
    }

    public Guid EncounterId { get; private set; }

    public EntityStore Entities { get; }

    public SceneBoundaryStore Boundary { get; }

    public RuntimeMetadataRegistry MetadataRegistry { get; }

    public CombatStore Combat { get; }

    public DomainEventApplier Applier { get; private set; }

    // The scene's clock as its events tell it: the latest entry applied (ms from the scene's start).
    public long LatestObservedAtMilliseconds { get; private set; }

    public CombatantStatisticsScope CombatantStatisticsScope => _combatantStatisticsScope;

    public SceneCombatSnapshotAdapter Adapter =>
        _adapter ??= new(
            Entities,
            Applier.EntityVitals,
            Combat,
            Applier.Mechanics,
            Applier.Resources,
            Boundary,
            Applier.BossFocus,
            EncounterId);

    private ICombatOccurrenceObserver? CombatOccurrenceObserver { get; }

    private IAuraLifecycleObserver? AuraLifecycleObserver { get; }

    public DomainEventMaterialization ApplyEntry(in ObservedEventEntry entry)
    {
        LatestObservedAtMilliseconds = Math.Max(LatestObservedAtMilliseconds, entry.ObservedAtMilliseconds);
        return Applier.ApplyEntry(entry);
    }

    public void CompleteFlush() =>
        Applier.CompleteFlush();

    // Where the time of an "All fights" scene runs to past its last hit (SceneCombatSnapshotAdapter.BuildSnapshot): on between
    // pulls and after the last boss, a whole second at a time. By the entries' clock, plus the lead: live, the time since
    // the latest entry was applied (SceneReadModelOwner), at most 15 s, so it stops soon after the packets do (the game
    // closed). The lead goes by the monotonic clock, which time sync can't step or set apart from the entries' capture clock.
    // 0 for a boss pull (its time ends at its last hit) and before any fight.
    public long RunningEnd(SceneKind kind, long leadMilliseconds = 0)
    {
        if (kind != SceneKind.Standard || Combat.Events.Count == 0)
            return 0;

        return (LatestObservedAtMilliseconds + Math.Clamp(leadMilliseconds, 0, RunningClockLeadMilliseconds)) / 1000 * 1000;
    }

    // The registry carries over, so does whom the player's credits went to (the registry can lose the player's mark).
    public void Reset(Guid encounterId, bool trackBossFocus)
    {
        var creditedPlayerId = Applier.CreditedPlayerId;
        EncounterId = encounterId;
        Applier = CreateApplier(trackBossFocus);
        Applier.CreditedPlayerId = creditedPlayerId;
        _adapter = null;
        LatestObservedAtMilliseconds = 0;
    }

    public void SetCombatantStatisticsScope(CombatantStatisticsScope scope)
    {
        _combatantStatisticsScope = scope;
        Applier.CombatantStatisticsScope = scope;
    }

    public void SetTrainingDummyRules(TrainingDummyRules rules)
    {
        _trainingDummyRules = rules;
        Applier.BossFights.DummyRules = rules;
    }

    private DomainEventApplier CreateApplier(bool trackBossFocus)
    {
        var applier = new DomainEventApplier(
            Entities,
            Boundary,
            MetadataRegistry,
            Combat,
            CombatOccurrenceObserver,
            AuraLifecycleObserver)
        {
            TrackBossFocus = trackBossFocus,
            CombatantStatisticsScope = _combatantStatisticsScope
        };
        applier.BossFights.DummyRules = _trainingDummyRules;
        return applier;
    }
}
