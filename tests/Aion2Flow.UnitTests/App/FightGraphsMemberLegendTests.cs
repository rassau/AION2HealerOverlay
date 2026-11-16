using System.Diagnostics;
using Avalonia.Threading;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

// Each party member's curve in the fight graphs: before the first pull the legend lists the party as the group frame has
// it; a member hidden there stays hidden under another id (a requeue), in a pull they haven't joined in yet or have, and
// after a restart; a new member shows, also under the hidden one's old id. One hidden before their server and name are
// known stays hidden once they are (not whoever has their id in another encounter); the tour's made-up players are kept
// nowhere.
[Collection(AvaloniaTestCollection.Name)]
public sealed class FightGraphsMemberLegendTests
{
    private const int Server = 2304;
    private const int Boss = 900;

    [Fact]
    public void The_Party_Is_In_The_Legend_Before_The_First_Pull_And_A_Hidden_Member_Stays_Hidden()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aion2flow-members-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            AvaloniaTestHost.Run(() =>
            {
                var language = new LanguageService();
                var localization = new LocalizationService(language);
                var resources = new GameResourceService(language);
                var settingsService = new SettingsService(Path.Combine(directory, "settings.json"));
                var names = new PlayerNameDisplayService(settingsService, localization);
                var settings = new SettingsFlyoutViewModel(localization, language, settingsService, names, new UiScaleService(settingsService), new AppUpdateService(),
                    new ProcessForegroundWatcher(), new GlobalHotkeyService());
                var ports = new ProcessPortDiscoveryService();
                var capture = new WinDivertCaptureService(ports);
                capture.Scene.Reset(DateTimeOffset.Now);
                var frameBatch = new UiFrameBatchService();
                var main = new MainViewModel(capture, ports, language, resources, new EncounterArchiveService(), new CombatantDetailsFlyoutViewModel(localization, frameBatch),
                    localization, settings, frameBatch) { IsCapturing = true };
                var started = capture.Scene.SessionStarted.ToUnixTimeMilliseconds();
                var sink = SceneSinkFactory.CreateForLive(capture.Scene)();
                settings.FightGraphsShowMembers = true;

                // In the dungeon, before any pull: the user (a cleric), a tank and a dps.
                Party(sink, started, 1, 100, (100, "Healer", CharacterClass.Cleric), (200, "Tank", CharacterClass.Gladiator), (300, "Dps", CharacterClass.Ranger));
                main.ProcessUiFrameForTesting();
                using (var graphs = new FightGraphsViewModel(main, settings, names, settingsService))
                {
                    graphs.Refresh();
                    Assert.Equal(["Tank", "Dps"], Members(graphs).Select(static item => item.Label));
                    Members(graphs).Single(static item => item.Label == "Tank").IsShown = false;
                    Assert.Equal([$"{Server}:Tank"], settingsService.Current.FightGraphsHiddenMembers);

                    // Requeued with the tank: new ids for everyone, a new member instead of the dps, with the tank's old id.
                    Party(sink, started + 5_000, 2, 110, (110, "Healer", CharacterClass.Cleric), (200, "Newbie", CharacterClass.Sorcerer), (210, "Tank", CharacterClass.Gladiator));
                    main.ProcessUiFrameForTesting();
                    graphs.Refresh();
                    Assert.Equal([("Newbie", true), ("Tank", false)], Members(graphs).Select(static item => (item.Label, item.IsShown)));

                    // The first pull: the user and the newcomer hit the boss, the tank hasn't yet. The tank keeps their
                    // entry, still hidden; the newcomer's curve is drawn.
                    sink.AppendNpcCode(Source(started + 9_000, 0), Boss, 2_300_261);
                    sink.AppendNpcKind(Source(started + 9_000, 0), Boss, NpcKind.Boss);
                    sink.SetNpcBattle(Source(started + 9_000, 0), Boss, true);
                    sink.AppendNpcHp(Source(started + 9_000, 0), Boss, 10_000_000, 10_000_000);
                    Hit(sink, started + 10_000, 110, Boss, 50_000);
                    Hit(sink, started + 10_500, 200, Boss, 80_000);
                    Hit(sink, started + 12_000, 200, Boss, 80_000);
                    main.ProcessUiFrameForTesting();
                    graphs.Refresh();
                    WaitFor(() => Damage(graphs)?.Series.Any(static series => series.Label == "Newbie") == true);
                    Assert.Equal([("Newbie", true), ("Tank", false)], Members(graphs).Select(static item => (item.Label, item.IsShown)));

                    // The tank joins in: their curve stays hidden.
                    Hit(sink, started + 13_000, 210, Boss, 60_000);
                    main.ProcessUiFrameForTesting();
                    graphs.Refresh();
                    WaitFor(() => Damage(graphs)?.Seconds == 3);
                    Assert.DoesNotContain(Damage(graphs)!.Series, static series => series.Label == "Tank");

                    // A stranger joins in, their class known before their server and name, and is hidden then. Once
                    // known, they stay hidden by server and name.
                    sink.AppendNickname(Source(started + 14_000, 0), 220, string.Empty, characterClass: CharacterClass.Ranger);
                    Hit(sink, started + 14_000, 220, Boss, 40_000);
                    main.ProcessUiFrameForTesting();
                    graphs.Refresh();
                    WaitFor(() => Damage(graphs)?.Seconds == 4);
                    Members(graphs).Single(static item => item.Identity is null).IsShown = false;
                    sink.AppendNickname(Source(started + 15_000, 0), 220, "Stranger", originServerId: Server);
                    Hit(sink, started + 15_000, 220, Boss, 40_000);
                    main.ProcessUiFrameForTesting();
                    graphs.Refresh();
                    WaitFor(() => Damage(graphs)?.Seconds == 5);
                    Assert.Equal([("Newbie", true), ("Tank", false), ("Stranger", false)], Members(graphs).Select(static item => (item.Label, item.IsShown)));
                    Assert.DoesNotContain(Damage(graphs)!.Series, static series => series.Label is "Tank" or "Stranger");
                }

                // After a restart: still hidden.
                var saved = new SettingsService(Path.Combine(directory, "settings.json"));
                using var again = new FightGraphsViewModel(main, settings, names, saved);
                again.Refresh();
                WaitFor(() => Damage(again)?.Series.Any(static series => series.Label == "Newbie") == true);
                Assert.Equal([("Newbie", true), ("Tank", false), ("Stranger", false)], Members(again).Select(static item => (item.Label, item.IsShown)));
                Assert.DoesNotContain(Damage(again)!.Series, static series => series.Label is "Tank" or "Stranger");

                // The tour's made-up players: one hidden there is kept nowhere.
                main.ShowHistoryEntry(TutorialDemoScene.Build(DateTimeOffset.Now - TutorialDemoScene.Length).Record);
                again.Refresh();
                WaitFor(() => Members(again).Any(static item => item.Label == "TankyBear"));
                Members(again).Single(static item => item.Label == "TankyBear").IsShown = false;
                Assert.Equal([$"{Server}:Stranger", $"{Server}:Tank"], saved.Current.FightGraphsHiddenMembers.Order());

                // Back from the tour, the made-up player's id is someone else's in the party: they show, and nothing is kept.
                Party(sink, started + 16_000, 3, 110, (110, "Healer", CharacterClass.Cleric), (200, "Newbie", CharacterClass.Sorcerer),
                    (210, "Tank", CharacterClass.Gladiator), (TutorialDemoScene.TankyBearId, "Support", CharacterClass.Chanter));
                main.ReturnToLiveCommand.Execute(null);
                main.ProcessUiFrameForTesting();
                again.Refresh();
                WaitFor(() => Members(again).Any(static item => item.Label == "Support"));
                Assert.Equal([("Newbie", true), ("Tank", false), ("Stranger", false), ("Support", true)], Members(again).Select(static item => (item.Label, item.IsShown)));
                Assert.Equal([$"{Server}:Stranger", $"{Server}:Tank"], saved.Current.FightGraphsHiddenMembers.Order());
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static FightGraphLegendItem[] Members(FightGraphsViewModel graphs) => [.. graphs.Legend.Where(static item => item.Key.StartsWith("member:", StringComparison.Ordinal))];

    private static FightGraphModel? Damage(FightGraphsViewModel graphs) => graphs.Graphs.Single(static graph => graph.Metric == FightMetric.Damage).Model;

    // A party list (0x0092) with everyone's server and name; me: the user's id in it.
    private static void Party(IRuntimeObservationSink sink, long at, long sequence, int me, params (int Id, string Name, CharacterClass Class)[] members)
    {
        for (var slot = 0; slot < members.Length; slot++)
        {
            var (id, name, characterClass) = members[slot];
            sink.AppendNickname(Source(at, 0x0092, sequence), id, name, characterClass: characterClass, isLocalPlayer: id == me, originServerId: Server);
            sink.AppendPlayerGroupMember(Source(at, 0x0092, sequence), id, PlayerGroupMembership.Party((byte)slot));
        }
    }

    private static void Hit(IRuntimeObservationSink sink, long at, int sourceId, int targetId, int amount)
    {
        var observation = new CombatWireObservation { SkillCode = 11_000_010, Damage = amount, HitCount = 1, AttemptCount = 1 };
        sink.AppendCombatWireObservation(Source(at, 0), sourceId, targetId, in observation);
    }

    private static PacketObservationSource Source(long at, ushort opcode, long sequence = 0) => new(at, 0, opcode, 0, sequence, default);

    // The pull is worked out off the UI thread and lands through the dispatcher.
    private static void WaitFor(Func<bool> condition)
    {
        var until = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(until.Elapsed < TimeSpan.FromSeconds(10), "the pull wasn't worked out");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
    }
}
