using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

// The meter's kill columns: Kills (most damage), Last hits (the game's kill message) and Credited (the game's count of the
// player's kills), as (Kills, LastHits, Credited). Kills are settled once a batch of packets is complete, so the entries
// here come in batch 1, completed after each part (batch 0 never is).
public sealed class KillColumnsTests
{
    private const int Abcd = 100, Efgh = 101, Spirit = 500, Boss = 300, OtherBoss = 301, Mob = 400, OtherMob = 401;
    private static readonly DateTimeOffset Started = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    // Efgh and their summon took 5,500 off, Abcd 4,000 plus the killing blow's last 500 (of 50,000): Efgh's kill. Counting
    // the overkill, the summon on its own or the hit after the death would make it Abcd's. The kill message names Abcd.
    [Fact]
    public void A_Kill_Goes_To_Most_Damage_With_The_Killing_Blow_Capped_And_Summons_For_Their_Owner()
    {
        var (scene, sink) = CreateScene();
        sink.AppendNpcHp(Source(1_000), Mob, 10_000, 10_000);
        AppendDamage(sink, Abcd, Mob, 4_000, 1_100);
        sink.AppendNpcHp(Source(1_100), Mob, 6_000);
        AppendDamage(sink, Efgh, Mob, 3_000, 1_200);
        AppendDamage(sink, Spirit, Mob, 2_500, 1_300);
        sink.AppendNpcHp(Source(1_300), Mob, 500);
        AppendDamage(sink, Abcd, Mob, 50_000, 1_400);
        sink.AppendNpcHp(Source(1_400), Mob, 0);
        AppendDamage(sink, Abcd, Mob, 9_000, 1_420);
        sink.AppendKill(Source(1_450), Mob, Abcd);
        sink.CompleteFlush(1);

        var snapshot = scene.CreateFrame().Snapshot;

        Assert.Equal((0, 1, 0), Counts(snapshot.Combatants[Abcd]));
        Assert.Equal((1, 0, 0), Counts(snapshot.Combatants[Efgh]));
    }

    // An NPC's hits take HP off but make no one's kill: the boss's blast took 7,000 of its add's 10,000, Abcd 2,000 and Efgh
    // the last 1,000. Abcd's kill.
    [Fact]
    public void Only_Players_Compete_For_A_Kill()
    {
        var (scene, sink) = CreateScene();
        sink.AppendNpcKind(Source(20), Boss, NpcKind.Boss);
        sink.AppendNpcHp(Source(1_000), Mob, 10_000, 10_000);
        AppendDamage(sink, Boss, Mob, 7_000, 1_100);
        AppendDamage(sink, Abcd, Mob, 2_000, 1_200);
        AppendDamage(sink, Efgh, Mob, 5_000, 1_300);
        sink.AppendNpcHp(Source(1_300), Mob, 0);
        sink.CompleteFlush(1);

        var snapshot = scene.CreateFrame().Snapshot;

        Assert.Equal((1, 0), (snapshot.Combatants[Abcd].Kills, snapshot.Combatants[Efgh].Kills));
    }

    // The game data files many adds as summons, with no owner or an NPC one (a 10-player force dungeon, July 2026: 101 of
    // the 110 NPCs the players hit before they died): they count like any NPC. A summon linked to a player doesn't.
    [Fact]
    public void Adds_The_Game_Data_Calls_Summons_Count_Unless_A_Player_Owns_Them()
    {
        var (scene, sink) = CreateScene();
        sink.AppendNpcKind(Source(20), Boss, NpcKind.Boss);
        sink.AppendNpcKind(Source(20), Mob, NpcKind.Summon);
        sink.AppendSummon(Source(20), Boss, OtherMob);
        Kill(sink, Abcd, Mob, 1_000);
        sink.AppendKill(Source(1_050), Mob, Abcd);
        Kill(sink, Abcd, OtherMob, 2_000);
        sink.AppendKill(Source(2_050), OtherMob, Abcd);
        Kill(sink, Abcd, Spirit, 3_000);
        sink.AppendKill(Source(3_050), Spirit, Abcd);
        sink.CompleteFlush(1);

        Assert.Equal((2, 2, 0), Counts(scene.CreateFrame().Snapshot.Combatants[Abcd]));
    }

