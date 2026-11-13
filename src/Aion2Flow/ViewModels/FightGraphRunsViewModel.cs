using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Layout;
using Avalonia.Threading;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cloris.Aion2Flow.ViewModels;

// A dungeon to pick in the runs tabs by its map (0: a scene the overlay didn't know); no map: all of them.
public sealed record FightRunDungeon(uint? MapId, string Name)
{
    public override string ToString() => Name;
}

// A metric's cell of a run: its value, marked where the run is the character's best of the boss.
public sealed record FightRunCell(string Text, bool IsBest);

// A column head of the runs tables: a click sorts by it, the best first; another click the other way round.
public sealed partial class FightRunColumn(FightGraphRunsViewModel owner, string key, double width, HorizontalAlignment alignment) : ObservableObject
{
    public string Key { get; } = key;

    [RelayCommand]
    private void Sort() => owner.SortBy(Key);

    public double Width { get; } = width;

    public HorizontalAlignment Alignment { get; } = alignment;

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    // The hover text: what it is (as the meter's column says), an addition that stands out (a cleric's UHS), then how the
    // runs have it and the sorting.
    [ObservableProperty]
    public partial string? Tip { get; set; }

    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial string? Hint { get; set; }
}

// A run in a table: which boss, when, how long, its values per metric, and what can be done with it.
public sealed partial class FightRunRow(FightGraphRunsViewModel owner, BestRun run, string boss, IReadOnlyList<FightRunCell> cells, bool highlight) : ObservableObject
{
    public BestRun Run { get; } = run;

    public string Boss { get; } = boss;

    public string When { get; } = run.At.ToLocalTime().ToString("yy-MM-dd HH:mm", CultureInfo.CurrentCulture);

    public string Time { get; } = TimeSpan.FromMilliseconds(run.Milliseconds).ToString(@"m\:ss", CultureInfo.CurrentCulture);

    public IReadOnlyList<FightRunCell> Cells { get; } = cells;

    // In the stored runs, a best stands out from the user's own.
    public bool Highlight { get; } = highlight;

    // Only what the user stored goes in the trash; a best stays while it's one.
    public bool CanDelete => Run.Custom;

    [ObservableProperty]
    public partial bool IsDeleteArmed { get; set; }

    [RelayCommand]
    private void Delete() => owner.Delete(this);

    [RelayCommand]
    private Task Open() => owner.OpenAsync(this);
}

// The fight graphs' Best runs and Stored runs tabs: the character's best pulls of each boss (a column per metric, the ones
// a run is the best in marked) and the pulls they stored, with the bests among them in another colour.
public sealed partial class FightGraphRunsViewModel : ObservableObject, IDisposable
{
    private const string BossKey = "Boss";
    private const string WhenKey = "When";
    private const string TimeKey = "Time";
    private static readonly FightMetric[] MetricColumns = [FightMetric.Damage, FightMetric.Healing, FightMetric.DamageTaken, FightMetric.UnhealedHealth, FightMetric.Aggro, FightMetric.Stagger];

    private readonly BestRunStore _store;
    private readonly SettingsFlyoutViewModel _settings;
    private readonly GameResourceService _resources;
    private readonly Func<(int ServerId, string Name)?> _character;
    private readonly Action<BestRun, ArchivedEncounterRecord> _show;
    private string _sortKey = WhenKey;
    private bool _bestFirst = true;
    private bool _refreshing;

    internal FightGraphRunsViewModel(BestRunStore store, SettingsFlyoutViewModel settings, GameResourceService resources, Func<(int ServerId, string Name)?> character,
        Action<BestRun, ArchivedEncounterRecord> show)
    {
        _store = store;
        _settings = settings;
        _resources = resources;
        _character = character;
        _show = show;
        Columns.Add(new FightRunColumn(this, BossKey, 140, HorizontalAlignment.Left));
        Columns.Add(new FightRunColumn(this, WhenKey, 92, HorizontalAlignment.Left));
        Columns.Add(new FightRunColumn(this, TimeKey, 40, HorizontalAlignment.Right));
        foreach (var metric in MetricColumns)
            Columns.Add(new FightRunColumn(this, metric.ToString(), 58, HorizontalAlignment.Right));
        _store.Changed += OnStoreChanged;
    }

    public ObservableCollection<FightRunDungeon> Dungeons { get; } = [];

