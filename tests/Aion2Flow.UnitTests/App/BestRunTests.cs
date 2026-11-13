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