    // An NPC the overlay runs under a new id (its raw id came back as another NPC, LifecycleRemapService) is the same NPC
    // in the kill message, which names the raw id.
    [Fact]
    public void A_Kill_Message_Names_An_Npc_By_Its_Lifecycle()
    {
        var (scene, sink) = CreateScene();
        var mob = sink.RebindInstanceLifecycle(Mob);
        Kill(sink, Abcd, Mob, 1_000);
        sink.AppendKill(Source(1_050), Mob, Abcd);
        sink.CompleteFlush(1);

        Assert.NotEqual(Mob, mob);
        Assert.Equal((1, 1, 0), Counts(scene.CreateFrame().Snapshot.Combatants[Abcd]));
    }

    // The kill message counts for an NPC the overlay saw die: not for a player, an NPC still alive or one never seen.
    [Fact]
    public void A_Kill_Message_Counts_Only_For_An_Npc_Seen_At_0_Hp()
    {
        var (scene, sink) = CreateScene();
        sink.AppendNpcHp(Source(1_000), Mob, 8_000, 10_000);
        AppendDamage(sink, Abcd, Mob, 2_000, 1_000);
        sink.AppendNpcHp(Source(1_000), Efgh, 0, 50_000);
        sink.AppendKill(Source(1_100), Mob, Abcd);
        sink.AppendKill(Source(1_100), Efgh, Abcd);
        sink.AppendKill(Source(1_100), OtherMob, Abcd);
        sink.CompleteFlush(1);

        Assert.Equal((0, 0, 0), Counts(scene.CreateFrame().Snapshot.Combatants[Abcd]));
    }

    // The game's count, a frame each time it goes up: the first frame and each step up count, a repeat doesn't, a jump far
    // up or down counts one. Those that came before the overlay knew who the player is wait for it.
    [Fact]
    public void Credited_Kills_Go_To_The_Player_Once_Known()
    {
        var (scene, sink) = CreateScene();
        AppendDamage(sink, Abcd, Mob, 1_000, 1_000);
        AppendDamage(sink, Efgh, Mob, 1_000, 1_000);
        sink.AppendKillCredit(Source(1_100), 41);
        sink.AppendKillCredit(Source(1_200), 42);
        sink.CompleteFlush(1);
        Assert.Equal(0, scene.CreateFrame().Snapshot.Combatants[Efgh].Credited);

        sink.AppendNickname(Source(1_300), Efgh, "Efgh", isLocalPlayer: true);
        sink.AppendKillCredit(Source(1_400), 42);
        sink.AppendKillCredit(Source(1_500), 45);
        sink.AppendKillCredit(Source(1_600), 2_000_000);
        sink.CompleteFlush(1);

        var snapshot = scene.CreateFrame().Snapshot;
        Assert.Equal(2 + 3 + 1, snapshot.Combatants[Efgh].Credited);
        Assert.Equal(0, snapshot.Combatants[Abcd].Credited);

        // A player known without their server loses the overlay's mark when their name and server come by (their own kill
        // message, say); the game's credits are still theirs.
        sink.AppendNickname(Source(1_700), Efgh, "Efgh", originServerId: 2_001);
        sink.AppendKillCredit(Source(1_800), 2_000_001);
        sink.AppendKillCredit(Source(1_900), 1_999_000);
        sink.CompleteFlush(1);
        Assert.Equal(8, scene.CreateFrame().Snapshot.Combatants[Efgh].Credited);

        // The overlay keeps them with the player across a reset on the same map (the meter's button, each pull of "Boss
        // battles only"), although the lost mark stays lost.
        scene.Reset(Started.AddSeconds(2));
        AppendDamage(sink, Efgh, Mob, 1_000, 3_000);
        sink.AppendKillCredit(Source(3_100), 1_999_001);
        sink.CompleteFlush(1);
        Assert.Equal(0, scene.Owner.MetadataRegistry.LocalPlayerEntityId);
        Assert.Equal(1, scene.CreateFrame().Snapshot.Combatants[Efgh].Credited);
    }

