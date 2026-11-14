using System.Globalization;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

// A training dummy can't die: it stops at 1 HP and is full again a moment later. Its pulls go by the user's own hits.
public sealed class TrainingDummyPullTests
{
    private const int Me = 100;
    private const int Other = 101;
    private const int Summon = 500;
    private const int Dummy = 300;
    private static readonly DateTimeOffset Started = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    // Other players' hits neither start a pull nor extend one, the dummy's HP means nothing, and what came in while the
    // cut-off ran (here another player's hit) leaves the pull once it's over.
    [Fact]
    public async Task A_Dummy_Pull_Is_The_Users_Hits_Until_They_Pause_For_The_Cut_Off()
    {
        var (scene, sink, clock) = CreateScene();
        Hit(sink, Other, Dummy, 2_000, 500);
        Hit(sink, Me, Dummy, 5_000, 1_000);
        Hit(sink, Summon, Dummy, 1_000, 2_000);
        Hit(sink, Other, Dummy, 2_000, 2_500);
        Hit(sink, Me, Dummy, 5_000, 3_000);
        sink.AppendNpcHp(Source(3_100), Dummy, 1);
        sink.AppendNpcHp(Source(3_400), Dummy, 120_000);
        Hit(sink, Other, Dummy, 9_999, 6_000);
        Hit(sink, Me, Dummy, 4_000, 9_000); // 6 s after the last: a new pull
        Hit(sink, Me, Dummy, 4_000, 10_000);

        // The clock ends the second one: its cut-off and a moment's grace for late hits have passed.
        clock.Now = Started.AddMilliseconds(17_000);
        var fights = scene.CreateFrame().BossFights;

        Assert.Equal(
            [(1, 1, BossFightOutcome.Stopped, true), (1, 2, BossFightOutcome.Stopped, true)],
            fights.Select(static f => (f.BossNumber, f.Attempt, f.Outcome, f.IsTrainingDummy)));
        var first = fights[0].Snapshot;
        Assert.Equal((1_000L, 3_000L, 2_000L), (first.EncounterStartTime, first.EncounterEndTime, first.EncounterTime));
        Assert.Equal((11_000L, 5_500d), (first.Combatants[Me].DamageAmount, first.Combatants[Me].DamagePerSecond));
        Assert.Equal(2_000, first.Combatants[Other].DamageAmount);
        var second = fights[1].Snapshot;
        Assert.Equal((9_000L, 10_000L, 8_000L), (second.EncounterStartTime, second.EncounterEndTime, second.Combatants[Me].DamageAmount));

        using var file = new MemoryStream();
        await SceneArchiveFile.WriteAsync(file, [new ArchivedEncounterRecord { ScenePayload = scene.CreateArchivePayload() }], TestContext.Current.CancellationToken);
        var imported = Assert.Single(await SceneArchiveFile.ReadAsync(new MemoryStream(file.ToArray()), TestContext.Current.CancellationToken));
        Assert.Equal(
            fights.Select(static f => (f.Attempt, f.Outcome, f.IsTrainingDummy, f.Snapshot.EncounterTime)),
            imported.ScenePayload.BossFights.AsSpan().ToArray().Select(static f => (f.Attempt, f.Outcome, f.IsTrainingDummy, f.Snapshot.EncounterTime)));
    }

    // With a length set, a pull ends when it's up; the user hitting on starts no new one until they pause for the cut-off.
    // A pull they paused in ends with their last hit, one still going when its length is up ends then.
    [Fact]
    public void A_Dummy_Pull_With_A_Length_Ends_When_It_Is_Up()
    {
        var (scene, sink, clock) = CreateScene(new TrainingDummyRules(9_500, 5_000));
        for (var at = 1_000; at <= 14_000; at += 1_000)
            Hit(sink, Me, Dummy, 1_000, at);
        Hit(sink, Me, Dummy, 1_000, 20_000);
        Hit(sink, Me, Dummy, 1_000, 21_000);
        clock.Now = Started.AddMilliseconds(27_100);
        scene.CreateFrame();
        Hit(sink, Me, Dummy, 1_000, 40_000);
        Hit(sink, Me, Dummy, 1_000, 45_000);
        Hit(sink, Me, Dummy, 1_000, 49_000);
        clock.Now = Started.AddMilliseconds(50_600);

        var fights = scene.CreateFrame().BossFights;

        Assert.Equal(
            [
                (1, BossFightOutcome.TimeUp, 1_000L, 10_500L, 10_000L),
                (2, BossFightOutcome.Stopped, 20_000L, 21_000L, 2_000L),
                (3, BossFightOutcome.TimeUp, 40_000L, 49_500L, 3_000L),
            ],
            fights.Select(static f => (f.Attempt, f.Outcome, f.Snapshot.EncounterStartTime, f.Snapshot.EncounterEndTime, f.Snapshot.Combatants[Me].DamageAmount)));
        Assert.All(fights, static f => Assert.Equal(9_500, f.LengthMilliseconds));
    }

    // Hits can reach the journal late (flushed sidecars, a summon's among them): one from inside a pull that was seen to be
    // over belongs to it, one from within the cut-off after its end takes it up again; neither starts a pull of its own.
    [Fact]
    public void A_Hit_That_Comes_In_Late_Joins_Its_Pull()
    {
        var (scene, sink, clock) = CreateScene();
        Hit(sink, Me, Dummy, 1_000, 1_000);
        Hit(sink, Me, Dummy, 1_000, 2_000);
        Hit(sink, Other, 401, 1_000, 9_000); // the scene's time moves on: the pull is over at 2 s
        Hit(sink, Summon, Dummy, 500, 1_500);
        Hit(sink, Summon, Dummy, 500, 4_000);
        clock.Now = Started.AddMilliseconds(20_000);

        var fight = Assert.Single(scene.CreateFrame().BossFights);
        Assert.Equal((BossFightOutcome.Stopped, 1_000L, 4_000L, 3_000L), (fight.Outcome, fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime, fight.Snapshot.Combatants[Me].DamageAmount));
    }

