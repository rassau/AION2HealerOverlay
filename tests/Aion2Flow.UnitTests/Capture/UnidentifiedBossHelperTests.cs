using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Diagnostics;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.Tests.Protocol;

namespace Cloris.Aion2Flow.Tests.Capture;

// Frames recorded on 2026-11-06 in Vakron's fight: Vakron's spawn (2320812, 27,918,000 HP), and a nameless helper (2920355,
// not in the game data) that carries his HP pool: its spawn, battle on, then the HP update after Vakron's Surging Thorn hit it.
public sealed class UnidentifiedBossHelperTests
{
    private const string VakronSpawn = "E5014136CFB5020C3000AC69230000020A3A18C75EC6B8C600D958470098B24300FE01B0FDA70DB0FDA70D640000006400000000000000000000000000000000000000A8DE0000A8DE000001000000000000000000000000000000000000000603110181969800FFFFFFFFFFFFFFFF8075D52ABB030000CFB50201080A3A18C75EC6B8C600D95847110284969800FFFFFFFFFFFFFFFF8075D52ABB030000CFB502010A3A18C75EC6B8C600D958471103E11CB700FFFFFFFFFFFFFFFF8075D52ABB030000CFB502010A3A18C75EC6B8C600D95847060046000000FF4D0F00FFE84F1F00";
    private const string HelperSpawn = "BB0141368C93031C0000A38F2C000002003A18C700C6B8C600D9584700000000000001B0FDA70DB0FDA70D640000006400000000000000000000000000000000000000C0A80000A8DE000001000000000000000000000000000000000000001502110181969800FFFFFFFFFFFFFFFF8075D52ABB0300008C93030104003A18C700C6B8C600D95847110284969800FFFFFFFFFFFFFFFF8075D52ABB0300008C930301003A18C700C6B8C600D9584706CF9A0000004600000000";
    private const string HelperBattleOn = "0B218D8C93030001";
    private const string HelperHp = "14008D8C930302010083D1A90100000000";
    private const int HelperId = 51_596;

    [Fact]
    public void Helper_With_The_Hp_Pool_Of_A_Known_Boss_Is_No_Boss()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var scene = Replay(VakronSpawn, HelperSpawn, HelperBattleOn, HelperHp);
        var frame = scene.CreateFrame();