    // A kill counts in the pull on at that moment, the boss's own within a second after it died: the boss went to Abcd's
    // 6 M of 10 M, the killing blow and the credit (the player is Efgh) came 50-60 ms after. Trash killed 5 s later counts
    // in the scene only. A history entry keeps all three, in the scene and in the fight.
    [Fact]
    public async Task Kill_Counts_Go_Into_The_Boss_Fight_On_And_Into_The_History()
    {
        var (scene, sink) = CreateScene();
        sink.AppendNickname(Source(20), Efgh, "Efgh", isLocalPlayer: true);
        sink.AppendNpcKind(Source(20), Boss, NpcKind.Boss);
        sink.AppendNpcHp(Source(30), Boss, 10_000_000, 10_000_000);
        AppendDamage(sink, Abcd, Boss, 6_000_000, 1_000);
        sink.AppendNpcHp(Source(1_000), Boss, 4_000_000);
        AppendDamage(sink, Efgh, Boss, 4_000_000, 2_000);
        sink.AppendNpcHp(Source(2_000), Boss, 0);
        sink.AppendKill(Source(2_050), Boss, Efgh);
        sink.AppendKillCredit(Source(2_060), 7);
        sink.CompleteFlush(1);
        Kill(sink, Abcd, Mob, 7_000);
        sink.AppendKill(Source(7_050), Mob, Abcd);
        sink.AppendKillCredit(Source(7_060), 8);
        sink.CompleteFlush(1);

        var frame = scene.CreateFrame();

        var fight = Assert.Single(frame.BossFights);
        Assert.Equal(BossFightOutcome.Killed, fight.Outcome);
        Assert.Equal((1, 0, 0), Counts(fight.Snapshot.Combatants[Abcd]));
        Assert.Equal((0, 1, 1), Counts(fight.Snapshot.Combatants[Efgh]));
        Assert.Equal((2, 1, 0), Counts(frame.Snapshot.Combatants[Abcd]));
        Assert.Equal((0, 1, 2), Counts(frame.Snapshot.Combatants[Efgh]));

        using var file = new MemoryStream();
        await SceneArchiveFile.WriteAsync(file, [new ArchivedEncounterRecord { ScenePayload = scene.CreateArchivePayload() }], TestContext.Current.CancellationToken);
        var imported = Assert.Single(await SceneArchiveFile.ReadAsync(new MemoryStream(file.ToArray()), TestContext.Current.CancellationToken)).ScenePayload;
        Assert.Equal((2, 1, 0), Counts(imported.Snapshot.Combatants[Abcd]));
        Assert.Equal((0, 1, 2), Counts(imported.Snapshot.Combatants[Efgh]));
        Assert.Equal((0, 1, 1), Counts(imported.BossFights[0].Snapshot.Combatants[Efgh]));
    }

