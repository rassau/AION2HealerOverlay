using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

// Your debuffs on your target, whom your last aimed skill went at: yours show in their skill's slot with their time left,
// also after a recast renamed the aura's origin and effect; another player's there and yours on another mob don't. After a
// meter reset nothing shows until you aim again.
public sealed class SkillMonitorDebuffTests
{
    private static readonly DateTimeOffset Started = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
    private const int Me = 100;
    private const int OtherCleric = 200;
    private const int Tank = 300;
    private const int Boss = 500;
    private const int Mob = 600;
    private const int Stranger = 700;
    private const int EnemyCleric = 800;
    private const int DebilitatingMark = 17_080_240, ChainOfTorment = 17_070_340, LightOfRegeneration = 17_090_350, LightOfProtection = 17_410_030,
        YustielsPower = 17_420_010, Grapple = 12_220_000, Shadowstrike = 13_070_020;

    [Fact]
    public void Your_Debuff_On_Your_Aimed_Target_Shows_In_Its_Slot_Through_Renewals()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var clock = new SettableTimeProvider { Now = Started.AddSeconds(1) };
        var scene = new SceneLiveReadModel(Started, clock);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var language = new LanguageService();
        language.SetLanguage(LanguageService.English);
        using var localization = new LocalizationService(language);
        using var resources = new GameResourceService(language);
        var settingsPath = Path.Combine(Path.GetTempPath(), $"aion2flow-debuffs-{Guid.NewGuid():N}.json");
        var settings = new SettingsService(settingsPath);
        var monitor = new SkillMonitorViewModel(new WinDivertCaptureService(new ProcessPortDiscoveryService()), resources, localization, settings) { DemoScene = scene };
        try
        {
            sink.AppendNickname(Source(0, 0x3336), Me, "Healer", isLocalPlayer: true);
            sink.RegisterCompactControl0238(Source(1, 0x0238), Me, 0, DebilitatingMark, 2, 2, Boss);
            OpenAura(sink, 1, Boss, Me, 7, durationMilliseconds: 10_000);
            OpenAura(sink, 1, Boss, OtherCleric, 8, durationMilliseconds: 20_000);
            OpenAura(sink, 1, Mob, Me, 9, durationMilliseconds: 30_000);
            // Recast at 4 s: renewed, with an origin that is no entity and a generic effect.
            sink.RegisterObservation2B38(Source(4, 0x2B38), Boss, 161, 19, 7, ResourceEffectRef.FromRaw(100_000_111), 0, 0, 0, 0);
            clock.Now = Started.AddSeconds(5);

            // The target comes from the journal, read after the frame's auras: it shows a frame later.
            monitor.ProcessUiFrame();
            monitor.ProcessUiFrame();

            var slot = Assert.Single(monitor.SkillRows);
            Assert.Equal(resources.ResolveBaseSkillIdForCode(DebilitatingMark), slot.RowBaseSkillId);
            Assert.Equal((true, false, "9.0s"), (slot.HasDebuff, slot.HasBuff, slot.DebuffRemainingText));
            Assert.Equal(["Debuffs: 9.0s"], slot.ToolTipText.Split('\n')[1..]);

            settings.Update(static current => current.SkillMonitorDebuffSelectAll = false);
            monitor.ProcessUiFrame();
            Assert.Empty(monitor.SkillRows);
            settings.Update(static current => current.SkillMonitorDebuffSelectAll = true);

            scene.Reset(Started.AddSeconds(5));
            OpenAura(sink, 6, Boss, Me, 10, durationMilliseconds: 10_000);
            clock.Now = Started.AddSeconds(7);
            monitor.ProcessUiFrame();
            monitor.ProcessUiFrame();
            Assert.Empty(monitor.SkillRows);

            sink.RegisterCompactControl0238(Source(7, 0x0238), Me, 0, DebilitatingMark, 2, 2, Boss);
            monitor.ProcessUiFrame();
            monitor.ProcessUiFrame();
            Assert.Equal("9.0s", Assert.Single(monitor.SkillRows).DebuffRemainingText);

            // The cast's cooldown in the same slot: one tooltip for both.
            sink.RegisterCooldown4738(Source(7, 0x4738), DebilitatingMark, 12_000);
            monitor.ProcessUiFrame();
            Assert.Equal(["Buffs: -", "Cooldowns: 12.0s", "Debuffs: 9.0s"], Assert.Single(monitor.SkillRows).ToolTipText.Split('\n')[1..]);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    // The last boss of a 20 Nov run changed form: the game named a new NPC for its id, so the overlay runs it under a new
    // id (a new lifecycle), its hits and debuffs with it. Your aimed cast named the old id; Earth Punishment never showed.
    [Fact]
    public void Your_Debuff_Shows_On_A_Boss_That_Changed_Form()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var clock = new SettableTimeProvider { Now = Started.AddSeconds(1) };
        var scene = new SceneLiveReadModel(Started, clock);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var language = new LanguageService();
        language.SetLanguage(LanguageService.English);
        using var localization = new LocalizationService(language);
        using var resources = new GameResourceService(language);
        var settingsPath = Path.Combine(Path.GetTempPath(), $"aion2flow-debuffs-{Guid.NewGuid():N}.json");
        var monitor = new SkillMonitorViewModel(new WinDivertCaptureService(new ProcessPortDiscoveryService()), resources, localization, new SettingsService(settingsPath)) { DemoScene = scene };
        try
        {
            sink.AppendNickname(Source(0, 0x3336), Me, "Healer", isLocalPlayer: true);
            var lifecycle = sink.RebindInstanceLifecycle(Boss);
            sink.RegisterCompactControl0238(Source(1, 0x0238), Me, 0, DebilitatingMark, 2, 2, Boss);
            OpenAura(sink, 1, Boss, Me, 7, durationMilliseconds: 10_000);
            clock.Now = Started.AddSeconds(2);

            monitor.ProcessUiFrame();
            monitor.ProcessUiFrame();

            Assert.NotEqual(Boss, lifecycle);
            Assert.Equal("9.0s", Assert.Single(monitor.SkillRows).DebuffRemainingText);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    // "Debuffs from anyone": on an enemy target everyone's debuffs show, a skill's with the most time left in its slot; off,
    // only yours, as before. "Buffs from anyone" changes nothing there: they're all debuffs, the other faction's too (an
    // Abyss boss both factions fight).
    [Fact]
    public void Debuffs_From_Anyone_Show_Everyones_Debuffs_On_An_Enemy_Target()
    {
        WithMonitor((sink, settings, clock, show) =>
        {
            sink.AppendNickname(Source(0, 0x3336), Me, "Abcd", Faction.Light, isLocalPlayer: true);
            sink.AppendNickname(Source(0, 0x4536), OtherCleric, "Efgh", Faction.Light);
            sink.AppendNickname(Source(0, 0x4536), EnemyCleric, "Ijkl", Faction.Dark);
            sink.AppendNpcCode(Source(0, 0x4136), Boss, 2_100_002);
            sink.RegisterCompactControl0238(Source(1, 0x0238), Me, 0, DebilitatingMark, 2, 2, Boss);
            OpenAura(sink, 1, Boss, Me, 7, durationMilliseconds: 10_000);
            OpenAura(sink, 1, Boss, OtherCleric, 8, durationMilliseconds: 20_000);
            OpenAura(sink, 1, Boss, OtherCleric, 9, durationMilliseconds: 12_000, ChainOfTorment);
            OpenAura(sink, 1, Boss, EnemyCleric, 10, durationMilliseconds: 14_000, ChainOfTorment);
            clock.Now = Started.AddSeconds(5);

            Assert.Equal(["Mark /6.0s"], show());
            settings.Update(static current => current.SkillMonitorBuffsFromAnyone = true);
            Assert.Equal(["Mark /6.0s"], show());
            settings.Update(static current => current.SkillMonitorDebuffsFromAnyone = true);
            Assert.Equal(["Chain /10.0s", "Mark /16.0s"], show());
            settings.Update(static current => current.SkillMonitorDebuffSelectAll = false);
            Assert.Empty(show());
        });
    }

    // "Buffs from anyone": on an ally target (a player of your faction) everything counts as a buff, yours too, and a skill
    // on you and on the target shows yours; off, yours there show as debuffs (as before) and the others' not at all. Buffs
    // on you show whoever cast them either way; "Debuffs from anyone" changes nothing here, as none of it is a debuff.
    [Fact]
    public void Buffs_From_Anyone_Show_Everyones_Buffs_On_An_Ally_Target()
    {
        WithMonitor((sink, settings, clock, show) =>
        {
            sink.AppendNickname(Source(0, 0x3336), Me, "Abcd", Faction.Light, isLocalPlayer: true);
            sink.AppendNickname(Source(0, 0x4536), OtherCleric, "Efgh", Faction.Light);
            sink.AppendNickname(Source(0, 0x4536), Tank, "Ijkl", Faction.Light);
            OpenAura(sink, 1, Me, OtherCleric, 20, durationMilliseconds: 30_000, LightOfProtection);
            OpenAura(sink, 2, Tank, OtherCleric, 21, durationMilliseconds: 60_000, LightOfProtection);
            OpenAura(sink, 2, Tank, Me, 22, durationMilliseconds: 12_000, LightOfRegeneration);
            OpenAura(sink, 2, Tank, OtherCleric, 23, durationMilliseconds: 20_000, YustielsPower);
            sink.RegisterCompactControl0238(Source(2, 0x0238), Me, 0, LightOfRegeneration, 2, 2, Tank);
            clock.Now = Started.AddSeconds(6);

            Assert.Equal(["Protection 25.0s/", "Regeneration /8.0s"], show());
            settings.Update(static current => current.SkillMonitorDebuffsFromAnyone = true);
            Assert.Equal(["Protection 25.0s/", "Regeneration /8.0s"], show());
            settings.Update(static current => current.SkillMonitorBuffsFromAnyone = true);
            Assert.Equal(["Protection 25.0s/", "Regeneration 8.0s/", "Yustiel 16.0s/"], show());
            settings.Update(static current => current.SkillMonitorBuffSelectAll = false);
            Assert.Empty(show());
        });
    }

    // PvP, where the game data's label and the caster's side agree: on an enemy its own buffs (known as a player or not) and
    // its side's are no debuffs, but buffs to "Buffs from anyone"; on an ally an enemy's debuff is a debuff, never a buff.
    // The settings are about your target: an enemy player's debuff on you stays a buff, as before.
    [Fact]
    public void In_PvP_Buffs_And_Debuffs_Show_On_An_Enemy_And_An_Ally()
    {
        WithMonitor((sink, settings, clock, show) =>
        {
            sink.AppendNickname(Source(0, 0x3336), Me, "Abcd", Faction.Light, isLocalPlayer: true);
            sink.AppendNickname(Source(0, 0x4536), OtherCleric, "Efgh", Faction.Light);
            sink.AppendNickname(Source(0, 0x4536), EnemyCleric, "Ijkl", Faction.Dark);
            OpenAura(sink, 1, Me, EnemyCleric, 7, durationMilliseconds: 10_000);
            // Your target, a player the overlay has no data on: their own buff, their cleric's, your side's debuff.
            OpenAura(sink, 1, Stranger, Stranger, 8, durationMilliseconds: 30_000, LightOfProtection);
            OpenAura(sink, 1, Stranger, EnemyCleric, 9, durationMilliseconds: 20_000, YustielsPower);
            OpenAura(sink, 1, Stranger, OtherCleric, 10, durationMilliseconds: 12_000, ChainOfTorment);
            sink.RegisterCompactControl0238(Source(1, 0x0238), Me, 0, DebilitatingMark, 2, 2, Stranger);
            clock.Now = Started.AddSeconds(5);

            Assert.Equal(["Mark 6.0s/"], show());
            settings.Update(static current => current.SkillMonitorDebuffsFromAnyone = true);
            Assert.Equal(["Chain /8.0s", "Mark 6.0s/"], show());
            settings.Update(static current => current.SkillMonitorBuffsFromAnyone = true);
            Assert.Equal(["Chain /8.0s", "Mark 6.0s/", "Protection 26.0s/", "Yustiel 16.0s/"], show());

            // Aimed at your ally, who carries the enemy's Chain.
            OpenAura(sink, 5, OtherCleric, EnemyCleric, 11, durationMilliseconds: 10_000, ChainOfTorment);
            sink.RegisterCompactControl0238(Source(5, 0x0238), Me, 0, LightOfRegeneration, 2, 2, OtherCleric);
            clock.Now = Started.AddSeconds(6);
            Assert.Equal(["Chain /9.0s", "Mark 5.0s/"], show());
            settings.Update(static current => current.SkillMonitorDebuffsFromAnyone = false);
            Assert.Equal(["Mark 5.0s/"], show());
        });
    }

    // The game data's label wins over the caster's side: the Chain of a player of no known faction (who counts as yours) is
    // a debuff on your ally, and the Protection your side put on an enemy player is never one, but a buff to "Buffs from
    // anyone". The Chain on you stays a buff, whatever the settings.
    [Fact]
    public void The_Game_Datas_Label_Wins_Over_The_Casters_Side()
    {
        WithMonitor((sink, settings, clock, show) =>
        {
            sink.AppendNickname(Source(0, 0x3336), Me, "Abcd", Faction.Light, isLocalPlayer: true);
            sink.AppendNickname(Source(0, 0x4536), OtherCleric, "Efgh", Faction.Light);
            sink.AppendNickname(Source(0, 0x4536), EnemyCleric, "Ijkl", Faction.Dark);
            sink.AppendNickname(Source(0, 0x4536), Stranger, "Mnop");
            OpenAura(sink, 1, Me, Stranger, 7, durationMilliseconds: 10_000, ChainOfTorment);
            OpenAura(sink, 1, OtherCleric, Stranger, 8, durationMilliseconds: 20_000, ChainOfTorment);
            OpenAura(sink, 1, EnemyCleric, OtherCleric, 9, durationMilliseconds: 30_000, LightOfProtection);
            sink.RegisterCompactControl0238(Source(1, 0x0238), Me, 0, DebilitatingMark, 2, 2, EnemyCleric);
            clock.Now = Started.AddSeconds(5);

            Assert.Equal(["Chain 6.0s/"], show());
            settings.Update(static current => current.SkillMonitorDebuffsFromAnyone = true);
            Assert.Equal(["Chain 6.0s/"], show());
            settings.Update(static current => current.SkillMonitorBuffsFromAnyone = true);
            Assert.Equal(["Chain 6.0s/", "Protection 26.0s/"], show());

            // Aimed at your ally, who carries the stranger's Chain.
            sink.RegisterCompactControl0238(Source(5, 0x0238), Me, 0, DebilitatingMark, 2, 2, OtherCleric);
            Assert.Equal(["Chain 6.0s/16.0s"], show());
        });
    }

    // Where the game data labels a skill both (Grapple) or neither by its own records (this Shadowstrike: only an effect
    // group says debuff), the caster's side still decides: on your ally an enemy player's is a debuff and the ally's own a
    // buff; on an enemy player your side's is a debuff and its own a buff.
    [Fact]
    public void Skills_Labelled_Both_Or_Neither_Go_By_The_Casters_Side()
    {
        WithMonitor((sink, settings, clock, show) =>
        {
            sink.AppendNickname(Source(0, 0x3336), Me, "Abcd", Faction.Light, isLocalPlayer: true);
            sink.AppendNickname(Source(0, 0x4536), OtherCleric, "Efgh", Faction.Light);
            sink.AppendNickname(Source(0, 0x4536), EnemyCleric, "Ijkl", Faction.Dark);
            OpenAura(sink, 1, OtherCleric, EnemyCleric, 7, durationMilliseconds: 10_000, Grapple);
            OpenAura(sink, 1, OtherCleric, OtherCleric, 8, durationMilliseconds: 12_000, Shadowstrike);
            OpenAura(sink, 1, EnemyCleric, OtherCleric, 9, durationMilliseconds: 14_000, Grapple);
            OpenAura(sink, 1, EnemyCleric, EnemyCleric, 10, durationMilliseconds: 16_000, Shadowstrike);
            sink.RegisterCompactControl0238(Source(1, 0x0238), Me, 0, LightOfRegeneration, 2, 2, OtherCleric);
            clock.Now = Started.AddSeconds(5);
            settings.Update(static current =>
            {
                current.SkillMonitorBuffsFromAnyone = true;
                current.SkillMonitorDebuffsFromAnyone = true;
            });

            Assert.Equal(["Grapple /6.0s", "Shadowstrike 8.0s/"], show());

            // Aimed at the enemy.
            sink.RegisterCompactControl0238(Source(5, 0x0238), Me, 0, DebilitatingMark, 2, 2, EnemyCleric);
            Assert.Equal(["Grapple /10.0s", "Shadowstrike 12.0s/"], show());
        });
    }

    // In a battlefield the other side carries your faction too, but a player you fought there is on the other side: your
    // Grapple on them (labelled both or neither) stays a debuff with "Buffs from anyone" on, whether buffs are ticked or not.
    // By faction alone it was a buff on an ally, and with no buffs ticked it was gone.
    [Fact]
    public void Your_Debuff_On_A_Player_Of_Your_Faction_You_Fought_Stays_A_Debuff()
    {
        const int GrappleStrike = 12_220_040;
        WithMonitor((sink, settings, clock, show) =>
        {
            sink.AppendNickname(Source(0, 0x3336), Me, "Abcd", Faction.Light, isLocalPlayer: true);
            sink.AppendNickname(Source(0, 0x4536), Stranger, "Efgh", Faction.Light);
            sink.AppendCombatWireObservation(Source(1, 0x0438), Me, Stranger, Hit(GrappleStrike));
            OpenAura(sink, 1, Stranger, Me, 7, durationMilliseconds: 10_000, GrappleStrike);
            sink.RegisterCompactControl0238(Source(1, 0x0238), Me, 0, GrappleStrike, 2, 2, Stranger);
            clock.Now = Started.AddSeconds(5);
            settings.Update(static current => current.SkillMonitorBuffsFromAnyone = true);

            Assert.Equal(["Grapple /6.0s"], show());
            settings.Update(static current => current.SkillMonitorBuffSelectAll = false);
            Assert.Equal(["Grapple /6.0s"], show());
        });
    }

    // A skill monitor on a scene of its own, in English. show() lets it read two frames (an aimed cast names the target a
    // frame later) and gives its slots as "skill buff/debuff".
    private static void WithMonitor(Action<IRuntimeObservationSink, SettingsService, SettableTimeProvider, Func<string[]>> test)
    {
        CombatResourceRegistry.EnsureCombatResources();
        var clock = new SettableTimeProvider { Now = Started.AddSeconds(1) };
        var scene = new SceneLiveReadModel(Started, clock);
        var language = new LanguageService();
        language.SetLanguage(LanguageService.English);
        using var localization = new LocalizationService(language);
        using var resources = new GameResourceService(language);
        var settingsPath = Path.Combine(Path.GetTempPath(), $"aion2flow-debuffs-{Guid.NewGuid():N}.json");
        var settings = new SettingsService(settingsPath);
        var monitor = new SkillMonitorViewModel(new WinDivertCaptureService(new ProcessPortDiscoveryService()), resources, localization, settings) { DemoScene = scene };
        var names = new Dictionary<int, string>
        {
            [resources.ResolveBaseSkillIdForCode(DebilitatingMark)] = "Mark",
            [resources.ResolveBaseSkillIdForCode(ChainOfTorment)] = "Chain",
            [resources.ResolveBaseSkillIdForCode(LightOfRegeneration)] = "Regeneration",
            [resources.ResolveBaseSkillIdForCode(LightOfProtection)] = "Protection",
            [resources.ResolveBaseSkillIdForCode(YustielsPower)] = "Yustiel",
            [resources.ResolveBaseSkillIdForCode(Grapple)] = "Grapple",
            [resources.ResolveBaseSkillIdForCode(Shadowstrike)] = "Shadowstrike",
        };
        try
        {
            test(SceneSinkFactory.CreateForLive(scene)(), settings, clock, () =>
            {
                monitor.ProcessUiFrame();
                monitor.ProcessUiFrame();
                return [.. monitor.SkillRows.Select(row => $"{names.GetValueOrDefault(row.RowBaseSkillId, $"{row.RowBaseSkillId}")} {row.BuffRemainingText}/{row.DebuffRemainingText}").Order()];
            });
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    // A direct hit the game data calls damage (the skill's first effect).
    private static CombatWireObservation Hit(int skill) =>
        new() { SkillCode = skill, Damage = 1_000, HitCount = 1, AttemptCount = 1, DetailResourceEffectRef = ResourceEffectRef.FromRaw((uint)skill * 100 + 11) };

    private static void OpenAura(IRuntimeObservationSink sink, double atSeconds, int target, int origin, int sequence, ushort durationMilliseconds, int effect = DebilitatingMark)
        => sink.RegisterObservation2A38(Source(atSeconds, 0x2A38), target, 1, 19, sequence, 0, durationMilliseconds, 0, 0, 0, origin, 1, ResourceEffectRef.FromRaw((uint)effect), 0, 0, 0);

    private static PacketObservationSource Source(double atSeconds, ushort opcode)
        => new(Started.AddSeconds(atSeconds).ToUnixTimeMilliseconds(), 0, opcode, 0, 0, default);

    private sealed class SettableTimeProvider : TestTimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
