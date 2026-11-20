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

    private static SceneLiveReadModel Replay(params string[] frames)
    {
        var scene = new SceneLiveReadModel();
        using var processor = new PacketStreamProcessor(SceneSinkFactory.CreateForLive(scene)(), null);
        var connection = new TcpConnection(1, 2, 3, 4);
        var timestamp = 1_800_000_000_000L;
        foreach (var frame in frames)
            Assert.True(processor.AppendAndProcess(Envelope(frame), in connection, timestamp += 50));
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