    // One that spawned before the overlay saw it has no NPC code; its refills from 1 HP give it away (the hit before that
    // starts nothing). A mob the game data knows and a player stay what they are.
    [Fact]
    public void An_Npc_Back_Up_From_1_Hp_Is_A_Training_Dummy()
    {
        const int unknown = 400;
        const int mob = 401;
        var (scene, sink, _) = CreateScene();
        sink.AppendNpcCode(Source(20), mob, 2_000_001);
        sink.AppendNpcKind(Source(20), mob, NpcKind.Monster);
        Hit(sink, Me, unknown, 5_000, 1_000);
        sink.AppendNpcHp(Source(1_050), unknown, 1);
        sink.AppendNpcHp(Source(1_400), unknown, 120_000);
        foreach (var id in (int[])[mob, Other])
        {
            sink.AppendNpcHp(Source(1_500), id, 1);
            sink.AppendNpcHp(Source(1_900), id, 50_000);
        }

        Hit(sink, Me, unknown, 5_000, 2_000);
        Hit(sink, Me, mob, 5_000, 2_100);

        var fight = Assert.Single(scene.CreateFrame().BossFights);
        Assert.Equal((unknown, true, 2_000L), (fight.BossId, fight.IsTrainingDummy, fight.Snapshot.EncounterStartTime));
        Assert.True(scene.Owner.Entities.TryGet(mob, out var monster) && monster.Kind == NpcKind.Monster);
        Assert.True(scene.Owner.Entities.TryGet(Other, out var player) && player.Kind != NpcKind.TrainingDummy);
    }

    // From a recording (Fixtures/training-dummies.txt): the user hit three dummies one after the other, about 25 s each, in a
    // city where other players hit the same dummies before and after them. The dummies had spawned before it, so each is
    // known from its first refill on: its pull starts with the user's first hit after that and ends with their last.
    [Fact]
    public void Three_Recorded_Dummies_Give_A_Pull_Each()
    {
        var (scene, sink, clock) = CreateScene(dummy: false);
        sink.AppendNickname(Source(10), 102, "Third", characterClass: CharacterClass.Assassin);
        sink.AppendSummon(Source(10), Me, 501);
        var last = 0L;
        var mine = new Dictionary<int, (long First, long Last)>();
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "training-dummies.txt")))
        {
            if (line.StartsWith('#'))
                continue;

            var parts = line.Split(' ');
            last = long.Parse(parts[1], CultureInfo.InvariantCulture);
            var id = int.Parse(parts[2], CultureInfo.InvariantCulture);
            if (parts[0] == "P")
            {
                sink.AppendNpcHp(Source(last), id, long.Parse(parts[3], CultureInfo.InvariantCulture));
                continue;
            }

            var dummy = int.Parse(parts[3], CultureInfo.InvariantCulture);
            Hit(sink, id, dummy, long.Parse(parts[4], CultureInfo.InvariantCulture), last);
            if (id == Me || id >= Summon)
                mine[dummy] = mine.TryGetValue(dummy, out var hits) ? (hits.First, last) : (last, last);
        }

        clock.Now = Started.AddMilliseconds(last + 10_000);
        var fights = scene.CreateFrame().BossFights;

        Assert.Equal([300, 301, 302], fights.Select(static f => f.BossId));
        Assert.All(fights, f =>
        {
            Assert.Equal((1, BossFightOutcome.Stopped, true), (f.Attempt, f.Outcome, f.IsTrainingDummy));
            Assert.Equal(mine[f.BossId].Last, f.Snapshot.EncounterEndTime);
            Assert.InRange(f.Snapshot.EncounterStartTime, mine[f.BossId].First, mine[f.BossId].First + 2_500);
            Assert.InRange(f.Snapshot.Combatants[Me].DamageAmount, 900_000, 1_700_000);
        });
    }

    private static (SceneLiveReadModel Scene, IRuntimeObservationSink Sink, Clock Clock) CreateScene(TrainingDummyRules? rules = null, bool dummy = true)
    {
        var clock = new Clock(Started);
        var scene = new SceneLiveReadModel(Started, clock);
        scene.SetTrainingDummyRules(rules ?? TrainingDummyRules.Default);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(10), Me, "Me", characterClass: CharacterClass.Cleric, isLocalPlayer: true, originServerId: 2304);
        sink.AppendNickname(Source(10), Other, "Other", characterClass: CharacterClass.Templar);
        sink.AppendSummon(Source(10), Me, Summon);
        if (dummy)
            sink.AppendNpcKind(Source(20), Dummy, NpcKind.TrainingDummy);
        return (scene, sink, clock);
    }

    private static void Hit(IRuntimeObservationSink sink, int sourceId, int targetId, long damage, long offsetMilliseconds)
    {
        var observation = new CombatWireObservation { SkillCode = 11_000_010, Damage = damage, HitCount = 1, AttemptCount = 1 };
        sink.AppendCombatWireObservation(Source(offsetMilliseconds), sourceId, targetId, in observation);
    }

    private static PacketObservationSource Source(long offsetMilliseconds) =>
        new(Started.ToUnixTimeMilliseconds() + offsetMilliseconds, 0, 0, 0, offsetMilliseconds, default);

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