    [ObservableProperty]
    public partial FightRunDungeon? SelectedDungeon { get; set; }

    public ObservableCollection<FightRunColumn> Columns { get; } = [];

    public ObservableCollection<FightRunRow> BestRows { get; } = [];

    public ObservableCollection<FightRunRow> StoredRows { get; } = [];

    [ObservableProperty]
    public partial string? BestEmptyText { get; set; }

    [ObservableProperty]
    public partial string? StoredEmptyText { get; set; }

    // Why the last run couldn't be opened, if it couldn't.
    [ObservableProperty]
    public partial string? Status { get; set; }

    // A click on a column head: by it, the best first (or the newest, the fastest, the boss's name), again the other way.
    internal void SortBy(string key)
    {
        _bestFirst = key != _sortKey || !_bestFirst;
        _sortKey = key;
        Refresh();
    }

    partial void OnSelectedDungeonChanged(FightRunDungeon? value)
    {
        if (!_refreshing)
            Refresh();
    }

    // Everything anew from the store: the character's runs, the dungeons they're in, the two tables.
    public void Refresh()
    {
        var localization = _settings.Localization;
        var includeOverhealing = _settings.IncludeOverhealing;
        var character = _character();
        var runs = _store.Runs
            .Where(run => character is not { } known || run.ServerId == known.ServerId && string.Equals(run.Character, known.Name, StringComparison.Ordinal))
            .ToArray();

        _refreshing = true;
        try
        {
            var picked = SelectedDungeon?.MapId;
            Dungeons.Clear();
            Dungeons.Add(new FightRunDungeon(null, localization["FightGraphs_AllDungeons"]));
            foreach (var mapId in runs.Select(static run => run.MapId).Distinct().Order())
                Dungeons.Add(new FightRunDungeon(mapId, DungeonName(mapId)));
            SelectedDungeon = Dungeons.FirstOrDefault(dungeon => dungeon.MapId == picked) ?? Dungeons[0];
        }
        finally
        {
            _refreshing = false;
        }

        var rows = runs
            .Where(run => SelectedDungeon?.MapId is not { } mapId || run.MapId == mapId)
            .Select(run =>
            {
                var bestKeys = _store.BestKeysOf(run);
                return (Run: run, Best: bestKeys.Intersect(KeysOf(run, includeOverhealing)).ToHashSet());
            })
            .ToArray();
        var sorted = Sorted(rows, includeOverhealing);

        Fill(BestRows, sorted.Where(static row => row.Best.Count > 0), highlight: false, includeOverhealing);
        Fill(StoredRows, sorted.Where(static row => row.Run.Custom || row.Best.Count > 0), highlight: true, includeOverhealing);
        BestEmptyText = BestRows.Count == 0 ? localization["FightGraphs_NoBestRuns"] : null;
        StoredEmptyText = StoredRows.Count == 0 ? localization["FightGraphs_NoStoredRuns"] : null;

        // A cleric's runs go by the group's missing HP: "UHS*", the note standing out in its hover text.
        var cleric = runs.Any(static run => BestRunKeys.UsesGroupUnhealedHealth(run.Class));
        foreach (var column in Columns)
        {
            var group = cleric && column.Key == nameof(FightMetric.UnhealedHealth);
            column.Text = HeaderOf(column.Key) + (group ? "*" : string.Empty) + (column.Key == _sortKey ? (_bestFirst ? " ▾" : " ▴") : string.Empty);
            var isMetric = column.Key is not (BossKey or WhenKey or TimeKey);
            column.Tip = isMetric ? localization[FightGraphsViewModel.ShortKeyOf(Enum.Parse<FightMetric>(column.Key)) + "Tip"] : null;
            column.Note = group ? localization["FightGraphs_ClericUhsNote"] : null;
            column.Hint = isMetric
                ? localization["FightGraphs_RunValuesNote"] + Environment.NewLine + localization["FightGraphs_SortTip"]
                : localization["FightGraphs_SortTip"];
        }
    }

    // A run's value keys per metric column: healing as the meter shows it, a cleric's missing HP the group's.
    private static string[] KeysOf(BestRun run, bool includeOverhealing) =>
        [.. MetricColumns.Select(metric => BestRunKeys.Of(metric, includeOverhealing, run.Class))];

