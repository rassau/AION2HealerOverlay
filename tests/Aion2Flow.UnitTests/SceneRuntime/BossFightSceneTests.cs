using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.SceneRuntime.Stores;

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

    // Out in the open world a boss can be hit and left (the group walks on, strangers fight it out of sight): it never
    // dies or heals back to full where the overlay sees it. Five minutes after its last action its pull ends there as a
    // failed attempt, with only what came in up to that action, and takes nothing more; hitting it again is attempt 2.
    [Fact]
    public void A_Boss_Left_Alone_For_Five_Minutes_Ends_Its_Pull_At_Its_Last_Action()
    {
        var (scene, sink) = CreateBossFight();
        sink.AppendBossTarget(Source(900), 100);
        AppendDamage(sink, 100, 300, 5_000, 1_000);
        AppendHealing(sink, 101, 100, 1_500, 1_500);
        Cast(sink, 300, 100, flag: 2, 2_000);
        AppendDamage(sink, 300, 100, 2_000, 2_500);
        // Ten minutes on mobs; the boss isn't heard of again, but its frame goes on naming the healer.
        sink.AppendNpcKind(Source(3_000), 400, NpcKind.Monster);
        sink.AppendBossTarget(Source(4_000), 101);
        for (var at = 10_000; at <= 600_000; at += 10_000)
        {
            AppendDamage(sink, 100, 400, 1_000, at);
            AppendHealing(sink, 101, 100, 500, at + 1);
        }

        var fight = Assert.Single(scene.CreateFrame().BossFights);

        Assert.Equal((1, BossFightOutcome.Reset, 10_000_000L), (fight.Attempt, fight.Outcome, fight.BossHp));
        Assert.Equal((1_000L, 2_500L), (fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime));
        var tank = fight.Snapshot.Combatants[100];
        var healer = fight.Snapshot.Combatants[101];
        Assert.Equal((5_000L, 2_000L, 1_500L), (tank.DamageAmount, tank.DamageTakenAmount, healer.HealingAmount));
        // The frame's time can't be told apart by when, so it stays: the tank 1-4 s, the healer 4-300 s.
        Assert.Equal(0.01, tank.AggroShare, 2);
        Assert.Equal(0.99, healer.AggroShare, 2);

        AppendHealing(sink, 101, 100, 500, 610_000);
        Assert.Same(fight, Assert.Single(scene.CreateFrame().BossFights));

        AppendDamage(sink, 100, 300, 7_000, 620_000);
        var fights = scene.CreateFrame().BossFights;
        Assert.Equal([(1, BossFightOutcome.Reset), (2, BossFightOutcome.Ongoing)], fights.Select(static f => (f.Attempt, f.Outcome)));
        Assert.Equal((620_000L, 7_000L), (fights[1].Snapshot.EncounterStartTime, fights[1].Snapshot.Combatants[100].DamageAmount));
    }

    // Events go by the capture's clock, the meter by the wall clock: set ahead after the capture started (time sync), it
    // mustn't end a pull whose boss is still at it.
    [Fact]
    public void A_Wall_Clock_Set_Ahead_Ends_No_Pull()
    {
        var (scene, sink) = CreateBossFight(now: Started.AddSeconds(400));
        AppendDamage(sink, 100, 300, 5_000, 1_000);
        scene.CreateFrame();
        AppendDamage(sink, 100, 300, 5_000, 2_000);

        var fight = Assert.Single(scene.CreateFrame().BossFights);

        Assert.Equal((BossFightOutcome.Ongoing, 10_000L), (fight.Outcome, fight.Snapshot.Combatants[100].DamageAmount));
    }

    // HP comes in while nobody fights too: a left boss seen again at full HP finds its pull over at its last action, not
    // ended at that late update with all that came in meanwhile.
    [Fact]
    public void A_Left_Boss_Seen_Again_Finds_Its_Pull_Over()
    {
        var (scene, sink) = CreateBossFight();
        AppendDamage(sink, 100, 300, 5_000, 1_000);
        Cast(sink, 300, 100, flag: 2, 1_050);
        sink.AppendNpcHp(Source(1_100), 300, 9_995_000, 10_000_000);
        AppendHealing(sink, 101, 100, 1_500, 200_000);
        sink.AppendNpcHp(Source(400_000), 300, 10_000_000, 10_000_000);

        var fight = Assert.Single(scene.CreateFrame().BossFights);

        Assert.Equal((BossFightOutcome.Reset, 9_995_000L, 1_000L), (fight.Outcome, fight.BossHp, fight.Snapshot.EncounterEndTime));
        // No boss frame (the open world): the aggro is the boss's aimed cast, which the pull keeps as it is cut back.
        Assert.Equal(1.0, fight.Snapshot.Combatants[100].AggroShare);
    }

    // Open world, unequal casts at two players: the trim keeps how many there were, not only at whom.
    [Fact]
    public void A_Left_Boss_Keeps_Its_Aimed_Casts_By_Count()
    {
        var (scene, sink) = CreateBossFight();
        AppendDamage(sink, 100, 300, 5_000, 1_000);
        AppendHealing(sink, 101, 100, 1_500, 1_050);
        Cast(sink, 300, 100, flag: 2, 1_100);
        Cast(sink, 300, 100, flag: 2, 1_200);
        Cast(sink, 300, 100, flag: 2, 1_300);
        Cast(sink, 300, 101, flag: 2, 1_400);
        AppendHealing(sink, 101, 100, 1_500, 200_000);
        AppendHealing(sink, 101, 100, 1_500, 400_000);

        var fight = Assert.Single(scene.CreateFrame().BossFights);

        Assert.Equal((0.75, 0.25), (fight.Snapshot.Combatants[100].AggroShare, fight.Snapshot.Combatants[101].AggroShare));
        Assert.Equal((BossFightOutcome.Reset, 1_000L, 1_050L), (fight.Outcome, fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime));
    }

    // The left boss's own aimed cast is a new pull too, not the old one going on.
    [Fact]
    public void A_Left_Boss_Casting_Again_Is_A_New_Attempt()
    {
        var (scene, sink) = CreateBossFight();
        AppendDamage(sink, 100, 300, 5_000, 1_000);
        Cast(sink, 300, 100, flag: 2, 400_000);

        Assert.Equal([(1, BossFightOutcome.Reset), (2, BossFightOutcome.Ongoing)], scene.CreateFrame().BossFights.Select(static f => (f.Attempt, f.Outcome)));
    }

    // A boss pulled along with another can wait its turn (seen: 228 s without a hit or an aimed cast): its pull goes on.
    [Fact]
    public void A_Boss_Waiting_Its_Turn_Keeps_Its_Pull()
    {
        var (scene, sink) = CreateBossFight();
        sink.AppendNpcKind(Source(20), 301, NpcKind.Boss);
        sink.AppendNpcHp(Source(30), 301, 5_000_000, 5_000_000);
        AppendDamage(sink, 100, 300, 1_000, 1_000);
        for (var at = 2_000; at <= 280_000; at += 2_000)
            AppendDamage(sink, 100, 301, 1_000, at);
        sink.AppendNpcHp(Source(280_500), 301, 0, 5_000_000);
        AppendDamage(sink, 100, 300, 9_000, 290_000);
        sink.AppendNpcHp(Source(291_000), 300, 0, 10_000_000);

        var fights = scene.CreateFrame().BossFights;

        Assert.Equal([(300, 1, BossFightOutcome.Killed), (301, 1, BossFightOutcome.Killed)], fights.Select(static f => (f.BossId, f.Attempt, f.Outcome)));
        Assert.Equal((1_000L, 290_000L, 10_000L), (fights[0].Snapshot.EncounterStartTime, fights[0].Snapshot.EncounterEndTime, fights[0].Snapshot.Combatants[100].DamageAmount));
    }

    // A boss beating on another boss-sized NPC (seen: a boss hitting a 28 M HP NPC of its own fight) isn't the group
    // pulling that NPC: no fight for it, before the pull or during it, and so no failed attempt in the history.
    [Fact]
    public void A_Boss_Hitting_Another_Boss_Sized_Npc_Is_No_Pull()
    {
        var (scene, sink) = CreateBossFight();
        sink.AppendNpcKind(Source(20), 301, NpcKind.Boss);
        sink.AppendNpcHp(Source(30), 301, 28_000_000, 28_000_000);
        AppendDamage(sink, 300, 301, 13_000, 500);
        AppendDamage(sink, 100, 300, 5_000, 1_000);
        AppendDamage(sink, 300, 301, 13_000, 2_000);

        var fight = Assert.Single(scene.CreateFrame().BossFights);
        Assert.Equal((300, 5_000L), (fight.BossId, fight.Snapshot.Combatants[100].DamageAmount));
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

    // The fight graphs' seconds add up to the fight's columns: damage on the boss only (an add's hit counts for the time,
    // not the damage), the summon's hits as its owner's, healing, damage taken; the boss frame's time as aggro.
    [Fact]
    public void A_Fights_Seconds_Add_Up_To_Its_Columns()
    {
        var (scene, sink) = CreateBossFight();
        sink.AppendBossTarget(Source(900), 100);
        AppendDamage(sink, 100, 300, 5_000, 1_000);
        AppendDamage(sink, 500, 300, 700, 1_500);
        AppendDamage(sink, 100, 301, 999, 2_000);
        AppendDamage(sink, 300, 100, 2_000, 2_500);
        AppendHealing(sink, 101, 100, 1_500, 3_000);
        AppendDamage(sink, 100, 300, 3_000, 4_200);
        sink.AppendNpcHp(Source(5_000), 300, 0, 10_000_000);

        var frame = scene.CreateFrame();
        var fight = Assert.Single(frame.BossFights);
        var (from, to) = (fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime);
        var timeline = FightTimeline.Build(from, to, fight.BossId, [100, 101], id => scene.CreateDetailDeltaWithin(frame.Snapshot, id, from, to).MetricEvents,
            scene.Owner.CreateLiveTimelineSegment(), sourcesOf: 101, cancellationToken: TestContext.Current.CancellationToken);

        foreach (var player in (int[])[100, 101])
        {
            var columns = fight.Snapshot.Combatants[player];
            Assert.Equal(columns.DamageAmount, timeline.Values[player][(int)FightMetric.Damage].Sum());
            Assert.Equal(columns.HealingAmount, timeline.Values[player][(int)FightMetric.Healing].Sum());
            Assert.Equal(columns.DamageTakenAmount, timeline.Values[player][(int)FightMetric.DamageTaken].Sum());
        }

        Assert.Equal(700, timeline.Values[101][(int)FightMetric.Damage].Sum());
        Assert.Equal((to - from) / 1000d, timeline.Values[100][(int)FightMetric.Aggro].Sum(), 6);
    }

    // A stretch's events, made only for that stretch (the fight graphs, a fight's details, best runs, a history entry's
    // pulls), are the scene's events cut to it: the same events in the same order, for players, a summon, the boss, an
    // unknown id, in several storage segments with hits that came late or early, also with an older snapshot.
    [Fact]
    public void A_Stretchs_Events_Are_The_Scenes_Cut_To_It()
    {
        var (scene, sink) = CreateBossFight();
        AppendDamage(sink, 100, 300, 5_000, 1_000);
        var early = scene.CreateFrame().Snapshot;
        for (var i = 0; i < 1_300; i++)
        {
            // Late arrivals: one of a later time in the first storage segment, one of an early time in the third.
            var at = i == 100 ? 20_000 : i == 1_100 ? 1_500 : 1_000 + i * 10;
            if (i % 4 == 3)
                AppendHealing(sink, 101, 100, 50 + i, at);
            else
                AppendDamage(sink, i % 4 == 1 ? 500 : 100, i % 7 == 0 ? 301 : 300, 100 + i, at);
            if (i % 9 == 0)
                AppendDamage(sink, 300, i % 2 == 0 ? 100 : 101, 20 + i, at + 1);
        }

        var frame = scene.CreateFrame();
        var payload = scene.CreateArchivePayload();
        int[] ids = [100, 101, 500, 300, 999, 0];
        foreach (var snapshot in (SceneCombatSnapshot[])[frame.Snapshot, early])
        {
            foreach (var (from, to) in ((long, long)[])[(0, long.MaxValue), (4_000, 9_000), (15_000, 25_000), (1_500, 1_500), (9_000, 4_000), (30_000, 40_000)])
            {
                var events = scene.CreateMetricEventsWithin(snapshot, ids, from, to);
                foreach (var id in ids)
                {
                    var whole = scene.Owner.ReadLocked((_, _, _, _, _, _, adapter) => adapter.CreateDetailEvents(snapshot, id));
                    var expected = new CombatDetailDelta { MetricEvents = whole.MetricEvents, MechanicEvents = whole.MechanicEvents, ResourceEvents = whole.ResourceEvents }
                        .Within(from, to);
                    var detail = scene.CreateDetailDeltaWithin(snapshot, id, from, to);
                    Assert.Equal(expected.MetricEvents, events(id));
                    Assert.Equal(expected.MetricEvents, detail.MetricEvents);
                    Assert.Equal(expected.MechanicEvents, detail.MechanicEvents);
                    Assert.Equal(expected.ResourceEvents, detail.ResourceEvents);
                    Assert.Equal(payload.CreateDetailDelta(id).Within(from, to).MetricEvents, payload.CreateMetricEventsWithin(id, from, to));
                }
            }
        }

        Assert.Contains(scene.CreateMetricEventsWithin(frame.Snapshot, ids, 15_000, 25_000)(100), static e => e.ObservedAtMilliseconds == 20_000);
        Assert.Empty(scene.CreateMetricEventsWithin(frame.Snapshot, [], 0, long.MaxValue)(100));
    }

    // A snapshot from before the scene moved on (a map change, the next pull in boss mode) says nothing about the scene now:
    // a stretch of it gets none of the new scene's events, which start at 0 again.
    [Fact]
    public void A_Snapshot_Of_An_Earlier_Encounter_Gets_No_Events()
    {
        var (scene, sink) = CreateBossFight();
        AppendDamage(sink, 100, 300, 5_000, 1_000);
        AppendDamage(sink, 100, 300, 5_000, 3_000);
        var before = scene.CreateFrame().Snapshot;

        scene.Reset(() => Started);
        AppendDamage(sink, 100, 300, 7_000, 1_500);
        AppendDamage(sink, 100, 300, 7_000, 2_500);
        var now = scene.CreateFrame().Snapshot;

        Assert.Empty(scene.CreateDetailDeltaWithin(before, 100, 0, long.MaxValue).MetricEvents);
        Assert.Empty(scene.CreateMetricEventsWithin(before, [100], 0, long.MaxValue)(100));
        Assert.Equal(2, scene.CreateDetailDeltaWithin(now, 100, 0, long.MaxValue).MetricEvents.Count);
        Assert.Equal(2, scene.CreateMetricEventsWithin(now, [100], 0, long.MaxValue)(100).Count);
    }

    // The fight graphs work a pull still going out anew each second, taking up the last build where nothing can have
    // changed since. However the scene goes on, each refresh equals a build from scratch (the scene's whole events cut to
    // the pull, the journal from its start), with overhealing and without: more hits and heals, a hit that comes late with an
    // earlier time, stagger credited to hits already read (in the last refresh's last second; credits to a second it kept:
    // the next test), an identity change that changes no event's fate and one that does (an entity found out to be a
    // player's summon), and a refresh with the same end. A build unlike the last one doesn't take it up: an earlier end (a
    // training dummy's pull cut back to its end), the other overhealing setting, another boss, the user found out, other
    // players of the same number.
    [Fact]
    public void A_Refresh_Taking_Up_The_Last_One_Is_A_Build_From_Scratch()
    {
        var (scene, sink) = CreateBossFight();
        sink.AppendNpcHp(Source(500), 100, 9_000, 10_000);
        sink.AppendStaggerGauge(Source(600), 300, 100, 100);
        // A skill no fixture puts into the shared registry as a player's: entity 600 must stay a non-player until step 16.
        AppendDamage(sink, 600, 300, 900, 800, skillCode: 11_000_020);
        var previous = new FightTimeline?[2];
        var firstSeconds = new List<int>();
        var hp = 9_000;
        var gauge = 100;
        for (var step = 1; step <= 24; step++)
        {
            var at = step * 1_000L;
            AppendDamage(sink, 100, 300, 1_000 + step, at + 100);
            AppendDamage(sink, 500, 300, 300 + step, at + 150, skillCode: 11_000_020);
            AppendHealing(sink, 101, 100, 700, at + 250);
            AppendDamage(sink, 300, 100, 400, at + 300);
            if (step == 8)
                AppendDamage(sink, 100, 300, 777, 3_500);
            if (step == 12)
                sink.AppendNickname(Source(at + 450), 102, "Bystander", characterClass: CharacterClass.Ranger);
            if (step == 16)
                sink.AppendSummon(Source(at + 450), 100, 600);
            firstSeconds.Add(RefreshAndCompare(scene, previous, $"step {step}"));
            // After the refresh read them: the tank's HP (no real healing here, as no flush completes) and, every third
            // step, a gauge drop, whose stagger goes to a hit in the refresh's last second.
            hp = Math.Min(10_000, hp - 400 + 700);
            sink.AppendNpcHp(Source(at + 500), 100, hp, 10_000);
            if (step % 3 == 0 && gauge > 10)
                sink.AppendStaggerGauge(Source(at + 110), 300, gauge -= 10, 100);
            if (step % 5 == 0)
                firstSeconds.Add(RefreshAndCompare(scene, previous, $"step {step}, the same end"));
        }

        // Taken up whenever it can be: not for the first refreshes (a pull of less than a second), nor after the identity
        // change that changes a fate (step 16); the late hit (3.5 s, step 8) is read again from its second on, and a credit
        // to a read hit (stagger) from the first second of its block of 64 events on.
        Assert.Equal([0, 0], firstSeconds[..2]);
        Assert.Equal(2, firstSeconds[8]);
        Assert.Equal(10, firstSeconds[13]); // step 12: a name changes no fate, so the refresh takes the last one up
        Assert.Equal(0, firstSeconds[18]);
        Assert.All(firstSeconds[19..], static second => Assert.True(second >= 15));
        Assert.True(scene.Owner.ReadLocked(static (_, _, _, combat, _, _, _) => combat.Events.Count(static e => e.Stagger > 0)) > 0);

        var from = Assert.Single(scene.CreateFrame().BossFights).Snapshot.EncounterStartTime;
        RefreshAndCompare(scene, [.. previous], "an earlier end", end: from + 3_500);
        RefreshAndCompare(scene, [previous[1], previous[0]], "the other overhealing setting");
        RefreshAndCompare(scene, [.. previous], "another boss", bossId: 301);
        RefreshAndCompare(scene, previous, "the user unknown", me: 0);
        RefreshAndCompare(scene, previous, "the user found out");
        // The bystander joins in as the healer drops out: the take-up would miss the bystander's values.
        AppendDamage(sink, 102, 300, 500, 24_900, skillCode: 11_000_020);
        RefreshAndCompare(scene, previous, "other players of the same number", players: [100, 102]);
    }

    // Real healing and stagger can reach events after a refresh read them, in a second it kept: the pull's end had moved on
    // into the next second before the HP or the gauge drop came. Each credit stamps its block of 64 events, so the next
    // refresh reads them again from that block's first second on (a hundred hits on an add after each step's events make
    // the credited block an earlier one than the newest).
    [Fact]
    public void A_Refresh_Reads_Again_What_Got_A_Late_Credit_Since()
    {
        var (scene, sink) = CreateBossFight();
        sink.AppendNpcHp(Source(500), 100, 5_000, 10_000);
        sink.AppendStaggerGauge(Source(600), 300, 100, 100);
        AppendDamage(sink, 100, 300, 1_000, 800);
        var previous = new FightTimeline?[2];
        var firstSeconds = new List<int>();
        var hp = 5_000;
        var gauge = 100;
        for (var step = 1; step <= 6; step++)
        {
            var at = step * 1_000L;
            var flush = 2L * step;
            if (step % 2 == 1)
                AppendHealing(sink, 101, 100, 700, at + 750, flush);
            // The second hit moves the end into the next second: the refresh keeps the heal's second and the first hit's.
            AppendDamage(sink, 100, 300, 1_000, at + 790, flushId: flush);
            AppendDamage(sink, 100, 300, 1_000, at + 810, flushId: flush);
            for (var i = 0; i < 100; i++)
                AppendDamage(sink, 100, 400, 10, at + 820 + i, flushId: flush);
            sink.CompleteFlush(flush);
            firstSeconds.Add(RefreshAndCompare(scene, previous, $"step {step}"));
            // By turns, each alone in a flush of its own: the HP the heal brought (its real healing), a gauge drop (both
            // hits' stagger).
            if (step % 2 == 1)
                sink.AppendNpcHp(Source(at + 900, flush + 1), 100, hp += 700, 10_000);
            else
                sink.AppendStaggerGauge(Source(at + 815, flush + 1), 300, gauge -= 10, 100);
            sink.CompleteFlush(flush + 1);
        }

        firstSeconds.Add(RefreshAndCompare(scene, previous, "after the last credit"));
        // From the first second of the credited block, the one before the newest. Without the stamps, or with the newest block
        // stamped, each would read again only from the last refresh's last second on (1, 2, ...) and miss the credit.
        Assert.Equal([0, 0, 1, 2, 3, 4, 5], firstSeconds);
        Assert.Equal((3, 6), scene.Owner.ReadLocked(static (_, _, _, combat, _, _, _) =>
            (combat.Events.Count(static e => e.EffectiveHealing > 0), combat.Events.Count(static e => e.Stagger > 0))));
    }

    // What decides an event's fate about an entity can change alone after a refresh read the event: a heal on an entity
    // found out to be an NPC no longer counts (its NPC fact, as the heal's target), nor do hits on an NPC found out to be a
    // summon (its summon fact; the graphs count hits only on the boss and on players, so an add stands in for the boss, as
    // no boss turns out to be a summon). The next refresh reads the pull again from its start.
    [Fact]
    public void A_Refresh_After_A_Target_Was_Found_Out_Is_A_Build_From_Scratch()
    {
        var (scene, sink) = CreateBossFight();
        sink.AppendNpcKind(Source(50), 400, NpcKind.Monster);
        var previous = new FightTimeline?[2];
        var firstSeconds = new List<int>();
        for (var step = 1; step <= 7; step++)
        {
            var at = step * 1_000L;
            AppendDamage(sink, 100, 300, 1_000, at + 100);
            AppendDamage(sink, 100, 400, 500, at + 150);
            AppendHealing(sink, 101, 700, 300, at + 200);
            if (step == 4)
                sink.AppendNpcKind(Source(at + 250), 700, NpcKind.Monster);
            if (step == 6)
                sink.AppendNpcKind(Source(at + 250), 400, NpcKind.Summon);
            firstSeconds.Add(RefreshAndCompare(scene, previous, $"step {step}", bossId: 400));
        }

        // Taken up from the last refresh's last second on, but read again from the start after each of the two.
        Assert.Equal([0, 0, 1, 0, 3, 0, 4], firstSeconds);
    }

    // An entity's owner can change alone too: an entity known to be a summon from the start (its NPC kind) gets its owner
    // later, while its summon and NPC facts stay as they were. From then on its hits on the boss count for the owner, in
    // every second of the pull.
    [Fact]
    public void A_Refresh_After_A_Summons_Owner_Was_Found_Out_Is_A_Build_From_Scratch()
    {
        var (scene, sink) = CreateBossFight();
        sink.AppendNpcKind(Source(50), 800, NpcKind.Summon);
        var previous = new FightTimeline?[2];
        var firstSeconds = new List<int>();
        for (var step = 1; step <= 6; step++)
        {
            var at = step * 1_000L;
            AppendDamage(sink, 100, 300, 1_000, at + 100);
            AppendDamage(sink, 800, 300, 500, at + 150, skillCode: 11_000_020);
            if (step == 4)
                sink.AppendSummon(Source(at + 250), 100, 800);
            firstSeconds.Add(RefreshAndCompare(scene, previous, $"step {step}"));
        }

        // Taken up from the last refresh's last second on, but read again from the start after the owner was found out.
        Assert.Equal([0, 0, 1, 0, 3, 4], firstSeconds);
    }

    // A checkpoint is taken up only for the same pull read alike; anything else reads the pull from its start. Changed by
    // hand here: another start (FightTimeline checks that too), another encounter, more events than the store holds (within
    // an encounter it only grows), other players that could be shown; and a pull no longer inside the scene's window, at
    // its end or at its start.
    [Fact]
    public void A_Checkpoint_Unlike_The_Read_Is_Not_Taken_Up()
    {
        var (scene, sink) = CreateBossFight();
        for (var at = 1_000L; at <= 6_000; at += 1_000)
            AppendDamage(sink, 100, 300, 1_000, at);
        var snapshot = scene.CreateFrame().Snapshot;
        var (from, to) = (snapshot.EncounterStartTime, snapshot.EncounterEndTime);
        var since = scene.CreateFightEvents(snapshot, [100, 101], from, to - 1_000, null).Checkpoint!;

        Assert.Equal(3, FirstSecond(from, to, since));
        Assert.Equal(0, FirstSecond(from + 1_000, to, since));
        Assert.Equal(0, FirstSecond(from, to, since with { EncounterId = Guid.NewGuid() }));
        Assert.Equal(0, FirstSecond(from, to, since with { EventCount = since.EventCount + 1 }));
        Assert.Equal(0, FirstSecond(from, to, since with { Projectable = [true, true] }));
        Assert.Equal(0, FirstSecond(from, to + 1_000, since));
        Assert.Equal(0, FirstSecond(from - 1_000, to, since with { From = from - 1_000 }));

        int FirstSecond(long fromMilliseconds, long toMilliseconds, FightEventsCheckpoint checkpoint) =>
            scene.CreateFightEvents(snapshot, [100, 101], fromMilliseconds, toMilliseconds, checkpoint).FirstSecond;
    }

    // A pull seen to be left moves its end back to the boss's last action: the refresh after that is still a build from
    // scratch, however often the graphs were refreshed while the boss waited (every 10 s, every minute, never).
    [Theory]
    [InlineData(10_000)]
    [InlineData(60_000)]
    [InlineData(0)]
    public void A_Refresh_After_The_Pull_Was_Left_Is_A_Build_From_Scratch(int refreshEveryMilliseconds)
    {
        var (scene, sink) = CreateBossFight();
        var previous = new FightTimeline?[2];
        for (var step = 1; step <= 20; step++)
        {
            var at = step * 1_000L;
            AppendDamage(sink, 100, 300, 1_000 + step, at + 100);
            AppendHealing(sink, 101, 100, 700, at + 250);
            AppendDamage(sink, 300, 100, 400, at + 300);
            RefreshAndCompare(scene, previous, $"step {step}");
        }

        // The group heals and fights mobs for more than five minutes.
        sink.AppendNpcKind(Source(20_500), 400, NpcKind.Monster);
        for (var at = 21_000L; at <= 340_000; at += 1_000)
        {
            AppendHealing(sink, 101, 100, 500, at);
            AppendDamage(sink, 100, 400, 100, at + 1);
            if (refreshEveryMilliseconds > 0 && at % refreshEveryMilliseconds == 0 && at < 320_000)
                RefreshAndCompare(scene, previous, $"waiting at {at}");
        }

        var fight = Assert.Single(scene.CreateFrame().BossFights);
        Assert.Equal((BossFightOutcome.Reset, 20_300L), (fight.Outcome, fight.Snapshot.EncounterEndTime));
        RefreshAndCompare(scene, previous, "left");
        RefreshAndCompare(scene, previous, "left, again");
    }

    // A refresh of the pull up to its end (or an earlier one) in two chains, overhealing on and off, each taking up its last
    // build (previous) as the fight graphs do, and each the same as a build from scratch: the scene's whole events cut to
    // the pull, the journal from its start. Returns the first second the first chain read the events from again.
    private static int RefreshAndCompare(SceneLiveReadModel scene, FightTimeline?[] previous, string label, long? end = null, int bossId = 300, int me = 100,
        int[]? players = null)
    {
        players ??= [100, 101];
        var frame = scene.CreateFrame();
        var fight = Assert.Single(frame.BossFights);
        var (from, to) = (fight.Snapshot.EncounterStartTime, end ?? fight.Snapshot.EncounterEndTime);
        var journal = scene.Owner.CreateLiveTimelineSegment().CreateBoundedSnapshot();
        var firstSecond = 0;
        for (var chain = 0; chain < previous.Length; chain++)
        {
            var overhealing = chain == 0;
            var timeline = FightTimeline.Build(from, to, bossId, players, since =>
            {
                var events = scene.CreateFightEvents(frame.Snapshot, players, from, to, since);
                if (overhealing)
                    firstSecond = events.FirstSecond;
                return events;
            }, journal, me, overhealing, TestContext.Current.CancellationToken, previous[chain]);
            var expected = FightTimeline.Build(from, to, bossId, players, id =>
            {
                var whole = scene.Owner.ReadLocked((_, _, _, _, _, _, adapter) => adapter.CreateDetailEvents(frame.Snapshot, id));
                return new CombatDetailDelta { MetricEvents = whole.MetricEvents }.Within(from, to).MetricEvents;
            }, journal, me, overhealing, TestContext.Current.CancellationToken);
            Assert.Equal(expected.Seconds, timeline.Seconds);
            foreach (var (player, values) in expected.Values)
            {
                for (var metric = 0; metric < values.Length; metric++)
                    Assert.True(values[metric].AsSpan().SequenceEqual(timeline.Values[player][metric]), $"{label}, player {player}, metric {(FightMetric)metric}");
            }

            Assert.Equal(expected.Deaths, timeline.Deaths);
            Assert.Equal(expected.Ping, timeline.Ping);
            Assert.Equal(expected.Sources.Length, timeline.Sources.Length);
            for (var metric = 0; metric < expected.Sources.Length; metric++)
            {
                for (var second = 0; second < expected.Sources[metric].Length; second++)
                    Assert.Equal(expected.Sources[metric][second], timeline.Sources[metric][second]);
            }

            previous[chain] = timeline;
        }

        return firstSecond;
    }

    private static (SceneLiveReadModel Scene, IRuntimeObservationSink Sink) CreateBossFight(DateTimeOffset? now = null)
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(now ?? Started));
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

    private static void AppendDamage(IRuntimeObservationSink sink, int sourceId, int targetId, int damage, long offsetMilliseconds, int skillCode = 11_000_010, long flushId = 0)
    {
        var observation = new CombatWireObservation
        {
            SkillCode = skillCode,
            Damage = damage,
            HitCount = 1,
            AttemptCount = 1
        };
        sink.AppendCombatWireObservation(Source(offsetMilliseconds, flushId), sourceId, targetId, in observation);
    }

    private static void AppendHealing(IRuntimeObservationSink sink, int sourceId, int targetId, int amount, long offsetMilliseconds, long flushId = 0)
    {
        var observation = new CombatWireObservation
        {
            SkillCode = 17_010_000,
            Damage = amount,
            ResourceKind = CombatResourceKind.Health
        };
        sink.AppendCombatWireObservation(Source(offsetMilliseconds, flushId), sourceId, targetId, in observation);
    }

    private static PacketObservationSource Source(long offsetMilliseconds, long flushId = 0) =>
        new(Started.ToUnixTimeMilliseconds() + offsetMilliseconds, flushId, 0, 0, offsetMilliseconds, default);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TestTimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
