using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class BossFightSceneTests
{
    private static readonly DateTimeOffset Started = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Aggro_Counts_The_Casts_Enemies_Aim_At_Players()
    {
        var (scene, sink) = CreateBossFight();
        AppendDamage(sink, 100, 300, 5_000, 40);
        AppendDamage(sink, 101, 300, 1_000, 45);
        AppendDamage(sink, 300, 100, 2_000, 50);
        sink.AppendNpcHp(Source(60), 300, 9_994_000, 10_000_000);

        Cast(sink, 300, 100, flag: 2, 100);
        Cast(sink, 300, 100, flag: 2, 200);
        Cast(sink, 300, 100, flag: 2, 300);
        Cast(sink, 300, 101, flag: 2, 400);
        Cast(sink, 300, 101, flag: 0, 410); // a mechanic's follow-up
        Cast(sink, 500, 100, flag: 2, 420); // the healer's turret
        Cast(sink, 101, 100, flag: 2, 430); // a player's heal
        Cast(sink, 100, 300, flag: 2, 440); // the tank's own attack

        var frame = scene.CreateFrame();

        Assert.Equal(0.75, frame.Snapshot.Combatants[100].AggroShare, 6);
        Assert.Equal(0.25, frame.Snapshot.Combatants[101].AggroShare, 6);
        Assert.Equal(0.75, Assert.Single(frame.BossFights).Snapshot.Combatants[100].AggroShare, 6);

        using var file = new MemoryStream();
        await SceneArchiveFile.WriteAsync(file, [new ArchivedEncounterRecord { ScenePayload = scene.CreateArchivePayload() }], TestContext.Current.CancellationToken);
        var imported = Assert.Single(await SceneArchiveFile.ReadAsync(new MemoryStream(file.ToArray()), TestContext.Current.CancellationToken));
        Assert.Equal(0.75, imported.ScenePayload.Snapshot.Combatants[100].AggroShare, 6);
    }

    [Fact]
    public void A_Boss_Fights_Aggro_Is_The_Share_Of_Time_The_Boss_Frame_Named_Them()
    {
        var (scene, sink) = CreateBossFight();
        sink.AppendBossTarget(Source(26), 101); // named on the pull, before the fight's first hit
        AppendDamage(sink, 100, 300, 5_000, 1_000);
        AppendDamage(sink, 101, 300, 1_000, 2_000);
        sink.AppendBossTarget(Source(4_000), 100);
        Cast(sink, 300, 101, flag: 2, 5_000); // a random-target cast is no aggro here
        AppendDamage(sink, 100, 300, 5_000, 6_000);
        // Another boss leaving the fight blanks the frame; this boss's next aimed cast fills it in until the kill.
        sink.AppendBossTarget(Source(7_000), 0);
        Cast(sink, 300, 100, flag: 2, 9_000);
        sink.AppendNpcHp(Source(11_000), 300, 0, 10_000_000);
        sink.AppendBossTarget(Source(11_500), 0);

        var frame = scene.CreateFrame();

        var fight = Assert.Single(frame.BossFights).Snapshot;
        Assert.Equal(0.625, fight.Combatants[100].AggroShare, 6); // 4-7 s and 9-11 s
        Assert.Equal(0.375, fight.Combatants[101].AggroShare, 6); // 1-4 s
        Assert.Equal(0.5, frame.Snapshot.Combatants[101].AggroShare, 6); // the scene still counts casts
    }

    [Fact]
    public void An_Archived_Scene_Keeps_Its_Events_While_The_Live_Journal_Lets_Go_Of_Them()
    {
        var (scene, sink) = CreateBossFight();
        for (var i = 0; i < 1_500; i++)
            AppendDamage(sink, 100, 300, 10, 1_000 + i);
        scene.CreateFrame();
        var events = scene.Journal.Count;

        var archive = scene.ArchiveAndReset(() => Started.AddMinutes(5));

        var archived = 0;
        var segment = archive.TimelineSegment;
        var cursor = segment.CreateCursor();
        while (segment.ReadEntries(cursor, 4_096, batch => archived += batch.Count) is { Count: > 0 } read)
            cursor = read.Cursor;
        Assert.Equal(events, archived);
        Assert.NotSame(scene.Journal, segment.Journal);
        Assert.True(scene.Journal.Count < ObservedEventJournal.SegmentCapacity, $"live journal still holds {scene.Journal.Count}");
        Assert.Equal(15_000, archive.Snapshot.Combatants[100].DamageAmount);
    }

    [Fact]
    public async Task A_Wipe_Stays_The_Boss_Fight_Until_The_Boss_Is_Pulled_Again()
    {
        var (scene, sink) = CreateBossFight();
        sink.AppendNpcKind(Source(20), 400, NpcKind.Monster);
        AppendDamage(sink, 100, 300, 5_000, 1_000);
        AppendDamage(sink, 100, 400, 3_000, 1_100); // an add: the table counts it, the fight doesn't
        AppendDamage(sink, 300, 101, 2_000, 1_200);
        AppendHealing(sink, 101, 101, 1_500, 1_300);
        Cast(sink, 300, 100, flag: 2, 1_400);
        sink.AppendNpcHp(Source(1_500), 300, 9_995_000, 10_000_000);

        // The group wipes and the boss heals back to full; a damage-over-time tick right after doesn't pull it again.
        sink.AppendNpcHp(Source(30_000), 300, 10_000_000, 10_000_000);
        AppendDamage(sink, 101, 300, 10, 30_500);

        var wipe = Assert.Single(scene.CreateFrame().BossFights);
        Assert.Equal((1, 1, BossFightOutcome.Reset, 9_995_000L), (wipe.BossNumber, wipe.Attempt, wipe.Outcome, wipe.BossHp));
        var tank = wipe.Snapshot.Combatants[100];
        var healer = wipe.Snapshot.Combatants[101];
        Assert.Equal((5_000L, 1.0), (tank.DamageAmount, tank.AggroShare));
        Assert.Equal((0L, 2_000L, 1_500L), (healer.DamageAmount, healer.DamageTakenAmount, healer.HealingAmount));

        // The next pull is attempt 2. The killing blow reaches the journal after the boss's 0 HP and still counts.
        AppendDamage(sink, 101, 300, 1_000, 60_000);
        sink.AppendNpcHp(Source(60_010), 300, 0, 10_000_000);
        AppendDamage(sink, 100, 300, 700, 60_200);
        sink.AppendNpcKind(Source(61_000), 301, NpcKind.Boss);
        sink.AppendNpcHp(Source(61_000), 301, 5_000_000, 5_000_000);
        AppendDamage(sink, 100, 301, 400, 62_000);

        var frame = scene.CreateFrame();

        Assert.Equal(
            [(1, 1, BossFightOutcome.Reset), (1, 2, BossFightOutcome.Killed), (2, 1, BossFightOutcome.Ongoing)],
            frame.BossFights.Select(static f => (f.BossNumber, f.Attempt, f.Outcome)));
        var kill = frame.BossFights[1].Snapshot;
        Assert.Equal((700L, 1_000L), (kill.Combatants[100].DamageAmount, kill.Combatants[101].DamageAmount));
        Assert.Equal(9_100, frame.Snapshot.Combatants[100].DamageAmount);

        // History entries keep every fight.
        using var file = new MemoryStream();
        await SceneArchiveFile.WriteAsync(file, [new ArchivedEncounterRecord { ScenePayload = scene.CreateArchivePayload() }], TestContext.Current.CancellationToken);
        var imported = Assert.Single(await SceneArchiveFile.ReadAsync(new MemoryStream(file.ToArray()), TestContext.Current.CancellationToken));
        var fights = imported.ScenePayload.BossFights.AsSpan().ToArray();
        Assert.Equal(frame.BossFights.Select(static f => (f.BossId, f.Attempt, f.Outcome, f.BossHp, f.BossMaxHp)), fights.Select(static f => (f.BossId, f.Attempt, f.Outcome, f.BossHp, f.BossMaxHp)));
        Assert.Equal(1_500, fights[0].Snapshot.Combatants[101].HealingAmount);
    }

    [Fact]
    public void Stagger_Goes_To_The_Hits_Right_Before_Each_Gauge_Drop()
    {
        var (scene, sink) = CreateBossFight();
        sink.AppendStaggerGauge(Source(900), 300, 100, 100);

        // Only the tank hit: the drop is theirs, and their skill's 10 is learned.
        AppendDamage(sink, 100, 300, 1_000, 1_000);
        sink.AppendStaggerGauge(Source(1_005), 300, 90, 100);

        // Both hit in the same tick: the tank's skill accounts for 10, the healer gets the 20 it leaves.
        AppendDamage(sink, 100, 300, 1_000, 2_000);
        AppendDamage(sink, 101, 300, 1_000, 2_001, skillCode: 17_000_020);
        sink.AppendStaggerGauge(Source(2_005), 300, 60, 100);

        // The healer empties it: the boss is staggered, and the refill five seconds later is nobody's.
        AppendDamage(sink, 101, 300, 1_000, 3_000, skillCode: 17_000_020);
        sink.AppendStaggerGauge(Source(3_005), 300, 0, 0);
        sink.AppendStaggerGauge(Source(8_100), 300, 100, 100);

        var frame = scene.CreateFrame();

        Assert.Equal(0.2, frame.Snapshot.Combatants[100].StaggerShare, 6);
        Assert.Equal(0.8, frame.Snapshot.Combatants[101].StaggerShare, 6);
        var fight = Assert.Single(frame.BossFights);
        Assert.Equal(1, fight.StaggerCount);
        Assert.Equal(0.8, fight.Snapshot.Combatants[101].StaggerShare, 6);
        var boss = Assert.Single(frame.BossFocuses.AsSpan().ToArray());
        Assert.Equal((100L, 100L), (boss.StaggerCurrent, boss.StaggerMax));
    }

    [Fact]
    public void Stagger_Of_A_Drop_Is_Split_Between_The_Hits_Before_It()
    {
        var (scene, sink) = CreateBossFight();
        sink.AppendStaggerGauge(Source(900), 300, 100, 100);

        // A lone hit teaches its skill's 10.
        AppendDamage(sink, 100, 300, 1_000, 1_000);
        sink.AppendStaggerGauge(Source(1_005), 300, 90, 100);

        // That skill and one not known yet in one tick: the known one keeps its 10, the other gets the rest.
        AppendDamage(sink, 100, 300, 1_000, 2_000);
        AppendDamage(sink, 100, 300, 1_000, 2_001, skillCode: 11_000_030);
        sink.AppendStaggerGauge(Source(2_005), 300, 65, 100);

        var frame = scene.CreateFrame();
        var detail = scene.CreateDetailDeltaWithin(frame.Snapshot, 100, 0, long.MaxValue);

        Assert.Equal([10d, 10d, 15d], detail.MetricEvents.Select(e => e.Stagger));
    }

    private static (SceneLiveReadModel Scene, IRuntimeObservationSink Sink) CreateBossFight()
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(10), 100, "Tank", characterClass: CharacterClass.Gladiator);
        sink.AppendNickname(Source(10), 101, "Healer", characterClass: CharacterClass.Cleric);
        sink.AppendNpcKind(Source(20), 300, NpcKind.Boss);
        sink.AppendSummon(Source(20), 101, 500);
        sink.SetNpcBattle(Source(25), 300, true);
        sink.AppendNpcHp(Source(30), 300, 10_000_000, 10_000_000);
        return (scene, sink);
    }

    private static void Cast(IRuntimeObservationSink sink, int sourceId, int targetId, int flag, long offsetMilliseconds) =>
        sink.RegisterCompactControl0238(Source(offsetMilliseconds) with { Opcode = 0x0238 }, sourceId, 0, 1_801_500, 2, flag, targetId);

    private static void AppendDamage(IRuntimeObservationSink sink, int sourceId, int targetId, int damage, long offsetMilliseconds, int skillCode = 11_000_010)
    {
        var observation = new CombatWireObservation
        {
            SkillCode = skillCode,
            Damage = damage,
            HitCount = 1,
            AttemptCount = 1
        };
        sink.AppendCombatWireObservation(Source(offsetMilliseconds), sourceId, targetId, in observation);
    }

    private static void AppendHealing(IRuntimeObservationSink sink, int sourceId, int targetId, int amount, long offsetMilliseconds)
    {
        var observation = new CombatWireObservation
        {
            SkillCode = 17_010_000,
            Damage = amount,
            ResourceKind = CombatResourceKind.Health
        };
        sink.AppendCombatWireObservation(Source(offsetMilliseconds), sourceId, targetId, in observation);
    }

    private static PacketObservationSource Source(long offsetMilliseconds) =>
        new(Started.ToUnixTimeMilliseconds() + offsetMilliseconds, 0, 0, 0, offsetMilliseconds, default);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