        Assert.True(scene.Owner.EntityVitals.TryGet(HelperId, out var vital));
        Assert.Equal(27_906_435, vital.CurrentHp);
        Assert.True(scene.Owner.Entities.TryGet(HelperId, out var helper));
        Assert.Equal(NpcKind.Monster, helper.Kind);
        Assert.DoesNotContain(frame.BossFocuses, boss => boss.InstanceId == HelperId);
    }

    // The helpers of the Fire Temple's second boss (2026-11-20, 22,809,600 HP) came without a spawn, their HP a little under the
    // boss's max (22,790,524-22,805,235), and stood in the boss bar 4-5 at a time. Here such an NPC (made-up frames with helper
    // 23817's id) against Vakron's 27,918,000: his exact pool makes it a helper whether he lives or not, up to 1 % under it only
    // while he is fought (hit, not dead), more than his pool never.
    private const string VakronHit = "14008DCFB502020100C0FC9B0100000000";
    private const string VakronDead = "14008DCFB5020201000000000000000000";
    private const string NamelessBattleOn = "0B218D89BA010001";
    private const int NamelessId = 23_817;

    [Theory]
    [InlineData(VakronHit, 27_900_917, NpcKind.Monster)] // 17,083 under, as Fire Temple helper 23817 was
    [InlineData(VakronHit, 27_638_820, NpcKind.Monster)] // 1 % under
    [InlineData(VakronHit, 27_638_819, NpcKind.Boss)]
    [InlineData(VakronHit, 27_918_001, NpcKind.Boss)]
    [InlineData(VakronHit, 27_918_000, NpcKind.Monster)]
    [InlineData(VakronDead, 27_918_000, NpcKind.Monster)]
    [InlineData(VakronDead, 27_900_917, NpcKind.Boss)] // another boss whose spawn the overlay missed
    [InlineData(null, 27_900_917, NpcKind.Boss)] // Vakron idle, not fought yet
    public void Npc_Without_A_Spawn_Is_A_Helper_By_Its_Hp_Against_A_Boss(string? vakron, long hp, NpcKind expected)
    {
        CombatResourceRegistry.EnsureCombatResources();
        var namelessHp = "14008D89BA01020100" + Convert.ToHexString(BitConverter.GetBytes(hp));
        string[] frames = vakron is null ? [VakronSpawn, NamelessBattleOn, namelessHp] : [VakronSpawn, vakron, NamelessBattleOn, namelessHp];
        var scene = Replay(frames);
        var frame = scene.CreateFrame();

        Assert.True(scene.Owner.Entities.TryGet(NamelessId, out var npc));
        Assert.Equal(expected, npc.Kind);
        Assert.Equal(expected == NpcKind.Boss, frame.BossFocuses.Any(boss => boss.InstanceId == NamelessId));
    }

    // In Vakron's dungeon the first boss's helper (with that boss's 20,412,000 HP) reports in at 0 HP right after the boss dies,
    // as a recording shows: still a helper, no boss bar at 0 %. Here Vakron's own helper the same way.
    [Fact]
    public void Helper_Reporting_In_At_0_Hp_After_Its_Boss_Died_Is_No_Boss()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var scene = Replay(VakronSpawn, HelperSpawn, HelperBattleOn, VakronDead, "14008D8C93030201000000000000000000");
        var frame = scene.CreateFrame();

        Assert.True(scene.Owner.Entities.TryGet(HelperId, out var helper));
        Assert.Equal(NpcKind.Monster, helper.Kind);
        Assert.DoesNotContain(frame.BossFocuses, boss => boss.InstanceId == HelperId);
    }

    // The sword boss event (map 510026): object 39471 (code 2920066, not in the game data) spawns with 45,000,000 HP, battle
    // on, and its one HP update is 0, nobody hitting it. No boss is first seen at 0 HP; here also Vakron's helper without him.
    private const string SwordObjectSpawn = "BB014136AFB4021C0000828E2C000002EA2AC545F36A8FC300C069C460813E41780801C0CABA15C0CABA1564000000640000000000000000000000000000000000000000000000DC05000001000000000000000000000000000000000000000602110181969800FFFFFFFFFFFFFFFF8075D52ABB030000AFB4020104EA2AC545F36A8FC300C069C4110284969800FFFFFFFFFFFFFFFF8075D52ABB030000AFB40201EA2AC545F36A8FC300C069C406BD950000001400000000";
    private const int SwordObjectId = 39_471;

    [Theory]
    [InlineData(SwordObjectSpawn, "0B218DAFB4020001", "14008DAFB4020201000000000000000000", SwordObjectId)]
    [InlineData(HelperSpawn, HelperBattleOn, "14008D8C93030201000000000000000000", HelperId)]
    public void Npc_First_Seen_At_0_Hp_Is_No_Boss(string spawn, string battleOn, string hp0, int id)
    {
        CombatResourceRegistry.EnsureCombatResources();
        var scene = Replay(spawn, battleOn, hp0);
        var frame = scene.CreateFrame();

        Assert.True(scene.Owner.Entities.TryGet(id, out var npc));
        Assert.Equal(NpcKind.Monster, npc.Kind);
        Assert.DoesNotContain(frame.BossFocuses, boss => boss.InstanceId == id);
        Assert.Empty(scene.Owner.BossFocus.GetEncounterBosses());
    }

    // A boss the game data doesn't know yet (a game update) still gets its bar from the HP fallback.
    [Fact]
    public void Npc_With_An_Unknown_Code_And_A_Boss_Hp_Pool_No_Known_Boss_Has_Is_A_Boss()
    {
        var scene = Replay(HelperSpawn, HelperBattleOn, HelperHp);
        var frame = scene.CreateFrame();

        Assert.True(scene.Owner.Entities.TryGet(HelperId, out var npc));
        Assert.Equal(NpcKind.Boss, npc.Kind);
        Assert.Contains(frame.BossFocuses, boss => boss.InstanceId == HelperId);
    }

    // Ketu's helpers (2920676, not in the game data) carry his 12,300,000 HP: before, the ones whose HP dropped became bosses
    // with a bar each at 0 HP.
    [Fact]
    public void Ketu_Helpers_Leave_Only_The_Three_Bosses()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var replay = PacketLogReplayService.Replay(FixtureHelper.GetPath("logs/aion2flow.stream.20260715000443.log"));

        Assert.Equal([22_723, 43_858, 39_906], replay.SceneOwner.BossFocus.GetEncounterBosses().Select(static boss => boss.InstanceId));
        Assert.True(replay.SceneOwner.Entities.TryGet(18_723, out var helper));
        Assert.Equal(NpcKind.Monster, helper.Kind);
    }

    // Eternal Ludra as the 22 Nov 2026 histories show him (map 1110, before the 18 Nov game data knew his raid's map 600081
    // and his boss code 2301038): his entity carries 2920772, most likely kept from one of his objects that had its id before
    // (TODO.md point 4); the game data files that code as a summon, as it does the other objects of his name around him, many
    // with his 194,512,500 HP pool; he has no owner. Made-up frames on the helper's spawn above:
    // him, a copy with his pool that gets no HP update, an object of his code without an HP pool, one first seen at 0 HP, and
    // one with his pool that gets an HP update while he fights. A player's hit (the 0x0438 hit of EffectIdLowByteTests) pulls
    // him; the group wipes at 1.8 % (battle off), and he comes back as a new NPC with his pool, as at Ludra under a code not in
    // the game data, or (made up) under his own; the player pulls that one too. His spawn coming again leaves him the boss.
    private const int LudraId = 26_956, LudraCopyId = 26_957, LudraObjectId = 26_958, LudraDeadObjectId = 26_959,
        LudraFightObjectId = 26_960, LudraResetId = 18_846;
    private const string LudraHit = "260438CCD2010600C1D60168D01200060220840100AB68590701000000904E98050100";

    [Theory]
    [InlineData("A38F2C00", null)]
    [InlineData("44912C00", "Eternal Ludra")]
    public void A_Summon_Of_No_Owner_With_A_Boss_Hp_Pool_Is_A_Boss(string resetCode, string? resetName)
    {
        CombatResourceRegistry.EnsureCombatResources();
        var scene = Replay(
            LudraSpawn("CCD201"),
            LudraSpawn("CDD201"),
            LudraSpawn("CED201").Replace("6400000064000000", "0000000000000000"),
            Hp("CED201", 1),
            LudraSpawn("CFD201"),
            Hp("CFD201", 0),
            Hp("CCD201", 188_934_178),
            LudraHit,
            LudraSpawn("D0D201"),
            Hp("D0D201", 194_000_000),
            Hp("CCD201", 3_519_492),
            "0B218DCCD2010000",
            LudraSpawn("9E9301", code: resetCode),
            Hp("9E9301", 194_509_581),
            LudraHit.Replace("CCD201", "9E9301"),
            LudraSpawn("CCD201", hp: "A2D08B5A"));
        var frame = scene.CreateFrame();

        int[] ids = [LudraId, LudraCopyId, LudraObjectId, LudraDeadObjectId, LudraFightObjectId, LudraResetId];
        Assert.Equal(
            [NpcKind.Boss, NpcKind.Summon, NpcKind.Summon, NpcKind.Monster, NpcKind.Monster, NpcKind.Boss],
            ids.Select(id => scene.Owner.Entities.TryGet(id, out var npc) ? npc.Kind : NpcKind.Unknown));
        Assert.Equal(["Eternal Ludra", resetName], new[] { LudraId, LudraResetId }.Select(id =>
            scene.Owner.Entities.TryGet(id, out var npc) && CombatResourceRegistry.TryResolveNpcCatalogEntry(npc.NpcCode ?? 0, out var entry) ? entry.Name : null));
        Assert.Contains(frame.BossFocuses, boss => boss.InstanceId == LudraResetId);
        Assert.Equal([(LudraId, 664L), (LudraResetId, 664L)], frame.BossFights.Select(fight => (fight.BossId, fight.Snapshot.Combatants[27_457].DamageAmount)));
    }

    // The Fire Temple's second boss (22,809,600 HP) without a code (the overlay missed his spawn; made-up frames, 23817's id)
    // and a Gas Rock (2920091, a summon of no owner in the game data) that carries his pool. After the boss's first HP update
    // the rock is his add; one that came before stands as a boss (PacketStateHandler's ponytail), and the boss keeps his bar.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_Summon_With_The_Pool_Of_A_Boss_Without_A_Code_Leaves_Him_The_Boss(bool bossFirst)
    {
        CombatResourceRegistry.EnsureCombatResources();
        const string pool = "8098F00A";
        string[] boss = [NamelessBattleOn, Hp("89BA01", 22_700_000)];
        string[] rock =
        [
            HelperSpawn.Replace("8C9303", "CCD201").Replace("A38F2C00", "9B8E2C00").Replace("B0FDA70DB0FDA70D", pool + pool),
            "0B218DCCD2010001", Hp("CCD201", 22_809_600), Hp("CCD201", 22_700_000),
        ];
        string[] frames = bossFirst ? [.. boss, .. rock] : [.. rock, .. boss];
        var scene = Replay(frames);
        var frame = scene.CreateFrame();

        Assert.True(scene.Owner.Entities.TryGet(NamelessId, out var npc));
        Assert.Equal(NpcKind.Boss, npc.Kind);
        Assert.Contains(frame.BossFocuses, focus => focus.InstanceId == NamelessId);
        Assert.True(scene.Owner.Entities.TryGet(26_956, out var gasRock));
        Assert.Equal(bossFirst ? NpcKind.Monster : NpcKind.Boss, gasRock.Kind);
    }

    // A boss without a code stays in the map's states once out of sight (9 in Eternal Ludra's map). Pulled while one is fought,
    // Eternal Ludra would be taken for its add (PacketStateHandler's ponytail); once its HP has stood still for 10 s (frames 6 s
    // apart here), he is the boss.
    [Theory]
    [InlineData(50, NpcKind.Monster)]
    [InlineData(6_000, NpcKind.Boss)]
    public void A_Boss_Without_A_Code_Takes_A_Summon_For_Its_Add_Only_While_Fought(long frameMilliseconds, NpcKind expected)
    {
        CombatResourceRegistry.EnsureCombatResources();
        var scene = Replay(frameMilliseconds, NamelessBattleOn, Hp("89BA01", 22_700_000), LudraSpawn("CCD201"), Hp("CCD201", 188_934_178));
        scene.CreateFrame();

        Assert.True(scene.Owner.Entities.TryGet(LudraId, out var ludra));
        Assert.Equal(expected, ludra.Kind);
    }

    // The helper's spawn under another id (and code), with Eternal Ludra's HP pool: all of the same byte length.
    private static string LudraSpawn(string id, string code = "44912C00", string hp = "F48CE05C") =>
        HelperSpawn.Replace("8C9303", id).Replace("A38F2C00", code).Replace("B0FDA70DB0FDA70D", hp + "F48CE05C");

    // An HP update (0x008D) of the NPC with this id (varint hex) without max HP.
    private static string Hp(string id, long hp) => "14008D" + id + "020100" + Convert.ToHexString(BitConverter.GetBytes(hp));

    // The game reuses ids: a spawn with another code than the id's last one (also a code the game data lacks) or naming another
    // owner is another creature, with an id of its own (int.MaxValue - 1, the first the overlay makes), and nothing of the old
    // one carries over. Made-up frames on 9303's Ancient Spirit (2920190, 40682; Packet4136ParserTests), Vakron and the helper.
    private const string SpiritSpawn = "AD014136EABD021F10000000FE8E2C00400200750FC7004706C700F457469A89FC42CB5901CCC30ACCC30AFC1B0000FC1B0000000000000000000000000000F0C6020064000000F04902000100000000000000A086010000000000C8C611000101011101A58DAF09FFFFFFFFFFFFFFFF8075D52ABB030000D748190251870FC7EE1F07C7002C57460702065724000003CD0094120000D00054030000D600C8000000320000004C1E030000";
    private const int SpiritId = 40_682, SpiritOwnerId = 9_303, VakronId = 39_631, NewCreatureId = int.MaxValue - 1;

    // Eternal Ludra's second comeback after a wipe (Ludra.aion2heal, 27681) came under the id of a player's Fire Spirit, with a
    // code the game data lacks and his pool: a creature of its own, which the HP check makes a boss.
    [Fact]
    public void A_Boss_Coming_Under_A_Spirits_Id_With_A_Code_The_Game_Data_Lacks_Is_A_New_Boss()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var scene = Replay(SpiritSpawn, LudraSpawn("EABD02", code: "A38F2C00"), "0B218DEABD020001", Hp("EABD02", 194_000_000));
        var frame = scene.CreateFrame();

        AssertSummon(scene, SpiritId, 2_920_190, SpiritOwnerId);
        Assert.True(scene.Owner.Entities.TryGet(NewCreatureId, out var ludra));
        Assert.Equal((NpcKind.Boss, (int?)null), (ludra.Kind, ludra.OwnerEntityId));
        Assert.Contains(frame.BossFocuses, focus => focus.InstanceId == NewCreatureId);
    }

    // A player's summon under the id of a monster the players had killed (sixBosses.aion2heal, 52230); here a summon of 9303
    // with a code the game data lacks under Vakron's id after a player hit him: he keeps his bar, and the hit counts.
    [Fact]
    public void A_Players_Summon_Under_A_Bosss_Id_Leaves_Him_His_Bar_And_The_Hits_On_Him()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var scene = Replay(VakronSpawn, LudraHit.Replace("CCD201", "CFB502"), SpiritSpawn.Replace("EABD02", "CFB502").Replace("FE8E2C00", "A38F2C00"));
        var frame = scene.CreateFrame();

        Assert.True(scene.Owner.Entities.TryGet(VakronId, out var vakron));
        Assert.Equal((NpcKind.Boss, (int?)null), (vakron.Kind, vakron.OwnerEntityId));
        Assert.Contains(frame.BossFocuses, focus => focus.InstanceId == VakronId);
        Assert.Equal(664, frame.Snapshot.Combatants[27_457].DamageAmount);
        AssertSummon(scene, NewCreatureId, null, SpiritOwnerId);
    }

    // Eternal Ludra's objects spawn with no owner, some under ids of players' summons (Ludra.aion2heal): such an object is an
    // NPC of its own, which the HP check judges (here his pool, which no named boss of the map has: a boss), and hits count.
    [Fact]
    public void An_Object_Under_A_Players_Summons_Id_Is_An_Npc_The_Hp_Check_Judges()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var scene = Replay(
            SpiritSpawn.Replace("FE8E2C00", "A38F2C00"),
            LudraSpawn("EABD02"),
            "0B218DEABD020001",
            Hp("EABD02", 194_000_000),
            LudraHit.Replace("CCD201", "EABD02"));
        var frame = scene.CreateFrame();

        AssertSummon(scene, SpiritId, null, SpiritOwnerId);
        Assert.True(scene.Owner.Entities.TryGet(NewCreatureId, out var ludra));
        Assert.Equal((2_920_772, NpcKind.Boss, (int?)null), (ludra.NpcCode, ludra.Kind, ludra.OwnerEntityId));
        Assert.Equal(664, frame.Snapshot.Combatants[27_457].DamageAmount);
    }

    // ponytail (JournalingRuntimeObservationSink.ObserveSpawn): a living boss whose id spawns again under a code the game data
    // lacks splits; the HP check takes the new part for his helper (his exact pool), his bar stays on the old part, and his
    // fight misses the later hit. None in 68 recordings.
    [Fact]
    public void A_Living_Boss_Spawning_Again_Under_A_Code_The_Game_Data_Lacks_Splits()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var hit = LudraHit.Replace("CCD201", "CFB502");
        var scene = Replay(VakronSpawn, hit, HelperSpawn.Replace("8C9303", "CFB502"), "0B218DCFB5020001", Hp("CFB502", 27_000_000), hit);
        var frame = scene.CreateFrame();

        Assert.True(scene.Owner.Entities.TryGet(NewCreatureId, out var part));
        Assert.Equal(NpcKind.Monster, part.Kind);
        Assert.Equal([VakronId], frame.BossFocuses.Select(focus => focus.InstanceId));
        Assert.Equal([(VakronId, 664L)], frame.BossFights.Select(fight => (fight.BossId, fight.Snapshot.Combatants[27_457].DamageAmount)));
        Assert.Equal(1_328, frame.Snapshot.Combatants[27_457].DamageAmount);
    }

    // A spirit's id spawning again with its code: another creature only under another owner (PvP: another player's spirit).
    [Theory]
    [InlineData("58240000", true)] // owner 9304
    [InlineData("57240000", false)] // owner 9303 again
    [InlineData(null, false)] // no owner: a re-sight
    public void A_Spirits_Id_Spawning_Again_With_Its_Code_Is_Another_Creature_Only_Under_Another_Owner(string? owner, bool newCreature)
    {
        CombatResourceRegistry.EnsureCombatResources();
        var again = owner is null ? HelperSpawn.Replace("8C9303", "EABD02").Replace("A38F2C00", "FE8E2C00") : SpiritSpawn.Replace("07020657240000", "070206" + owner);
        var scene = Replay(SpiritSpawn, again);
        scene.CreateFrame();

        AssertSummon(scene, SpiritId, 2_920_190, SpiritOwnerId);
        Assert.Equal(newCreature, scene.Owner.Entities.TryGet(NewCreatureId, out _));
        if (newCreature)
            AssertSummon(scene, NewCreatureId, 2_920_190, 9_304);
    }

    // The same through the legacy 0x4036 state spawn (152 kind), after a hit by the spirit: the hit stays 9303's.
    [Theory]
    [InlineData("58240000", true)] // owner 9304
    [InlineData("57240000", false)] // owner 9303 again
    public void A_Legacy_4036_State_Spawn_Naming_Another_Owner_Is_Another_Creature(string owner, bool newCreature)
    {
        CombatResourceRegistry.EnsureCombatResources();
        var state = SpiritSpawn.Replace("AD014136", "AD014036").Replace("1F10000000FE8E2C00", "1F1000FE8E2C000000").Replace("07020657240000", "070206" + owner);
        var scene = Replay(SpiritSpawn, LudraHit.Replace("C1D601", "EABD02"), state);
        var frame = scene.CreateFrame();

        AssertSummon(scene, SpiritId, 2_920_190, SpiritOwnerId);
        Assert.Equal(664, frame.Snapshot.Combatants.TryGetValue(SpiritOwnerId, out var row) ? row.DamageAmount : 0);
        Assert.Equal(newCreature, scene.Owner.Entities.TryGet(NewCreatureId, out _));
        if (newCreature)
            AssertSummon(scene, NewCreatureId, 2_920_190, 9_304);
    }

    private static void AssertSummon(SceneLiveReadModel scene, int id, int? npcCode, int ownerId)
    {
        Assert.True(scene.Owner.Entities.TryGet(id, out var summon));
        Assert.Equal((npcCode, NpcKind.Summon, (int?)ownerId), (summon.NpcCode, summon.Kind, summon.OwnerEntityId));
    }

    // Boss focus follows a summon to its owner by its owner link, as IsSummon does, also once a later kind state overwrote
    // Summon (the HP fallback's, say).
    [Fact]
    public void Summon_Whose_Kind_Was_Overwritten_Still_Acts_For_Its_Owner()
    {
        var entities = new EntityStore();
        var metadata = new RuntimeMetadataRegistry();
        metadata.UpsertPcMetadata(100, "Owner", isLocalPlayer: true);
        var applier = new DomainEventApplier(entities, new SceneBoundaryStore(), metadata, new CombatStore()) { CombatantStatisticsScope = CombatantStatisticsScope.Self };
        entities.ApplySummon(100, 600);
        entities.ApplyNpcKind(600, NpcKind.Monster);

        Assert.True(applier.IsBossFocusActivitySource(600));
    }

    private static SceneLiveReadModel Replay(params string[] frames) => Replay(50, frames);

    private static SceneLiveReadModel Replay(long frameMilliseconds, params string[] frames)
    {
        var scene = new SceneLiveReadModel();
        using var processor = new PacketStreamProcessor(SceneSinkFactory.CreateForLive(scene)(), null);
        var connection = new TcpConnection(1, 2, 3, 4);
        var timestamp = 1_800_000_000_000L;
        foreach (var frame in frames)
            Assert.True(processor.AppendAndProcess(Envelope(frame), in connection, timestamp += frameMilliseconds));
        return scene;
    }

    private static byte[] Envelope(string frameHex)
    {
        var frame = Convert.FromHexString(frameHex);
        var envelope = new byte[PacketTransportCodec.LengthPrefixedHeaderLength + frame.Length];
        BitConverter.TryWriteBytes(envelope, frame.Length);
        frame.CopyTo(envelope, PacketTransportCodec.LengthPrefixedHeaderLength);
        return envelope;
    }
}
