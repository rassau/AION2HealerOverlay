using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Twins;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

// Hovering a buff in the group frame: the numbers at the caster's skill level, with the note naming that caster, and
// every text in the language just chosen.
[Collection(AvaloniaTestCollection.Name)]
public sealed class GroupFrameAuraTipTests
{
    private const int Me = 100;
    private const int OtherHealer = 200;
    private const int Tank = 300;
    // A cleric's buff whose numbers follow the skill level (see SkillEffectsTests).
    private const int Buff = 17410030;

    // Two clerics at the same level of the same buff: each icon names its own caster (the first one's name showed on both).
    [Fact]
    public void Each_Buff_Names_Its_Own_Caster()
    {
        AvaloniaTestHost.Run(() =>
        {
            var language = new LanguageService();
            language.SetLanguage(LanguageService.English);
            using var party = Party.Create(language);

            party.ViewModel.UpdateMembers();

            var mine = Assert.Single(party.Row(Tank).MyAuras);
            var theirs = Assert.Single(party.Row(Me).OtherAuras);
            Assert.Equal((Buff, Buff), (mine.SkillId, theirs.SkillId));
            Assert.Contains("Healer A", mine.EffectNote);
            Assert.Contains("Healer B", theirs.EffectNote);
        });
    }

    // Names, effect texts and notes follow a language switch on the next frame, for icons already shown too.
    [Fact]
    public void Aura_Texts_Follow_A_Language_Switch()
    {
        AvaloniaTestHost.Run(() =>
        {
            var language = new LanguageService();
            language.SetLanguage(LanguageService.English);
            try
            {
                using var party = Party.Create(language, theirBuff: false);
                party.ViewModel.UpdateMembers();
                var english = Assert.Single(party.Row(Tank).MyAuras);
                party.AssertShownInCurrentLanguage(english);

                language.SetLanguage(LanguageService.German);
                party.ViewModel.UpdateMembers();

                var german = Assert.Single(party.Row(Tank).MyAuras);
                party.AssertShownInCurrentLanguage(german);
                Assert.NotEqual(english.EffectNote, german.EffectNote);
            }
            finally
            {
                language.SetLanguage(LanguageService.English);
                CombatResourceRegistry.LoadSkillMap(LanguageService.English);
            }
        });
    }

    // A note for the level (one the game sent in a fight) stands in for the website's where the numbers are at that level;
    // a skill without numbers per level keeps its range.
    [Fact]
    public void A_Level_Note_Stands_In_Only_Where_The_Numbers_Are_At_A_Level()
    {
        using var localization = new LocalizationService(new LanguageService());

        Assert.Equal("seen", GroupFrameViewModel.DescribeAura(Buff, localization, 21, "Healer A", levelNote: "seen").EffectNote);
        Assert.Equal(string.Format(System.Globalization.CultureInfo.CurrentCulture, localization["GroupFrame_EffectLevels"], SkillEffects.Get(17390000)!.MaxLevel),
            GroupFrameViewModel.DescribeAura(17390000, localization, 5, "Healer A", levelNote: "seen").EffectNote);
    }

    // You (Healer A) and another cleric (Healer B), both at level 21 of the buff by their digital twins: yours on the
    // tank, theirs (unless left out) on you.
    private sealed class Party : IDisposable
    {
        private readonly LocalizationService _localization;
        private readonly GameResourceService _resources;
        private readonly UiScaleService _uiScale;
        private readonly DigitalTwinService _twins;

        private Party(GroupFrameViewModel viewModel, LocalizationService localization, GameResourceService resources, UiScaleService uiScale, DigitalTwinService twins)
        {
            ViewModel = viewModel;
            _localization = localization;
            _resources = resources;
            _uiScale = uiScale;
            _twins = twins;
        }

        public GroupFrameViewModel ViewModel { get; }

        public static Party Create(LanguageService language, bool theirBuff = true)
        {
            CombatResourceRegistry.EnsureCombatResources();
            var localization = new LocalizationService(language);
            var resources = new GameResourceService(language);
            var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-aura-tips-{Guid.NewGuid():N}.json"));
            var uiScale = new UiScaleService(settingsService);
            var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService()) { GroupFrameShowOtherBuffs = true };
            var twins = new DigitalTwinService();
            twins.Preload([Twin("Healer A"), Twin("Healer B")]);

            var capture = new WinDivertCaptureService(new ProcessPortDiscoveryService());
            capture.Scene.Reset(DateTimeOffset.Now);
            var sink = SceneSinkFactory.CreateForLive(capture.Scene)();
            var at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            sink.AppendNickname(Source(at, 0x048D), Me, "Healer A", isLocalPlayer: true, originServerId: 1301);
            sink.AppendNickname(Source(at, 0x048D), OtherHealer, "Healer B", originServerId: 1301);
            sink.AppendNickname(Source(at, 0x048D), Tank, "Tank", originServerId: 1301);
            sink.AppendPlayerGroupMember(Source(at, 0x0092, sequence: 1), Me, PlayerGroupMembership.Party(0));
            sink.AppendPlayerGroupMember(Source(at, 0x0092, sequence: 1), OtherHealer, PlayerGroupMembership.Party(1));
            sink.AppendPlayerGroupMember(Source(at, 0x0092, sequence: 1), Tank, PlayerGroupMembership.Party(2));
            OpenBuff(sink, at, target: Tank, caster: Me, sequence: 1);
            if (theirBuff)
                OpenBuff(sink, at, target: Me, caster: OtherHealer, sequence: 2);

            return new Party(new GroupFrameViewModel(capture, resources, localization, settings, twins), localization, resources, uiScale, twins);
        }

        public GroupFrameMemberRow Row(int entityId) => ViewModel.Members.Single(row => row.EntityId == entityId);

        // As a newly made icon of Healer A's buff shows it in the language now chosen.
        public void AssertShownInCurrentLanguage(GroupFrameAuraIcon icon)
        {
            var name = _resources.ResolveSkillName(Buff);
            var (effect, note, _) = GroupFrameViewModel.DescribeAura(Buff, _localization, 21, "Healer A", name, _resources.ResolveSkillName(Buff / 10_000 * 10_000));
            Assert.Equal(name, icon.ToolTip);
            Assert.Equal(effect, icon.Effect);
            Assert.Equal(note, icon.EffectNote);
            Assert.Contains("Healer A", icon.EffectNote);
        }

        public void Dispose()
        {
            _twins.Dispose();
            _uiScale.Dispose();
            _resources.Dispose();
            _localization.Dispose();
        }

        private static DigitalTwin Twin(string name) =>
            new(name, 1301, "Server", "Cleric", 45, 0, 0, DateTimeOffset.UnixEpoch, [], [], [new TwinSkill(Buff / 10_000 * 10_000, "Buff", 21, "Passive", true)], [], [], []);

        private static void OpenBuff(IRuntimeObservationSink sink, long at, int target, int caster, int sequence) =>
            sink.RegisterObservation2A38(Source(at, 0x2A38), target, 1, 19, sequence, 0, 60_000, 0, 0, 0, caster, 1, ResourceEffectRef.FromRaw(Buff), 0, 0, 0);

        private static PacketObservationSource Source(long at, ushort opcode, long sequence = 0) => new(at, 0, opcode, 0, sequence, default);
    }
}