    public void Dispose() => _store.Changed -= OnStoreChanged;

    // The trash: asks once, then the run leaves the stored ones (a best stays a best).
    internal void Delete(FightRunRow row)
    {
        if (!row.IsDeleteArmed)
        {
            foreach (var other in StoredRows)
                other.IsDeleteArmed = false;
            row.IsDeleteArmed = true;
            return;
        }

        _store.Remove(row.Run);
    }

    // The run's history entry in the meter, its pull in the fight graphs.
    internal async Task OpenAsync(FightRunRow row)
    {
        Status = null;
        try
        {
            await using var stream = File.OpenRead(_store.FileOf(row.Run));
            var records = await SceneArchiveFile.ReadAsync(stream);
            if (records.Count > 0)
                _show(row.Run, records[0]);
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Opening the run '{_store.FileOf(row.Run)}' failed: {ex}");
            Status = _settings.Localization["FightGraphs_OpenFailed"];
        }
    }

    private void OnStoreChanged() => Dispatcher.UIThread.Post(Refresh);

    private string DungeonName(uint mapId) =>
        _resources.ResolveMapName(mapId) is { Length: > 0 } name ? name : _settings.Localization["Scene_Unknown"];

    private string BossName(BestRun run) =>
        _resources.ResolveNpcName(run.NpcCode) is { Length: > 0 } name && !name.StartsWith("NPC-", StringComparison.Ordinal) ? name : run.Boss;

    private string HeaderOf(string key) => key switch
    {
        BossKey => _settings.Localization["FightGraphs_ColumnBoss"],
        WhenKey => _settings.Localization["FightGraphs_ColumnWhen"],
        TimeKey => _settings.Localization["FightGraphs_ColumnTime"],
        _ => _settings.Localization[FightGraphsViewModel.ShortKeyOf(Enum.Parse<FightMetric>(key))],
    };

    // By the picked column: a metric's best first (more or less, as it counts for the character), the newest, the
    // fastest, the boss's name; the other way round after a second click.
    private (BestRun Run, HashSet<string> Best)[] Sorted((BestRun Run, HashSet<string> Best)[] rows, bool includeOverhealing)
    {
        IOrderedEnumerable<(BestRun Run, HashSet<string> Best)> ordered = _sortKey switch
        {
            BossKey => rows.OrderBy(row => BossName(row.Run), StringComparer.CurrentCulture),
            TimeKey => rows.OrderBy(static row => row.Run.Milliseconds),
            WhenKey => rows.OrderByDescending(static row => row.Run.At),
            _ => MetricOrder(rows, Enum.Parse<FightMetric>(_sortKey), includeOverhealing),
        };
        var sorted = ordered.ThenByDescending(static row => row.Run.At).ToArray();
        if (!_bestFirst)
            Array.Reverse(sorted);
        return sorted;
    }

    private static IOrderedEnumerable<(BestRun Run, HashSet<string> Best)> MetricOrder((BestRun Run, HashSet<string> Best)[] rows, FightMetric metric, bool includeOverhealing) =>
        rows.OrderByDescending(row =>
        {
            var key = BestRunKeys.Of(metric, includeOverhealing, row.Run.Class);
            var value = row.Run.Values.GetValueOrDefault(key);
            return BestRunKeys.HigherIsBetter(key, row.Run.Class) ? value : -value;
        });

    private void Fill(ObservableCollection<FightRunRow> target, IEnumerable<(BestRun Run, HashSet<string> Best)> rows, bool highlight, bool includeOverhealing)
    {
        target.Clear();
        foreach (var (run, best) in rows)
        {
            var cells = KeysOf(run, includeOverhealing)
                .Select(key => new FightRunCell(run.Values.TryGetValue(key, out var value) ? Format(key, value) : "–", best.Contains(key)))
                .ToArray();
            target.Add(new FightRunRow(this, run, BossName(run), cells, highlight && best.Count > 0));
        }
    }

    // As the store has them: per second, missing HP on average, aggro as the share of the pull.
    private static string Format(string key, double value) => key switch
    {
        nameof(FightMetric.Aggro) => (value * 100).ToString("0", CultureInfo.CurrentCulture) + "%",
        nameof(FightMetric.Stagger) => value.ToString("0.0", CultureInfo.CurrentCulture),
        _ => FightGraph.Compact(value),
    };
}
