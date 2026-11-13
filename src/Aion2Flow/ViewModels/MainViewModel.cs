using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Diagnostics;
using Cloris.Aion2Flow.Collections;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.Presentation;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Playback;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.SceneRuntime.Twins;
using Cloris.Aion2Flow.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cloris.Aion2Flow.ViewModels;

public sealed partial class MainViewModel : FrameBatchedObservableObject, IAsyncDisposable
{
    internal static readonly TimeSpan LiveProjectionInterval = TimeSpan.FromMilliseconds(40);
    internal static readonly TimeSpan CaptureIndicatorRefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly BossDamageContribution[] EmptyBossDamageContributions = [];
    private static readonly IBrush BossRemainingHpBrush = new ImmutableSolidColorBrush(Color.FromArgb(0xF0, 0xDF, 0x21, 0x4A));
    private static readonly IBrush BossStaggerBrush = new ImmutableSolidColorBrush(Color.FromArgb(0xF0, 0xE8, 0xB0, 0x4A));

    private const string IndicatorIdleColor = "#6F7A8A";
    private const string IndicatorOkColor = "#6FD38A";
    private const string IndicatorWarnColor = "#F3C969";
    private const string IndicatorErrorColor = "#F07C82";
    private const int GoodLatencyUpperBoundMilliseconds = 100;
    private const int WarningLatencyUpperBoundMilliseconds = 200;
    private const double BarColorMinSaturation = 0.52d;
    private const double BarColorSaturationRange = 0.16d;
    private const double BarColorMinLightness = 0.52d;
    private const double BarColorLightnessRange = 0.12d;
    private const int BarHueGap = 40;
    private const int BarHueRingSize = 360 / BarHueGap;

    private readonly WinDivertCaptureService _captureService;
    private readonly ProcessPortDiscoveryService _processPortDiscoveryService;
    private readonly LanguageService _languageService;
    private readonly GameResourceService _gameResourceService;
    private readonly EncounterArchiveService _encounterArchiveService;
    private readonly CombatantDetailsFlyoutViewModel _combatantDetails;
    private readonly UiFrameBatchService _frameBatchService;

    private SceneCombatSnapshot _latestLiveSnapshot = new();
    private SceneCombatSnapshot _displayedSnapshot = new();
    private SceneReadModelFrame _latestLiveFrame = new();
    private SceneCombatSnapshot? _displayContextSnapshot;
    private ArchivedEncounterRecord? _displayContextArchivedRecord;
    private readonly Dictionary<int, IBrush> _combatantBarBrushes = [];
    private readonly List<BossFocusDisplayGroup> _bossFocusDisplayGroups = [];
    private readonly List<BossFocusDisplayGroup> _bossFocusDisplayCandidates = [];
    private readonly List<SceneBossFocusSnapshot> _frozenBossFocusSnapshots = [];
    private readonly List<BossDamageContribution> _frozenBossDamageContributions = [];
    private readonly List<BossFocusDisplayGroup> _killedBossShareGroups = [];
    private readonly List<SceneBossFocusSnapshot> _archivedBossFocusSnapshots = [];
    private readonly List<BossDamageContribution> _archivedBossDamageContributions = [];
    private Guid _bossFightListEncounterId;
    private int _currentBossFightIndex = -1;
    // A row of a boss fight section opens the detail of that fight (by its index in the encounter) instead of the
    // whole encounter.
    private int _detailFightIndex = -1;
    private (Guid Encounter, long From, long To, int Combatant)? _buffUptimeShown;
    private long _buffUptimeComputedAt;
    private CancellationTokenSource? _buffUptimeCancellation;
    // Digital twins (null in tests without them); the player the details' twin tab shows (server id, name).
    private readonly DigitalTwinService? _digitalTwins;
    private readonly BestRunRecorder? _bestRuns;
    private (int ServerId, string Name)? _twinShown;
    // The specializations the twin tab shows: for which stretch and player (like _buffUptimeShown).
    private (Guid Encounter, long From, long To, int Combatant)? _specializationsShown;
    private long _specializationsComputedAt;
    private CancellationTokenSource? _specializationsCancellation;
    private Guid _detailFightEncounterId;
    private SceneBossFight? _detailFightShown;
    private CombatDetailDelta _detailFightDelta = new();
    private long _detailFightBuiltAt;
    private bool _selectingCombatantRow;
    private int _displayContextVersion;
    private int _displayContextBuiltVersion = -1;
    private long _displayContextMetadataRevision = -1;
    private RuntimeMetadataRegistry? _displayContextMetadataRegistry;
    private bool _displayContextIsArchived;
    private Guid _barBrushEncounterId;
    private uint _barBrushSeed;
    private int _barBrushBaseHue;
    private int _nextBarHueIndex;
    private Guid _frozenBossFocusEncounterId;
    private long _lastLiveProjectionPollTimestampTicks = long.MinValue;
    private long _lastCaptureIndicatorRefreshTimestampTicks = long.MinValue;
    private volatile bool _suppressRefresh;
    private bool _isDisposed;

    public LocalizationService Localization { get; }
    public CombatantDetailsFlyoutViewModel CombatantDetails => _combatantDetails;
    public SettingsFlyoutViewModel SettingsFlyout { get; }
    public CombatantColumnLayoutViewModel CombatantColumns { get; }
    public BossFightViewModel CurrentBossFight { get; }
    public KeyedObservableCollection<int, BossFightViewModel> BossFights { get; } = new(x => x.Key);

    public bool HasBossFights
    {
        get;
        private set => SetFrameProperty(ref field, value);
    }

    public bool IsBossFightListExpanded
    {
        get;
        private set => SetFrameProperty(ref field, value);
    }

    // "Boss fights (3)".
    public string BossFightListTitle
    {
        get;
        private set => SetFrameProperty(ref field, value);
    } = string.Empty;
    public ObservableCollection<BossFocusViewModel> BossFocuses { get; } = [];
    public KeyedObservableCollection<int, CombatantRowViewModel> Combatants { get; } = new(x => x.Id);
    public ObservableCollection<EncounterHistoryItemViewModel> EncounterHistory { get; } = [];

    public SceneDisplayContext? DisplayContext
    {
        get;
        set => SetFrameProperty(ref field, value);
    }

    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    public int RoundTripTimeMilliseconds
    {
        get;
        set => SetFrameProperty(ref field, value);
    }

    public TimeSpan EncounterDuration
    {
        get;
        set => SetFrameProperty(ref field, value);
    }

    public double TotalFilteredDamagePerSecond
    {
        get;
        private set => SetFrameProperty(ref field, value);
    }

    public double TotalUnhealedHealthSeconds
    {
        get;
        private set => SetFrameProperty(ref field, value);
    }

    public Guid NumericStableWidthScopeKey
    {
        get;
        set => SetFrameProperty(ref field, value);
    }

    public uint LiveSceneMapId
    {
        get;
        set => SetFrameProperty(ref field, value);
    }

    public string DriverIndicatorColor
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = IndicatorIdleColor;

    public string GamePortIndicatorColor
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = IndicatorIdleColor;

    public string CaptureLockIndicatorColor
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = IndicatorIdleColor;

    public string LatencyIndicatorColor
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = IndicatorIdleColor;

    public string DriverIndicatorToolTip
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = string.Empty;

    public string GamePortIndicatorToolTip
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = string.Empty;

    public string CaptureLockIndicatorToolTip
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = string.Empty;

    public string LatencyToolTip
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = string.Empty;

    [ObservableProperty]
    public partial bool IsCapturing { get; set; }

    [ObservableProperty]
    public partial CombatantRowViewModel? SelectedCombatant { get; set; }

    [ObservableProperty]
    public partial EncounterHistoryItemViewModel? SelectedEncounterHistory { get; set; }

    // Only the title and the bottom bar show; capture goes on. The window keeps it in the settings.
    [ObservableProperty]
    public partial bool IsMeterFolded { get; set; }

    [ObservableProperty]
    public partial bool IsViewingArchivedEncounter { get; set; }