    // A left boss's pull ends at its last action (5 min later): an add killed before that stays in it, trash killed while
    // it waited to be seen as left leaves it again, and so does a credit when nothing else came after the end. The meter
    // reads the scene as it goes, so the kills are in the pull before it ends.
    [Fact]
    public void A_Left_Pull_Keeps_Only_The_Kills_Up_To_Its_End()
    {
        var (scene, sink) = CreateScene();
        sink.AppendNpcKind(Source(20), Boss, NpcKind.Boss);
        sink.AppendNpcHp(Source(30), Boss, 10_000_000, 10_000_000);
        AppendDamage(sink, Abcd, Boss, 5_000, 1_000);
        Kill(sink, Efgh, Mob, 1_200);
        AppendDamage(sink, Abcd, Boss, 5_000, 2_000);
        sink.CompleteFlush(1);
        Assert.Equal(1, Assert.Single(scene.CreateFrame().BossFights).Snapshot.Combatants[Efgh].Kills);
        Kill(sink, Abcd, OtherMob, 60_000);
        sink.CompleteFlush(1);
        Assert.Equal(1, Assert.Single(scene.CreateFrame().BossFights).Snapshot.Combatants[Abcd].Kills);
        AppendDamage(sink, Abcd, 402, 1_000, 400_000);
        sink.CompleteFlush(1);

        var frame = scene.CreateFrame();

        var fight = Assert.Single(frame.BossFights);
        Assert.Equal(BossFightOutcome.Reset, fight.Outcome);
        Assert.Equal((1, 0), (fight.Snapshot.Combatants[Efgh].Kills, fight.Snapshot.Combatants[Abcd].Kills));
        Assert.Equal((1, 1), (frame.Snapshot.Combatants[Efgh].Kills, frame.Snapshot.Combatants[Abcd].Kills));

        (scene, sink) = CreateScene();
        sink.AppendNickname(Source(20), Efgh, "Efgh", isLocalPlayer: true);
        sink.AppendNpcKind(Source(20), Boss, NpcKind.Boss);
        sink.AppendNpcHp(Source(30), Boss, 10_000_000, 10_000_000);
        AppendDamage(sink, Efgh, Boss, 5_000, 1_000);
        sink.AppendKillCredit(Source(60_000), 8);
        sink.CompleteFlush(1);
        Assert.Equal(1, Assert.Single(scene.CreateFrame().BossFights).Snapshot.Combatants[Efgh].Credited);
        AppendDamage(sink, Efgh, 402, 1_000, 400_000);
        sink.CompleteFlush(1);

        frame = scene.CreateFrame();

        Assert.Equal(0, Assert.Single(frame.BossFights).Snapshot.Combatants[Efgh].Credited);
        Assert.Equal(1, frame.Snapshot.Combatants[Efgh].Credited);
    }

    // "Boss battles only" freezes its scene at the boss's 0 HP update: the kill message and the credit that follow still
    // count, live and in the history entry the scene leaves when the next boss is pulled; a credit long after (trash) stays
    // out. The boss went to Abcd's 6 M of 10 M, the killing blow was Efgh's (the player).
    [Fact]
    public void A_Boss_Scene_Keeps_The_Kill_Message_And_The_Credit_After_Its_Boss_Died()
    {
        var (scene, sink, _) = CreateBossScene();
        AppendDamage(sink, Abcd, Boss, 6_000_000, 1_000);
        sink.AppendNpcHp(Source(1_000), Boss, 4_000_000);
        AppendDamage(sink, Efgh, Boss, 4_000_000, 2_000);
        sink.AppendNpcHp(Source(2_000), Boss, 0);
        sink.AppendKill(Source(2_050), Boss, Efgh);
        sink.AppendKillCredit(Source(2_060), 7);
        sink.CompleteFlush(1);

        var frame = scene.CreateFrame();

        Assert.Equal(BossSceneState.Frozen, scene.BossState);
        Assert.Equal(((1, 0, 0), (0, 1, 1)), (Counts(frame.Snapshot.Combatants[Abcd]), Counts(frame.Snapshot.Combatants[Efgh])));
        Assert.Equal((0, 1, 1), Counts(Assert.Single(frame.BossFights).Snapshot.Combatants[Efgh]));

        sink.AppendKillCredit(Source(5_000), 8);
        sink.AppendNpcCode(Source(9_000), OtherBoss, 2_100_003);
        sink.AppendNpcKind(Source(9_000), OtherBoss, NpcKind.Boss);
        AppendDamage(sink, Abcd, OtherBoss, 1_000, 10_000);
        sink.CompleteFlush(1);

        Assert.True(scene.TryDequeuePendingArchive(out var archived));
        Assert.Equal(((1, 0, 0), (0, 1, 1)), (Counts(archived.Snapshot.Combatants[Abcd]), Counts(archived.Snapshot.Combatants[Efgh])));
        Assert.Equal((0, 1, 1), Counts(Assert.Single(archived.BossFights).Snapshot.Combatants[Efgh]));
    }

