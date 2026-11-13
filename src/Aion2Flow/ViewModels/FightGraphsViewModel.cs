using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Logging;
using Cloris.Aion2Flow.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cloris.Aion2Flow.ViewModels;

// The boss fights of the encounter the meter shows (live, or a history entry), with what it takes to work one out second
// by second off the UI thread: a player's metric events in a stretch of it, and its journal.
internal sealed record FightGraphSource(
    Guid EncounterId,
    DateTimeOffset SceneStarted,
    uint MapId,
    IReadOnlyList<SceneBossFight> Fights,
    IReadOnlyList<string> Titles,
    Func<int, long, long, IReadOnlyList<CombatMetricDetailEvent>> EventsOf,
    Func<SceneJournalSegment> Journal,
    // The pull's history entry, to store it with: the live scene so far, or the history entry shown.
    Func<ArchivedEncounterRecord> HistoryEntry,
    SceneDisplayContext? Context);

// A legend entry: a curve (or in the overview a metric) to show or hide.
public sealed partial class FightGraphLegendItem(string key, Color color, bool isArea, bool isShown, Action<FightGraphLegendItem> changed, bool isDotted = false) : ObservableObject
{
    public string Key { get; } = key;

    public IBrush Brush { get; } = new ImmutableSolidColorBrush(color);

    public Color Color { get; } = color;

    // A filled block for the user's area, a stroke for a line, dashes for the best pull.
    public double SwatchHeight { get; } = isArea ? 9 : 3;

    public bool IsDotted { get; } = isDotted;

    [ObservableProperty]
    public partial string Label { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Tip { get; set; }

    // An addition to the hover text that stands out (a cleric's UHS*).
    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial bool IsShown { get; set; } = isShown;

    partial void OnIsShownChanged(bool value) => changed(this);

    [RelayCommand]
    private void Toggle() => IsShown = !IsShown;
}

// The frame's tabs: the pull shown, the user's best runs, the runs they stored, the comparison of ticked ones.
public enum FightGraphsTab
{
    Fight,
    Best,
    Stored,
    Compare,
}

// A metric's section in the comparison: a graph per compared run, one below the other. Its title and folding are the Fight
// tab's, so a fold folds it there too.
public sealed class FightCompareMetric(FightGraphViewModel graph, IReadOnlyList<FightCompareGraph> runs)
{
    public FightGraphViewModel Graph { get; } = graph;

    public IReadOnlyList<FightCompareGraph> Runs { get; } = runs;
}

// A compared run's graph of a metric, under the run's heading (boss, when, how long).
public sealed partial class FightCompareGraph : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial FightGraphModel? Model { get; set; }
}

// One metric's graph: its curves over the fight, under a header that folds it away.
public sealed partial class FightGraphViewModel(FightMetric metric, bool isFolded, Action<FightGraphViewModel> folded) : ObservableObject
{
    public FightMetric Metric { get; } = metric;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFolded { get; set; } = isFolded;

    [ObservableProperty]
    public partial FightGraphModel? Model { get; set; }

    partial void OnIsFoldedChanged(bool value) => folded(this);

    [RelayCommand]
    private void ToggleFold() => IsFolded = !IsFolded;
}

// The fight graphs frame: one boss fight (pull) of what the meter shows, second by second. On top the overview, the user's
// own metrics each scaled to its peak, metrics picked in its legend; below a graph per metric with the user's area, the
// party's total, each member's curve if wanted, the ping on its own scale, and grave crosses where someone died.
public sealed partial class FightGraphsViewModel : ObservableObject, IDisposable
{
    internal const string YouKey = "You";
    internal const string PartyKey = "Party";
    internal const string PingKey = "Ping";
    internal const string BestKey = "Best";

    // Validated against the frame's dark surface (colour-blind safe as neighbours): the metrics in the overview, the user
    // and the members in the metric graphs; the party and the ping stay neutral. In the overview healing is green and the
    // damage taken red, as they read; the other metrics as the order lets them stand apart.
    private static readonly (FightMetric Metric, string Short, string Title, Color Color)[] Metrics =
    [
        (FightMetric.Damage, "ColumnHeader_Dps", "FightGraphs_Damage", Color.Parse("#3987E5")),
        (FightMetric.Healing, "ColumnHeader_Hps", "FightGraphs_Healing", Color.Parse("#008300")),
        (FightMetric.DamageTaken, "ColumnHeader_DamageTaken", "FightGraphs_DamageTaken", Color.Parse("#E66767")),
        (FightMetric.UnhealedHealth, "ColumnHeader_UnhealedHealth", "FightGraphs_UnhealedHealth", Color.Parse("#9085E9")),
        (FightMetric.Aggro, "ColumnHeader_Aggro", "FightGraphs_Aggro", Color.Parse("#199E70")),
        (FightMetric.Stagger, "ColumnHeader_Stagger", "FightGraphs_Stagger", Color.Parse("#C98500")),
    ];

    private static readonly Color YouColor = Color.Parse("#1FA093");
    private static readonly Color PartyColor = Color.Parse("#F0EFEC");
    private static readonly Color PingColor = Color.Parse("#898781");
    private static readonly Color[] MemberColors = [.. ((string[])["#3987E5", "#D95926", "#9085E9", "#C98500", "#D55181", "#008300"]).Select(Color.Parse)];
    private static readonly Color MyDeathColor = Color.Parse("#F07C82");
    // The overview's red is the damage taken, so the user's deaths are white there (no other white line in it).
    private static readonly Color OverviewDeathColor = Color.Parse("#F0EFEC");
    private static readonly Color OtherDeathColor = Color.Parse("#B0C1D0");
    private const long RefreshMilliseconds = 1_000;

