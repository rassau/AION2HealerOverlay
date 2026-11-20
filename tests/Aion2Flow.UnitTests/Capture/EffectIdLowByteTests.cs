using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.Resources.Catalog;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Stores;

using static Cloris.Aion2Flow.Tests.Capture.TransportTestData;

namespace Cloris.Aion2Flow.Tests.Capture;

// A 0x0438 value's 8-byte detail is an effect id (u32) and a count, so its first byte is only the id's low byte. It was read
// as HP (0x57) or MP (0x58), which made such hits heals or nothing. Frames from recordings of Nov 2026, ids and numbers only.
public sealed class EffectIdLowByteTests
{
    // An HP update first, as the stream is only picked up at an opcode it knows.
    private const string HpUpdate = "14008D93A101020100" + "76EEA20000000000";

    public EffectIdLowByteTests() => CombatResourceRegistry.SetGameResources(ResourceCatalog.Load(ResourceLanguage.English));

    [Fact]
    public void Hits_Whose_Effect_Id_Ends_In_0x57_Or_0x58_Are_Damage()
    {
        // Ashy Call (16052450) by player 12024 in an Abyss farm: effect 1605245015 (low byte 0x57) hit NPC 28347 for 5,952 and
        // counted as healing, effect 1605245016 (0x58) hit NPC 35849 for 6,756 and counted as nothing.
        var events = Play(
            (0, "210438BBDD010400F85DE2F0F400B4035718AE5F01000000BC69C02E0100"),
            (0, "2804388998022400F85DE2F0F400B4035818AE5F01000000BC69E43403A305A305A3050100"));

        Assert.Equal(
            [(12_024, 28_347, CombatMetricKind.Damage, 5_952L), (12_024, 35_849, CombatMetricKind.Damage, 6_756L)],
            events.Select(static e => (e.SourceId, e.TargetId, e.Contribution.Metric, e.Contribution.Amount)));
    }

    [Fact]
    public void The_Full_Hp_And_Mp_Refill_Heals_By_Its_Cast_And_Leaves_The_Mp_Out()
    {
        // Skill 1900911, the full refill player 8271 got on himself after a boss: cast and close, then HP 27,843 (effect
        // 19009111, low byte 0x57), MP 4,574 (19009112, 0x58) and a value-less 19009113. His HP was already full (25,312, his
        // maximum with a buff), so all of the 27,843 was overheal; his MP rose to its maximum (4,557 to 4,574), and the game
        // data has the MP effect as no HP. The cast makes the HP a heal, as with a serum, not the byte.
        var events = Play(
            (0, "190238CF40006F011D00A900CF40E5FECB42D4680100"),
            (0, "0E0638CF406F011D00A900"),
            (40, "210438CF400400CF406F011D00A902570E220101000000D468C3D9010100"),
            (40, "200438CF400400CF406F011D00A902580E220101000000D468DE230100"),
            (40, "1E0438CF400000CF406F011D00A902590E220101000000D4680100"));

        var heal = Assert.Single(events);
        Assert.Equal((8_271, 8_271, CombatMetricKind.Healing, 27_843L), (heal.SourceId, heal.TargetId, heal.Contribution.Metric, heal.Contribution.Amount));
        Assert.Equal(CombatPacketRule.CompactRecovery, heal.Contribution.Resolution.PacketRule);
    }

    [Fact]
    public void Potions_And_Serums_Still_Heal()
    {
        // Player 8601: a Life Potion (2011101) below full HP (22,128 of 24,633), a Mana Potion (2011201) and a Superior Life
        // Serum (2010102). The potion's two pools (2,117 + 1,739) shrink by 423 + 347 every 2 s, and HP rose by 770 a tick
        // until full; the Mana Potion carries no amount; the serum heals 4,225 + 3,470 at once, all of it overheal, as HP was
        // full again by then. None of them has an effect id ending in 0x57 or 0x58.
        var events = Play(
            (0, "190238994300DDAF1E00BB0099439E437243904E0100"),
            (0, "0E06389943DDAF1E00BB00"),
            (48, "1E0438994300009943DDAF1E00BB025FB2FC0B01000000904E0100"),
            (48, "1705389943099943F0095FB2FC0BC510DDAF1E00"),
            (48, "1705389943099943F00960B2FC0BCB0DDDAF1E00"),
            (2_003, "19053899430B9943F0095FB2FC0B9E0DA703DDAF1E00"),
            (2_003, "19053899430B9943F00960B2FC0BF00ADB02DDAF1E00"),
            (4_002, "19053899430B9943F0095FB2FC0BF709A703DDAF1E00"),
            (4_002, "19053899430B9943F00960B2FC0B9508DB02DDAF1E00"),
            (5_899, "19053899430B9943F0095FB2FC0BD006A703DDAF1E00"),
            (5_899, "19053899430B9943F00960B2FC0BBA05DB02DDAF1E00"),
            (7_899, "19053899430B9943F0095FB2FC0BA903A703DDAF1E00"),
            (7_899, "19053899430B9943F00960B2FC0BDF02DB02DDAF1E00"),
            (9_797, "18053899430B9943F0095FB2FC0B02A703DDAF1E00"),
            (9_797, "18053899430B9943F00960B2FC0B04DB02DDAF1E00"),
            (10_453, "19023899430041B01E00C60099439E437243904E0100"),
            (10_453, "0E0638994341B01E00C600"),
            (10_497, "1E043899430000994341B01E00C6026FD9FC0B01000000904E0100"),
            (22_453, "190238994300F6AB1E00D30099439E437243904E0100"),
            (22_453, "0E06389943F6AB1E00D300"),
            (22_498, "200438994304009943F6AB1E00D302232CFB0B01000000904E81210100"),
            (22_498, "200438994304009943F6AB1E00D302242CFB0B01000000904E8E1B0100"));

        Assert.All(events, static e => Assert.Equal((8_601, 8_601, CombatMetricKind.Healing), (e.SourceId, e.TargetId, e.Contribution.Metric)));
        (CombatDeliveryKind, long)[] tick = [(CombatDeliveryKind.Periodic, 423), (CombatDeliveryKind.Periodic, 347)];
        Assert.Equal(
            [.. tick, .. tick, .. tick, .. tick, .. tick, (CombatDeliveryKind.Direct, 4_225L), (CombatDeliveryKind.Direct, 3_470L)],
            events.Select(static e => (e.Contribution.Delivery, e.Contribution.Amount)));
    }

    private static CombatEventRecord[] Play(params (int AtMilliseconds, string Frame)[] frames)
    {
        const long start = 1_795_127_735_716;
        var scene = new SceneLiveReadModel();
        using var processor = new PacketStreamProcessor(SceneSinkFactory.CreateForLive(scene)(), null);
        var connection = new TcpConnection(1, 2, 3, 4);
        Assert.True(processor.AppendAndProcess(BuildLengthPrefixedEnvelope(Convert.FromHexString(HpUpdate)), in connection, start));
        foreach (var (at, frame) in frames)
            processor.AppendAndProcess(BuildLengthPrefixedEnvelope(Convert.FromHexString(frame)), in connection, start + 100 + at);

        scene.Owner.Refresh();
        return [.. scene.Owner.Combat.Events];
    }
}