    // A boss scene waiting between pulls (the boss left alone a while) leaves out the credits for trash killed meanwhile:
    // back on the same boss, the next credit counts one, not those.
    [Fact]
    public void A_Boss_Scene_Leaves_Out_The_Credits_While_It_Waits()
    {
        var (scene, sink, clock) = CreateBossScene();
        sink.AppendNpcHp(Source(30), Boss, 99_500, 100_000);
        AppendDamage(sink, Efgh, Boss, 500, 1_000);
        sink.AppendKillCredit(Source(1_100), 41);
        sink.CompleteFlush(1);
        _ = scene.CreateFrame();
        clock.Now = Started.AddMilliseconds(13_000);
        _ = scene.CreateFrame();
        Assert.Equal(BossSceneState.Frozen, scene.BossState);

        sink.AppendKillCredit(Source(14_000), 42);
        sink.AppendKillCredit(Source(15_000), 43);
        sink.AppendNpcHp(Source(16_000), Boss, 90_000, 100_000);
        AppendDamage(sink, Efgh, Boss, 700, 16_500);
        sink.AppendKillCredit(Source(16_600), 44);
        sink.CompleteFlush(1);
        clock.Now = Started.AddMilliseconds(16_700);

        var snapshot = scene.CreateFrame().Snapshot;

        Assert.Equal(BossSceneState.Recording, scene.BossState);
        Assert.Equal(2, snapshot.Combatants[Efgh].Credited);
    }

    private static (int Kills, int LastHits, int Credited) Counts(SceneCombatantMetrics metrics) => (metrics.Kills, metrics.LastHits, metrics.Credited);

    private static (SceneLiveReadModel Scene, IRuntimeObservationSink Sink) CreateScene()
    {
        var scene = new SceneLiveReadModel(Started, new ManualTimeProvider(Started));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayers(sink);
        return (scene, sink);
    }

    // "Boss battles only" waiting for its pull, Efgh the player, the boss at full HP.
    private static (SceneLiveReadModel Scene, IRuntimeObservationSink Sink, ManualTimeProvider Clock) CreateBossScene()
    {
        var clock = new ManualTimeProvider(Started);
        var scene = new SceneLiveReadModel(Started, clock);
        scene.ChangeKind(SceneKind.Boss, Started, archiveCurrent: false);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        AppendPlayers(sink);
        sink.AppendNickname(Source(10), Efgh, "Efgh", isLocalPlayer: true);
        sink.AppendNpcCode(Source(20), Boss, 2_100_002);
        sink.AppendNpcKind(Source(20), Boss, NpcKind.Boss);
        sink.AppendNpcHp(Source(30), Boss, 10_000_000, 10_000_000);
        return (scene, sink, clock);
    }

    private static void AppendPlayers(IRuntimeObservationSink sink)
    {
        sink.AppendNickname(Source(10), Abcd, "Abcd", characterClass: CharacterClass.Gladiator);
        sink.AppendNickname(Source(10), Efgh, "Efgh", characterClass: CharacterClass.Cleric);
        sink.AppendSummon(Source(20), Efgh, Spirit);
    }

    // An NPC at full HP that the killer's one hit takes all of.
    private static void Kill(IRuntimeObservationSink sink, int killerId, int npcId, long at)
    {
        sink.AppendNpcHp(Source(at), npcId, 1_000, 1_000);
        AppendDamage(sink, killerId, npcId, 1_000, at);
        sink.AppendNpcHp(Source(at), npcId, 0);
    }

    private static void AppendDamage(IRuntimeObservationSink sink, int sourceId, int targetId, int damage, long offsetMilliseconds)
    {
        var observation = new CombatWireObservation
        {
            SkillCode = 11_000_010,
            Damage = damage,
            HitCount = 1,
            AttemptCount = 1
        };
        sink.AppendCombatWireObservation(Source(offsetMilliseconds), sourceId, targetId, in observation);
    }

    private static PacketObservationSource Source(long offsetMilliseconds) =>
        new(Started.ToUnixTimeMilliseconds() + offsetMilliseconds, 1, 0, 0, offsetMilliseconds, default);

    private sealed class ManualTimeProvider(DateTimeOffset now) : TestTimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
