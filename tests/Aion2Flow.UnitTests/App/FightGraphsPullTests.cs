using System.Diagnostics;
using Avalonia.Threading;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

// Picking another pull in the fight graphs: the graphs keep the pull they show until the picked one is worked out, then
// show it with its best pull's dotted line; picking the shown one again drops the work on the other.
[Collection(AvaloniaTestCollection.Name)]
public sealed class FightGraphsPullTests
{
    private const int Server = 2304;
    private const string Name = "Healer";
    private const int FirstBoss = 2_300_261;
    private const int SecondBoss = 2_300_262;
    private static readonly DateTimeOffset Started = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_Pull_Switch_Keeps_The_Shown_Pull_Until_The_Picked_One_Is_Worked_Out()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aion2flow-pulls-{Guid.NewGuid():N}");
        try
        {
            // The user's bests of both bosses, 9 s and 12 s long.
            var store = new BestRunStore(directory);
            await store.ConsiderAsync(Best(FirstBoss, 9), Entry, TestContext.Current.CancellationToken);
            await store.ConsiderAsync(Best(SecondBoss, 12), Entry, TestContext.Current.CancellationToken);
            AvaloniaTestHost.Run(() =>
            {
                using var graphs = Graphs(store, directory);

                // The newest pull first (the second boss, 4 s), with its best's line once worked out.
                graphs.Refresh();
                WaitFor(() => Damage(graphs) is { } model && You(model)?.Values.Length == 4);
                Assert.Equal(12, Dotted(Damage(graphs)!)?.Values.Length);

                // The first boss's pull (2 s) picked: the graphs stay as they were until it's worked out.
                var shown = graphs.Graphs.Select(static graph => graph.Model).Append(graphs.OverviewModel).ToList();
                graphs.SelectedFightIndex = 0;
                Assert.Equal(shown, graphs.Graphs.Select(static graph => graph.Model).Append(graphs.OverviewModel), ReferenceEqualityComparer.Instance);
                WaitFor(() => You(Damage(graphs)!)?.Values.Length == 2);
                Assert.Equal(9, Dotted(Damage(graphs)!)?.Values.Length);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Back to the shown pull before the other was worked out: the graphs keep it, and a legend click redraws it at once.
    [Fact]
    public void Picking_The_Shown_Pull_Again_Drops_The_Work_On_The_Other()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aion2flow-pulls-{Guid.NewGuid():N}");
        try
        {
            AvaloniaTestHost.Run(() =>
            {
                using var graphs = Graphs(new BestRunStore(directory), directory);
                graphs.Refresh();
                WaitFor(() => Damage(graphs) is { } model && You(model)?.Values.Length == 4);

                graphs.SelectedFightIndex = 0;
                graphs.SelectedFightIndex = 1;
                var until = Stopwatch.StartNew();
                while (until.ElapsedMilliseconds < 500)
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(10);
                }

                Assert.Equal(4, You(Damage(graphs)!)?.Values.Length);
                graphs.Legend.Single(static item => item.Key == FightGraphsViewModel.PartyKey).IsShown = false;
                Assert.Equal([FightGraphStyle.Area], Damage(graphs)!.Series.Select(static series => series.Style));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // The fight graphs over a history entry with two kills by the user (101, a cleric) and a tank: the first boss's in 2 s,
    // the second boss's in 4 s.
    private static FightGraphsViewModel Graphs(BestRunStore store, string directory)
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
            localization, settings, frameBatch);

        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(10), 100, "Tank", characterClass: CharacterClass.Gladiator, originServerId: Server);
        sink.AppendNickname(Source(10), 101, Name, characterClass: CharacterClass.Cleric, isLocalPlayer: true, originServerId: Server);
        foreach (var (boss, npcCode, at, seconds) in ((int, int, long, int)[])[(300, FirstBoss, 0, 2), (301, SecondBoss, 10_000, 4)])
        {
            sink.AppendNpcCode(Source(at + 20), boss, npcCode);
            sink.AppendNpcKind(Source(at + 20), boss, NpcKind.Boss);
            sink.SetNpcBattle(Source(at + 25), boss, true);
            sink.AppendNpcHp(Source(at + 30), boss, 10_000_000, 10_000_000);
            Hit(sink, 100, boss, 5_000_000, 11_000_010, at + 1_000);
            Hit(sink, 101, 100, 1_500, 17_010_000, at + 2_000);
            Hit(sink, 100, boss, 4_600_000, 11_000_010, at + 1_000 + seconds * 1_000);
            sink.AppendNpcHp(Source(at + 1_100 + seconds * 1_000), boss, 0, 10_000_000);
        }

        var payload = scene.CreateArchivePayload();
        Assert.Equal(2, payload.BossFights.Count);
        main.ShowHistoryEntry(new ArchivedEncounterRecord { ScenePayload = payload });
        return new FightGraphsViewModel(main, settings, names, settingsService, new BestRunRecorder(store, capture.Scene, resources), resources);
    }

    private static FightGraphModel? Damage(FightGraphsViewModel graphs) => graphs.Graphs.Single(static graph => graph.Metric == FightMetric.Damage).Model;

    private static FightGraphSeries? You(FightGraphModel model) => model.Series.SingleOrDefault(static series => series.Style == FightGraphStyle.Area);

    private static FightGraphSeries? Dotted(FightGraphModel model) => model.Series.SingleOrDefault(static series => series.Style == FightGraphStyle.Dotted);

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

    // A best of the user's against a boss, from another encounter, with its damage second by second.
    private static BestRun Best(int npcCode, int seconds) => new()
    {
        ServerId = Server,
        Character = Name,
        Class = CharacterClass.Cleric,
        NpcCode = npcCode,
        MapId = 1,
        At = Started.AddDays(-1),
        Milliseconds = seconds * 1_000,
        EncounterId = Guid.NewGuid(),
        Values = new() { ["Damage"] = 100 },
        Curves = new() { ["Damage"] = new double[seconds] },
    };

    private static ArchivedEncounterRecord Entry() => new() { ScenePayload = new SceneLiveReadModel(Started, new FixedTimeProvider(Started)).CreateArchivePayload() };

    private static void Hit(IRuntimeObservationSink sink, int sourceId, int targetId, int amount, int skillCode, long offsetMilliseconds)
    {
        var observation = skillCode == 17_010_000
            ? new CombatWireObservation { SkillCode = skillCode, Damage = amount, ResourceKind = CombatResourceKind.Health }
            : new CombatWireObservation { SkillCode = skillCode, Damage = amount, HitCount = 1, AttemptCount = 1 };
        sink.AppendCombatWireObservation(Source(offsetMilliseconds), sourceId, targetId, in observation);
    }

    private static PacketObservationSource Source(long offsetMilliseconds) =>
        new(Started.ToUnixTimeMilliseconds() + offsetMilliseconds, 0, 0, 0, offsetMilliseconds, default);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