    private readonly MainViewModel _main;
    private readonly PlayerNameDisplayService _names;
    private readonly SettingsService _settingsService;
    private readonly BestRunRecorder? _bestRuns;
    private CancellationTokenSource? _building;
    private (Guid Encounter, int Fight) _buildingFight;
    private object? _builtKey;
    private FightData? _data;
    // The user's best pull of the shown pull's boss by the picked metric; null while it's the shown pull itself.
    private BestRun? _best;
    // The pull shown, for "Store run".
    private (FightGraphSource Source, int Index, SceneBossFight Fight, int Me)? _shown;
    // A stored run opened from the runs tabs: its pull is picked once the meter shows its encounter.
    private (Guid Encounter, int Fight)? _pendingFight;
    private Guid _encounterId;
    private long _nextRefreshAt;
    private bool _syncingFights;
    // The comparison: the runs ticked, each worked out; members in a colour of their own by name across the runs.
    private const string CompareMemberPrefix = "name:";
    private readonly List<(BestRun Run, FightData Data)> _compared = [];
    private readonly Dictionary<string, Color> _compareColors = new(StringComparer.Ordinal);
    private IReadOnlyList<BestRun> _comparedRuns = [];
    private CancellationTokenSource? _comparing;

    internal FightGraphsViewModel(MainViewModel main, SettingsFlyoutViewModel settings, PlayerNameDisplayService names, SettingsService settingsService,
        BestRunRecorder? bestRuns = null, GameResourceService? resources = null)
    {
        _main = main;
        _names = names;
        _settingsService = settingsService;
        _bestRuns = bestRuns;
        if (bestRuns is not null && resources is not null)
            Runs = new FightGraphRunsViewModel(bestRuns.Store, settings, resources, () => main.LocalCharacter, ShowRun, runs => CompareAsync(runs));
        Settings = settings;
        var persisted = settingsService.Current;
        foreach (var (metric, _, _, color) in Metrics)
        {
            OverviewLegend.Add(new FightGraphLegendItem(metric.ToString(), color, false, !persisted.FightGraphsOverviewHidden.Contains(metric.ToString()), OverviewLegendChanged));
            Graphs.Add(new FightGraphViewModel(metric, persisted.FightGraphsFolded.Contains(metric.ToString()), GraphFolded));
        }

        foreach (var (key, color, isArea) in (ReadOnlySpan<(string, Color, bool)>)[(YouKey, YouColor, true), (PartyKey, PartyColor, false), (PingKey, PingColor, false)])
            Legend.Add(new FightGraphLegendItem(key, color, isArea, !persisted.FightGraphsHiddenCurves.Contains(key), LegendChanged));
        // The comparison's legend shares these with the Fight tab's.
        foreach (var item in Legend)
            CompareLegend.Add(item);
        CompareEqualTime = persisted.FightGraphsCompareEqualTime;

        ApplyLanguage();
        settings.PropertyChanged += OnSettingsChanged;
        settings.Localization.LanguageChanged += OnLanguageChanged;
    }

    public SettingsFlyoutViewModel Settings { get; }

    // The pulls of the shown encounter by title; the newest is followed while it's the one picked.
    public ObservableCollection<string> Fights { get; } = [];