    public MainViewModel(
        WinDivertCaptureService captureService,
        ProcessPortDiscoveryService processPortDiscoveryService,
        LanguageService languageService,
        GameResourceService gameResourceService,
        EncounterArchiveService encounterArchiveService,
        CombatantDetailsFlyoutViewModel combatantDetails,
        LocalizationService localization,
        SettingsFlyoutViewModel settingsFlyout,
        UiFrameBatchService frameBatchService,
        DigitalTwinService? digitalTwins = null,
        BestRunRecorder? bestRuns = null)
        : base(frameBatchService)
    {
        _digitalTwins = digitalTwins;
        _bestRuns = bestRuns;
        _captureService = captureService;
        _processPortDiscoveryService = processPortDiscoveryService;
        _languageService = languageService;
        _gameResourceService = gameResourceService;
        _encounterArchiveService = encounterArchiveService;
        _combatantDetails = combatantDetails;
        _frameBatchService = frameBatchService;
        Localization = localization;
        SettingsFlyout = settingsFlyout;
        _combatantDetails.EncounterTimeDisplayFormat = SettingsFlyout.EncounterTimeDisplayFormat;
        _combatantDetails.IncludeOverhealing = SettingsFlyout.IncludeOverhealing;
        // Opening the details' Buffs or Digital twin tab works out what it shows (see RefreshBuffUptime,
        // RefreshSeenSpecializations and RefreshDigitalTwin).
        _combatantDetails.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CombatantDetailsFlyoutViewModel.IsBuffsSelected) && _combatantDetails.IsBuffsSelected)
            {
                _buffUptimeShown = null;
                RefreshCombatantDetails();
            }
            else if (e.PropertyName == nameof(CombatantDetailsFlyoutViewModel.IsTwinSelected) && _combatantDetails.IsTwinSelected)
            {
                _specializationsShown = null;
                RefreshCombatantDetails();
            }
        };
        _combatantDetails.TwinLookUpRequested += () =>
        {
            if (_twinShown is { } shown)
                _digitalTwins?.Request(shown.ServerId, shown.Name, again: true);
        };
        if (_digitalTwins is not null)
        {
            _digitalTwins.AutoLookup = SettingsFlyout.LookUpDigitalTwins;
            OwnTwinToolTip = Localization["OwnTwin_Refresh"];
            // The hover texts of the player's skills in the details take their levels from the player's twin.
            CombatantDetails.SkillLevels = CasterSkillLevel;
            _digitalTwins.Changed += (serverId, name) => Dispatcher.UIThread.Post(() =>
            {
                if (_twinShown is { } shown && shown.ServerId == serverId && string.Equals(shown.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
                    RefreshDigitalTwin();
                if (_ownTwinPending is { } own && own.ServerId == serverId && string.Equals(own.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
                    ShowOwnTwinState();
                if (_digitalTwins.Get(serverId, name).State != DigitalTwinState.Ready)
                    return;
                CombatantDetails.RefreshSkillTips();
                // The Buffs tab's hover texts take the casters' levels from their twins.
                if (CombatantDetails.IsBuffsSelected)
                {
                    _buffUptimeShown = default;
                    RefreshCombatantDetails(false);
                }
            });
        }
        if (_captureService.Scene.Kind != SettingsFlyout.SceneKind)
            _captureService.Scene.ChangeKind(SettingsFlyout.SceneKind, _captureService.Scene.SessionStarted, archiveCurrent: false);
        _captureService.Scene.SetCombatantStatisticsScope(SettingsFlyout.CombatantStatisticsScope);
        if (SettingsFlyout.RememberedLocalPlayer is { } localPlayer)
            _captureService.Scene.RememberLocalPlayer(localPlayer.ServerId, localPlayer.Nickname);
        CombatantColumns = new CombatantColumnLayoutViewModel(frameBatchService);
        CurrentBossFight = new BossFightViewModel(frameBatchService, -1, SettingsFlyout.BossSectionExpanded, expanded => SettingsFlyout.BossSectionExpanded = expanded);
        ApplyCombatantMetricDisplaySettings();
        CombatantColumns.SetBossShareColumnVisibility(false);
        DisplayContext = CreateDisplayContext(
            _displayedSnapshot,
            SceneIdentityScope.Empty,
            _captureService.Scene.Owner.MetadataRegistry);
        _combatantDetails.DisplayContext = DisplayContext;

        _captureService.StatusChanged += OnCaptureStatusChanged;
        _captureService.RttResolved += OnRttResolved;
        _processPortDiscoveryService.Discovered += OnProcessPortChanged;
        _processPortDiscoveryService.Removed += OnProcessPortChanged;
        _languageService.LanguageChanged += OnLanguageChanged;
        _gameResourceService.ResourcesChanged += OnResourcesChanged;
        _encounterArchiveService.HistoryChanged += OnEncounterHistoryChanged;
        SettingsFlyout.PropertyChanged += OnSettingsFlyoutPropertyChanged;

        RebuildEncounterHistory();
        ApplyLocalizedUiText();
        RefreshCaptureIndicators();
    }

    public Task InitializeAsync() => StartCaptureAsync();

    private void OnRttResolved(double rtt)
    {
        Dispatcher.UIThread.Post(RefreshCaptureIndicators);
    }

    private void OnProcessPortChanged(uint processId, ushort port)
    {
        Dispatcher.UIThread.Post(RefreshCaptureIndicators);
    }

    private void OnCaptureStatusChanged(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Status = message;
            RefreshCaptureIndicators();
        });
    }

    private void OnLanguageChanged(object? sender, string language)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _displayContextVersion++;
            RebuildEncounterHistory();
            ApplyLocalizedUiText();
            RefreshCaptureIndicators();
            RefreshDisplayedSnapshot(forceDetailRefresh: true);
        });
    }

    private void OnResourcesChanged(object? sender, string language)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _displayContextVersion++;
            RebuildEncounterHistory();
            RefreshDisplayedSnapshot(forceDetailRefresh: true);
        });
    }

    private void OnEncounterHistoryChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RebuildEncounterHistory);
    }

    private void OnSettingsFlyoutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsFlyoutViewModel.CombatantSortMetric) or nameof(SettingsFlyoutViewModel.CombatantSortAscending))
            Dispatcher.UIThread.Post(() => RefreshDisplayedSnapshot());
        else if (e.PropertyName == nameof(SettingsFlyoutViewModel.LookUpDigitalTwins) && _digitalTwins is not null)
        {
            _digitalTwins.AutoLookup = SettingsFlyout.LookUpDigitalTwins;
            Dispatcher.UIThread.Post(() => RefreshDisplayedSnapshot());
        }
        else if (e.PropertyName == nameof(SettingsFlyoutViewModel.IncludeOverhealing))
        {
            _combatantDetails.IncludeOverhealing = SettingsFlyout.IncludeOverhealing;
            Dispatcher.UIThread.Post(() => RefreshDisplayedSnapshot(forceDetailRefresh: true));
        }
        else if (e.PropertyName == nameof(SettingsFlyoutViewModel.SceneKind))
            Dispatcher.UIThread.Post(ChangeSceneKind);
        else if (e.PropertyName == nameof(SettingsFlyoutViewModel.UseCompactMainMetrics) ||
                 e.PropertyName == nameof(SettingsFlyoutViewModel.ShowDamagePerSecondColumn) ||
                 e.PropertyName == nameof(SettingsFlyoutViewModel.ShowDamageColumn) ||
                 e.PropertyName == nameof(SettingsFlyoutViewModel.ShowHealingPerSecondColumn) ||
                 e.PropertyName == nameof(SettingsFlyoutViewModel.ShowHealingColumn) ||
                 e.PropertyName == nameof(SettingsFlyoutViewModel.ShowDamageTakenColumn) ||
                 e.PropertyName == nameof(SettingsFlyoutViewModel.ShowUnhealedHealthColumn) ||
                 e.PropertyName == nameof(SettingsFlyoutViewModel.ShowAggroColumn) ||
                 e.PropertyName == nameof(SettingsFlyoutViewModel.ShowStaggerColumn) ||
                 e.PropertyName == nameof(SettingsFlyoutViewModel.ShowBossShareColumn) ||
                 e.PropertyName == nameof(SettingsFlyoutViewModel.ShowTotalDamagePerSecond))
            ApplyCombatantMetricDisplaySettings();
        else if (e.PropertyName == nameof(SettingsFlyoutViewModel.EncounterTimeDisplayFormat))
            _combatantDetails.EncounterTimeDisplayFormat = SettingsFlyout.EncounterTimeDisplayFormat;
        else if (e.PropertyName == nameof(SettingsFlyoutViewModel.CombatantStatisticsScope))
        {
            ClearBossFocusProjection();
            _captureService.Scene.SetCombatantStatisticsScope(SettingsFlyout.CombatantStatisticsScope);
            Dispatcher.UIThread.Post(() => RefreshDisplayedSnapshot());
        }
        else if (e.PropertyName is nameof(SettingsFlyoutViewModel.ShowFocusStatusBar) or nameof(SettingsFlyoutViewModel.BossTimeLimitMinutes))
            Dispatcher.UIThread.Post(() => RefreshDisplayedSnapshot());
    }

    [RelayCommand]
    private void SelectCombatant(CombatantRowViewModel? combatant)
    {
        SelectedCombatant = combatant;
    }

    private async Task StartCaptureAsync()
    {
        if (IsCapturing) return;
        try
        {
            await _processPortDiscoveryService.StartAsync();
            await _captureService.StartAsync();
            IsCapturing = true;
            RefreshCaptureIndicators();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            RefreshCaptureIndicators();
        }
    }

    private async Task StopCaptureAsync()
    {
        if (!IsCapturing) return;
        await _captureService.StopAsync();
        await _processPortDiscoveryService.StopAsync();
        IsCapturing = false;
        RefreshCaptureIndicators();
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (_suppressRefresh)
        {
            return;
        }

        _suppressRefresh = true;
        try
        {
            CloseCurrentArchiveScope("manual-reset", isAutomatic: true);
            ResetLivePresentation();
            RefreshCaptureIndicators();
        }
        finally
        {
            _suppressRefresh = false;
        }
    }

    // A tapped row of the main table or of a boss fight section; a fight's row opens the detail of that fight. The
    // selection itself stays on the player's row of the main table, which lets go of rows it doesn't hold.
    public void SelectCombatantRow(CombatantRowViewModel row)
    {
        var fightIndex = FindBossFightIndex(row);
        var tableRow = Combatants.TryGetValue(row.Id, out var mainRow) ? mainRow : row;
        if (fightIndex == _detailFightIndex && ReferenceEquals(tableRow, SelectedCombatant))
            return;

        _selectingCombatantRow = true;
        try
        {
            SelectedCombatant = tableRow;
        }
        finally
        {
            _selectingCombatantRow = false;
        }

        _detailFightIndex = fightIndex;
        _detailFightEncounterId = _displayedSnapshot.EncounterId;
        _detailFightShown = null;
        RefreshCombatantDetails(forceRefresh: true);
    }

    partial void OnSelectedCombatantChanged(CombatantRowViewModel? oldValue, CombatantRowViewModel? newValue)
    {
        if (_selectingCombatantRow)
            return;

        // Selected by the table itself (or cleared): the whole encounter.
        _detailFightIndex = -1;
        RefreshCombatantDetails(forceRefresh: true);
    }

    // The encounter's index of the fight whose section holds the row; -1 for a row of the main table.
    private int FindBossFightIndex(CombatantRowViewModel? row)
    {
        if (row is null)
            return -1;

        if (CurrentBossFight.IsVisible && CurrentBossFight.Rows.TryGetValue(row.Id, out var currentRow) && ReferenceEquals(currentRow, row))
            return _currentBossFightIndex;

        foreach (var fight in BossFights)
        {
            if (fight.Rows.TryGetValue(row.Id, out var fightRow) && ReferenceEquals(fightRow, row))
                return fight.Key;
        }

        return -1;
    }

    partial void OnSelectedEncounterHistoryChanged(EncounterHistoryItemViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        IsViewingArchivedEncounter = true;
        _displayedSnapshot = value.Record.ScenePayload.Snapshot;
        ApplySnapshot(_displayedSnapshot);
    }

    [RelayCommand]
    private void ArchiveCurrentEncounter()
    {
        if (_suppressRefresh)
        {
            return;
        }

        _suppressRefresh = true;
        try
        {
            var record = CloseCurrentArchiveScope("manual", isAutomatic: false);
            ResetLivePresentation();
            RefreshCaptureIndicators();
            if (record is null)
            {
                return;
            }

            RebuildEncounterHistory();
            SelectedEncounterHistory = EncounterHistory.FirstOrDefault(x => x.Record.Id == record.Id);
        }
        finally
        {
            _suppressRefresh = false;
        }
    }

    [RelayCommand]
    private void ReturnToLive()
    {
        IsViewingArchivedEncounter = false;
        SelectedEncounterHistory = null;
        ApplySnapshot(_latestLiveSnapshot);
    }

    private void RefreshCombatStats()
    {
        if (_suppressRefresh)
        {
            return;
        }

        DrainPendingBossArchives();
        DrainMapTransitionArchives();
        var selectedCombatantId = IsViewingArchivedEncounter || _detailFightIndex >= 0 ? 0 : SelectedCombatant?.Id ?? 0;
        var nextLiveFrame = CreateLiveFrame(selectedCombatantId);
        DrainPendingBossArchives();

        _latestLiveFrame = nextLiveFrame;
        _latestLiveSnapshot = nextLiveFrame.Snapshot;
        // Kills go to the personal bests, whatever the meter shows meanwhile.
        _bestRuns?.Observe(nextLiveFrame);
        RefreshCaptureIndicators();

        if (ShouldReturnToLiveAfterBoss(nextLiveFrame.Snapshot))
        {
            ReturnToLive();
            return;
        }

        if (IsViewingArchivedEncounter)
        {
            return;
        }

        _displayedSnapshot = _latestLiveSnapshot;
        ApplySnapshot(_displayedSnapshot);
    }

    public void ProcessUiFrame(TimeSpan timestamp)
    {
        if (_isDisposed || !IsCapturing || _suppressRefresh)
            return;

        var timestampTicks = timestamp.Ticks;
        if (_lastLiveProjectionPollTimestampTicks != long.MinValue &&
            timestampTicks >= _lastLiveProjectionPollTimestampTicks &&
            timestampTicks - _lastLiveProjectionPollTimestampTicks < LiveProjectionInterval.Ticks)
        {
            return;
        }

        _lastLiveProjectionPollTimestampTicks = timestampTicks;
        ProcessPendingProjectionChanges();

        if (_lastCaptureIndicatorRefreshTimestampTicks == long.MinValue ||
            timestampTicks < _lastCaptureIndicatorRefreshTimestampTicks ||
            timestampTicks - _lastCaptureIndicatorRefreshTimestampTicks >= CaptureIndicatorRefreshInterval.Ticks)
        {
            _lastCaptureIndicatorRefreshTimestampTicks = timestampTicks;
            RefreshCaptureIndicators();
        }
    }

    internal void RefreshCombatStatsForTesting() => RefreshCombatStats();

    internal void ProcessUiFrameForTesting()
    {
        if (!_isDisposed && IsCapturing && !_suppressRefresh)
            ProcessPendingProjectionChanges();
    }

    internal void ProcessUiFrameForTesting(TimeSpan timestamp) => ProcessUiFrame(timestamp);

    internal void ResetLiveModelsForTesting() => ResetLiveModels(static () => DateTimeOffset.Now);

    private SceneReadModelFrame CreateLiveFrame(int detailCombatantId = 0, bool forceDetailRefresh = false) =>
        detailCombatantId > 0
            ? _captureService.Scene.CreateFrame(detailCombatantId, _combatantDetails, forceDetailRefresh)
            : _captureService.Scene.CreateFrame();

    private void ProcessPendingProjectionChanges()
    {
        if (_captureService.Scene.HasPendingProjectionChanges)
            RefreshCombatStats();
    }

    private void ApplySnapshot(SceneCombatSnapshot snapshot, bool forceDetailRefresh = false)
    {
        UpdateDisplayContext(snapshot);
        NumericStableWidthScopeKey = snapshot.EncounterId;
        EncounterDuration = TimeSpan.FromMilliseconds(snapshot.EncounterTime);
        LiveSceneMapId = snapshot.MapId;
        EnsureBarBrushScope(snapshot.EncounterId);

        using var deferral = Combatants.SuspendNotifications();
        SyncCombatantRows(Combatants, snapshot, onlyTableRows: false);

        if (SelectedCombatant is { } selectedCombatant && !Combatants.ContainsKey(selectedCombatant.Id))
            SelectedCombatant = null;

        // The boss shares first, as the rows can go by them.
        var liveFrame = IsViewingArchivedEncounter ? (SceneReadModelFrame?)null : _latestLiveFrame;
        RefreshBossFocus(liveFrame, snapshot);

        RefreshCombatantBars(Combatants, snapshot.EncounterId);
        Combatants.Sort(CompareCombatantRows);
        RefreshFooterTotals();

        RefreshCombatantDetails(forceDetailRefresh);
        RequestGroupTwins();
        RememberLocalPlayer();
    }

    // The title bar's "refresh my twin": the player looked up again on the official website; the new snapshot goes into
    // the library. Its tooltip follows the lookup, and it lights up while one is under way.
    [ObservableProperty]
    public partial bool IsOwnTwinLoading { get; set; }

    [ObservableProperty]
    public partial string OwnTwinToolTip { get; set; } = "";

    private (int ServerId, string Name)? _ownTwinPending;

    [RelayCommand]
    private void RefreshOwnTwin()
    {
        if (_digitalTwins is null)
            return;

        if ((_latestLiveFrame.MetadataRegistry?.LocalPlayerIdentity ?? SettingsFlyout.RememberedLocalPlayer) is not { } player)
        {
            OwnTwinToolTip = Localization["OwnTwin_Unknown"];
            return;
        }

        _ownTwinPending = (player.Item1, player.Item2);
        _digitalTwins.Request(player.Item1, player.Item2, again: true);
        ShowOwnTwinState();
    }

    private void ShowOwnTwinState()
    {
        if (_ownTwinPending is not { } pending || _digitalTwins is null)
            return;

        var lookup = _digitalTwins.Get(pending.ServerId, pending.Name);
        IsOwnTwinLoading = lookup.State is DigitalTwinState.Queued or DigitalTwinState.Loading;
        OwnTwinToolTip = lookup.State switch
        {
            DigitalTwinState.Queued or DigitalTwinState.Loading => string.Format(Localization["OwnTwin_Looking"], pending.Name),
            DigitalTwinState.Ready => string.Format(Localization["OwnTwin_Added"], lookup.Twin?.Name ?? pending.Name),
            DigitalTwinState.NotFound => Localization["Twin_NotFound"],
            DigitalTwinState.Failed => string.Format(Localization["Twin_Failed"], lookup.Error),
            _ => Localization["OwnTwin_Refresh"]
        };
    }

    // Who the player is, once the game says so, is kept for the next start (see RuntimeMetadataRegistry.RememberLocalPlayerIdentity).
    private void RememberLocalPlayer()
    {
        if (_latestLiveFrame.MetadataRegistry?.LocalPlayerIdentity is not { } localPlayer || SettingsFlyout.RememberedLocalPlayer == localPlayer)
            return;

        SettingsFlyout.RememberLocalPlayer(localPlayer.OriginServerId, localPlayer.Nickname);
        _captureService.Scene.RememberLocalPlayer(localPlayer.OriginServerId, localPlayer.Nickname);
    }

    // With automatic lookups on, the live meter's party and force members (and the player) get their digital twin; each
    // is looked up once (DigitalTwinService.Request skips players it knows).
    private void RequestGroupTwins()
    {
        if (IsViewingArchivedEncounter || _digitalTwins is not { AutoLookup: true } twins || DisplayContext is not { } context)
            return;

        foreach (var row in Combatants)
        {
            if (context.TryResolvePcMetadata(row.Id, out var pc) && pc.HasNickname && pc.OriginServerId is > 0 &&
                (pc.IsLocalPlayer || pc.GroupRelation != PlayerGroupRelation.Unknown))
                twins.Request(pc.OriginServerId.Value, pc.Nickname);
        }
    }

    // The details' Digital twin tab: for a history entry the twin saved with it, else the one looked up closest to the
    // entry's time; live the latest. Without one it says why, with "Look up" where that can help.
    private void RefreshDigitalTwin()
    {
        if (!CombatantDetails.IsTwinSelected || SelectedCombatant is null)
            return;

        if (_digitalTwins is null)
        {
            _twinShown = null;
            CombatantDetails.ShowDigitalTwin(null, Localization["Twin_Unavailable"], canLookUp: false);
            return;
        }

        if (DisplayContext is not { } context || !context.TryResolvePcMetadata(SelectedCombatant.Id, out var pc) || !pc.HasNickname || pc.OriginServerId is not > 0)
        {
            _twinShown = null;
            CombatantDetails.ShowDigitalTwin(null, Localization["Twin_NoIdentity"], canLookUp: false);
            return;
        }

        var (serverId, name) = (pc.OriginServerId.Value, pc.Nickname.Trim());
        _twinShown = (serverId, name);
        var record = IsViewingArchivedEncounter ? SelectedEncounterHistory?.Record : null;
        var lookup = _digitalTwins.Get(serverId, name, record?.ArchivedAt);
        var twin = TwinOf(serverId, name);
        var (status, canLookUp) = lookup.State switch
        {
            DigitalTwinState.Queued or DigitalTwinState.Loading => (Localization["Twin_Loading"], false),
            DigitalTwinState.NotFound => (Localization["Twin_NotFound"], true),
            DigitalTwinState.Unsupported => (Localization["Twin_Unsupported"], false),
            DigitalTwinState.Failed => (string.Format(Localization["Twin_Failed"], lookup.Error), true),
            _ when twin is null => (Localization["Twin_NotLookedUp"], true),
            _ => ((string?)null, true)
        };
        CombatantDetails.ShowDigitalTwin(twin, status, canLookUp);
    }

    // The player's own server as the meter knows it (null before they've shown up), to look others up there.
    public int? LocalPlayerServerId
    {
        get
        {
            if (DisplayContext is not { } context)
                return null;

            foreach (var combatant in _displayedSnapshot.Combatants.AsSpan())
            {
                if (context.TryResolvePcMetadata(combatant.Id, out var pc) && pc.IsLocalPlayer && pc.OriginServerId is > 0)
                    return pc.OriginServerId;
            }

            return null;
        }
    }

    // A player's digital twin for what's shown: a history entry's saved one, else the one looked up closest to the entry's
    // time; live the latest.
    private DigitalTwin? TwinOf(int serverId, string name)
    {
        var record = IsViewingArchivedEncounter ? SelectedEncounterHistory?.Record : null;
        return record?.Twins.FirstOrDefault(t => t.ServerId == serverId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? _digitalTwins?.Get(serverId, name, record?.ArchivedAt).Twin;
    }

    // A player's level of a skill and their name, from their digital twin (see DigitalTwin.SkillLevel), for the Buffs tab.
    private (int Level, string Caster)? CasterSkillLevel(int casterId, int skillCode)
    {
        if (DisplayContext is not { } context || !context.TryResolvePcMetadata(casterId, out var pc) || !pc.HasNickname || pc.OriginServerId is not > 0)
            return null;

        var name = pc.Nickname.Trim();
        return TwinOf(pc.OriginServerId.Value, name)?.SkillLevel(skillCode) is { } level ? (level, name) : null;
    }

    // Exported history entries carry the twins of their players, so they show on another PC or after a restart too.
    public void AttachDigitalTwins(IEnumerable<ArchivedEncounterRecord> records)
    {
        if (_digitalTwins is null)
            return;

        foreach (var record in records)
        {
            var twins = record.Twins.ToList();
            foreach (var entry in record.ScenePayload.IdentityScope.PcMetadataSpan)
            {
                var pc = entry.Metadata;
                if (!pc.HasNickname || pc.OriginServerId is not > 0 ||
                    twins.Exists(t => t.ServerId == pc.OriginServerId && string.Equals(t.Name, pc.Nickname.Trim(), StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (_digitalTwins.Get(pc.OriginServerId.Value, pc.Nickname, record.ArchivedAt).Twin is { } twin)
                    twins.Add(twin);
            }

            record.Twins = twins;
        }
    }

    // Fight sections list the players the table lists (onlyTableRows), with the fight's numbers.
    private void SyncCombatantRows(KeyedObservableCollection<int, CombatantRowViewModel> rows, SceneCombatSnapshot snapshot, bool onlyTableRows)
    {
        var includeOverhealing = SettingsFlyout.IncludeOverhealing;
        using var deferral = rows.SuspendNotifications();
        foreach (var row in deferral.Snapshot)
        {
            if (snapshot.Combatants.TryGetValue(row.Id, out var data) && ShouldDisplayRow(row.Id, data))
                UpdateRow(row, data);
            else
                rows.Remove(row);
        }

        foreach (ref readonly var entry in snapshot.Combatants.AsSpan())
        {
            if (rows.ContainsKey(entry.Id) || !ShouldDisplayRow(entry.Id, entry.Metrics))
                continue;

            var row = new CombatantRowViewModel(_frameBatchService, CombatantColumns, entry.Id, entry.Metrics.CharacterClass, 0, 0, 0, 0, 0, 0);
            UpdateRow(row, entry.Metrics);
            rows.Add(row);
        }

        bool ShouldDisplayRow(int id, SceneCombatantMetrics data) =>
            onlyTableRows ? Combatants.ContainsKey(id) : ShouldDisplayCombatant(id, data);

        void UpdateRow(CombatantRowViewModel row, SceneCombatantMetrics data)
        {
            row.CharacterClass = data.CharacterClass;
            row.DamagePerSecond = data.DamagePerSecond;
            row.HealingPerSecond = includeOverhealing ? data.HealingPerSecond : data.EffectiveHealingPerSecond;
            row.Damage = data.DamageAmount;
            row.Healing = includeOverhealing ? data.HealingAmount : data.EffectiveHealingAmount;
            row.DamageTaken = data.DamageTakenAmount;
            row.UnhealedHealthSeconds = data.UnhealedHealthSeconds;
            row.AggroShare = data.AggroShare;
            row.StaggerShare = data.StaggerShare;
        }
    }

    private void RefreshDisplayedSnapshot(bool forceDetailRefresh = false)
    {
        if (IsViewingArchivedEncounter && SelectedEncounterHistory is not null)
        {
            _displayedSnapshot = SelectedEncounterHistory.Record.ScenePayload.Snapshot;
            ApplySnapshot(_displayedSnapshot, forceDetailRefresh);
            return;
        }

        _displayedSnapshot = _latestLiveSnapshot;
        ApplySnapshot(_displayedSnapshot, forceDetailRefresh);
    }

    private void RefreshBossFocus(SceneReadModelFrame? liveFrame, SceneCombatSnapshot snapshot)
    {
        if (IsViewingArchivedEncounter)
        {
            BossFocuses.Clear();
            if (SelectedEncounterHistory is { Record.ScenePayload: var payload })
            {
                BuildArchivedBossShareProjection(payload);
                BossFocusDisplayBuilder.Build(
                    _archivedBossFocusSnapshots,
                    _archivedBossDamageContributions,
                    DisplayContext,
                    SettingsFlyout.CombatantStatisticsScope,
                    Combatants.ContainsKey,
                    _bossFocusDisplayGroups);
                if (SettingsFlyout.ShowFocusStatusBar)
                    SyncBossFocuses();
                RefreshCombatantBossShares(_bossFocusDisplayGroups, _archivedBossDamageContributions);
                RefreshBossFights(payload.BossFights, snapshot.EncounterId, archived: true);
            }
            else
            {
                _bossFocusDisplayGroups.Clear();
                RefreshCombatantBossShares(_bossFocusDisplayGroups, EmptyBossDamageContributions);
                RefreshBossFights([], snapshot.EncounterId, archived: true);
            }
            return;
        }

        var snapshots = liveFrame?.BossFocuses ?? snapshot.BossFocuses;
        var damageContributions = liveFrame?.BossDamageContributions ?? EmptyBossDamageContributions;
        BossFocusDisplayBuilder.Build(
            snapshots,
            damageContributions,
            DisplayContext,
            SettingsFlyout.CombatantStatisticsScope,
            Combatants.ContainsKey,
            _bossFocusDisplayCandidates);

        // Kept in every scene kind: boss scenes freeze on it, others keep a killed boss's share column (below).
        if (_bossFocusDisplayCandidates.Count > 0)
            CaptureFrozenBossProjection(snapshot.EncounterId, snapshots, damageContributions);

        var useFrozenBossProjection = snapshot.Kind == SceneKind.Boss &&
                                      _captureService.Scene.BossState == BossSceneState.Frozen &&
                                      _bossFocusDisplayCandidates.Count == 0 &&
                                      _frozenBossFocusEncounterId == snapshot.EncounterId &&
                                      _frozenBossFocusSnapshots.Count > 0;
        if (useFrozenBossProjection)
        {
            BossFocusDisplayBuilder.Build(
                _frozenBossFocusSnapshots,
                _frozenBossDamageContributions,
                DisplayContext,
                SettingsFlyout.CombatantStatisticsScope,
                Combatants.ContainsKey,
                _bossFocusDisplayCandidates);
            damageContributions = _frozenBossDamageContributions;
        }

        _bossFocusDisplayGroups.Clear();
        _bossFocusDisplayGroups.AddRange(_bossFocusDisplayCandidates);
        if (SettingsFlyout.ShowFocusStatusBar)
        {
            SyncBossFocuses();
        }
        else
        {
            BossFocuses.Clear();
        }
        if (!TryRefreshKilledBossShares(snapshot.EncounterId))
            RefreshCombatantBossShares(_bossFocusDisplayGroups, damageContributions);
        RefreshBossFights(liveFrame?.BossFights ?? [], snapshot.EncounterId, archived: false);
    }

    // A killed boss leaves the bar once it's out of sight, but its share column stays, as in the history, until another
    // boss comes up or the scene ends. A boss that is merely out of sight still takes the column with it.
    private bool TryRefreshKilledBossShares(Guid encounterId)
    {
        if (_bossFocusDisplayGroups.Count > 0 || _frozenBossFocusEncounterId != encounterId)
            return false;

        BossFocusDisplayBuilder.Build(
            _frozenBossFocusSnapshots,
            _frozenBossDamageContributions,
            DisplayContext,
            SettingsFlyout.CombatantStatisticsScope,
            Combatants.ContainsKey,
            _killedBossShareGroups);
        if (_killedBossShareGroups.Count == 0 || !_killedBossShareGroups.TrueForAll(static group => group.Representative is { HasHp: true, Hp: <= 0 }))
            return false;

        RefreshCombatantBossShares(_killedBossShareGroups, _frozenBossDamageContributions);
        return true;
    }

    // The section under the bar shows the newest fight of a boss in the bar. A wipe stays there, bar or not, until the
    // next pull; every other fight sits folded in the list below the table.
    private void RefreshBossFights(IReadOnlyList<SceneBossFight> fights, Guid encounterId, bool archived)
    {
        var current = -1;
        for (var i = fights.Count - 1; i >= 0 && current < 0 && SettingsFlyout.ShowFocusStatusBar; i--)
        {
            foreach (var group in _bossFocusDisplayGroups)
            {
                if (group.Representative.InstanceId == fights[i].BossId)
                {
                    current = i;
                    break;
                }
            }
        }

        if (current < 0 && !archived && SettingsFlyout.ShowFocusStatusBar && fights.Count > 0 && fights[^1].Outcome == BossFightOutcome.Reset)
            current = fights.Count - 1;

        _currentBossFightIndex = current;
        CurrentBossFight.IsVisible = current >= 0;
        if (current >= 0)
            FillBossFight(CurrentBossFight, fights[current], encounterId, archived);
        else
        {
            CurrentBossFight.Rows.Clear();
            CurrentBossFight.HasDpsCheck = false; // the bottom bar's gap goes with the section
        }

        if (_bossFightListEncounterId != encounterId)
        {
            BossFights.Clear();
            _bossFightListEncounterId = encounterId;
        }

        using (var deferral = BossFights.SuspendNotifications())
        {
            foreach (var fight in deferral.Snapshot)
            {
                if (fight.Key >= fights.Count || fight.Key == current)
                    BossFights.Remove(fight);
            }

            for (var i = 0; i < fights.Count; i++)
            {
                if (i == current)
                    continue;

                if (!BossFights.TryGetValue(i, out var fight))
                {
                    fight = new BossFightViewModel(_frameBatchService, i, isExpanded: false);
                    BossFights.Add(fight);
                }

                FillBossFight(fight, fights[i], encounterId, archived);
            }

            BossFights.Sort(static (left, right) => left.Key.CompareTo(right.Key));
        }

        HasBossFights = BossFights.Count > 0;
        BossFightListTitle = string.Format(Localization["BossFight_List"], BossFights.Count);
    }

    private void FillBossFight(BossFightViewModel view, SceneBossFight fight, Guid encounterId, bool archived)
    {
        var failed = IsFailed(fight, archived);
        view.Title = GetBossFightTitle(fight, archived);
        var hpLeft = failed && fight.BossMaxHp > 0 ? string.Format(Localization["BossFight_HpLeft"], (double)fight.BossHp / fight.BossMaxHp) : null;
        var staggers = fight.StaggerCount > 0 ? string.Format(Localization["BossFight_Staggers"], fight.StaggerCount) : null;
        view.DetailText = hpLeft is null ? staggers ?? string.Empty : staggers is null ? hpLeft : $"{hpLeft} · {staggers}";

        var damage = 0d;
        using (var deferral = view.Rows.SuspendNotifications())
        {
            SyncCombatantRows(view.Rows, fight.Snapshot, onlyTableRows: true);
            foreach (var row in view.Rows)
                damage += row.Damage;
            // A boss that gets HP back takes more damage than it has HP: the shares then split what the group took off.
            // Before the order, as the rows can go by them.
            var bossHp = Math.Max(fight.BossMaxHp, damage);
            foreach (var row in view.Rows)
                row.UpdateBossShare(bossHp > 0 ? row.Damage / bossHp : 0);
            RefreshCombatantBars(view.Rows, encounterId);
            view.Rows.Sort(CompareCombatantRows);
        }

        var durationMilliseconds = fight.Snapshot.EncounterTime;
        view.Duration = TimeSpan.FromMilliseconds(durationMilliseconds);
        view.DamagePerSecond = durationMilliseconds > 0 ? damage / (durationMilliseconds / 1000d) : 0;

        // Needed = HP left / time left, against the group's DPS on the boss; a fight that ended shows where it stood.
        var remainingHp = fight.Outcome == BossFightOutcome.Killed ? 0 : fight.BossHp;
        var limitMilliseconds = SettingsFlyout.BossTimeLimitMinutes * 60_000L;
        view.HasDpsCheck = limitMilliseconds > 0 && remainingHp > 0;
        if (!view.HasDpsCheck)
            return;

        if (durationMilliseconds >= limitMilliseconds)
        {
            view.DpsCheckText = string.Empty;
            view.DpsGapText = Localization["BossSection_TimeUp"];
            view.DpsGapShortText = "(−∞)";
            view.IsBehind = true;
            return;
        }

        var required = remainingHp / ((limitMilliseconds - durationMilliseconds) / 1000d);
        var gap = view.DamagePerSecond - required;
        view.IsBehind = gap < 0;
        view.DpsCheckText = string.Format(Localization["BossSection_Required"], FormatPerSecond(required));
        view.DpsGapText = string.Format(Localization[gap < 0 ? "BossSection_Missing" : "BossSection_Ahead"], FormatPerSecond(Math.Abs(gap)));
        view.DpsGapShortText = FormatCompact(Math.Abs(gap), 3, gap < 0 ? "(−" : "(+", ")");
    }

    // A history entry that ends mid-fight ended it too: the group left.
    private static bool IsFailed(SceneBossFight fight, bool archived) =>
        fight.Outcome == BossFightOutcome.Reset || archived && fight.Outcome == BossFightOutcome.Ongoing;

    // "Boss #1 (Kromede's Desolation)": with the boss's name where the game data knows the boss.
    private string GetBossFightTitle(SceneBossFight fight, bool archived)
    {
        var number = DisplayContext is { } context && context.TryResolveNpcCode(fight.BossId, out var code) &&
                     context.ResolveNpcCodeCatalogEntry(code) is { Name: { Length: > 0 } name }
            ? $"{fight.BossNumber} ({name})"
            : fight.BossNumber.ToString(System.Globalization.CultureInfo.CurrentCulture);
        return IsFailed(fight, archived)
            ? string.Format(Localization["BossFight_FailedAttempt"], number, fight.Attempt)
            : string.Format(Localization["BossFight_Title"], number);
    }

    // The fight graphs' pulls: the boss fights of what the meter shows, live or a history entry, with their events and
    // journal for working one out second by second (see FightGraphsViewModel).
    internal FightGraphSource CreateFightGraphSource()
    {
        if (IsViewingArchivedEncounter && SelectedEncounterHistory is { Record: var record })
        {
            var payload = record.ScenePayload;
            return new FightGraphSource(
                payload.Snapshot.EncounterId,
                payload.SceneStarted,
                payload.Snapshot.MapId,
                payload.BossFights,
                [.. payload.BossFights.Select(fight => GetBossFightTitle(fight, archived: true))],
                (combatantId, from, to) => payload.CreateDetailDelta(combatantId).Within(from, to).MetricEvents,
                () => payload.TimelineSegment,
                () => record,
                DisplayContext);
        }

        var scene = _captureService.Scene;
        var snapshot = _displayedSnapshot;
        var fights = _latestLiveFrame.BossFights;
        return new FightGraphSource(
            snapshot.EncounterId,
            scene.SessionStarted,
            snapshot.MapId,
            fights,
            [.. fights.Select(fight => GetBossFightTitle(fight, archived: false))],
            (combatantId, from, to) => scene.CreateDetailDeltaWithin(snapshot, combatantId, from, to).MetricEvents,
            () => scene.Owner.CreateLiveTimelineSegment().CreateBoundedSnapshot(),
            () =>
            {
                var payload = scene.CreateArchivePayload();
                return new ArchivedEncounterRecord { ArchivedAt = payload.SceneStarted.ToLocalTime(), Trigger = "stored", ScenePayload = payload };
            },
            DisplayContext);
    }

    // The character playing (as the game said, or remembered from an earlier session): whose runs the fight graphs list.
    internal (int ServerId, string Name)? LocalCharacter => _latestLiveFrame.MetadataRegistry?.LocalPlayerIdentity ?? SettingsFlyout.RememberedLocalPlayer;

    // A stored run's history entry (fight graphs): into the history unless it's there already, and shown in the meter.
    internal void ShowHistoryEntry(ArchivedEncounterRecord record)
    {
        var encounterId = record.ScenePayload.Snapshot.EncounterId;
        if (_encounterArchiveService.Import([record]) > 0)
            RebuildEncounterHistory();
        SelectedEncounterHistory = EncounterHistory.FirstOrDefault(item => item.Record.ScenePayload.Snapshot.EncounterId == encounterId);
    }

    [RelayCommand]
    private void ToggleBossFightList() => IsBossFightListExpanded = !IsBossFightListExpanded;

    private static string FormatPerSecond(double value) => FormatCompact(value, 4, null, "/s");

    private static string FormatCompact(double value, int significantDigits, string? prefix, string? suffix)
    {
        Span<char> buffer = stackalloc char[32];
        return NumericFormatter.TryFormat(value, buffer, new NumericFormatOptions(true, 1000, significantDigits, "N0", prefix, suffix), out var written)
            ? buffer[..written].ToString()
            : string.Empty;
    }

    private void CaptureFrozenBossProjection(
        Guid encounterId,
        IReadOnlyList<SceneBossFocusSnapshot> snapshots,
        IReadOnlyList<BossDamageContribution> damageContributions)
    {
        _frozenBossFocusSnapshots.Clear();
        for (var i = 0; i < snapshots.Count; i++)
            _frozenBossFocusSnapshots.Add(snapshots[i]);

        _frozenBossDamageContributions.Clear();
        for (var i = 0; i < damageContributions.Count; i++)
            _frozenBossDamageContributions.Add(damageContributions[i]);

        _frozenBossFocusEncounterId = encounterId;
    }

    private void BuildArchivedBossShareProjection(SceneArchivePayload payload)
    {
        _archivedBossFocusSnapshots.Clear();
        _archivedBossDamageContributions.Clear();
        if (payload.Bosses.Count == 0)
            return;

        var latestObservedAtMilliseconds = 0L;
        for (var i = 0; i < payload.Bosses.Count; i++)
            latestObservedAtMilliseconds = Math.Max(latestObservedAtMilliseconds, payload.Bosses[i].LastObservedAtMilliseconds);

        var activeBossIds = new HashSet<int>();
        for (var i = 0; i < payload.Bosses.Count; i++)
        {
            var boss = payload.Bosses[i];
            if (Math.Max(0, latestObservedAtMilliseconds - boss.LastObservedAtMilliseconds) > SceneReadModelOwner.BossFocusVisibilityTimeoutMilliseconds)
                continue;

            activeBossIds.Add(boss.InstanceId);
            _archivedBossFocusSnapshots.Add(new SceneBossFocusSnapshot
            {
                InstanceId = boss.InstanceId,
                Kind = ResolveArchivedBossKind(payload, boss.InstanceId),
                Hp = boss.Hp,
                MaxHp = boss.MaxHp,
                CumulativeLostHp = boss.CumulativeLostHp,
                LastObservedAtMilliseconds = boss.LastObservedAtMilliseconds,
                HasHp = boss.HasHp,
                HasMaxHp = boss.HasMaxHp
            });
        }

        for (var i = 0; i < payload.Pairs.Count; i++)
        {
            var pair = payload.Pairs[i];
            if (pair.TotalDamage <= 0 || !activeBossIds.Contains(pair.Key.TargetId))
                continue;

            _archivedBossDamageContributions.Add(new BossDamageContribution(
                pair.Key.TargetId,
                pair.Key.SourceId,
                pair.TotalDamage,
                pair.LastObserved));
        }

        _archivedBossDamageContributions.Sort(static (left, right) =>
        {
            var comparison = left.BossId.CompareTo(right.BossId);
            return comparison != 0 ? comparison : left.SourceCombatantId.CompareTo(right.SourceCombatantId);
        });
    }

    private static NpcKind ResolveArchivedBossKind(SceneArchivePayload payload, int instanceId)
    {
        for (var i = 0; i < payload.Entities.Count; i++)
        {
            var entity = payload.Entities[i];
            if (entity.EntityId == instanceId)
                return entity.Kind;
        }

        return NpcKind.Boss;
    }

    private void SyncBossFocuses()
    {
        for (var i = BossFocuses.Count - 1; i >= 0; i--)
        {
            var existing = BossFocuses[i];
            var stillPresent = false;
            for (var j = 0; j < _bossFocusDisplayGroups.Count; j++)
            {
                if (_bossFocusDisplayGroups[j].DisplayKey == existing.DisplayKey)
                {
                    stillPresent = true;
                    break;
                }
            }
            if (!stillPresent)
            {
                BossFocuses.RemoveAt(i);
            }
        }

        for (var i = 0; i < _bossFocusDisplayGroups.Count; i++)
        {
            var group = _bossFocusDisplayGroups[i];
            BossFocusViewModel? row = null;
            for (var j = 0; j < BossFocuses.Count; j++)
            {
                if (BossFocuses[j].DisplayKey == group.DisplayKey)
                {
                    row = BossFocuses[j];
                    break;
                }
            }

            if (row is null)
            {
                row = new BossFocusViewModel(
                    _frameBatchService,
                    group.DisplayKey,
                    group.Representative.InstanceId,
                    group.NpcCode,
                    group.InstanceCount,
                    BossFocusDisplayBuilder.NormalizeHpForDisplay(group.Representative.Kind, group.NpcCode, group.Representative.Hp),
                    BossFocusDisplayBuilder.NormalizeHpForDisplay(group.Representative.Kind, group.NpcCode, group.Representative.MaxHp),
                    group.Representative.HasHp,
                    group.Representative.HasMaxHp);
                BossFocuses.Add(row);
            }
            else
            {
                row.Update(
                    group.Representative.InstanceId,
                    group.NpcCode,
                    group.InstanceCount,
                    BossFocusDisplayBuilder.NormalizeHpForDisplay(group.Representative.Kind, group.NpcCode, group.Representative.Hp),
                    BossFocusDisplayBuilder.NormalizeHpForDisplay(group.Representative.Kind, group.NpcCode, group.Representative.MaxHp),
                    group.Representative.HasHp,
                    group.Representative.HasMaxHp);
            }

            row.UpdateSegment(CreateBossSegment(group.Representative));
            var stagger = group.Representative;
            row.UpdateStagger(
                stagger.StaggerMax > 0 ? new ProgressSegment((double)stagger.StaggerCurrent / stagger.StaggerMax, BossStaggerBrush) : null,
                stagger.StaggerMax > 0 ? string.Format(Localization["BossFocus_Stagger"], stagger.StaggerCurrent, stagger.StaggerMax) : string.Empty);
        }
    }

    private static ProgressSegment? CreateBossSegment(SceneBossFocusSnapshot boss)
    {
        if (!boss.HasHp || !boss.HasMaxHp)
            return null;

        var maxHp = Math.Max(1, boss.MaxHp);
        var hpRatio = Math.Clamp(Math.Max(0, boss.Hp) / (double)maxHp, 0d, 1d);
        return hpRatio > 0
            ? new ProgressSegment(hpRatio, BossRemainingHpBrush)
            : null;
    }

    private void RefreshCombatantBossShares(
        IReadOnlyList<BossFocusDisplayGroup> focusGroups,
        IReadOnlyList<BossDamageContribution> damageContributions)
    {
        var scope = BossFocusDisplayBuilder.CreateShareScope(focusGroups);
        var hasBossShareScope = scope.EffectiveHp > 0;
        var hasBossFocusProjection = focusGroups.Count > 0;
        CombatantColumns.SetBossShareColumnVisibility(hasBossFocusProjection);

        if (!hasBossShareScope)
        {
            for (var i = 0; i < Combatants.Count; i++)
                Combatants[i].UpdateBossShare(0);
            return;
        }

        // A boss that gets HP back between its HP updates takes more damage than they show: the shares then split the damage.
        var total = 0L;
        for (var i = 0; i < Combatants.Count; i++)
            total += BossFocusDisplayBuilder.FindAggregateContributionAmount(focusGroups, damageContributions, Combatants[i].Id);
        var bossHp = (double)Math.Max(scope.EffectiveHp, total);
        for (var i = 0; i < Combatants.Count; i++)
        {
            var row = Combatants[i];
            var damage = BossFocusDisplayBuilder.FindAggregateContributionAmount(
                focusGroups,
                damageContributions,
                row.Id);
            row.UpdateBossShare(damage > 0 ? damage / bossHp : 0);
        }
    }

    private void ApplyCombatantMetricDisplaySettings()
    {
        CombatantColumns.ApplyMetricDisplaySettings(
            SettingsFlyout.ShowDamagePerSecondColumn,
            SettingsFlyout.ShowDamageColumn,
            SettingsFlyout.ShowHealingPerSecondColumn,
            SettingsFlyout.ShowHealingColumn,
            SettingsFlyout.ShowDamageTakenColumn,
            SettingsFlyout.ShowUnhealedHealthColumn,
            SettingsFlyout.ShowAggroColumn,
            SettingsFlyout.ShowStaggerColumn,
            SettingsFlyout.ShowBossShareColumn,
            SettingsFlyout.ShowTotalDamagePerSecond,
            SettingsFlyout.UseCompactMainMetrics);
    }

    private void RefreshCombatantBars(IReadOnlyList<CombatantRowViewModel> rows, Guid encounterId)
    {
        var maxMetric = 0d;
        for (var i = 0; i < rows.Count; i++)
            maxMetric = Math.Max(maxMetric, ResolveCombatantSortMetric(rows[i]));

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var ratio = maxMetric > 0 ? ResolveCombatantSortMetric(row) / maxMetric : 0d;
            row.UpdateBar(ratio, ResolveCombatantBarBrush(encounterId, row.Id));
        }
    }

    // By the column picked, the highest first or the lowest; ties the most damage first, then by id.
    private int CompareCombatantRows(CombatantRowViewModel left, CombatantRowViewModel right)
    {
        var cmp = ResolveCombatantSortMetric(right).CompareTo(ResolveCombatantSortMetric(left));
        if (cmp != 0)
            return SettingsFlyout.CombatantSortAscending ? -cmp : cmp;

        cmp = right.Damage.CompareTo(left.Damage);
        return cmp != 0 ? cmp : left.Id.CompareTo(right.Id);
    }

    private double ResolveCombatantSortMetric(CombatantRowViewModel row) =>
        SettingsFlyout.CombatantSortMetric switch
        {
            CombatantSortMetric.TotalDamage => row.Damage,
            CombatantSortMetric.HealingPerSecond => row.HealingPerSecond,
            CombatantSortMetric.TotalHealing => row.Healing,
            CombatantSortMetric.DamageTaken => row.DamageTaken,
            CombatantSortMetric.UnhealedHealth => row.UnhealedHealthSeconds,
            CombatantSortMetric.Aggro => row.AggroShare,
            CombatantSortMetric.Stagger => row.StaggerShare,
            CombatantSortMetric.BossShare => row.BossShareRatio,
            _ => row.DamagePerSecond
        };

    private void RefreshFooterTotals()
    {
        var total = 0d;
        var unhealed = 0d;
        for (var i = 0; i < Combatants.Count; i++)
        {
            total += Combatants[i].DamagePerSecond;
            unhealed += Combatants[i].UnhealedHealthSeconds;
        }

        TotalFilteredDamagePerSecond = total;
        TotalUnhealedHealthSeconds = unhealed;
    }

    private void EnsureBarBrushScope(Guid encounterId)
    {
        if (_barBrushEncounterId == encounterId)
            return;

        _barBrushEncounterId = encounterId;
        _barBrushSeed = CreateColorSeed(encounterId);
        _barBrushBaseHue = (int)(_barBrushSeed % 360);
        _nextBarHueIndex = 0;
        _combatantBarBrushes.Clear();
    }

    private IBrush ResolveCombatantBarBrush(Guid encounterId, int combatantId)
    {
        EnsureBarBrushScope(encounterId);
        if (_combatantBarBrushes.TryGetValue(combatantId, out var brush))
            return brush;

        brush = CreateGeneratedBrush(_nextBarHueIndex++);
        _combatantBarBrushes.Add(combatantId, brush);
        return brush;
    }

    private SolidColorBrush CreateGeneratedBrush(int index)
    {
        var hue = AllocateBarHue(index);
        var variant = MixHash(_barBrushSeed, unchecked((uint)index));
        var saturation = BarColorMinSaturation + BarColorSaturationRange * ((variant & 0xffff) / 65_535d);
        var lightness = BarColorMinLightness + BarColorLightnessRange * (((variant >> 16) & 0xffff) / 65_535d);
        return new SolidColorBrush(HslColor.ToRgb(hue, saturation, lightness, 1d));
    }

    private int AllocateBarHue(int index)
    {
        var ring = index / BarHueRingSize;
        var slot = index % BarHueRingSize;
        var offset = GetHueRingOffset(ring);
        return NormalizeHue(_barBrushBaseHue + offset + slot * BarHueGap);
    }

    private static int GetHueRingOffset(int ring)
    {
        if (ring == 0)
            return 0;

        var level = 0;
        var value = ring;
        while (value > 1)
        {
            value >>= 1;
            level++;
        }

        var denominator = 1 << (level + 1);
        var odd = ((ring - (1 << level)) << 1) + 1;
        return BarHueGap * odd / denominator;
    }

    private static int NormalizeHue(int hue)
    {
        hue %= 360;
        return hue < 0 ? hue + 360 : hue;
    }

    private static uint CreateColorSeed(Guid encounterId)
    {
        Span<byte> bytes = stackalloc byte[16];
        encounterId.TryWriteBytes(bytes);
        var hash = 2_166_136_261u;
        for (var i = 0; i < bytes.Length; i++)
            hash = (hash ^ bytes[i]) * 16_777_619u;
        return hash;
    }

    private static uint MixHash(uint hash, uint value)
    {
        hash = (hash ^ (byte)value) * 16_777_619u;
        hash = (hash ^ (byte)(value >> 8)) * 16_777_619u;
        hash = (hash ^ (byte)(value >> 16)) * 16_777_619u;
        return (hash ^ (byte)(value >> 24)) * 16_777_619u;
    }

    private bool ShouldDisplayCombatant(int combatantId, SceneCombatantMetrics data)
    {
        if (!data.IsVisiblePlayerCombatant)
        {
            return false;
        }

        var scope = SettingsFlyout.CombatantStatisticsScope;
        if (scope == CombatantStatisticsScope.All)
        {
            return data.CharacterClass is not null;
        }

        if (DisplayContext?.TryResolvePcMetadata(combatantId, out var metadata) != true)
        {
            return false;
        }

        if (metadata.IsLocalPlayer)
        {
            return true;
        }

        return scope switch
        {
            CombatantStatisticsScope.Party => metadata.GroupRelation == PlayerGroupRelation.PartyMember,
            CombatantStatisticsScope.Force => metadata.GroupRelation is PlayerGroupRelation.PartyMember or PlayerGroupRelation.ForceMember,
            _ => false
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _captureService.StatusChanged -= OnCaptureStatusChanged;
        _captureService.RttResolved -= OnRttResolved;
        _processPortDiscoveryService.Discovered -= OnProcessPortChanged;
        _processPortDiscoveryService.Removed -= OnProcessPortChanged;
        _languageService.LanguageChanged -= OnLanguageChanged;
        _gameResourceService.ResourcesChanged -= OnResourcesChanged;
        _encounterArchiveService.HistoryChanged -= OnEncounterHistoryChanged;
        SettingsFlyout.PropertyChanged -= OnSettingsFlyoutPropertyChanged;
        _combatantDetails.Dispose();
        await StopCaptureAsync().ConfigureAwait(false);
        await _processPortDiscoveryService.DisposeAsync().ConfigureAwait(false);
    }

    private void ApplyLocalizedUiText()
    {
        Status = Localization["Status_Ready"];
        NumericStableWidthScopeKey = _displayedSnapshot.EncounterId;
        EncounterDuration = TimeSpan.Zero;
        RoundTripTimeMilliseconds = 0;
        LiveSceneMapId = _displayedSnapshot.MapId;
        UpdateDisplayContext(_displayedSnapshot);
    }

    private void RebuildEncounterHistory()
    {
        var selectedId = SelectedEncounterHistory?.Record.Id;
        EncounterHistory.Clear();
        foreach (var record in _encounterArchiveService.History)
        {
            var displayContext = CreateArchivedDisplayContext(record);
            var archivedAt = record.ArchivedAt.ToLocalTime();
            EncounterHistory.Add(new EncounterHistoryItemViewModel(
                record,
                displayContext,
                displayContext.ResolveSceneName(record.ScenePayload.Kind, record.ScenePayload.Snapshot.MapId, record.ScenePayload.BossNpcCodes),
                archivedAt.ToString(archivedAt.Date == DateTime.Today ? "HH:mm:ss" : "MM-dd HH:mm")));
        }

        SelectedEncounterHistory = EncounterHistory.FirstOrDefault(x => x.Record.Id == selectedId);
    }

    internal void RenameEncounterHistory(EncounterHistoryItemViewModel item, string? name)
        => _encounterArchiveService.Rename(item.Record, name);

    // Each entry holds its own events, so removing it frees them; the memory goes back right away, not at some later
    // collection.
    internal void RemoveEncounterHistory(EncounterHistoryItemViewModel item)
    {
        if (IsViewingArchivedEncounter && ReferenceEquals(SelectedEncounterHistory?.Record, item.Record))
            ReturnToLive();
        _encounterArchiveService.Remove(item.Record);
        ReleaseMemoryAfterRebuild();
    }

    internal void ClearEncounterHistory()
    {
        if (IsViewingArchivedEncounter)
            ReturnToLive();
        _encounterArchiveService.Clear();
        ReleaseMemoryAfterRebuild();
    }

    // The history list is rebuilt at normal priority; after that nothing holds the entries.
    private static void ReleaseMemoryAfterRebuild() =>
        Dispatcher.UIThread.Post(static () => GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true), DispatcherPriority.Background);

    // Shows the newest imported entry, like a freshly archived one.
    internal void ImportEncounterHistory(IReadOnlyList<ArchivedEncounterRecord> records)
    {
        if (_encounterArchiveService.Import(records) == 0)
            return;

        RebuildEncounterHistory();
        SelectedEncounterHistory = EncounterHistory.FirstOrDefault();
    }

    private void UpdateDisplayContext(SceneCombatSnapshot snapshot)
    {
        var isArchived = IsViewingArchivedEncounter && SelectedEncounterHistory is not null;
        var archivedRecord = isArchived ? SelectedEncounterHistory!.Record : null;
        var metadataRegistry = isArchived ? null : _latestLiveFrame.MetadataRegistry;
        var metadataRevision = metadataRegistry?.Revision ?? -1;
        if (ReferenceEquals(_displayContextSnapshot, snapshot) &&
            ReferenceEquals(_displayContextArchivedRecord, archivedRecord) &&
            ReferenceEquals(_displayContextMetadataRegistry, metadataRegistry) &&
            _displayContextBuiltVersion == _displayContextVersion &&
            _displayContextMetadataRevision == metadataRevision &&
            _displayContextIsArchived == isArchived)
        {
            return;
        }

        _displayContextSnapshot = snapshot;
        _displayContextArchivedRecord = archivedRecord;
        _displayContextBuiltVersion = _displayContextVersion;
        _displayContextMetadataRevision = metadataRevision;
        _displayContextMetadataRegistry = metadataRegistry;
        _displayContextIsArchived = isArchived;
        DisplayContext = isArchived
            ? CreateArchivedDisplayContext(archivedRecord!)
            : CreateDisplayContext(snapshot, SceneIdentityScope.Empty, metadataRegistry);
        _combatantDetails.DisplayContext = DisplayContext;
    }

    private SceneDisplayContext CreateArchivedDisplayContext(ArchivedEncounterRecord record)
        => CreateDisplayContext(record.ScenePayload.Snapshot, record.ScenePayload.IdentityScope, null);

    internal ScenePlaybackOpenContext CreateLivePlaybackContext()
    {
        var source = _captureService.Scene.CreatePlaybackSource(out var metadataRegistry);
        var displayContext = CreateDisplayContext(source.CreateSnapshot(), SceneIdentityScope.Empty, metadataRegistry);
        return new ScenePlaybackOpenContext(source, displayContext);
    }

    internal static ScenePlaybackOpenContext CreatePlaybackContext(EncounterHistoryItemViewModel item)
        => new(new ArchivedScenePlaybackSource(item.Record.ScenePayload), item.DisplayContext);

    private SceneDisplayContext CreateDisplayContext(SceneCombatSnapshot snapshot, SceneIdentityScope scope, RuntimeMetadataRegistry? metadataRegistry)
        => new(scope, metadataRegistry, snapshot, _gameResourceService, Localization["Scene_Unknown"]);

    private ArchivedEncounterRecord? CloseCurrentArchiveScope(string trigger, bool isAutomatic)
    {
        DrainPendingBossArchives();
        var payload = _captureService.Scene.ArchiveAndReset(RawPacketDump.RotateLogs);
        return _encounterArchiveService.Archive(payload, trigger, isAutomatic);
    }

    private void ResetLiveModels(Func<DateTimeOffset> resolveSessionStarted)
    {
        _captureService.Scene.Reset(resolveSessionStarted);
    }

    private void ChangeSceneKind()
    {
        if (_isDisposed || _suppressRefresh || _captureService.Scene.Kind == SettingsFlyout.SceneKind)
            return;

        _suppressRefresh = true;
        try
        {
            DrainPendingBossArchives();
            var archive = _captureService.Scene.ChangeKind(SettingsFlyout.SceneKind, RawPacketDump.RotateLogs, archiveCurrent: true);
            if (archive is { } payload)
                _encounterArchiveService.Archive(payload, "scene-kind-change", isAutomatic: true);
            ResetLivePresentation();
        }
        finally
        {
            _suppressRefresh = false;
        }
    }

    private void DrainPendingBossArchives()
    {
        while (_captureService.Scene.TryDequeuePendingArchive(out var payload))
            _encounterArchiveService.Archive(payload, "boss-encounter-completed", isAutomatic: true);
    }

    private void DrainMapTransitionArchives()
    {
        while (_captureService.Scene.TryDequeueMapTransition(out var payload))
        {
            RawPacketDump.RotateLogs();
            if (payload is not null)
            {
                var record = _encounterArchiveService.Archive(payload, "map-transition", isAutomatic: true);
                if (record is not null && payload.Kind == SceneKind.Boss && !IsViewingArchivedEncounter)
                    SelectMapTransitionHistory(record);
            }
        }
    }

    private void SelectMapTransitionHistory(ArchivedEncounterRecord record)
    {
        RebuildEncounterHistory();
        var history = EncounterHistory.FirstOrDefault(item => item.Record.Id == record.Id);
        if (history is not null)
            SelectedEncounterHistory = history;
    }

    private bool ShouldReturnToLiveAfterBoss(SceneCombatSnapshot liveSnapshot) =>
        IsViewingArchivedEncounter &&
        liveSnapshot.Kind == SceneKind.Boss &&
        liveSnapshot.BossFocuses.Count > 0;

    private void ResetLivePresentation()
    {
        _latestLiveSnapshot = new SceneCombatSnapshot();
        _displayedSnapshot = new SceneCombatSnapshot();
        _latestLiveFrame = new SceneReadModelFrame();
        Combatants.Clear();
        TotalFilteredDamagePerSecond = 0;
        TotalUnhealedHealthSeconds = 0;
        _buffUptimeShown = null;
        _specializationsShown = null;
        CombatantDetails.Clear();
        ClearBossFocusProjection();
        ApplyCombatantMetricDisplaySettings();
        CombatantColumns.SetBossShareColumnVisibility(false);
        SelectedCombatant = null;
        SelectedEncounterHistory = null;
        IsViewingArchivedEncounter = false;
        ApplyLocalizedUiText();
    }

    private void ClearBossFocusProjection()
    {
        BossFocuses.Clear();
        _bossFocusDisplayGroups.Clear();
        _bossFocusDisplayCandidates.Clear();
        _frozenBossFocusSnapshots.Clear();
        _frozenBossDamageContributions.Clear();
        _frozenBossFocusEncounterId = Guid.Empty;
    }

    private void RefreshCombatantDetails(bool forceRefresh = false)
    {
        if (SelectedCombatant is null)
        {
            CombatantDetails.Deactivate();
            return;
        }

        if (_detailFightIndex >= 0)
        {
            IReadOnlyList<SceneBossFight> fights = IsViewingArchivedEncounter
                ? SelectedEncounterHistory?.Record.ScenePayload.BossFights ?? (IReadOnlyList<SceneBossFight>)[]
                : _latestLiveFrame.BossFights;
            if (_displayedSnapshot.EncounterId == _detailFightEncounterId && _detailFightIndex < fights.Count)
            {
                RefreshBossFightDetails(fights[_detailFightIndex], forceRefresh);
                return;
            }

            // Another encounter is shown now: back to the whole of it.
            _detailFightIndex = -1;
            forceRefresh = true;
        }

        CombatantDetails.ScopeText = null;
        var encounterContextId = IsViewingArchivedEncounter
            ? SelectedEncounterHistory?.Record.ScenePayload.Snapshot.EncounterId ?? Guid.Empty
            : _displayedSnapshot.EncounterId;

        var snapshot = IsViewingArchivedEncounter && SelectedEncounterHistory is not null
            ? SelectedEncounterHistory.Record.ScenePayload.Snapshot
            : _displayedSnapshot;

        if (IsViewingArchivedEncounter && SelectedEncounterHistory is { } history)
        {
            var detail = history.Record.ScenePayload.CreateDetailDelta(SelectedCombatant.Id);
            CombatantDetails.SelectSceneEncounterCombatant(encounterContextId, SelectedCombatant.Id, snapshot, detail, forceRefresh);
            RefreshBuffUptime(snapshot, SelectedCombatant.Id, forceRefresh);
            RefreshSeenSpecializations(snapshot, SelectedCombatant.Id, forceRefresh);
            RefreshDigitalTwin();
            return;
        }

        if (!IsViewingArchivedEncounter)
        {
            var selectedCombatantId = SelectedCombatant.Id;
            var detailContextChanged = _latestLiveFrame.DetailCombatantId != selectedCombatantId ||
                                       _latestLiveFrame.DetailUpdate.CombatantId != selectedCombatantId;
            var requiresFullDetail = forceRefresh || detailContextChanged;
            if (requiresFullDetail)
            {
                _latestLiveFrame = CreateLiveFrame(selectedCombatantId, forceDetailRefresh: true);
                _latestLiveSnapshot = _latestLiveFrame.Snapshot;
                _displayedSnapshot = _latestLiveFrame.Snapshot;
                snapshot = _displayedSnapshot;
            }

            CombatantDetails.SelectLiveSceneEncounterCombatant(snapshot.EncounterId, selectedCombatantId, snapshot, _latestLiveFrame.DetailUpdate, requiresFullDetail);
            RefreshBuffUptime(snapshot, selectedCombatantId, requiresFullDetail);
            RefreshSeenSpecializations(snapshot, selectedCombatantId, requiresFullDetail);
            RefreshDigitalTwin();
            return;
        }

        _buffUptimeShown = null;
        _specializationsShown = null;
        CombatantDetails.Clear();
    }

    // The details' Buffs tab, over what they cover (the encounter, or one boss fight), from the journal: the history entry's,
    // or the live one, then again at most every 2 s. Worked out off the UI thread, and only while the tab is open.
    private void RefreshBuffUptime(SceneCombatSnapshot scope, int combatantId, bool forceRefresh)
    {
        if (!CombatantDetails.IsBuffsSelected)
            return;

        var archived = IsViewingArchivedEncounter && SelectedEncounterHistory is not null;
        (Guid, long, long, int) key = (scope.EncounterId, scope.EncounterStartTime, archived ? scope.EncounterEndTime : 0, combatantId);
        var now = Environment.TickCount64;
        if (!forceRefresh && _buffUptimeShown == key && (archived || now - _buffUptimeComputedAt < 2_000))
            return;

        _buffUptimeShown = key;
        _buffUptimeComputedAt = now;
        var segment = archived
            ? SelectedEncounterHistory!.Record.ScenePayload.TimelineSegment
            : _captureService.Scene.Owner.CreateLiveTimelineSegment().CreateBoundedSnapshot();
        var players = _displayedSnapshot.Combatants.AsSpan().ToArray().Where(static c => c.Metrics.IsVisiblePlayerCombatant).Select(static c => c.Id).ToHashSet();
        var (from, to, range) = (scope.EncounterStartTime, scope.EncounterEndTime, SettingsFlyout.GroupFrameRangeMeters);
        _buffUptimeCancellation?.Cancel();
        var cancellation = _buffUptimeCancellation = new CancellationTokenSource();
        Task.Run(() => Cloris.Aion2Flow.SceneRuntime.Stores.BuffUptime.Compute(segment, from, to, players, range, cancellation.Token), cancellation.Token).ContinueWith(
            task => Dispatcher.UIThread.Post(() =>
            {
                if (task.IsCompletedSuccessfully && !cancellation.IsCancellationRequested && SelectedCombatant?.Id == combatantId)
                    CombatantDetails.ShowBuffUptime(task.Result, combatantId, CasterSkillLevel);
            }),
            TaskScheduler.Default);
    }

    // The twin tab's specializations over what the details cover (see SkillSpecializationCodes.Read), from the journal
    // like RefreshBuffUptime: the history entry's, or the live one at most every 2 s; off the UI thread, only while the tab
    // is open.
    private void RefreshSeenSpecializations(SceneCombatSnapshot scope, int combatantId, bool forceRefresh)
    {
        if (!CombatantDetails.IsTwinSelected)
            return;

        var archived = IsViewingArchivedEncounter && SelectedEncounterHistory is not null;
        (Guid, long, long, int) key = (scope.EncounterId, scope.EncounterStartTime, archived ? scope.EncounterEndTime : 0, combatantId);
        var now = Environment.TickCount64;
        if (!forceRefresh && _specializationsShown == key && (archived || now - _specializationsComputedAt < 2_000))
            return;

        _specializationsShown = key;
        _specializationsComputedAt = now;
        var segment = archived
            ? SelectedEncounterHistory!.Record.ScenePayload.TimelineSegment
            : _captureService.Scene.Owner.CreateLiveTimelineSegment().CreateBoundedSnapshot();
        var (from, to) = (scope.EncounterStartTime, scope.EncounterEndTime);
        _specializationsCancellation?.Cancel();
        var cancellation = _specializationsCancellation = new CancellationTokenSource();
        Task.Run(() => Cloris.Aion2Flow.SceneRuntime.Twins.SkillSpecializationCodes.Read(segment, combatantId, from, to, SkillSpecializations.Has, cancellation.Token), cancellation.Token).ContinueWith(
            task => Dispatcher.UIThread.Post(() =>
            {
                if (task.IsCompletedSuccessfully && !cancellation.IsCancellationRequested && SelectedCombatant?.Id == combatantId)
                    CombatantDetails.ShowSeenSpecializations(task.Result);
            }),
            TaskScheduler.Default);
    }

    // A boss fight's detail: the encounter's events from the fight's first to its last blow, as if the meter had been
    // reset right before the pull and stopped after it.
    // ponytail: rebuilt from scratch, so a live fight at most every 500 ms; feed it like the live detail if that lags.
    private void RefreshBossFightDetails(SceneBossFight fight, bool forceRefresh)
    {
        var combatantId = SelectedCombatant!.Id;
        var now = Environment.TickCount64;
        if (forceRefresh || _detailFightShown is null ||
            !ReferenceEquals(fight, _detailFightShown) && now - _detailFightBuiltAt >= 500)
        {
            var from = fight.Snapshot.EncounterStartTime;
            var to = fight.Snapshot.EncounterEndTime;
            _detailFightDelta = IsViewingArchivedEncounter && SelectedEncounterHistory is { } history
                ? history.Record.ScenePayload.CreateDetailDelta(combatantId).Within(from, to)
                : _captureService.Scene.CreateDetailDeltaWithin(_displayedSnapshot, combatantId, from, to);
            _detailFightShown = fight;
            _detailFightBuiltAt = now;
            forceRefresh = true;
            CombatantDetails.ScopeText = GetBossFightTitle(fight, IsViewingArchivedEncounter);
        }

        CombatantDetails.SelectSceneEncounterCombatant(_displayedSnapshot.EncounterId, combatantId, _detailFightShown.Snapshot, _detailFightDelta, forceRefresh);
        RefreshBuffUptime(_detailFightShown.Snapshot, combatantId, forceRefresh);
        RefreshSeenSpecializations(_detailFightShown.Snapshot, combatantId, forceRefresh);
        RefreshDigitalTwin();
    }

    private void RefreshCaptureIndicators()
    {
        DriverIndicatorColor = _captureService.HasDriverError
            ? IndicatorErrorColor
            : _captureService.IsDriverActive
                ? IndicatorOkColor
                : IndicatorIdleColor;
        DriverIndicatorToolTip = _captureService.HasDriverError && !string.IsNullOrWhiteSpace(_captureService.LastStatusMessage)
            ? _captureService.LastStatusMessage
            : _captureService.IsDriverActive
                ? Localization["Status_DriverReady"]
                : Localization["Status_DriverIdle"];

        if (!_processPortDiscoveryService.IsMonitoring)
        {
            GamePortIndicatorColor = IndicatorIdleColor;
            GamePortIndicatorToolTip = Localization["Status_PortIdle"];
        }
        else if (_processPortDiscoveryService.AllPorts.Length > 0)
        {
            GamePortIndicatorColor = IndicatorOkColor;
            GamePortIndicatorToolTip = Localization["Status_PortReady"];
        }
        else
        {
            GamePortIndicatorColor = IndicatorWarnColor;
            GamePortIndicatorToolTip = Localization["Status_PortWaiting"];
        }

        var isCaptureLocked = CaptureConnectionGate.IsLocked;
        if (!isCaptureLocked)
        {
            CaptureLockIndicatorColor = IndicatorIdleColor;
            CaptureLockIndicatorToolTip = Localization["Status_Unlocked"];
        }
        else
        {
            CaptureLockIndicatorColor = IndicatorOkColor;
            CaptureLockIndicatorToolTip = Localization["Status_Locked"];
        }

        var currentRttMilliseconds = _captureService.CurrentRoundTripTimeMilliseconds;
        if (!currentRttMilliseconds.HasValue || currentRttMilliseconds.Value <= 0)
        {
            RoundTripTimeMilliseconds = 0;
            LatencyIndicatorColor = IndicatorIdleColor;
            LatencyToolTip = Localization["Status_LatencyUnavailable"];
            return;
        }

        RoundTripTimeMilliseconds = Math.Max(1, (int)Math.Round(currentRttMilliseconds.Value));
        LatencyIndicatorColor = RoundTripTimeMilliseconds switch
        {
            <= GoodLatencyUpperBoundMilliseconds => IndicatorOkColor,
            <= WarningLatencyUpperBoundMilliseconds => IndicatorWarnColor,
            _ => IndicatorErrorColor
        };
        LatencyToolTip = Localization["Status_LatencyMeasured"];
    }

}
