using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.Resources.Catalog;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Playback;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class BossSceneCollectionTests
{
    private static readonly DateTimeOffset Started = new(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WaitingBossSceneCollectsMetadataAndDropsCombat()
    {
        var scene = CreateBossScene();
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.SetCurrentMap(Source(1), 910_035);
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 200, 2_100_001, NpcKind.Monster, 20);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 30);

        AppendDamage(sink, 100, 200, 400, 50, 1);
        sink.AppendNpcHp(Source(55), 300, 100_000, 100_000);
        sink.SetNpcBattle(Source(58), 300, true);
        sink.RegisterObservation2A38(Source(60), 100, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, default, 0, 0, 0);
        sink.RegisterObservation2B38(Source(70), 100, 100, 1, 1, default, 0, 0, 0, 0);

        var snapshot = scene.CreateFrame().Snapshot;

        Assert.Equal(SceneKind.Boss, snapshot.Kind);
        Assert.Equal(BossSceneState.Waiting, scene.BossState);
        Assert.Empty(snapshot.Combatants);
        Assert.Empty(snapshot.BossFocuses);
        Assert.Equal(910_035u, snapshot.MapId);
        Assert.True(scene.Owner.MetadataRegistry.TryGetPcMetadata(100, out var player));
        Assert.Equal("Player", player.Nickname);
        Assert.True(scene.Owner.MetadataRegistry.TryGetNpcCode(300, out var bossCode));
        Assert.Equal(2_100_002, bossCode);
        Assert.DoesNotContain(ReadJournal(scene), static entry => entry.Domain is ObservedEventDomain.Combat or ObservedEventDomain.EntityVital or ObservedEventDomain.Aura or ObservedEventDomain.Action);
        Assert.DoesNotContain(ReadJournal(scene), static entry => entry.State?.StateCode is StateCodes.NpcBattle or StateCodes.NpcBattleToggle);
    }

    [Fact]
    public void DroppedWaitingResourceDoesNotConsumeJournalOrdinal()
    {
        var scene = CreateBossScene();
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);

        sink.AppendNpcHp(Source(30), 300, 100_000, 100_000);
        AppendNpc(sink, 301, 2_100_003, NpcKind.Boss, 40);

        var entries = ReadJournal(scene);
        for (var i = 0; i < entries.Length; i++)
            Assert.Equal(i, entries[i].Stamp.ObservationOrdinal);

        _ = scene.CreateFrame();
        Assert.True(scene.Owner.MetadataRegistry.TryGetNpcCode(301, out var code));
        Assert.Equal(2_100_003, code);
    }

    [Fact]
    public void FirstBossCombatStartsNewSceneAndPreservesCatalog()
    {
        var scene = CreateBossScene();
        var waitingEncounterId = scene.SessionId;
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);

        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        AppendDamage(sink, 100, 300, 200, 1_200, 2);
        sink.CompleteFlush(2);
        var snapshot = scene.CreateFrame().Snapshot;

        Assert.NotEqual(waitingEncounterId, scene.SessionId);
        Assert.Equal(BossSceneState.Recording, scene.BossState);
        Assert.Equal(SceneKind.Boss, snapshot.Kind);
        Assert.Equal(700, snapshot.Combatants[100].DamageAmount);
        Assert.Equal([2_100_002], snapshot.BossNpcCodes.AsSpan().ToArray());
        Assert.True(scene.Owner.MetadataRegistry.TryGetPcMetadata(100, out var player));
        Assert.Equal("Player", player.Nickname);
        Assert.All(ReadJournal(scene).Where(entry => entry.Stamp.ObservationOrdinal >= scene.Owner.SceneStartObservationOrdinal), entry => Assert.Equal(scene.SessionId, entry.SceneSessionId));
    }

    [Fact]
    public void FirstBossCombatSeedsWaitingBossHpMaximum()
    {
        var scene = CreateBossScene();
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);
        sink.AppendNpcHp(Source(30), 300, 243_719_813, 243_750_000);

        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        sink.CompleteFlush(1);
        var snapshot = scene.CreateFrame().Snapshot;

        var focus = Assert.Single(snapshot.BossFocuses.AsSpan().ToArray());
        Assert.True(focus.HasHp);
        Assert.True(focus.HasMaxHp);
        Assert.Equal(243_750_000, focus.MaxHp);
        Assert.True(scene.Owner.EntityVitals.TryGet(300, out var vital));
        Assert.Equal(243_750_000, vital.MaxHp);
        Assert.Contains(
            ReadJournal(scene).Where(entry => entry.Stamp.ObservationOrdinal >= scene.Owner.SceneStartObservationOrdinal),
            static entry => entry.EntityVital is { EntityId: 300, CurrentHp: 243_719_813, MaxHp: 243_750_000 });
    }

    [Fact]
    public void StandardResetRehydratesActiveBossStateBeforeNextCombat()
    {
        var timeProvider = new MutableTimeProvider(Started);
        var scene = new SceneLiveReadModel(Started, timeProvider);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);
        sink.AppendNpcHp(Source(30), 300, 99_500, 100_000);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        sink.CompleteFlush(1);

        var beforeReset = scene.CreateFrame();
        var beforeResetBoss = Assert.Single(beforeReset.BossFocuses.AsSpan().ToArray());
        Assert.True(beforeResetBoss.HasHp);
        Assert.True(beforeResetBoss.HasMaxHp);

        scene.Reset(Started.AddSeconds(2));
        AppendDamage(sink, 100, 300, 200, 3_000, 2);
        sink.CompleteFlush(2);

        var afterReset = scene.CreateFrame();
        var afterResetBoss = Assert.Single(afterReset.BossFocuses.AsSpan().ToArray());
        Assert.True(afterResetBoss.HasHp);
        Assert.True(afterResetBoss.HasMaxHp);
        Assert.Equal(100_000, afterResetBoss.MaxHp);
        Assert.Equal(100_000, afterResetBoss.EffectiveHp);
        Assert.Equal(200, afterReset.Snapshot.Combatants[100].DamageAmount);
        Assert.Contains(
            afterReset.BossDamageContributions,
            static contribution => contribution is { BossId: 300, SourceCombatantId: 100, DamageAmount: 200 });
    }

    [Fact]
    public void StandardMapTransitionRequiresNewMapBossStateBeforeFirstCombat()
    {
        var timeProvider = new MutableTimeProvider(Started);
        var scene = new SceneLiveReadModel(Started, timeProvider);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.SetCurrentMap(Source(1), 200_003);
        Assert.True(scene.TryDequeueMapTransition(out var initialArchive));
        Assert.Null(initialArchive);
        sink.RegisterMapEvent(Source(2), 113_515);
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 200, 2_100_001, NpcKind.Monster, 20);
        AppendDamage(sink, 100, 200, 500, 1_000, 1);
        AppendDamage(sink, 100, 200, 500, 1_200, 1);
        sink.CompleteFlush(1);

        var beforeTransition = scene.CreateFrame();
        Assert.Empty(beforeTransition.BossFocuses);

        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 1_500);
        sink.AppendNpcHp(Source(1_510), 300, 99_500, 100_000);
        sink.StageMapCandidate(Source(2_000, 2), 200_004);
        sink.UnregisterMapEvent(Source(2_005, 2), 113_515);
        sink.ConfirmDestinationMapArrival(Source(2_010, 2));
        sink.CompleteFlush(2);

        Assert.True(scene.TryDequeueMapTransition(out var archived));
        Assert.NotNull(archived);
        Assert.Equal(1_000, archived.Snapshot.Combatants[100].DamageAmount);
        Assert.False(scene.TryDequeueMapTransition(out _));

        AppendPlayer(sink, 100, "Player", 2_020);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 2_030);
        sink.AppendNpcHp(Source(2_040), 300, 99_500, 100_000);
        sink.RegisterMapEvent(Source(2_050, 2), 113_516);
        AppendDamage(sink, 100, 300, 200, 3_000, 3);
        sink.CompleteFlush(3);

        var afterTransition = scene.CreateFrame();
        Assert.Equal(200_004u, afterTransition.Snapshot.MapId);
        Assert.Equal(113_516u, afterTransition.Snapshot.MapInstanceId);
        var afterTransitionBoss = Assert.Single(afterTransition.BossFocuses.AsSpan().ToArray());
        Assert.True(afterTransitionBoss.HasHp);
        Assert.True(afterTransitionBoss.HasMaxHp);
        Assert.Equal(100_000, afterTransitionBoss.MaxHp);
        Assert.Equal(100_000, afterTransitionBoss.EffectiveHp);
        Assert.Equal(200, afterTransition.Snapshot.Combatants[100].DamageAmount);
        Assert.Contains(
            afterTransition.BossDamageContributions,
            static contribution => contribution is { BossId: 300, SourceCombatantId: 100, DamageAmount: 200 });
    }

    [Fact]
    public void FirstTrainingDummyCombatStartsBossModeSceneWithoutClassifyingAsBoss()
    {
        var scene = CreateBossScene();
        var waitingEncounterId = scene.SessionId;
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_500_075, NpcKind.TrainingDummy, 20);

        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        AppendDamage(sink, 100, 300, 200, 1_200, 2);
        sink.CompleteFlush(2);
        var snapshot = scene.CreateFrame().Snapshot;

        Assert.NotEqual(waitingEncounterId, scene.SessionId);
        Assert.Equal(BossSceneState.Recording, scene.BossState);
        Assert.Equal(SceneKind.Boss, snapshot.Kind);
        Assert.Equal(700, snapshot.Combatants[100].DamageAmount);
        Assert.Equal([2_500_075], snapshot.BossNpcCodes.AsSpan().ToArray());
        var focus = Assert.Single(snapshot.BossFocuses.AsSpan().ToArray());
        Assert.Equal(300, focus.InstanceId);
        Assert.True(scene.Owner.Entities.TryGet(300, out var entity));
        Assert.Equal(NpcKind.TrainingDummy, entity.Kind);
    }

    [Fact]
    public void FirstCatalogResolvedCityTrainingDummyCombatStartsBossModeScene()
    {
        var catalog = ResourceCatalog.Load(ResourceLanguage.English).NpcCatalog;
        CombatResourceTestFixture.SetResources([], catalog);
        var scene = CreateBossScene();
        var waitingEncounterId = scene.SessionId;
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var writer = new SceneObservationWriter(sink);
        AppendPlayer(sink, 100, "Player", 10);
        writer.ApplyNpcCatalog(Source(20), 300, 2_400_032, requireCatalogEntry: true);

        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        AppendDamage(sink, 100, 300, 200, 1_200, 2);
        sink.CompleteFlush(2);
        var snapshot = scene.CreateFrame().Snapshot;

        Assert.NotEqual(waitingEncounterId, scene.SessionId);
        Assert.Equal(BossSceneState.Recording, scene.BossState);
        Assert.Equal(SceneKind.Boss, snapshot.Kind);
        Assert.Equal(700, snapshot.Combatants[100].DamageAmount);
        Assert.Equal([2_400_032], snapshot.BossNpcCodes.AsSpan().ToArray());
        var focus = Assert.Single(snapshot.BossFocuses.AsSpan().ToArray());
        Assert.Equal(300, focus.InstanceId);
        Assert.True(scene.Owner.Entities.TryGet(300, out var entity));
        Assert.Equal(NpcKind.TrainingDummy, entity.Kind);
    }

    [Fact]
    public void RecordingBossSceneIncludesMultipleBosses()
    {
        var scene = CreateBossScene();
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);
        AppendNpc(sink, 301, 2_100_003, NpcKind.Boss, 30);

        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        AppendDamage(sink, 100, 301, 700, 2_000, 2);
        sink.CompleteFlush(2);
        var snapshot = scene.CreateFrame().Snapshot;

        Assert.Equal(BossSceneState.Recording, scene.BossState);
        Assert.Equal(1_200, snapshot.Combatants[100].DamageAmount);
        Assert.Equal([2_100_002, 2_100_003], snapshot.BossNpcCodes.AsSpan().ToArray());
        Assert.Equal(2, snapshot.BossFocuses.Count);
    }

    [Fact]
    public void AllDeadBossFocusFreezesSceneImmediatelyAndDropsFollowingCombat()
    {
        var scene = CreateBossScene(out var timeProvider);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 200, 2_100_001, NpcKind.Monster, 20);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 30);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        AppendDamage(sink, 100, 300, 200, 1_200, 2);
        sink.CompleteFlush(2);
        _ = scene.CreateFrame();

        sink.AppendNpcHp(Source(2_000), 300, 0, 100_000);
        var deathFrame = scene.CreateFrame();

        Assert.Equal(BossSceneState.Frozen, scene.BossState);
        var deadBoss = Assert.Single(deathFrame.BossFocuses.AsSpan().ToArray());
        Assert.True(deadBoss.HasHp);
        Assert.Equal(0, deadBoss.Hp);
        Assert.Equal(200, deathFrame.Snapshot.EncounterTime);
        var frozenCount = scene.Journal.Count;
        var frozenSessionId = scene.SessionId;
        sink.SetNpcBattle(Source(2_500), 300, false);
        AppendDamage(sink, 100, 300, 800, 2_600, 3);
        AppendDamage(sink, 100, 200, 900, 3_000, 3);
        sink.CompleteFlush(3);
        var snapshot = scene.CreateFrame().Snapshot;

        Assert.Equal(frozenSessionId, scene.SessionId);
        Assert.Equal(BossSceneState.Frozen, scene.BossState);
        Assert.Equal(frozenCount, scene.Journal.Count);
        Assert.Equal(700, snapshot.Combatants[100].DamageAmount);
        Assert.Equal(200, snapshot.EncounterTime);
        Assert.Single(snapshot.BossFocuses.AsSpan().ToArray());
        Assert.Equal([2_100_002], snapshot.BossNpcCodes.AsSpan().ToArray());
        Assert.False(scene.TryDequeuePendingArchive(out _));

        timeProvider.SetUtcNow(Started.AddMilliseconds(12_001));
        var expiredSnapshot = scene.CreateFrame().Snapshot;

        Assert.Empty(expiredSnapshot.BossFocuses);
        Assert.Equal(700, expiredSnapshot.Combatants[100].DamageAmount);
        Assert.Equal(200, expiredSnapshot.EncounterTime);
    }

    // "All fights": the time runs on past the last hit with the scene's entries (a ping here), a whole second at a time,
    // and the per-second values with it; the window (UHS, details) still ends at the last hit.
    [Fact]
    public void StandardTimeRunsOnBetweenPullsWithTheSceneEvents()
    {
        var scene = new SceneLiveReadModel(Started, new MutableTimeProvider(Started));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_001, NpcKind.Monster, 20);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        AppendDamage(sink, 100, 300, 500, 3_000, 2);
        sink.CompleteFlush(2);
        sink.AppendPing(Source(20_400), 9);

        var snapshot = scene.CreateFrame().Snapshot;

        Assert.Equal((1_000L, 3_000L, 19_000L), (snapshot.EncounterStartTime, snapshot.EncounterEndTime, snapshot.EncounterTime));
        Assert.Equal(1_000d / 19, snapshot.Combatants[100].DamagePerSecond, 6);

        sink.AppendPing(Source(20_900), 9);
        Assert.Equal(19_000, scene.CreateFrame().Snapshot.EncounterTime);

        sink.AppendPing(Source(21_100), 9);
        Assert.Equal(20_000, scene.CreateFrame().Snapshot.EncounterTime);
    }

    // Live, the time follows the scene's own clock between entries too (solo and idle they can be the 10 s pings apart),
    // from when the latest was read, but no further than 15 s past it, so it stops soon after the packets do (the game
    // closed).
    [Fact]
    public void StandardTimeRunsOnWithTheSceneClockUntil15SecondsPastTheLatestEntry()
    {
        var timeProvider = new MutableTimeProvider(Started);
        var scene = new SceneLiveReadModel(Started, timeProvider);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_001, NpcKind.Monster, 20);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        AppendDamage(sink, 100, 300, 500, 3_000, 2);
        sink.CompleteFlush(2);
        // The meter reads the hits as they come.
        timeProvider.SetUtcNow(Started.AddMilliseconds(3_000));
        Assert.Equal(2_000, scene.CreateFrame().Snapshot.EncounterTime);

        timeProvider.SetUtcNow(Started.AddMilliseconds(7_900));
        var snapshot = scene.CreateFrame().Snapshot;

        Assert.Equal((1_000L, 3_000L, 6_000L), (snapshot.EncounterStartTime, snapshot.EncounterEndTime, snapshot.EncounterTime));
        Assert.Equal(1_000d / 6, snapshot.Combatants[100].DamagePerSecond, 6);

        timeProvider.SetUtcNow(Started.AddMilliseconds(7_999));
        Assert.Equal(6_000, scene.CreateFrame().Snapshot.EncounterTime);
        // No new entry: the next second alone is a change for the meter's poll.
        timeProvider.SetUtcNow(Started.AddMilliseconds(8_000));
        Assert.True(scene.Owner.HasPendingProjectionChanges);
        Assert.Equal(7_000, scene.CreateFrame().Snapshot.EncounterTime);

        timeProvider.SetUtcNow(Started.AddMinutes(10));
        Assert.Equal(17_000, scene.CreateFrame().Snapshot.EncounterTime);
    }

    // Entries go by the capture's clock, the time between them by the monotonic clock: with the wall clock set 400 s ahead
    // of the capture clock or behind it (time sync), the times are those without, in the fight and past it.
    [Theory]
    [InlineData(0)]
    [InlineData(400)]
    [InlineData(-400)]
    public void StandardTimeIsTheSameWithTheWallClockSetApart(int wallClockAheadSeconds)
    {
        var wallClock = Started.AddSeconds(wallClockAheadSeconds);
        var timeProvider = new MutableTimeProvider(wallClock);
        var scene = new SceneLiveReadModel(Started, timeProvider);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_001, NpcKind.Monster, 20);
        // A hit every 500 ms from 1 s to 30 s, each read by the meter 40 ms on.
        for (var at = 1_000L; at <= 30_000; at += 500)
        {
            AppendDamage(sink, 100, 300, 500, at, at / 500);
            sink.CompleteFlush(at / 500);
            timeProvider.SetUtcNow(wallClock.AddMilliseconds(at + 40));
            Assert.Equal(at - 1_000, scene.CreateFrame().Snapshot.EncounterTime);
        }

        timeProvider.SetUtcNow(wallClock.AddMilliseconds(35_040));
        Assert.Equal(34_000, scene.CreateFrame().Snapshot.EncounterTime);
        timeProvider.SetUtcNow(wallClock.AddMinutes(10));
        Assert.Equal(44_000, scene.CreateFrame().Snapshot.EncounterTime);
    }

    // A time sync stepping the wall clock while no entries come doesn't show either: the time goes on as without it, and
    // the meter's poll sees no change.
    [Theory]
    [InlineData(60)]
    [InlineData(-5)]
    public void StandardTimeIgnoresATimeSyncBetweenEntries(int stepSeconds)
    {
        var timeProvider = new MutableTimeProvider(Started);
        var scene = new SceneLiveReadModel(Started, timeProvider);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_001, NpcKind.Monster, 20);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        AppendDamage(sink, 100, 300, 500, 15_000, 2);
        sink.CompleteFlush(2);
        timeProvider.SetUtcNow(Started.AddMilliseconds(15_040));
        Assert.Equal(14_000, scene.CreateFrame().Snapshot.EncounterTime);
        timeProvider.SetUtcNow(Started.AddMilliseconds(18_040));
        Assert.Equal(17_000, scene.CreateFrame().Snapshot.EncounterTime);

        timeProvider.StepWallClock(TimeSpan.FromSeconds(stepSeconds));
        Assert.False(scene.Owner.HasPendingProjectionChanges);
        Assert.Equal(17_000, scene.CreateFrame().Snapshot.EncounterTime);
        timeProvider.SetUtcNow(Started.AddMilliseconds(19_040));
        Assert.Equal(18_000, scene.CreateFrame().Snapshot.EncounterTime);
    }

    [Theory]
    [InlineData(CombatantStatisticsScope.Self, BossSceneState.Frozen, 0, NpcKind.Boss)]
    [InlineData(CombatantStatisticsScope.All, BossSceneState.Recording, 1, NpcKind.Boss)]
    [InlineData(CombatantStatisticsScope.Self, BossSceneState.Frozen, 0, NpcKind.TrainingDummy)]
    [InlineData(CombatantStatisticsScope.All, BossSceneState.Recording, 1, NpcKind.TrainingDummy)]
    public void BossSceneActivityTimeoutUsesConfiguredPlayerScope(CombatantStatisticsScope scope, BossSceneState expectedState, int expectedFocusCount, NpcKind targetKind)
    {
        var scene = CreateBossScene(out var timeProvider);
        scene.SetCombatantStatisticsScope(scope);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Local", 10, isLocalPlayer: true);
        AppendPlayer(sink, 200, "Other", 20);
        AppendNpc(sink, 300, 2_100_002, targetKind, 30);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        sink.CompleteFlush(1);
        _ = scene.CreateFrame();

        AppendDamage(sink, 200, 300, 700, 5_000, 2);
        sink.CompleteFlush(2);

        timeProvider.SetUtcNow(Started.AddMilliseconds(12_001));
        var frame = scene.CreateFrame();

        Assert.Equal(expectedState, scene.BossState);
        Assert.Equal(expectedFocusCount, frame.BossFocuses.Count);
    }

    [Fact]
    public void StandardSceneActivityTimeoutUsesConfiguredPlayerScope()
    {
        var timeProvider = new MutableTimeProvider(Started);
        var scene = new SceneLiveReadModel(Started, timeProvider);
        scene.SetCombatantStatisticsScope(CombatantStatisticsScope.Self);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Local", 10, isLocalPlayer: true);
        AppendPlayer(sink, 200, "Other", 20);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 30);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        sink.CompleteFlush(1);
        _ = scene.CreateFrame();

        AppendDamage(sink, 200, 300, 700, 5_000, 2);
        sink.CompleteFlush(2);

        timeProvider.SetUtcNow(Started.AddMilliseconds(12_001));
        var frame = scene.CreateFrame();

        Assert.Empty(frame.BossFocuses);
    }

    [Fact]
    public void ChangingCombatantStatisticsScopeReconcilesExistingBossActivity()
    {
        var scene = CreateBossScene(out var timeProvider);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Local", 10, isLocalPlayer: true);
        AppendPlayer(sink, 200, "Other", 20);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 30);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        sink.CompleteFlush(1);
        _ = scene.CreateFrame();

        AppendDamage(sink, 200, 300, 700, 5_000, 2);
        sink.CompleteFlush(2);
        _ = scene.CreateFrame();

        scene.SetCombatantStatisticsScope(CombatantStatisticsScope.Self);
        scene.SetCombatantStatisticsScope(CombatantStatisticsScope.All);
        var widened = scene.CreateFrame();
        Assert.Equal(BossSceneState.Recording, scene.BossState);
        Assert.Single(widened.BossFocuses);

        scene.SetCombatantStatisticsScope(CombatantStatisticsScope.Self);
        timeProvider.SetUtcNow(Started.AddMilliseconds(11_001));
        var expired = scene.CreateFrame();

        Assert.Equal(BossSceneState.Frozen, scene.BossState);
        Assert.Empty(expired.BossFocuses);

        scene.SetCombatantStatisticsScope(CombatantStatisticsScope.All);
        var widenedAfterExpiry = scene.CreateFrame();

        Assert.Equal(BossSceneState.Frozen, scene.BossState);
        Assert.Single(widenedAfterExpiry.BossFocuses);

        scene.SetCombatantStatisticsScope(CombatantStatisticsScope.Self);
        AppendDamage(sink, 100, 300, 900, 12_000, 3);
        sink.CompleteFlush(3);
        var resumed = scene.CreateFrame();

        Assert.Equal(BossSceneState.Recording, scene.BossState);
        Assert.Single(resumed.BossFocuses);
    }

    [Fact]
    public void FrozenBossSceneResumesSameNonFullBossWithoutArchiving()
    {
        var scene = CreateBossScene(out var timeProvider);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);
        sink.AppendNpcHp(Source(30), 300, 99_500, 100_000);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        sink.CompleteFlush(1);
        _ = scene.CreateFrame();

        timeProvider.SetUtcNow(Started.AddMilliseconds(12_001));
        _ = scene.CreateFrame();
        var frozenSessionId = scene.SessionId;

        sink.AppendNpcHp(Source(13_000), 300, 90_000, 100_000);
        AppendDamage(sink, 100, 300, 700, 13_500, 2);
        sink.CompleteFlush(2);

        var resumed = scene.CreateFrame();
        var focus = Assert.Single(resumed.BossFocuses.AsSpan().ToArray());

        Assert.Equal(frozenSessionId, scene.SessionId);
        Assert.Equal(BossSceneState.Recording, scene.BossState);
        Assert.False(scene.TryDequeuePendingArchive(out _));
        Assert.Equal(1_200, resumed.Snapshot.Combatants[100].DamageAmount);
        Assert.Equal(90_000, focus.Hp);
        Assert.Equal(100_000, focus.MaxHp);
    }

    [Fact]
    public void FrozenBossSceneArchivesSameBossWhenItReturnsAtFullHealth()
    {
        var scene = CreateBossScene(out var timeProvider);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);
        sink.AppendNpcHp(Source(30), 300, 99_500, 100_000);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        sink.CompleteFlush(1);
        _ = scene.CreateFrame();

        timeProvider.SetUtcNow(Started.AddMilliseconds(12_001));
        _ = scene.CreateFrame();
        var frozenSessionId = scene.SessionId;

        sink.AppendNpcHp(Source(13_000), 300, 100_000, 100_000);
        AppendDamage(sink, 100, 300, 700, 13_500, 2);
        sink.CompleteFlush(2);

        var resumed = scene.CreateFrame();

        Assert.NotEqual(frozenSessionId, scene.SessionId);
        Assert.Equal(BossSceneState.Recording, scene.BossState);
        Assert.True(scene.TryDequeuePendingArchive(out var archived));
        Assert.Equal(500, archived.Snapshot.Combatants[100].DamageAmount);
        Assert.Equal(700, resumed.Snapshot.Combatants[100].DamageAmount);
    }

    [Fact]
    public void FrozenBossSceneArchivesWhenBossIdentityChanges()
    {
        var scene = CreateBossScene(out var timeProvider);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);
        sink.AppendNpcHp(Source(30), 300, 99_500, 100_000);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        sink.CompleteFlush(1);
        _ = scene.CreateFrame();

        timeProvider.SetUtcNow(Started.AddMilliseconds(12_001));
        _ = scene.CreateFrame();
        var frozenSessionId = scene.SessionId;

        AppendNpc(sink, 300, 2_100_003, NpcKind.Boss, 13_000);
        sink.AppendNpcHp(Source(13_000), 300, 90_000, 100_000);
        AppendDamage(sink, 100, 300, 700, 13_500, 2);
        sink.CompleteFlush(2);

        var resumed = scene.CreateFrame();

        Assert.NotEqual(frozenSessionId, scene.SessionId);
        Assert.True(scene.TryDequeuePendingArchive(out var archived));
        Assert.Equal(2_100_002, Assert.Single(archived.Snapshot.BossNpcCodes));
        Assert.Equal(700, resumed.Snapshot.Combatants[100].DamageAmount);
    }

    [Fact]
    public void LivePlaybackSourceFreezesWhenBossSceneFreezes()
    {
        var scene = CreateBossScene();
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        sink.CompleteFlush(1);
        _ = scene.CreateFrame();
        var source = scene.CreatePlaybackSource();
        var heldSegment = source.CreateTimelineSegment();

        sink.AppendNpcHp(Source(2_000), 300, 0, 100_000);
        _ = scene.CreateFrame();
        var frozenSegment = source.CreateTimelineSegment();

        Assert.Equal(BossSceneState.Frozen, scene.BossState);
        Assert.False(heldSegment.IsLiveGrowing);
        Assert.False(frozenSegment.IsLiveGrowing);
        Assert.Equal(frozenSegment.EndObservationOrdinalExclusive, heldSegment.EndObservationOrdinalExclusive);
        Assert.Equal(scene.SessionId, source.EncounterId);
        Assert.Equal(500, source.CreateSnapshot().Combatants[100].DamageAmount);
    }

    [Fact]
    public void DeadBossSceneFreezesImmediatelyButKeepsFocusUntilTimeout()
    {
        var scene = CreateBossScene(out var timeProvider);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        sink.CompleteFlush(1);
        _ = scene.CreateFrame();

        sink.AppendNpcHp(Source(2_000), 300, 0, 100_000);
        var deadFrame = scene.CreateFrame();
        Assert.Equal(BossSceneState.Frozen, scene.BossState);
        Assert.Single(deadFrame.BossFocuses.AsSpan().ToArray());
        Assert.False(scene.TryDequeuePendingArchive(out _));

        timeProvider.SetUtcNow(Started.AddMilliseconds(12_001));
        var frozenFrame = scene.CreateFrame();

        Assert.Equal(BossSceneState.Frozen, scene.BossState);
        Assert.Empty(frozenFrame.BossFocuses);
        Assert.False(scene.TryDequeuePendingArchive(out _));
    }

    [Fact]
    public void MultipleBossSceneFreezesOnlyAfterEveryFocusedBossIsDead()
    {
        var scene = CreateBossScene();
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);
        AppendNpc(sink, 301, 2_100_003, NpcKind.Boss, 30);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        AppendDamage(sink, 100, 301, 700, 1_200, 2);
        sink.AppendNpcHp(Source(1_300), 300, 1_000, 1_000);
        sink.AppendNpcHp(Source(1_400), 301, 2_000, 2_000);
        sink.CompleteFlush(2);
        _ = scene.CreateFrame();

        sink.AppendNpcHp(Source(2_000), 300, 0, 1_000);
        var oneDeadFrame = scene.CreateFrame();

        Assert.Equal(BossSceneState.Recording, scene.BossState);
        Assert.Equal(2, oneDeadFrame.BossFocuses.Count);
        Assert.Contains(oneDeadFrame.BossFocuses.AsSpan().ToArray(), static boss => boss.InstanceId == 300 && boss.HasHp && boss.Hp == 0);
        Assert.Contains(oneDeadFrame.BossFocuses.AsSpan().ToArray(), static boss => boss.InstanceId == 301 && boss.HasHp && boss.Hp == 2_000);

        sink.AppendNpcHp(Source(2_100), 301, 0, 2_000);
        var allDeadFrame = scene.CreateFrame();

        Assert.Equal(BossSceneState.Frozen, scene.BossState);
        Assert.Equal(2, allDeadFrame.BossFocuses.Count);
        Assert.All(allDeadFrame.BossFocuses.AsSpan().ToArray(), static boss =>
        {
            Assert.True(boss.HasHp);
            Assert.Equal(0, boss.Hp);
        });
    }

    [Fact]
    public void UnknownFocusedBossHpPreventsPrematureDeathFreeze()
    {
        var scene = CreateBossScene();
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);
        AppendNpc(sink, 301, 2_100_003, NpcKind.Boss, 30);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        AppendDamage(sink, 100, 301, 700, 1_200, 2);
        sink.CompleteFlush(2);
        _ = scene.CreateFrame();

        sink.AppendNpcHp(Source(2_000), 300, 0, 1_000);
        var frame = scene.CreateFrame();

        Assert.Equal(BossSceneState.Recording, scene.BossState);
        Assert.Equal(2, frame.BossFocuses.Count);
        Assert.Contains(frame.BossFocuses.AsSpan().ToArray(), static boss => boss.InstanceId == 300 && boss.HasHp && boss.Hp == 0);
        Assert.Contains(frame.BossFocuses.AsSpan().ToArray(), static boss => boss.InstanceId == 301 && !boss.HasHp);
    }

    [Fact]
    public void NextBossCombatArchivesFrozenSceneBeforeStartingNewScene()
    {
        var scene = CreateBossScene();
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        AppendDamage(sink, 100, 300, 200, 1_200, 2);
        sink.CompleteFlush(2);
        _ = scene.CreateFrame();
        sink.AppendNpcHp(Source(2_000), 300, 0, 100_000);
        Assert.Equal(BossSceneState.Frozen, scene.BossState);
        var frozenEnd = scene.Owner.AppliedNextObservationOrdinal;

        AppendNpc(sink, 301, 2_100_003, NpcKind.Boss, 3_000);
        AppendDamage(sink, 100, 301, 700, 4_000, 3);
        AppendDamage(sink, 100, 301, 300, 4_200, 4);
        sink.CompleteFlush(4);

        Assert.True(scene.TryDequeuePendingArchive(out var archived));
        Assert.Equal(SceneKind.Boss, archived.Snapshot.Kind);
        Assert.Equal(700, archived.Snapshot.Combatants[100].DamageAmount);
        Assert.Equal([2_100_002], archived.BossNpcCodes);
        Assert.Equal(frozenEnd, archived.TimelineSegment.EndObservationOrdinalExclusive);

        var current = scene.CreateFrame().Snapshot;
        Assert.Equal(BossSceneState.Recording, scene.BossState);
        Assert.Equal(1_000, current.Combatants[100].DamageAmount);
        Assert.Equal([2_100_003], current.BossNpcCodes.AsSpan().ToArray());
        Assert.False(scene.TryDequeuePendingArchive(out _));
    }

    [Fact]
    public async Task FrozenBossSceneArchiveCanOpenPlayback()
    {
        var scene = CreateBossScene(out var timeProvider);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayer(sink, 100, "Player", 10);
        AppendNpc(sink, 300, 2_100_002, NpcKind.Boss, 20);
        AppendDamage(sink, 100, 300, 500, 1_000, 1);
        AppendDamage(sink, 100, 300, 200, 1_800, 2);
        sink.CompleteFlush(2);
        _ = scene.CreateFrame();
        sink.AppendNpcHp(Source(2_000), 300, 0, 100_000);
        timeProvider.SetUtcNow(Started.AddMilliseconds(12_001));
        _ = scene.CreateFrame();
        var archive = scene.CreateArchivePayload();

        await using var controller = new ScenePlaybackController(
            new ArchivedScenePlaybackSource(archive),
            new ManualTickSourceFactory(),
            TimeSpan.FromMilliseconds(33));

        var frame = await controller.SeekAsync(controller.DurationMilliseconds, TestContext.Current.CancellationToken);

        Assert.Equal(SceneKind.Boss, frame.Snapshot.Kind);
        Assert.Equal(700, frame.CombatTotals.TotalDamage);
        Assert.Equal(frame.TimeRange.DurationMilliseconds, frame.PositionMilliseconds);
        Assert.Equal(BossSceneState.Frozen, scene.BossState);
    }

    private static SceneLiveReadModel CreateBossScene() => CreateBossScene(out _);

    private static SceneLiveReadModel CreateBossScene(out MutableTimeProvider timeProvider)
    {
        timeProvider = new MutableTimeProvider(Started);
        var scene = new SceneLiveReadModel(Started, timeProvider);
        scene.ChangeKind(SceneKind.Boss, Started, archiveCurrent: false);
        return scene;
    }

    private static void AppendPlayer(IRuntimeObservationSink sink, int entityId, string name, long offsetMilliseconds, bool isLocalPlayer = false)
    {
        sink.AppendNickname(Source(offsetMilliseconds), entityId, name, characterClass: CharacterClass.Gladiator, isLocalPlayer: isLocalPlayer);
    }

    private static void AppendNpc(IRuntimeObservationSink sink, int instanceId, int npcCode, NpcKind kind, long offsetMilliseconds)
    {
        var source = Source(offsetMilliseconds);
        sink.AppendNpcCode(in source, instanceId, npcCode);
        sink.AppendNpcKind(in source, instanceId, kind);
    }

    private static void AppendDamage(IRuntimeObservationSink sink, int sourceId, int targetId, int damage, long offsetMilliseconds, long flushId)
    {
        var source = Source(offsetMilliseconds, flushId);
        var observation = new CombatWireObservation
        {
            SkillCode = 11_000_010,
            Damage = damage,
            HitCount = 1,
            AttemptCount = 1
        };
        sink.AppendCombatWireObservation(in source, sourceId, targetId, in observation);
    }

    private static PacketObservationSource Source(long offsetMilliseconds, long flushId = 0) =>
        new(Started.ToUnixTimeMilliseconds() + offsetMilliseconds, flushId, 0, 0, offsetMilliseconds, default);

    private static JournalEntrySnapshot[] ReadJournal(SceneLiveReadModel scene)
    {
        var entries = new List<JournalEntrySnapshot>(scene.Journal.Count);
        var cursor = scene.Journal.CreateCursor(scene.Journal.FirstObservationOrdinal);
        while (cursor.NextObservationOrdinal < scene.Journal.NextObservationOrdinal)
        {
            var result = scene.Journal.ReadEntries(cursor, ObservedEventJournal.SegmentCapacity, batch =>
            {
                for (var i = 0; i < batch.Count; i++)
                {
                    var entry = batch[i];
                    entries.Add(new JournalEntrySnapshot(
                        entry.SceneSessionId,
                        entry.Stamp,
                        entry.Domain,
                        entry.Domain == ObservedEventDomain.State ? entry.State : null,
                        entry.Domain == ObservedEventDomain.EntityVital ? entry.EntityVital : null));
                }
            });
            if (result.Count == 0)
                break;
            cursor = result.Cursor;
        }

        return [.. entries];
    }

    private readonly record struct JournalEntrySnapshot(
        Guid SceneSessionId,
        TimelineStamp Stamp,
        ObservedEventDomain Domain,
        StateObservation? State,
        EntityVitalObservation? EntityVital);

    // The clock a test moves (SetUtcNow), its timestamp with it; a time sync (StepWallClock) steps the wall clock alone.
    private sealed class MutableTimeProvider(DateTimeOffset now) : TestTimeProvider
    {
        private DateTimeOffset _now = now;
        private TimeSpan _wallClockStep;

        public override DateTimeOffset GetUtcNow() => _now + _wallClockStep;

        public override long GetTimestamp() => _now.UtcTicks;

        public void SetUtcNow(DateTimeOffset now) => _now = now;

        public void StepWallClock(TimeSpan by) => _wallClockStep += by;
    }

    private sealed class ManualTickSourceFactory : IScenePlaybackTickSourceFactory
    {
        public IScenePlaybackTickSource Create(TimeSpan interval) => new ManualTickSource();
    }

    private sealed class ManualTickSource : IScenePlaybackTickSource
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<ScenePlaybackTick> WaitForNextTickAsync(CancellationToken cancellationToken)
            => new(new ScenePlaybackTick(TimeSpan.FromMilliseconds(33)));
    }
}