    [ObservableProperty]
    public partial int SelectedFightIndex { get; set; } = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStoreShown))]
    public partial bool HasFight { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    // What the shown pull beat the user's bests in, when it ended (see BestRunRecorder).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBanner))]
    public partial string? BannerText { get; set; }

    public bool HasBanner => BannerText is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFightTab), nameof(IsBestTab), nameof(IsStoredTab), nameof(IsCompareTab), nameof(IsRunsTab), nameof(IsStoreShown))]
    public partial FightGraphsTab Tab { get; set; }

    // "Store run" with the pull shown, on its tab.
    public bool IsStoreShown => HasRuns && HasFight && Tab == FightGraphsTab.Fight;

    public bool IsFightTab { get => Tab == FightGraphsTab.Fight; set => ShowTab(FightGraphsTab.Fight, value); }

    public bool IsBestTab { get => Tab == FightGraphsTab.Best; set => ShowTab(FightGraphsTab.Best, value); }

    public bool IsStoredTab { get => Tab == FightGraphsTab.Stored; set => ShowTab(FightGraphsTab.Stored, value); }

    public bool IsCompareTab { get => Tab == FightGraphsTab.Compare; set => ShowTab(FightGraphsTab.Compare, value); }

    public bool IsRunsTab => Tab is FightGraphsTab.Best or FightGraphsTab.Stored;

    // The comparison (Compare tab, there once runs were compared): a section per metric with the ticked runs' graphs one
    // below the other; a legend of its own sharing You, Party and Ping with the Fight tab's, the members by name.
    [ObservableProperty]
    public partial bool HasComparison { get; set; }

    public ObservableCollection<FightCompareMetric> CompareMetrics { get; } = [];

    public ObservableCollection<FightGraphLegendItem> CompareLegend { get; } = [];

    // The runs being read and worked out, or why one couldn't be.
    [ObservableProperty]
    public partial string? CompareStatus { get; set; }

    // On: a second as wide in every graph, the longest run filling the width; off: each run fills the width.
    [ObservableProperty]
    public partial bool CompareEqualTime { get; set; }

    partial void OnCompareEqualTimeChanged(bool value)
    {
        if (_settingsService.Current.FightGraphsCompareEqualTime != value)
            Persist(settings => settings.FightGraphsCompareEqualTime = value);
        RebuildCompareModels();
    }

    // The Best runs and Stored runs tabs, with the store; none without one (tests).
    public FightGraphRunsViewModel? Runs { get; }

    public bool HasRuns => Runs is not null;

    // "Store run" for the pull shown: stored already, or being stored right now.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StoreText), nameof(CanStoreRun))]
    public partial bool IsPullStored { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStoreRun))]
    public partial bool IsStoring { get; set; }

    public bool CanStoreRun => HasRuns && !IsPullStored && !IsStoring;

    public string StoreText => Settings.Localization[IsPullStored ? "FightGraphs_Stored" : "FightGraphs_Store"];

    // Why the pull couldn't be stored, if it couldn't.
    [ObservableProperty]
    public partial string? StoreStatus { get; set; }

    // Clicking a checked tab again leaves it checked.
    private void ShowTab(FightGraphsTab tab, bool show, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (show)
            Tab = tab;
        else if (Tab == tab)
            Dispatcher.UIThread.Post(() => OnPropertyChanged(property));
    }

    partial void OnTabChanged(FightGraphsTab value)
    {
        if (value is FightGraphsTab.Best or FightGraphsTab.Stored)
            Runs?.Refresh();
    }

    // "Compare" in the Stored runs tab: each run's history entry read and its pull worked out off the UI thread, then the
    // Compare tab shows them (show: false works them out anew where they are, as with another healing setting).
    private async Task CompareAsync(IReadOnlyList<BestRun> runs, bool show = true)
    {
        if (Runs is not { } store)
            return;

        _comparing?.Cancel();
        var cancellation = _comparing = new CancellationTokenSource();
        _comparedRuns = runs;
        HasComparison = true;
        if (show)
            Tab = FightGraphsTab.Compare;
        CompareStatus = Settings.Localization["FightGraphs_CompareLoading"];
        var includeOverhealing = Settings.IncludeOverhealing;
        var built = new List<(BestRun Run, FightData Data)>();
        var failed = false;
        foreach (var run in runs)
        {
            var record = await store.RecordOfAsync(run);
            if (cancellation.IsCancellationRequested)
                return;

            var source = record is null ? null : _main.CreateFightGraphSource(record);
            if (source is null || run.FightIndex >= source.Fights.Count)
            {
                failed = true;
                continue;
            }

            var fight = source.Fights[run.FightIndex];
            var players = BestRunRecorder.PlayersOf(fight);
            var me = CharacterOf(source.Context, players, run);
            var (from, to) = (fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime);
            var journal = source.Journal();
            try
            {
                var timeline = await Task.Run(() => FightTimeline.Build(from, to, fight.BossId, players, id => source.EventsOf(id, from, to), journal, me,
                    includeOverhealing, cancellation.Token), cancellation.Token);
                built.Add((run, new FightData(timeline, players, me, source.Context)));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                AppLog.Write(AppLogLevel.Warning, $"Comparing the run {run.Id} failed: {ex}");
                failed = true;
            }
        }

        if (!ReferenceEquals(_comparing, cancellation))
            return;

        CompareStatus = failed ? Settings.Localization["FightGraphs_OpenFailed"] : null;
        _compared.Clear();
        _compared.AddRange(built);
        _compareColors.Clear();
        CompareMetrics.Clear();
        foreach (var graph in Graphs)
            CompareMetrics.Add(new FightCompareMetric(graph, [.. built.Select(run => new FightCompareGraph { Title = store.TitleOf(run.Run) })]));
        RebuildCompareModels();
    }

    // The run's character among the pull's players, by name and server as stored; else whoever the entry says was the user.
    private static int CharacterOf(SceneDisplayContext? context, int[] players, BestRun run)
    {
        foreach (var id in players)
        {
            if (context is not null && context.TryResolvePcMetadata(id, out var pc) && pc.OriginServerId == run.ServerId &&
                string.Equals(pc.Nickname, run.Character, StringComparison.Ordinal))
            {
                return id;
            }
        }

        return players.FirstOrDefault(id => context?.IsLocalPlayer(id) == true);
    }

    // "Store run": the pull shown, with its history entry, goes into the Stored runs tab (and counts as a best when it's a
    // complete kill that beats one).
    [RelayCommand]
    private async Task StoreRun()
    {
        if (_bestRuns is null || _shown is not var (source, index, fight, me))
            return;

        var context = source.Context;
        if (me <= 0 || context is null || !context.TryResolvePcMetadata(me, out var pc) || pc.OriginServerId is not > 0 || !pc.HasNickname ||
            !context.TryResolveNpcCode(fight.BossId, out var npcCode))
        {
            StoreStatus = Settings.Localization["FightGraphs_StoreFailed"];
            return;
        }

        var run = new BestRun
        {
            ServerId = pc.OriginServerId.Value,
            Character = pc.Nickname,
            Class = fight.Snapshot.Combatants.TryGetValue(me, out var mine) ? mine.CharacterClass ?? pc.CharacterClass : pc.CharacterClass,
            NpcCode = npcCode,
            Boss = context.ResolveNpcCodeName(npcCode),
            MapId = source.MapId,
            At = source.SceneStarted.AddMilliseconds(fight.Snapshot.EncounterStartTime).ToLocalTime(),
            Milliseconds = fight.Snapshot.EncounterTime,
            EncounterId = source.EncounterId,
            FightIndex = index,
            Complete = BestRunRecorder.IsComplete(fight),
        };
        var (from, to) = (fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime);
        var journal = source.Journal();
        StoreStatus = null;
        IsStoring = true;
        try
        {
            // Off the UI thread, the history entry too: a long session's entry takes a moment to write.
            var store = _bestRuns.Store;
            await Task.Run(async () =>
            {
                BestRunRecorder.WorkOut(run, me, BestRunRecorder.PlayersOf(fight), fight, source.EventsOf(me, from, to), journal);
                await store.StoreAsync(run, source.HistoryEntry).ConfigureAwait(false);
            });
            IsPullStored = true;
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Storing the run failed: {ex}");
            StoreStatus = Settings.Localization["FightGraphs_StoreFailed"];
        }
        finally
        {
            IsStoring = false;
        }
    }

    // A run opened from the runs tabs: the meter shows its history entry, this tab its pull.
    private void ShowRun(BestRun run, ArchivedEncounterRecord record)
    {
        _pendingFight = (run.EncounterId, run.FightIndex);
        _main.ShowHistoryEntry(record);
        Tab = FightGraphsTab.Fight;
        Refresh();
    }

    public ObservableCollection<FightGraphLegendItem> OverviewLegend { get; } = [];

    [ObservableProperty]
    public partial FightGraphModel? OverviewModel { get; set; }

    // Shown while wanted and at least one metric is picked: with none it hides itself.
    public bool IsOverviewShown => Settings.FightGraphsShowOverview && OverviewLegend.Any(static item => item.IsShown);

    public string OverviewToggleText => Settings.Localization[IsOverviewShown ? "FightGraphs_HideOverview" : "FightGraphs_ShowOverview"];

    public ObservableCollection<FightGraphViewModel> Graphs { get; } = [];

    // You, the party, the ping, and each member while their curves are wanted.
    public ObservableCollection<FightGraphLegendItem> Legend { get; } = [];

    // The moment the cursor is on, in every graph (-1: none); the window follows the cursor.
    [ObservableProperty]
    public partial int HoverSecond { get; set; } = -1;

    // The overview's button: hides it, or shows it again (with every metric, if none was left).
    [RelayCommand]
    private void ToggleOverview()
    {
        if (IsOverviewShown)
        {
            Settings.FightGraphsShowOverview = false;
            return;
        }

        if (!OverviewLegend.Any(static item => item.IsShown))
        {
            foreach (var item in OverviewLegend)
                item.IsShown = true;
        }

        Settings.FightGraphsShowOverview = true;
    }

    // The frame's clock (Environment.TickCount64 milliseconds): the first frame refreshes, later ones once a second.
    public void RefreshIfDue(long nowMilliseconds)
    {
        if (nowMilliseconds < _nextRefreshAt)
            return;

        _nextRefreshAt = nowMilliseconds + RefreshMilliseconds;
        Refresh();
    }

    // Once a second, and whenever what it shows changes: the pulls of the encounter the meter shows; the picked one worked
    // out anew while it goes on (once, when it's over).
    public void Refresh()
    {
        var source = _main.CreateFightGraphSource();
        SyncFights(source);
        var index = SelectedFightIndex;
        if (index < 0 || index >= source.Fights.Count)
        {
            _building?.Cancel();
            _building = null;
            _data = null;
            _builtKey = null;
            _best = null;
            _shown = null;
            HasFight = false;
            BannerText = null;
            IsPullStored = false;
            StoreStatus = null;
            StatusText = Settings.Localization["FightGraphs_NoFight"];
            OverviewModel = null;
            foreach (var graph in Graphs)
                graph.Model = null;
            return;
        }

        HasFight = true;
        StatusText = string.Empty;
        var fight = source.Fights[index];
        var includeOverhealing = Settings.IncludeOverhealing;
        var context = source.Context;
        var players = BestRunRecorder.PlayersOf(fight);
        // Who the user is can come late (an overlay started mid-session learns it from 0x4a36): the fight is worked out
        // anew once they're known.
        var me = players.FirstOrDefault(id => context?.IsLocalPlayer(id) == true);
        if (_shown is not { } shown || shown.Source.EncounterId != source.EncounterId || shown.Index != index)
            StoreStatus = null;
        _shown = (source, index, fight, me);
        IsPullStored = _bestRuns?.Store.IsStored(source.EncounterId, index) == true;
        BannerText = Banner(_bestRuns?.RecordOf(source.EncounterId, index), includeOverhealing);
        var best = BestOf(source, index, fight, me, context, includeOverhealing);
        if (best?.Id != _best?.Id)
        {
            _best = best;
            RebuildModels();
        }

        // The user's class: a cleric's overview shows the group's missing HP (UHS*).
        var characterClass = me > 0 && fight.Snapshot.Combatants.TryGetValue(me, out var mine) && mine.CharacterClass is { } known
            ? known
            : me > 0 && context is not null && context.TryResolvePcMetadata(me, out var pc) ? pc.CharacterClass : null;
        var key = (source.EncounterId, index, fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime, fight.Outcome, includeOverhealing, me, characterClass);
        if (Equals(key, _builtKey))
            return;

        // A fight still going changes every second: the build under way finishes first, and the next one catches up.
        if (_building is not null)
        {
            if (_buildingFight == (source.EncounterId, index))
                return;

            _building.Cancel();
        }

        var cancellation = _building = new CancellationTokenSource();
        _buildingFight = (source.EncounterId, index);
        var (from, to) = (fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime);
        var journal = source.Journal();
        Task.Run(() => new FightData(
            FightTimeline.Build(from, to, fight.BossId, players, id => source.EventsOf(id, from, to), journal, me, includeOverhealing, cancellation.Token),
            players, me, context, characterClass), cancellation.Token).ContinueWith(task => Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_building, cancellation))
                return;

            _building = null;
            _builtKey = key;
            if (task.IsCompletedSuccessfully)
            {
                _data = task.Result;
                RebuildModels();
            }
            else if (task.Exception is { } exception)
            {
                AppLog.Write(AppLogLevel.Warning, $"Building the fight graphs failed: {exception}");
            }
        }), TaskScheduler.Default);
    }

    public void Dispose()
    {
        _building?.Cancel();
        _comparing?.Cancel();
        Runs?.Dispose();
        Settings.PropertyChanged -= OnSettingsChanged;
        Settings.Localization.LanguageChanged -= OnLanguageChanged;
    }

    partial void OnSelectedFightIndexChanged(int value)
    {
        if (!_syncingFights)
            Refresh();
    }

    // The titles of the shown encounter's pulls. A new encounter starts at its newest pull; a new pull is followed while the
    // newest one was picked.
    private void SyncFights(FightGraphSource source)
    {
        _syncingFights = true;
        try
        {
            var follow = source.EncounterId != _encounterId || SelectedFightIndex < 0 || SelectedFightIndex >= Fights.Count - 1;
            if (source.EncounterId != _encounterId)
            {
                _encounterId = source.EncounterId;
                Fights.Clear();
            }

            var picked = SelectedFightIndex;
            while (Fights.Count > source.Titles.Count)
                Fights.RemoveAt(Fights.Count - 1);
            for (var i = 0; i < source.Titles.Count; i++)
            {
                if (i == Fights.Count)
                    Fights.Add(source.Titles[i]);
                else if (Fights[i] != source.Titles[i])
                    Fights[i] = source.Titles[i];
            }

            SelectedFightIndex = follow || picked >= Fights.Count ? Fights.Count - 1 : picked;
            // A stored run just opened: its pull, once its encounter is the one shown.
            if (_pendingFight is var (encounter, fight) && encounter == source.EncounterId)
            {
                _pendingFight = null;
                if (fight < Fights.Count)
                    SelectedFightIndex = fight;
            }
        }
        finally
        {
            _syncingFights = false;
        }
    }

    // The curves from the worked-out fight, as the legends and settings want them.
    private void RebuildModels()
    {
        if (_data is not { } data)
            return;

        var localization = Settings.Localization;
        var members = data.Players.Where(id => id != data.Me).ToArray();
        SyncMemberLegend(data, members);
        var shown = Legend.Where(static item => item.IsShown).Select(static item => item.Key).ToHashSet();
        var timeline = data.Timeline;
        var (marks, myMarks) = MarksOf(data, member => MemberColor(members, member));
        SyncBestLegend(data);
        foreach (var graph in Graphs)
        {
            var seconds = timeline.Seconds;
            var series = SeriesOf(data, graph.Metric, shown, member => (MemberKey(member), MemberColor(members, member), NameOf(data, member)));

            // The best pull in full, however long: the axis takes the longer of the two. A cleric's missing HP is the
            // group's, next to the party's line.
            var bestKey = _best is { } best ? BestRunKeys.Of(graph.Metric, Settings.IncludeOverhealing, best.Class) : null;
            if (data.Me > 0 && _best is { } shownBest && bestKey is not null && shown.Contains(BestKey) && shownBest.Curves.TryGetValue(bestKey, out var bestCurve))
            {
                var label = localization[bestKey == BestRunKeys.GroupUnhealedHealth ? "FightGraphs_BestGroup" : "FightGraphs_Best"];
                series.Add(new FightGraphSeries(label, bestCurve, YouColor, FightGraphStyle.Dotted, FormatOf(graph.Metric)));
                seconds = Math.Max(seconds, bestCurve.Length);
            }

            graph.Model = new FightGraphModel(seconds, series, marks) { Smooth = Settings.FightGraphsSmoothCurves };
        }

        // A cleric's missing HP is the whole group's, as their runs go by it: "UHS*", the note in its legend entry's hover.
        var groupUhs = BestRunKeys.UsesGroupUnhealedHealth(data.Class);
        var uhs = OverviewLegend.First(static item => item.Key == nameof(FightMetric.UnhealedHealth));
        uhs.Label = localization[ShortKeyOf(FightMetric.UnhealedHealth)] + (groupUhs ? "*" : string.Empty);
        uhs.Note = groupUhs ? localization["FightGraphs_ClericUhsOverviewNote"] : null;
        OverviewModel = data.Me > 0
            ? new FightGraphModel(timeline.Seconds, [.. Metrics
                .Where(definition => OverviewLegend.First(item => item.Key == definition.Metric.ToString()).IsShown)
                .Select(definition => definition.Metric == FightMetric.UnhealedHealth && groupUhs
                    ? new FightGraphSeries(uhs.Label, PartyTotal(timeline, data.Players, (int)definition.Metric), definition.Color, FightGraphStyle.Line, FormatOf(definition.Metric))
                    : new FightGraphSeries(localization[definition.Short], timeline.Values[data.Me][(int)definition.Metric], definition.Color, FightGraphStyle.Line, FormatOf(definition.Metric)))],
                [.. myMarks.Select(static mark => mark with { Color = OverviewDeathColor })])
            {
                Normalized = true,
                Smooth = Settings.FightGraphsSmoothCurves,
            }
            : null;
    }

    // A metric's curves of a worked-out pull, as the legend wants them: the ping on its own scale, the party's total, each
    // member (member gives their legend key, colour and name) and the user's area with the skills it came from.
    private List<FightGraphSeries> SeriesOf(FightData data, FightMetric metric, HashSet<string> shown, Func<int, (string Key, Color Color, string Name)> member)
    {
        var localization = Settings.Localization;
        var timeline = data.Timeline;
        var index = (int)metric;
        var format = FormatOf(metric);
        var series = new List<FightGraphSeries>();
        if (shown.Contains(PingKey) && timeline.Ping.Any(static value => !double.IsNaN(value)))
            series.Add(new FightGraphSeries(localization["FightGraphs_Ping"], timeline.Ping, PingColor, FightGraphStyle.Line, Milliseconds) { RightAxis = true });
        if (shown.Contains(PartyKey))
            series.Add(new FightGraphSeries(localization["FightGraphs_Party"], PartyTotal(timeline, data.Players, index), PartyColor, FightGraphStyle.Line, format));
        if (Settings.FightGraphsShowMembers)
        {
            foreach (var id in data.Players)
            {
                if (id == data.Me)
                    continue;

                var (key, color, name) = member(id);
                if (shown.Contains(key))
                    series.Add(new FightGraphSeries(name, timeline.Values[id][index], color, FightGraphStyle.Line, format));
            }
        }

        if (data.Me > 0 && shown.Contains(YouKey))
        {
            series.Add(new FightGraphSeries(localization["FightGraphs_You"], timeline.Values[data.Me][index], YouColor, FightGraphStyle.Area, format)
            {
                Sources = timeline.Sources.Length > 0 ? NamedSources(data, timeline.Sources[index]) : null
            });
        }

        return series;
    }

    // Who died when: the user always, the others while wanted (in their curve's colour while the members are shown); the
    // user's alone for the overview.
    private (List<FightGraphMark> All, List<FightGraphMark> Mine) MarksOf(FightData data, Func<int, Color> memberColor)
    {
        var localization = Settings.Localization;
        var all = new List<FightGraphMark>();
        var mine = new List<FightGraphMark>();
        foreach (var (player, at) in data.Timeline.Deaths)
        {
            if (player == data.Me)
            {
                var mark = new FightGraphMark(at, MyDeathColor, string.Format(CultureInfo.CurrentCulture, localization["FightGraphs_Died"], localization["FightGraphs_You"]));
                all.Add(mark);
                mine.Add(mark);
            }
            else if (Settings.FightGraphsShowOtherDeaths)
            {
                var color = Settings.FightGraphsShowMembers ? memberColor(player) : OtherDeathColor;
                all.Add(new FightGraphMark(at, color, string.Format(CultureInfo.CurrentCulture, localization["FightGraphs_Died"], NameOf(data, player))));
            }
        }

        return (all, mine);
    }

    // The compared runs' graphs as the legend and the options want them; with an equal time scale every axis is as long as
    // the longest run, so a second is as wide in each.
    private void RebuildCompareModels()
    {
        if (_compared.Count == 0)
            return;

        SyncCompareLegend();
        var shown = CompareLegend.Where(static item => item.IsShown).Select(static item => item.Key).ToHashSet();
        var longest = _compared.Max(static run => run.Data.Timeline.Seconds);
        for (var index = 0; index < _compared.Count; index++)
        {
            var data = _compared[index].Data;
            var seconds = CompareEqualTime ? longest : data.Timeline.Seconds;
            var (marks, _) = MarksOf(data, member => CompareColor(NameOf(data, member)));
            foreach (var section in CompareMetrics)
            {
                var series = SeriesOf(data, section.Graph.Metric, shown, member =>
                {
                    var name = NameOf(data, member);
                    return (CompareMemberPrefix + name, CompareColor(name), name);
                });
                section.Runs[index].Model = new FightGraphModel(seconds, series, marks) { Smooth = Settings.FightGraphsSmoothCurves };
            }
        }
    }

    // A legend entry per member while their curves are wanted, by name across the compared runs (the same people in
    // several runs share one).
    private void SyncCompareLegend()
    {
        string[] wanted = Settings.FightGraphsShowMembers
            ? [.. _compared.SelectMany(run => run.Data.Players.Where(id => id != run.Data.Me).Select(id => NameOf(run.Data, id))).Distinct()]
            : [];
        for (var i = CompareLegend.Count - 1; i >= 0; i--)
        {
            var key = CompareLegend[i].Key;
            if (key.StartsWith(CompareMemberPrefix, StringComparison.Ordinal) && !wanted.Contains(key[CompareMemberPrefix.Length..]))
                CompareLegend.RemoveAt(i);
        }

        foreach (var name in wanted)
        {
            var key = CompareMemberPrefix + name;
            if (!CompareLegend.Any(item => item.Key == key))
                CompareLegend.Add(new FightGraphLegendItem(key, CompareColor(name), false, true, _ => RebuildCompareModels()) { Label = name, Tip = Settings.Localization["FightGraphs_LegendTip"] });
        }
    }

    // A member keeps their colour across the compared runs: by name, in the order they first come up.
    private Color CompareColor(string name)
    {
        if (!_compareColors.TryGetValue(name, out var color))
            _compareColors[name] = color = MemberColors[_compareColors.Count % MemberColors.Length];
        return color;
    }

    // The best pull's legend entry, after the user's, while there's one to draw.
    private void SyncBestLegend(FightData data)
    {
        var item = Legend.FirstOrDefault(static item => item.Key == BestKey);
        if (_best is not { } best || data.Me <= 0)
        {
            if (item is not null)
                Legend.Remove(item);
            return;
        }

        if (item is null)
        {
            item = new FightGraphLegendItem(BestKey, YouColor, false, !_settingsService.Current.FightGraphsHiddenCurves.Contains(BestKey), LegendChanged, isDotted: true);
            Legend.Insert(Math.Min(1, Legend.Count), item);
        }

        var localization = Settings.Localization;
        item.Label = localization["FightGraphs_Best"];
        item.Tip = string.Format(CultureInfo.CurrentCulture, localization["FightGraphs_BestTip"], localization[ShortKeyOf(Settings.FightGraphsBestMetric)],
            best.At.ToString("g", CultureInfo.CurrentCulture), TimeSpan.FromMilliseconds(best.Milliseconds).ToString(@"m\:ss", CultureInfo.CurrentCulture));
    }

    // The user's best pull of this pull's boss by the picked metric (see BestRunStore), unless it's this very pull.
    private BestRun? BestOf(FightGraphSource source, int index, SceneBossFight fight, int me, SceneDisplayContext? context, bool includeOverhealing)
    {
        if (_bestRuns is null || me <= 0 || context is null || !context.TryResolvePcMetadata(me, out var pc) || pc.OriginServerId is not > 0 ||
            !pc.HasNickname || !context.TryResolveNpcCode(fight.BossId, out var npcCode))
        {
            return null;
        }

        var characterClass = fight.Snapshot.Combatants.TryGetValue(me, out var mine) ? mine.CharacterClass ?? pc.CharacterClass : pc.CharacterClass;
        var best = _bestRuns.Store.BestOf(pc.OriginServerId.Value, pc.Nickname, npcCode, BestRunKeys.Of(Settings.FightGraphsBestMetric, includeOverhealing, characterClass));
        return best is not null && best.EncounterId == source.EncounterId && best.FightIndex == index ? null : best;
    }

    // "★ New personal best on Vakron: DPS 8.2K/s (was 7.3K/s) · …" for a pull that beat bests when it ended; the healing the
    // meter doesn't show (with or without overhealing) is left out.
    private string? Banner(BestRecord? record, bool includeOverhealing)
    {
        if (record is null)
            return null;

        var localization = Settings.Localization;
        if (record.IsFirst)
            return string.Format(CultureInfo.CurrentCulture, localization["FightGraphs_FirstBest"], record.Boss);

        var hidden = BestRunKeys.Of(FightMetric.Healing, !includeOverhealing, null);
        var parts = record.Improvements
            .Where(improvement => improvement.Key != hidden && improvement.Previous is not null)
            .Select(improvement =>
            {
                var metric = MetricOf(improvement.Key);
                // A cleric's missing HP is the group's: marked like the runs tables' "UHS*".
                var name = localization[ShortKeyOf(metric)] + (improvement.Key == BestRunKeys.GroupUnhealedHealth ? "*" : string.Empty);
                return string.Format(CultureInfo.CurrentCulture, localization["FightGraphs_BestPart"], name,
                    BestValue(metric, improvement.Value), BestValue(metric, improvement.Previous!.Value));
            })
            .ToArray();
        return parts.Length == 0 ? null : string.Format(CultureInfo.CurrentCulture, localization["FightGraphs_NewBest"], record.Boss, string.Join(" · ", parts));
    }

    // A pull's value as the store has it: per second, missing HP on average, aggro as the share of the pull.
    private static string BestValue(FightMetric metric, double value) => metric switch
    {
        FightMetric.Aggro => Percent(value),
        FightMetric.UnhealedHealth => FightGraph.Compact(value),
        _ => FightGraph.Compact(value) + "/s",
    };

    internal static string ShortKeyOf(FightMetric metric) => Metrics.First(definition => definition.Metric == metric).Short;

    // The metric a best-run key belongs to.
    internal static FightMetric MetricOf(string key) => key switch
    {
        BestRunKeys.EffectiveHealing => FightMetric.Healing,
        BestRunKeys.GroupUnhealedHealth => FightMetric.UnhealedHealth,
        _ => Enum.Parse<FightMetric>(key),
    };

    // A legend entry per member while their curves are wanted, in the colour of their curve.
    private void SyncMemberLegend(FightData data, int[] members)
    {
        var wanted = Settings.FightGraphsShowMembers ? members : [];
        for (var i = Legend.Count - 1; i >= 0; i--)
        {
            if (Legend[i].Key.StartsWith("member:", StringComparison.Ordinal) && !wanted.Any(member => MemberKey(member) == Legend[i].Key))
                Legend.RemoveAt(i);
        }

        foreach (var member in wanted)
        {
            var key = MemberKey(member);
            var item = Legend.FirstOrDefault(item => item.Key == key);
            if (item is null || item.Color != MemberColor(members, member))
            {
                if (item is not null)
                    Legend.Remove(item);
                item = new FightGraphLegendItem(key, MemberColor(members, member), false, true, LegendChanged);
                Legend.Add(item);
            }

            item.Label = NameOf(data, member);
        }
    }

    private string NameOf(FightData data, int player) => _names.FormatPcName(data.Context, player);

    // The skills of the user's values, by the names the details give them (variants of one skill together).
    private (string Skill, double Amount)[][] NamedSources(FightData data, (CombatEventKey Skill, double Amount)[][] perSecond)
    {
        var names = new Dictionary<CombatEventKey, string>();
        var result = new (string, double)[perSecond.Length][];
        for (var second = 0; second < perSecond.Length; second++)
        {
            result[second] = [.. perSecond[second]
                .GroupBy(source =>
                {
                    if (!names.TryGetValue(source.Skill, out var name))
                        names[source.Skill] = name = SkillDetailRowBuilder.ResolveSkillBaseProjection(source.Skill, data.Context, Settings.Localization).DisplayName;
                    return name;
                })
                .Select(static skill => (skill.Key, skill.Sum(static source => source.Amount)))
                .OrderByDescending(static skill => skill.Item2)];
        }

        return result;
    }

    private static double[] PartyTotal(FightTimeline timeline, int[] players, int metric)
    {
        var total = new double[timeline.Seconds];
        foreach (var player in players)
        {
            var values = timeline.Values[player][metric];
            for (var second = 0; second < total.Length; second++)
                total[second] += values[second];
        }

        return total;
    }

    private static string MemberKey(int member) => "member:" + member.ToString(CultureInfo.InvariantCulture);

    // A member keeps their colour while the party stays the same (by id, not by rank).
    private static Color MemberColor(int[] members, int member) => MemberColors[Math.Max(0, Array.IndexOf(members, member)) % MemberColors.Length];

    private static Func<double, string> FormatOf(FightMetric metric) => metric == FightMetric.Aggro ? Percent : FightGraph.Compact;

    private static string Percent(double value) => (value * 100).ToString("0", CultureInfo.CurrentCulture) + "%";

    private static string Milliseconds(double value) => value.ToString("0", CultureInfo.CurrentCulture) + " ms";

    private void OverviewLegendChanged(FightGraphLegendItem item)
    {
        Persist(settings => settings.FightGraphsOverviewHidden = [.. OverviewLegend.Where(static item => !item.IsShown).Select(static item => item.Key)]);
        OnPropertyChanged(nameof(IsOverviewShown));
        OnPropertyChanged(nameof(OverviewToggleText));
        RebuildModels();
    }

    // You, the party and the ping are in both legends.
    private void LegendChanged(FightGraphLegendItem item)
    {
        if (item.Key is YouKey or PartyKey or PingKey or BestKey)
            Persist(settings => settings.FightGraphsHiddenCurves = [.. Legend.Where(static item => !item.IsShown && item.Key is YouKey or PartyKey or PingKey or BestKey).Select(static item => item.Key)]);
        RebuildModels();
        RebuildCompareModels();
    }

    private void GraphFolded(FightGraphViewModel graph) =>
        Persist(settings => settings.FightGraphsFolded = [.. Graphs.Where(static graph => graph.IsFolded).Select(static graph => graph.Metric.ToString())]);

    private void Persist(Action<AppSettings> update) => _settingsService.Update(update);

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsFlyoutViewModel.FightGraphsShowOverview):
                OnPropertyChanged(nameof(IsOverviewShown));
                OnPropertyChanged(nameof(OverviewToggleText));
                break;
            case nameof(SettingsFlyoutViewModel.FightGraphsShowMembers):
            case nameof(SettingsFlyoutViewModel.FightGraphsShowOtherDeaths):
            case nameof(SettingsFlyoutViewModel.FightGraphsSmoothCurves):
                RebuildModels();
                RebuildCompareModels();
                break;
            case nameof(SettingsFlyoutViewModel.IncludeOverhealing):
                Refresh();
                Runs?.Refresh();
                if (HasComparison)
                    _ = CompareAsync(_comparedRuns, show: false);
                break;
            case nameof(SettingsFlyoutViewModel.FightGraphsBestMetric):
                Refresh();
                break;
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLanguage();
        OnPropertyChanged(nameof(StoreText));
        _builtKey = null;
        Refresh();
        Runs?.Refresh();
        if (Runs is { } runs)
        {
            foreach (var section in CompareMetrics)
            {
                for (var index = 0; index < section.Runs.Count; index++)
                    section.Runs[index].Title = runs.TitleOf(_compared[index].Run);
            }
        }

        RebuildCompareModels();
    }

    private void ApplyLanguage()
    {
        var localization = Settings.Localization;
        foreach (var item in OverviewLegend)
        {
            var definition = Metrics.First(definition => definition.Metric.ToString() == item.Key);
            item.Label = localization[definition.Short];
            item.Tip = localization[definition.Title];
        }

        foreach (var graph in Graphs)
            graph.Title = localization[Metrics.First(definition => definition.Metric == graph.Metric).Title];

        foreach (var item in Legend.Union(CompareLegend))
        {
            (item.Label, item.Tip) = item.Key switch
            {
                YouKey => (localization["FightGraphs_You"], localization["FightGraphs_LegendTip"]),
                PartyKey => (localization["FightGraphs_Party"], localization["FightGraphs_PartyTip"]),
                BestKey => (localization["FightGraphs_Best"], item.Tip),
                PingKey => (localization["FightGraphs_Ping"], localization["FightGraphs_LegendTip"]),
                _ => (item.Label, localization["FightGraphs_LegendTip"]),
            };
        }

        OnPropertyChanged(nameof(OverviewToggleText));
    }

    private sealed record FightData(FightTimeline Timeline, int[] Players, int Me, SceneDisplayContext? Context, CharacterClass? Class = null);
}
