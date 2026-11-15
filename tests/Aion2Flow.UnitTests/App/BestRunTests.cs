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

public sealed class BestRunTests
{
    private const int Server = 2304;
    private const int Npc = 2_300_261;
    private const int DummyNpc = 2_400_032; // a Training Scarecrow
    private const string Name = "Healer";
    private static readonly DateTimeOffset Started = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    // A pull stays while it's the best in at least one metric, its history entry beside the index; one that beats nothing
    // isn't kept, and one beaten in everything goes, file and all.
    [Fact]
    public async Task A_Pull_Stays_While_It_Is_The_Best_In_Something()
    {
        var directory = TempDirectory();
        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new BestRunStore(directory);
            var first = Run(damage: 1_000, healing: 500, taken: 500, missing: 300, aggro: 0.2, stagger: 10);
            var improvements = await store.ConsiderAsync(first, Entry, cancellationToken);
            Assert.Equal(BestRunKeys.RankedFor(CharacterClass.Cleric), improvements.Select(static improvement => improvement.Key));
            Assert.All(improvements, static improvement => Assert.Null(improvement.Previous));
            Assert.True(File.Exists(store.FileOf(first)));

            // More damage, but more damage taken too: the best in damage, the first still in damage taken.
            var second = Run(damage: 1_200, healing: 500, taken: 700, missing: 300, aggro: 0.2, stagger: 10);
            Assert.Equal([("Damage", 1_200d, (double?)1_000d)],
                (await store.ConsiderAsync(second, Entry, cancellationToken)).Select(static improvement => (improvement.Key, improvement.Value, improvement.Previous)));
            Assert.Same(second, store.BestOf(Server, Name, Npc, "Damage"));
            Assert.Same(first, store.BestOf(Server, Name, Npc, "DamageTaken"));

            var worse = Run(damage: 900, healing: 400, taken: 800, missing: 400, aggro: 0.3, stagger: 5);
            Assert.Empty(await store.ConsiderAsync(worse, Entry, cancellationToken));
            Assert.False(File.Exists(store.FileOf(worse)));

            // Less taken, less missing HP and less aggro (a cleric) count as better.
            var third = Run(damage: 1_100, healing: 600, taken: 400, missing: 200, aggro: 0.1, stagger: 12);
            Assert.Equal(["Healing", BestRunKeys.EffectiveHealing, "DamageTaken", BestRunKeys.GroupUnhealedHealth, "Aggro", "Stagger"],
                (await store.ConsiderAsync(third, Entry, cancellationToken)).Select(static improvement => improvement.Key));
            Assert.False(File.Exists(store.FileOf(first)));
            Assert.True(File.Exists(store.FileOf(second)));

            var reread = new BestRunStore(directory);
            Assert.Equal(second.Id, reread.BestOf(Server, Name, Npc, "Damage")?.Id);
            Assert.Equal(third.Id, reread.BestOf(Server, Name, Npc, "Aggro")?.Id);
            Assert.Null(reread.BestOf(Server, "Alt", Npc, "Damage"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // "Store run" keeps a pull whatever it beats; a pull the overlay came into halfway is never a best. Storing a best marks it
    // as the user's; the trash takes a stored run away but leaves a best a best.
    [Fact]
    public async Task Stored_Runs_Stay_Until_The_User_Removes_Them()
    {
        var directory = TempDirectory();
        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new BestRunStore(directory);
            var best = Run(damage: 1_000, healing: 500, taken: 500, missing: 300, aggro: 0.2, stagger: 10);
            await store.ConsiderAsync(best, Entry, cancellationToken);

            var halfway = Run(damage: 9_000, healing: 9_000, taken: 0, missing: 0, aggro: 0, stagger: 99);
            halfway.Complete = false;
            Assert.Empty(await store.StoreAsync(halfway, Entry, cancellationToken));
            Assert.True(halfway.Custom);
            Assert.True(store.IsStored(halfway.EncounterId, halfway.FightIndex));
            Assert.Same(best, store.BestOf(Server, Name, Npc, "Damage"));

            // Beaten in everything: the best goes, the stored run stays.
            var better = Run(damage: 2_000, healing: 900, taken: 100, missing: 50, aggro: 0.05, stagger: 20);
            await store.ConsiderAsync(better, Entry, cancellationToken);
            Assert.DoesNotContain(best, store.Runs);
            Assert.Contains(halfway, store.Runs);

            // Storing the best marks it as the user's; the trash then leaves it a best.
            Assert.Empty(await store.StoreAsync(Run(damage: 2_000, healing: 900, taken: 100, missing: 50, aggro: 0.05, stagger: 20, encounter: better.EncounterId), Entry, cancellationToken));
            Assert.True(better.Custom);
            store.Remove(better);
            Assert.False(better.Custom);
            Assert.Contains(better, store.Runs);
            Assert.True(File.Exists(store.FileOf(better)));

            store.Remove(halfway);
            Assert.DoesNotContain(halfway, store.Runs);
            Assert.False(File.Exists(store.FileOf(halfway)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // One entry per pull, also when "Store run" and the pull's best cross (a pull can be stored while it's still on, and a
    // best takes a moment to work out and write): the complete copy stays, as the user's.
    [Fact]
    public async Task A_Pull_Stored_While_Its_Best_Comes_In_Is_Kept_Once()
    {
        var directory = TempDirectory();
        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new BestRunStore(directory);

            // Stored halfway through; the kill then sets a best.
            var halfway = Run(damage: 500, healing: 100, taken: 100, missing: 100, aggro: 0.1, stagger: 1);
            halfway.Complete = false;
            await store.StoreAsync(halfway, Entry, cancellationToken);
            var kill = Run(damage: 1_000, healing: 500, taken: 500, missing: 300, aggro: 0.2, stagger: 10, encounter: halfway.EncounterId);
            Assert.NotEmpty(await store.ConsiderAsync(kill, Entry, cancellationToken));
            Assert.Same(kill, Assert.Single(store.Runs));
            Assert.True(kill.Custom);
            Assert.False(File.Exists(store.FileOf(halfway)));

            // Stored while the kill's best is still being written.
            var best = Run(damage: 2_000, healing: 900, taken: 100, missing: 50, aggro: 0.05, stagger: 20, at: 1);
            using var writing = new SemaphoreSlim(0);
            using var stored = new SemaphoreSlim(0);
            var recording = Task.Run(() => store.ConsiderAsync(best, () =>
            {
                writing.Release();
                stored.Wait(cancellationToken);
                return Entry();
            }, cancellationToken), cancellationToken);
            await writing.WaitAsync(cancellationToken);
            var copy = Run(damage: 2_000, healing: 900, taken: 100, missing: 50, aggro: 0.05, stagger: 20, at: 1, encounter: best.EncounterId);
            await store.StoreAsync(copy, Entry, cancellationToken);
            stored.Release();
            await recording;

            var pull = Assert.Single(store.Runs, run => run.EncounterId == best.EncounterId);
            Assert.True(pull.Custom && pull.Complete);
            Assert.Same(pull, store.BestOf(Server, Name, Npc, "Damage"));
            Assert.Equal(2, Directory.EnumerateFiles(directory, "*" + SceneArchiveFile.Extension).Count());
            Assert.Equal(2, new BestRunStore(directory).Runs.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // The likelier crossing: the best, which started writing first, is added before the store's copy arrives; the copy
    // then goes, and the best becomes the user's.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Store_That_Arrives_After_Its_Best_Marks_The_Best(bool complete)
    {
        var directory = TempDirectory();
        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new BestRunStore(directory);
            var best = Run(damage: 2_000, healing: 900, taken: 100, missing: 50, aggro: 0.05, stagger: 20);
            var copy = Run(damage: 2_000, healing: 900, taken: 100, missing: 50, aggro: 0.05, stagger: 20, encounter: best.EncounterId);
            copy.Complete = complete;
            using var making = new SemaphoreSlim(0);
            using var go = new SemaphoreSlim(0);
            var storing = Task.Run(() => store.StoreAsync(copy, () =>
            {
                making.Release();
                go.Wait(cancellationToken);
                return Entry();
            }, cancellationToken), cancellationToken);
            await making.WaitAsync(cancellationToken);
            await store.ConsiderAsync(best, Entry, cancellationToken);
            go.Release();
            await storing;

            Assert.Same(best, Assert.Single(store.Runs));
            Assert.True(best.Custom);
            Assert.Single(Directory.EnumerateFiles(directory, "*" + SceneArchiveFile.Extension));
            Assert.True(Assert.Single(new BestRunStore(directory).Runs).Custom);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // A history entry that can't be made, or not written to the end, leaves no file behind.
    [Fact]
    public async Task A_Run_Whose_History_Entry_Fails_Leaves_No_File()
    {
        var directory = TempDirectory();
        try
        {
            var store = new BestRunStore(directory);
            var run = Run(damage: 1_000, healing: 500, taken: 500, missing: 300, aggro: 0.2, stagger: 10);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsiderAsync(run, static () => throw new InvalidOperationException(), TestContext.Current.CancellationToken));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ConsiderAsync(run, Entry, new CancellationToken(canceled: true)));

            Assert.Empty(store.Runs);
            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // index.json held by another process (OneDrive, a virus scanner) when it's saved: the old index stays on disk, so the
    // runs it lists keep their files (a stored copy the kill replaced, a stored run in the trash). A run that lost the merge
    // is in no index, and its file goes.
    [Fact]
    public async Task A_Run_Keeps_Its_File_While_The_Index_On_Disk_Lists_It()
    {
        var directory = TempDirectory();
        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new BestRunStore(directory);
            var halfway = Run(damage: 500, healing: 100, taken: 100, missing: 100, aggro: 0.1, stagger: 1);
            halfway.Complete = false;
            await store.StoreAsync(halfway, Entry, cancellationToken);
            var other = Run(damage: 400, healing: 100, taken: 100, missing: 100, aggro: 0.1, stagger: 1, at: 1);
            other.Complete = false;
            await store.StoreAsync(other, Entry, cancellationToken);

            var kill = Run(damage: 1_000, healing: 500, taken: 500, missing: 300, aggro: 0.2, stagger: 10, encounter: halfway.EncounterId);
            var copy = Run(damage: 2_000, healing: 900, taken: 100, missing: 50, aggro: 0.05, stagger: 20, encounter: halfway.EncounterId);
            // Open without FileShare.Delete: nothing can replace the file meanwhile.
            using (File.Open(Path.Combine(directory, "index.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                await store.ConsiderAsync(kill, Entry, cancellationToken);
                await store.ConsiderAsync(copy, Entry, cancellationToken);
                store.Remove(other);
            }

            Assert.Same(kill, Assert.Single(store.Runs));
            Assert.False(File.Exists(store.FileOf(copy)));
            var onDisk = new BestRunStore(directory).Runs;
            Assert.Equal([halfway.Id, other.Id], onDisk.Select(static run => run.Id));
            Assert.All(onDisk, run => Assert.True(File.Exists(store.FileOf(run)), $"No file for run {run.Id}"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // The runs tabs: the character's bests (a cell marked where a run is the best) and the runs they stored with the bests
    // among them highlighted, by dungeon; a column head sorts by it, the best first, a second click the other way.
    [Fact]
    public async Task Runs_Tabs_List_The_Characters_Bests_And_Stored_Runs()
    {
        var directory = TempDirectory();
        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new BestRunStore(directory);
            var first = Run(damage: 1_000, healing: 500, taken: 500, missing: 300, aggro: 0.2, stagger: 10, at: 0);
            var second = Run(damage: 1_200, healing: 500, taken: 700, missing: 300, aggro: 0.2, stagger: 10, at: 1);
            var stored = Run(damage: 500, healing: 100, taken: 900, missing: 900, aggro: 0.9, stagger: 1, at: 2, mapId: 2);
            stored.Complete = false;
            var alt = Run(damage: 5_000, healing: 500, taken: 500, missing: 300, aggro: 0.2, stagger: 10, at: 3);
            alt.Character = "Alt";
            await store.ConsiderAsync(first, Entry, cancellationToken);
            await store.ConsiderAsync(second, Entry, cancellationToken);
            await store.StoreAsync(stored, Entry, cancellationToken);
            await store.ConsiderAsync(alt, Entry, cancellationToken);

            var runs = new FightGraphRunsViewModel(store, Settings(directory), new GameResourceService(new LanguageService()), () => (Server, Name), (_, _) => { });
            runs.Refresh();
            // A cleric's runs go by the group's missing HP: "UHS*", the note in its hover text; every metric says what it is.
            var missing = runs.Columns.Single(static column => column.Key == "UnhealedHealth");
            Assert.EndsWith("*", missing.Text);
            Assert.NotNull(missing.Note);
            Assert.All(runs.Columns.Skip(3), static column => Assert.False(string.IsNullOrEmpty(column.Tip)));
            Assert.All(runs.Columns.Where(static column => column.Key != "UnhealedHealth"), static column => Assert.Null(column.Note));
            Assert.Equal(3, runs.Dungeons.Count);
            Assert.Equal([second.Id, first.Id], runs.BestRows.Select(static row => row.Run.Id));
            Assert.Equal([stored.Id, second.Id, first.Id], runs.StoredRows.Select(static row => row.Run.Id));
            Assert.Equal([false, true, true], runs.StoredRows.Select(static row => row.Highlight));
            Assert.Equal([true, false, false, false, false, false], runs.BestRows[0].Cells.Select(static cell => cell.IsBest));
            Assert.Equal([false, true, true, true, true, true], runs.BestRows[1].Cells.Select(static cell => cell.IsBest));
            Assert.Equal([false, false, true], runs.StoredRows.Select(static row => row.CanDelete).Reverse());

            runs.SortBy("Damage");
            Assert.Equal([second.Id, first.Id, stored.Id], runs.StoredRows.Select(static row => row.Run.Id));
            runs.SortBy("DamageTaken");
            Assert.Equal([first.Id, second.Id, stored.Id], runs.StoredRows.Select(static row => row.Run.Id));
            runs.SortBy("DamageTaken");
            Assert.Equal([stored.Id, second.Id, first.Id], runs.StoredRows.Select(static row => row.Run.Id));

            runs.SelectedDungeon = runs.Dungeons.Single(static dungeon => dungeon.MapId == 2);
            Assert.Empty(runs.BestRows);
            Assert.NotNull(runs.BestEmptyText);
            Assert.Equal([stored.Id], runs.StoredRows.Select(static row => row.Run.Id));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Two ticked stored runs compared: a section per metric with each run's graph (the user's area in it), one below the
    // other; with an equal time scale every graph is as long as the longest run, without it each as long as its own.
    [Fact]
    public async Task Ticked_Stored_Runs_Are_Compared_One_Below_The_Other()
    {
        var directory = TempDirectory();
        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new BestRunStore(directory);
            // A 2 s pull, then (an hour later) a 4 s one.
            foreach (var (lastBlow, at) in ((long, int)[])[(3_000, 0), (5_000, 1)])
            {
                var (scene, sink) = BossFight();
                Damage(sink, 100, 300, 5_000_000, 1_000);
                Heal(sink, 101, 100, 1_500, 2_000);
                Damage(sink, 100, 300, 4_600_000, lastBlow);
                sink.AppendNpcHp(Source(lastBlow + 100), 300, 0, 10_000_000);
                var payload = scene.CreateArchivePayload();
                var run = Run(damage: 1_000, healing: 500, taken: 0, missing: 0, aggro: 0, stagger: 0, at: at, encounter: payload.Snapshot.EncounterId);
                run.Milliseconds = lastBlow - 1_000;
                await store.StoreAsync(run, () => new ArchivedEncounterRecord { ScenePayload = payload }, cancellationToken);
            }

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
            using var graphs = new FightGraphsViewModel(main, settings, names, settingsService, new BestRunRecorder(store, capture.Scene, resources), resources);

            var runs = graphs.Runs!;
            runs.Refresh();
            Assert.False(runs.CompareCommand.CanExecute(null));
            foreach (var row in runs.StoredRows)
                row.IsTicked = true;
            Assert.Equal(2, runs.TickedCount);
            await runs.CompareCommand.ExecuteAsync(null);

            Assert.Equal(FightGraphsTab.Compare, graphs.Tab);
            Assert.Null(graphs.CompareStatus);
            // Ticked newest first (the table's order): the 4 s pull leads in every section.
            Assert.Equal(6, graphs.CompareMetrics.Count);
            Assert.Equal([4, 2], graphs.CompareMetrics[0].Runs.Select(static run => run.Model!.Seconds));
            Assert.All(graphs.CompareMetrics, static section => Assert.Equal(2, section.Runs.Count));
            Assert.All(graphs.CompareMetrics[1].Runs, static run => Assert.Contains(run.Model!.Series, static series => series.Style == FightGraphStyle.Area));
            Assert.All(graphs.CompareMetrics, static section => Assert.EndsWith("0:04", section.Runs[0].Title));

            graphs.CompareEqualTime = true;
            Assert.Equal([4, 4], graphs.CompareMetrics[0].Runs.Select(static run => run.Model!.Seconds));
            Assert.True(new SettingsService(settingsService.FilePath).Current.FightGraphsCompareEqualTime);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // A ticked best that a better kill pushes out of the store (on a worker thread) before the refresh posted with it ran:
    // Compare leaves it out, in the order the rest were ticked, rather than failing.
    [Fact]
    public async Task Compare_Leaves_Out_A_Ticked_Best_The_Store_Just_Dropped()
    {
        var directory = TempDirectory();
        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new BestRunStore(directory);
            var older = Run(damage: 500, healing: 100, taken: 900, missing: 900, aggro: 0.9, stagger: 1, at: 0);
            var best = Run(damage: 1_000, healing: 500, taken: 500, missing: 300, aggro: 0.2, stagger: 10, at: 1);
            var newer = Run(damage: 600, healing: 100, taken: 900, missing: 900, aggro: 0.9, stagger: 1, at: 2);
            (older.Complete, newer.Complete) = (false, false);
            await store.StoreAsync(older, Entry, cancellationToken);
            await store.ConsiderAsync(best, Entry, cancellationToken);
            await store.StoreAsync(newer, Entry, cancellationToken);
            IReadOnlyList<BestRun>? compared = null;
            var runs = new FightGraphRunsViewModel(store, Settings(directory), new GameResourceService(new LanguageService()), () => (Server, Name), (_, _) => { },
                picked =>
                {
                    compared = picked;
                    return Task.CompletedTask;
                });
            runs.Refresh();
            foreach (var row in runs.StoredRows)
                row.IsTicked = true;
            Assert.Equal(3, runs.TickedCount);

            await store.ConsiderAsync(Run(damage: 2_000, healing: 900, taken: 100, missing: 50, aggro: 0.05, stagger: 20, at: 3), Entry, cancellationToken);
            Assert.DoesNotContain(best, store.Runs);
            await runs.CompareCommand.ExecuteAsync(null);

            Assert.Equal([newer.Id, older.Id], compared?.Select(static run => run.Id));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // The boss's attention is good for tanks (Templars, and Gladiators, who tank too), bad for everyone else.
    [Fact]
    public void More_Aggro_Is_Better_For_Tanks_Only()
    {
        Assert.True(BestRunKeys.Beats(0.6, 0.4, "Aggro", CharacterClass.Templar));
        Assert.True(BestRunKeys.Beats(0.6, 0.4, "Aggro", CharacterClass.Gladiator));
        Assert.False(BestRunKeys.Beats(0.6, 0.4, "Aggro", CharacterClass.Cleric));
        Assert.True(BestRunKeys.Beats(100, 200, "DamageTaken", CharacterClass.Templar));
        Assert.True(BestRunKeys.Beats(200, 100, "Stagger", CharacterClass.Cleric));

        // Unhealed health is a healer's key figure: a cleric goes by the whole group's, everyone else by their own.
        Assert.Equal(BestRunKeys.GroupUnhealedHealth, BestRunKeys.Of(FightMetric.UnhealedHealth, true, CharacterClass.Cleric));
        Assert.Equal("UnhealedHealth", BestRunKeys.Of(FightMetric.UnhealedHealth, true, CharacterClass.Templar));
        Assert.True(BestRunKeys.Beats(100, 200, BestRunKeys.GroupUnhealedHealth, CharacterClass.Cleric));
        Assert.Equal(BestRunKeys.EffectiveHealing, BestRunKeys.Of(FightMetric.Healing, false, CharacterClass.Cleric));
    }

    // A kill the overlay saw from the start is worked out second by second and kept as the first best of its boss; a pull
    // it came into halfway (the boss had lost less than 90 % of its HP while it watched) isn't.
    [Fact]
    public async Task A_Complete_Kill_Becomes_A_Best()
    {
        var directory = TempDirectory();
        try
        {
            var (scene, sink) = BossFight();
            Damage(sink, 100, 300, 5_000_000, 1_000);
            Heal(sink, 101, 100, 1_500, 2_000);
            Damage(sink, 100, 300, 4_600_000, 3_000);
            sink.AppendNpcHp(Source(3_100), 300, 0, 10_000_000);
            var recorder = new BestRunRecorder(new BestRunStore(directory), scene, new GameResourceService(new LanguageService()));
            var frame = scene.CreateFrame();
            recorder.Observe(frame);
            await recorder.Pending;

            Assert.True(recorder.RecordOf(frame.Snapshot.EncounterId, 0)?.IsFirst);
            var best = recorder.Store.BestOf(Server, Name, Npc, "Healing");
            Assert.NotNull(best);
            // 1,500 healed in the second second of the 2 s from the first blow to the last.
            Assert.Equal([0d, 1_500], best.Curves["Healing"]);
            Assert.Equal(750, best.Values["Healing"]);
            Assert.True(File.Exists(recorder.Store.FileOf(best)));
            Assert.Equal(2, best.Curves[BestRunKeys.GroupUnhealedHealth].Length);

            var (late, lateSink) = BossFight();
            Damage(lateSink, 100, 300, 5_000_000, 1_000);
            lateSink.AppendNpcHp(Source(1_100), 300, 0, 10_000_000);
            var halfway = new BestRunRecorder(new BestRunStore(Path.Combine(directory, "late")), late, new GameResourceService(new LanguageService()));
            var lateFrame = late.CreateFrame();
            halfway.Observe(lateFrame);
            await halfway.Pending;
            Assert.Null(halfway.RecordOf(lateFrame.Snapshot.EncounterId, 0));
            Assert.Null(halfway.Store.BestOf(Server, Name, Npc, "Damage"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // A kill seen only once the scene had moved on (a map change, the next pull in boss mode) can't be worked out any more:
    // the events and journal read would be the next scene's, an empty run that would beat every lower-is-better best.
    [Fact]
    public async Task A_Kill_Of_A_Scene_That_Moved_On_Is_Not_Kept()
    {
        var directory = TempDirectory();
        try
        {
            var (scene, sink) = BossFight();
            Damage(sink, 100, 300, 5_000_000, 1_000);
            Heal(sink, 101, 100, 1_500, 2_000);
            Damage(sink, 100, 300, 4_600_000, 3_000);
            sink.AppendNpcHp(Source(3_100), 300, 0, 10_000_000);
            var recorder = new BestRunRecorder(new BestRunStore(directory), scene, new GameResourceService(new LanguageService()));
            var frame = scene.CreateFrame();
            scene.Reset(() => Started);
            recorder.Observe(frame);
            await recorder.Pending;

            Assert.Null(recorder.RecordOf(frame.Snapshot.EncounterId, 0));
            Assert.Null(recorder.Store.BestOf(Server, Name, Npc, "Healing"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    // A kill's journal is taken in Observe, on the UI thread, as the fight graphs take theirs: what came since the frame is
    // applied there, not by a pool thread while the UI reads the scene's registry without a lock.
    [Fact]
    public async Task A_Kill_Takes_Its_Journal_On_The_Thread_That_Observes_It()
    {
        var directory = TempDirectory();
        try
        {
            var (scene, sink) = BossFight();
            Damage(sink, 100, 300, 5_000_000, 1_000);
            Heal(sink, 101, 100, 1_500, 2_000);
            Damage(sink, 100, 300, 4_600_000, 3_000);
            sink.AppendNpcHp(Source(3_100), 300, 0, 10_000_000);
            var recorder = new BestRunRecorder(new BestRunStore(directory), scene, new GameResourceService(new LanguageService()));
            var frame = scene.CreateFrame();
            sink.AppendNickname(Source(3_200), 102, "Newcomer", characterClass: CharacterClass.Ranger, originServerId: Server);

            recorder.Observe(frame);
            var appliedByObserve = frame.MetadataRegistry!.TryGetPcMetadata(102, out _);
            await recorder.Pending;

            Assert.True(appliedByObserve);
            Assert.True(recorder.RecordOf(frame.Snapshot.EncounterId, 0)?.IsFirst);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // A training dummy pull that ran its full length is a best of that length, by damage alone; one the user stopped
    // early isn't, and neither is a boss's best or another length's. A dummy whose spawn the overlay missed (no code) too.
    [Theory]
    [InlineData(DummyNpc)]
    [InlineData(0)]
    public async Task A_Full_Length_Dummy_Pull_Becomes_A_Best_Of_Its_Length(int npcCode)
    {
        var directory = TempDirectory();
        try
        {
            var (scene, sink) = DummyFight(lengthMilliseconds: 10_000, npcCode);
            for (var at = 1_000; at <= 10_000; at += 1_000)
                Damage(sink, 101, 300, 5_000, at);
            Damage(sink, 101, 300, 5_000, 11_500); // its length is up at 11 s: no part of it, and no pull of its own
            Damage(sink, 101, 300, 9_000, 30_000);
            Damage(sink, 101, 300, 9_000, 31_000);
            Damage(sink, 101, 300, 9_000, 50_000); // a pause of 19 s: the second pull stopped short of its length
            var recorder = new BestRunRecorder(new BestRunStore(directory), scene, new GameResourceService(new LanguageService()));
            var frame = scene.CreateFrame();
            recorder.Observe(frame);
            await recorder.Pending;

            var record = recorder.RecordOf(frame.Snapshot.EncounterId, 0);
            Assert.NotNull(record);
            Assert.True(record.IsFirst && record.Dummy);
            Assert.Equal(["Damage"], record.Improvements.Select(static improvement => improvement.Key));
            Assert.Null(recorder.RecordOf(frame.Snapshot.EncounterId, 1));
            var best = recorder.Store.BestOf(Server, Name, DummyNpc, "Damage", 10_000);
            Assert.NotNull(best);
            Assert.Equal((true, 10_000L, 5_000d), (best.Dummy, best.Milliseconds, best.Values["Damage"]));
            Assert.Null(recorder.Store.BestOf(Server, Name, DummyNpc, "Damage"));
            Assert.Null(recorder.Store.BestOf(Server, Name, DummyNpc, "Damage", 60_000));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Dummy_Runs_Are_Bests_Of_Their_Length_By_Damage_Alone()
    {
        var directory = TempDirectory();
        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new BestRunStore(directory);
            var minute = DummyRun(damage: 1_000, healing: 100, milliseconds: 60_000);
            Assert.Equal(["Damage"], (await store.ConsiderAsync(minute, Entry, cancellationToken)).Select(static improvement => improvement.Key));
            // More healing, less damage: a dummy is there to hit.
            Assert.Empty(await store.ConsiderAsync(DummyRun(damage: 900, healing: 5_000, milliseconds: 60_000), Entry, cancellationToken));
            // Two minutes: a best of its own, whatever the minute's was.
            var twoMinutes = DummyRun(damage: 800, healing: 0, milliseconds: 120_000);
            Assert.Single(await store.ConsiderAsync(twoMinutes, Entry, cancellationToken));

            Assert.Same(minute, store.BestOf(Server, Name, DummyNpc, "Damage", 60_000));
            Assert.Same(twoMinutes, store.BestOf(Server, Name, DummyNpc, "Damage", 120_000));
            Assert.Null(store.BestOf(Server, Name, DummyNpc, "Damage"));
            Assert.Equal(["Damage"], store.BestKeysOf(minute));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Every kind of dummy takes the same damage (the two in Altgard, hit for hit), so they share their bests per length: a
    // pull on the melee one, or on one whose spawn the overlay missed (no code), counts against the scarecrow's.
    [Fact]
    public async Task Every_Training_Dummy_Shares_Its_Bests()
    {
        var directory = TempDirectory();
        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new BestRunStore(directory);
            await store.ConsiderAsync(DummyRun(damage: 1_000, healing: 0, milliseconds: 60_000), Entry, cancellationToken);
            Assert.Empty(await store.ConsiderAsync(DummyRun(damage: 900, healing: 0, milliseconds: 60_000, npcCode: 2_400_035), Entry, cancellationToken));
            var unknown = DummyRun(damage: 1_100, healing: 0, milliseconds: 60_000, npcCode: 0);
            Assert.Equal([1_000d], (await store.ConsiderAsync(unknown, Entry, cancellationToken)).Select(static improvement => improvement.Previous));

            Assert.Same(unknown, store.BestOf(Server, Name, 2_400_035, "Damage", 60_000));
            Assert.Same(unknown, store.BestOf(Server, Name, 0, "Damage", 60_000));
            Assert.Equal([unknown], store.Runs);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static BestRun DummyRun(double damage, double healing, long milliseconds, int npcCode = DummyNpc)
    {
        var run = Run(damage, healing, taken: 0, missing: 0, aggro: 0, stagger: 0);
        (run.NpcCode, run.Dummy, run.Milliseconds) = (npcCode, true, milliseconds);
        return run;
    }

    // The user (101, a cleric with a known character) at a training scarecrow, the game data's (or no code: recognized by
    // its refill).
    private static (SceneLiveReadModel Scene, IRuntimeObservationSink Sink) DummyFight(long lengthMilliseconds, int npcCode = DummyNpc)
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started));
        scene.SetTrainingDummyRules(new TrainingDummyRules(lengthMilliseconds, 5_000));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(10), 101, Name, characterClass: CharacterClass.Cleric, isLocalPlayer: true, originServerId: Server);
        if (npcCode > 0)
            sink.AppendNpcCode(Source(20), 300, npcCode);
        sink.AppendNpcKind(Source(20), 300, NpcKind.TrainingDummy);
        return (scene, sink);
    }

    private static BestRun Run(double damage, double healing, double taken, double missing, double aggro, double stagger, int at = 0, uint mapId = 1, Guid? encounter = null) => new()
    {
        ServerId = Server,
        Character = Name,
        Class = CharacterClass.Cleric,
        NpcCode = Npc,
        MapId = mapId,
        At = Started.AddHours(at),
        EncounterId = encounter ?? Guid.NewGuid(),
        Values = new()
        {
            ["Damage"] = damage,
            ["Healing"] = healing,
            [BestRunKeys.EffectiveHealing] = healing,
            ["DamageTaken"] = taken,
            ["UnhealedHealth"] = missing,
            [BestRunKeys.GroupUnhealedHealth] = missing,
            ["Aggro"] = aggro,
            ["Stagger"] = stagger,
        },
    };

    private static ArchivedEncounterRecord Entry() => new() { ScenePayload = new SceneLiveReadModel(Started, new FixedTimeProvider(Started)).CreateArchivePayload() };

    private static string TempDirectory() => Path.Combine(Path.GetTempPath(), $"aion2flow-bests-{Guid.NewGuid():N}");

    private static SettingsFlyoutViewModel Settings(string directory)
    {
        var settingsService = new SettingsService(Path.Combine(directory, "settings.json"));
        var language = new LanguageService();
        var localization = new LocalizationService(language);
        return new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), new UiScaleService(settingsService),
            new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
    }

    // The user (101, a cleric with a known character) and a tank against a 10M HP boss whose type the game data knows.
    private static (SceneLiveReadModel Scene, IRuntimeObservationSink Sink) BossFight()
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(10), 100, "Tank", characterClass: CharacterClass.Gladiator, originServerId: Server);
        sink.AppendNickname(Source(10), 101, Name, characterClass: CharacterClass.Cleric, isLocalPlayer: true, originServerId: Server);
        sink.AppendNpcCode(Source(20), 300, Npc);
        sink.AppendNpcKind(Source(20), 300, NpcKind.Boss);
        sink.SetNpcBattle(Source(25), 300, true);
        sink.AppendNpcHp(Source(30), 300, 10_000_000, 10_000_000);
        return (scene, sink);
    }

    private static void Damage(IRuntimeObservationSink sink, int sourceId, int targetId, int damage, long offsetMilliseconds)
    {
        var observation = new CombatWireObservation { SkillCode = 11_000_010, Damage = damage, HitCount = 1, AttemptCount = 1 };
        sink.AppendCombatWireObservation(Source(offsetMilliseconds), sourceId, targetId, in observation);
    }

    private static void Heal(IRuntimeObservationSink sink, int sourceId, int targetId, int amount, long offsetMilliseconds)
    {
        var observation = new CombatWireObservation { SkillCode = 17_010_000, Damage = amount, ResourceKind = CombatResourceKind.Health };
        sink.AppendCombatWireObservation(Source(offsetMilliseconds), sourceId, targetId, in observation);
    }

    private static PacketObservationSource Source(long offsetMilliseconds) =>
        new(Started.ToUnixTimeMilliseconds() + offsetMilliseconds, 0, 0, 0, offsetMilliseconds, default);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
