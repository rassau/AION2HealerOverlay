using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Combat;
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
    private const int Boss = 500;
    private const int Mob = 600;
    private const int DebilitatingMark = 17_080_240;

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

    private static void OpenAura(IRuntimeObservationSink sink, double atSeconds, int target, int origin, int sequence, ushort durationMilliseconds)
        => sink.RegisterObservation2A38(Source(atSeconds, 0x2A38), target, 1, 19, sequence, 0, durationMilliseconds, 0, 0, 0, origin, 1, ResourceEffectRef.FromRaw(DebilitatingMark), 0, 0, 0);

    private static PacketObservationSource Source(double atSeconds, ushort opcode)
        => new(Started.AddSeconds(atSeconds).ToUnixTimeMilliseconds(), 0, opcode, 0, 0, default);

    private sealed class SettableTimeProvider : TestTimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
