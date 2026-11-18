using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using Avalonia.Threading;
using Cloris.Aion2Flow.Presentation;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Audio;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cloris.Aion2Flow.ViewModels;

// The settings window's tabs.
public enum SettingsTab
{
    General,
    Shortcuts,
    Scenes,
    Workflows,
    Visuals,
    Audio,
    Meter,
    GroupFrame,
    SkillMonitor,
    FightGraphs,
    HiddenCubes,
    TwinLibrary,
    System,
}

// A meter column that shows by itself for some classes: aggro (Auto: tanks), recovery time (Auto: Clerics); or always
// Show or Hide.
public enum AggroColumnMode
{
    Auto,
    Show,
    Hide,
}

public sealed partial class SettingsFlyoutViewModel : ObservableObject
{
    private readonly LanguageService _languageService;
    private readonly SettingsService _settingsService;
    private readonly PlayerNameDisplayService _playerNameDisplay;
    private readonly UiScaleService _uiScale;
    private readonly AppUpdateService _updateService;
    private readonly ProcessForegroundWatcher _processForegroundWatcher;
    private readonly GlobalHotkeyService _globalHotkeyService;
    private readonly bool _isApplyingPersistedSettings;
    private bool _hasObservedHotkeyRegistrationState;
    private bool _isHotkeyRegistrationWindowAttached;

    public SettingsFlyoutViewModel(LocalizationService localization, LanguageService languageService, SettingsService settingsService, PlayerNameDisplayService playerNameDisplay, UiScaleService uiScale, AppUpdateService updateService, ProcessForegroundWatcher processForegroundWatcher, GlobalHotkeyService globalHotkeyService)
    {
        Localization = localization;
        _languageService = languageService;
        _settingsService = settingsService;
        _playerNameDisplay = playerNameDisplay;
        _uiScale = uiScale;
        _updateService = updateService;
        _processForegroundWatcher = processForegroundWatcher;
        _globalHotkeyService = globalHotkeyService;
        AlertSounds = new AlertSounds(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsService.FilePath)) ?? ".", "alert-sounds"));

        BossParticipation = new BossParticipationViewModel(this);
        var persisted = _settingsService.Current;
        if (!string.IsNullOrWhiteSpace(persisted.Language))
        {
            _languageService.SetLanguage(persisted.Language);
        }

        _isApplyingPersistedSettings = true;
        try
        {
            TopmostMode = persisted.TopmostMode;
            MaxVisibleCombatantRows = persisted.MaxVisibleCombatantRows;
            CombatantSortMetric = persisted.CombatantSortMetric;
            CombatantSortAscending = persisted.CombatantSortAscending;
            CombatantStatisticsScope = persisted.CombatantStatisticsScope;
            SceneKind = persisted.SceneKind;
            UseCompactMainMetrics = persisted.UseCompactMainMetrics;
            ShowDamagePerSecondColumn = persisted.ShowDamagePerSecondColumn;
            ShowDamageColumn = persisted.ShowDamageColumn;
            ShowHealingPerSecondColumn = persisted.ShowHealingPerSecondColumn;
            ShowHealingColumn = persisted.ShowHealingColumn;
            IncludeOverhealing = persisted.IncludeOverhealing;
            ShowDamageTakenColumn = persisted.ShowDamageTakenColumn;
            ShowUnhealedHealthColumn = persisted.ShowUnhealedHealthColumn;
            AggroColumn = persisted.AggroColumn ?? (persisted.ShowAggroColumn ? AggroColumnMode.Show : AggroColumnMode.Auto);
            RecoveryTimeColumn = persisted.RecoveryTimeColumn;
            ShowStaggerColumn = persisted.ShowStaggerColumn;
            ShowBossShareColumn = persisted.ShowBossShareColumn;
            ShowTotalDamagePerSecond = persisted.ShowTotalDamagePerSecond;
            ShowColumnHeaders = persisted.ShowColumnHeaders;
            BossSectionExpanded = persisted.BossSectionExpanded;
            ShowBossSumRow = persisted.ShowBossSumRow;
            BossTimeLimitMinutes = persisted.BossTimeLimitMinutes;
            DummyPullSeconds = persisted.DummyPullSeconds;
            DummyCutoffSeconds = persisted.DummyCutoffSeconds;
            EncounterTimeDisplayFormat = persisted.EncounterTimeDisplayFormat;
            ShowFocusStatusBar = persisted.ShowFocusStatusBar;
            ShowPlayerNames = persisted.ShowPlayerNames;
            PlayerSelfMarkerDisplayMode = persisted.PlayerSelfMarkerDisplayMode;
            ShowPlayerShortServerName = persisted.ShowPlayerShortServerName;
            ShowPlayerLegionName = persisted.ShowPlayerLegionName;
            TintPlayerNamesByFaction = persisted.TintPlayerNamesByFaction;
            SkillMonitorEnabled = persisted.SkillMonitorEnabled;
            GroupFrameEnabled = persisted.GroupFrameEnabled;
            FightGraphsEnabled = persisted.FightGraphsEnabled;
            FightGraphsShowMembers = persisted.FightGraphsShowMembers;
            FightGraphsShowOtherDeaths = persisted.FightGraphsShowOtherDeaths;
            FightGraphsSmoothCurves = persisted.FightGraphsSmoothCurves;
            FightGraphsShowOverview = persisted.FightGraphsShowOverview;
            FightGraphsNewBestsInTitle = persisted.FightGraphsNewBestsInTitle;
            FightGraphsScalePercent = persisted.FightGraphsScalePercent;
            FightGraphsBestMetric = persisted.FightGraphsBestMetric;
            // The meter clickable without focus, as in its old interactive mode, once for settings from before it had that.
            ClickableWithoutFocus = persisted.MeterClickableAdded || persisted.ClickableWithoutFocus.Contains(MeterKey)
                ? [.. persisted.ClickableWithoutFocus]
                : [.. persisted.ClickableWithoutFocus, MeterKey];
            TitleClickable = [.. persisted.TitleClickable];
            FightGraphsOpacityPercent = persisted.FightGraphsOpacityPercent;
            SkillMonitorOpacityPercent = persisted.SkillMonitorOpacityPercent;
            TwinLibraryScalePercent = persisted.TwinLibraryScalePercent;
            TwinLibraryOpacityPercent = persisted.TwinLibraryOpacityPercent;
            SkillMonitorScalePercent = persisted.SkillMonitorScalePercent;
            UiScalePercent = persisted.UiScalePercent;
            OverlayOpacityPercent = persisted.OverlayOpacityPercent;
            DetailOpacityPercent = persisted.DetailOpacityPercent;
            GroupFrameScalePercent = persisted.GroupFrameScalePercent;
            GroupFrameShowMyBuffs = persisted.GroupFrameShowMyBuffs;
            GroupFrameShowDebuffs = persisted.GroupFrameShowDebuffs;
            GroupFrameShowOtherBuffs = persisted.GroupFrameShowOtherBuffs;
            GroupFrameShowPermanentBuffs = persisted.GroupFrameShowPermanentBuffs;
            GroupFrameShieldOnRight = persisted.GroupFrameShieldOnRight;
            GroupFrameShieldAnimation = persisted.GroupFrameShieldAnimation;
            GroupFrameGreenMeters = persisted.GroupFrameGreenMeters;
            GroupFrameOrangeMeters = persisted.GroupFrameOrangeMeters;
            GroupFrameRangeMeters = persisted.GroupFrameRangeMeters;
            GroupFrameShowDeaths = persisted.GroupFrameShowDeaths;
            // The cube finder was the group frame's until it got a frame of its own: shown there (with the group frame on)
            // is on here.
            HiddenCubesEnabled = persisted.HiddenCubesEnabled ?? (persisted.GroupFrameEnabled && persisted.GroupFrameCubeFinder != GroupFrameCubeFinder.Off);
            HiddenCubesSound = persisted.HiddenCubesSound ?? persisted.GroupFrameCubeFinder == GroupFrameCubeFinder.Sound;
            HiddenCubesScalePercent = persisted.HiddenCubesScalePercent;
            HiddenCubesOpacityPercent = persisted.HiddenCubesOpacityPercent;
            MinimizedFrames = persisted.MeterFolded && !persisted.MinimizedFrames.Contains(MeterKey) ? [.. persisted.MinimizedFrames, MeterKey] : [.. persisted.MinimizedFrames];
            GroupFrameAuraSide = persisted.GroupFrameAuraSide;
            GroupFrameArrowSide = persisted.GroupFrameArrowSide;
            GroupFrameDistanceSide = persisted.GroupFrameDistanceSide;
            GroupFrameHpOpacityPercent = persisted.GroupFrameHpOpacityPercent;
            GroupFrameAuraOpacityPercent = persisted.GroupFrameAuraOpacityPercent;
            GroupFrameArrowOpacityPercent = persisted.GroupFrameArrowOpacityPercent;
            GroupFrameDistanceOpacityPercent = persisted.GroupFrameDistanceOpacityPercent;
            GroupFrameAlertEnabled = persisted.GroupFrameAlertEnabled;
            GroupFrameAlertVolumePercent = persisted.GroupFrameAlertVolumePercent;
            GroupFrameAlertQuietSeconds = persisted.GroupFrameAlertQuietSeconds;
            GroupFrameAlertSound = persisted.GroupFrameAlertSound;
            GroupFrameAlertSounds = [.. persisted.GroupFrameAlertSounds];
            UpdateAutomatically = persisted.UpdateAutomatically;
            LookUpDigitalTwins = persisted.LookUpDigitalTwins;
            UpdateMyselfOnStart = persisted.UpdateMyselfOnStart;
            UseSeenSkillLevels = persisted.UseSeenSkillLevels;
            TwinLookupPauseSeconds = persisted.TwinLookupPauseSeconds;
            BattleResetHotkey =CreateHotkey(persisted.BattleResetHotkeyModifiers, persisted.BattleResetHotkeyVirtualKey);
            OverlayInteractionHotkey = CreateHotkey(persisted.OverlayInteractionHotkeyModifiers, persisted.OverlayInteractionHotkeyVirtualKey);
            if (BattleResetHotkey is not null && BattleResetHotkey == OverlayInteractionHotkey)
            {
                OverlayInteractionHotkey = null;
            }
            MinimizeAllHotkey = CreateHotkey(persisted.MinimizeAllHotkeyModifiers, persisted.MinimizeAllHotkeyVirtualKey);
            if (MinimizeAllHotkey is not null && (MinimizeAllHotkey == BattleResetHotkey || MinimizeAllHotkey == OverlayInteractionHotkey))
            {
                MinimizeAllHotkey = null;
            }
            LoadScenes(persisted);
            LoadWorkflows(persisted);
            TutorialDone = persisted.TutorialDone;
            BossParticipationWarning = persisted.BossParticipationWarning;
            BossParticipationPercent = persisted.BossParticipationPercent;
        }
        finally
        {
            _isApplyingPersistedSettings = false;
        }

        foreach (var action in AllHotkeyActions())
        {
            if (!_globalHotkeyService.TrySetHotkey(action, GetHotkey(action)))
                SetHotkeyValue(action, null);
        }

        RebuildLanguageOptions();
        RefreshWorkflowTriggerLabels();
        SelectedLanguage = Languages.FirstOrDefault(x => string.Equals(x.Code, _languageService.CurrentLanguage, StringComparison.Ordinal));

        _processForegroundWatcher.ForegroundChanged += OnForegroundChanged;
        _updateService.PropertyChanged += OnUpdatePropertyChanged;
        Localization.LanguageChanged += OnLocalizationLanguageChanged;
    }

    public LocalizationService Localization { get; }

    public ObservableCollection<LanguageOption> Languages { get; } = [];

    public IReadOnlyList<TopmostMode> TopmostModeOptions { get; } = [TopmostMode.GameForeground, TopmostMode.Always, TopmostMode.Never];

    public IReadOnlyList<int> RowCountOptions { get; } = [5, 6, 7, 8, 9, 10];

    public IReadOnlyList<CombatantSortMetric> CombatantSortMetricOptions { get; } = Enum.GetValues<CombatantSortMetric>();

    public IReadOnlyList<CombatantStatisticsScope> CombatantStatisticsScopeOptions { get; } = [CombatantStatisticsScope.Self, CombatantStatisticsScope.Party, CombatantStatisticsScope.Force, CombatantStatisticsScope.All];

    public IReadOnlyList<SceneKind> SceneKindOptions { get; } = [SceneKind.Standard, SceneKind.Boss];

    public IReadOnlyList<EncounterTimeDisplayFormat> EncounterTimeDisplayFormatOptions { get; } =
        [EncounterTimeDisplayFormat.DecimalSeconds, EncounterTimeDisplayFormat.MinutesSeconds];

    public IReadOnlyList<PlayerSelfMarkerDisplayMode> PlayerSelfMarkerDisplayModeOptions { get; } = [PlayerSelfMarkerDisplayMode.Always, PlayerSelfMarkerDisplayMode.WhenNamesHidden, PlayerSelfMarkerDisplayMode.Hidden];

    public IReadOnlyList<int> BossTimeLimitOptions { get; } = [0, 3, 4, 5, 6, 8, 10, 15];

    public IReadOnlyList<int> GroupFrameDistanceOptions { get; } = [5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlwaysOnTop))]
    [NotifyPropertyChangedFor(nameof(TopmostModeDisplay))]
    public partial TopmostMode TopmostMode { get; set; } = TopmostMode.GameForeground;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlwaysOnTop))]
    public partial bool IsTopMost { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxVisibleCombatantRowsDisplay))]
    public partial int MaxVisibleCombatantRows { get; set; } = 5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CombatantSortMetricDisplay))]
    public partial CombatantSortMetric CombatantSortMetric { get; set; } = CombatantSortMetric.DamagePerSecond;

    // The meter's rows the highest first, or (on) the lowest first.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CombatantSortOrderDisplay), nameof(SortArrow))]
    public partial bool CombatantSortAscending { get; set; }

    // Beside the sorted column's head, as in the fight graphs' runs tables.
    public string SortArrow => CombatantSortAscending ? " ▴" : " ▾";

    // A click on a column's head: the rows go by that column, the highest first; a click on the column they go by already
    // turns the order round.
    [RelayCommand]
    private void SortBy(CombatantSortMetric metric)
    {
        if (CombatantSortMetric == metric)
            CombatantSortAscending = !CombatantSortAscending;
        else
            (CombatantSortMetric, CombatantSortAscending) = (metric, false);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CombatantStatisticsScopeDisplay))]
    public partial CombatantStatisticsScope CombatantStatisticsScope { get; set; } = CombatantStatisticsScope.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SceneKindDisplay))]
    public partial SceneKind SceneKind { get; set; } = SceneKind.Standard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseCompactMainMetricsDisplay))]
    public partial bool UseCompactMainMetrics { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDamagePerSecondColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial bool ShowDamagePerSecondColumn { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDamageColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial bool ShowDamageColumn { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHealingPerSecondColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial bool ShowHealingPerSecondColumn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHealingColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial bool ShowHealingColumn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IncludeOverhealingDisplay))]
    public partial bool IncludeOverhealing { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDamageTakenColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial bool ShowDamageTakenColumn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUnhealedHealthColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial bool ShowUnhealedHealthColumn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAggroColumn))]
    [NotifyPropertyChangedFor(nameof(ShowAggroColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial AggroColumnMode AggroColumn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRecoveryTimeColumn))]
    [NotifyPropertyChangedFor(nameof(ShowRecoveryTimeColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial AggroColumnMode RecoveryTimeColumn { get; set; }

    // The player's character's class, from the live meter (not saved: the game says it again within seconds of a fight).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAggroColumn))]
    [NotifyPropertyChangedFor(nameof(ShowAggroColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(ShowRecoveryTimeColumn))]
    [NotifyPropertyChangedFor(nameof(ShowRecoveryTimeColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial CharacterClass? LocalPlayerClass { get; set; }

    // Tanks want the boss's attention, like in the best runs' aggro (BestRunKeys.HigherIsBetter): Templars and Gladiators.
    public bool ShowAggroColumn => AggroColumn switch
    {
        AggroColumnMode.Show => true,
        AggroColumnMode.Hide => false,
        _ => LocalPlayerClass is CharacterClass.Templar or CharacterClass.Gladiator,
    };

    // A healer's figure, like UHS: Auto shows it for Clerics.
    public bool ShowRecoveryTimeColumn => RecoveryTimeColumn switch
    {
        AggroColumnMode.Show => true,
        AggroColumnMode.Hide => false,
        _ => LocalPlayerClass is CharacterClass.Cleric,
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStaggerColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial bool ShowStaggerColumn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBossShareColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial bool ShowBossShareColumn { get; set; } = true;

    [ObservableProperty]
    public partial bool BossSectionExpanded { get; set; } = true;

    // "Add boss sum row": the Sum row under each boss fight's players (BossFightViewModel.ShowSum).
    [ObservableProperty]
    public partial bool ShowBossSumRow { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BossTimeLimitDisplay))]
    public partial int BossTimeLimitMinutes { get; set; }

    // A training dummy pull's length (0: until you stop) and the pause that ends one (see TrainingDummyRules).
    [ObservableProperty]
    public partial int DummyPullSeconds { get; set; }

    [ObservableProperty]
    public partial int DummyCutoffSeconds { get; set; } = 5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTotalDamagePerSecondDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial bool ShowTotalDamagePerSecond { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowColumnHeadersDisplay))]
    public partial bool ShowColumnHeaders { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EncounterTimeDisplayFormatDisplay))]
    public partial EncounterTimeDisplayFormat EncounterTimeDisplayFormat { get; set; } = EncounterTimeDisplayFormat.DecimalSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFocusStatusBarDisplay))]
    public partial bool ShowFocusStatusBar { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlayerNamesDisplay))]
    [NotifyPropertyChangedFor(nameof(PlayerNameSettingsDisplay))]
    public partial bool ShowPlayerNames { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerSelfMarkerDisplayModeDisplay))]
    public partial PlayerSelfMarkerDisplayMode PlayerSelfMarkerDisplayMode { get; set; } = PlayerSelfMarkerDisplayMode.WhenNamesHidden;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlayerShortServerNameDisplay))]
    public partial bool ShowPlayerShortServerName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlayerLegionNameDisplay))]
    public partial bool ShowPlayerLegionName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TintPlayerNamesByFactionDisplay))]
    public partial bool TintPlayerNamesByFaction { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SkillMonitorEnabledDisplay))]
    public partial bool SkillMonitorEnabled { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GroupFrameEnabledDisplay))]
    public partial bool GroupFrameEnabled { get; set; }

    // The fight graphs frame (see FightGraphsViewModel): shown while on; a curve per party member; the other members'
    // deaths marked too; the overview above the metric graphs.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FightGraphsEnabledDisplay))]
    public partial bool FightGraphsEnabled { get; set; }

    [ObservableProperty]
    public partial bool FightGraphsShowMembers { get; set; }

    [ObservableProperty]
    public partial bool FightGraphsShowOtherDeaths { get; set; }

    // The graphs' curves drawn smooth (an average of the seconds around each).
    [ObservableProperty]
    public partial bool FightGraphsSmoothCurves { get; set; }

    [ObservableProperty]
    public partial bool FightGraphsShowOverview { get; set; } = true;

    // The folded frame's title line names the metrics the shown pull set new personal bests in.
    [ObservableProperty]
    public partial bool FightGraphsNewBestsInTitle { get; set; } = true;

    // Saved once its slider is let go (SaveOverlayOpacity), like the other sliders.
    [ObservableProperty]
    public partial int FightGraphsScalePercent { get; set; } = 100;

    // The metric that picks a boss's best pull (the graphs' dotted line).
    [ObservableProperty]
    public partial Cloris.Aion2Flow.SceneRuntime.Stores.FightMetric FightGraphsBestMetric { get; set; }

    // The overlay's frames: those in ClickableWithoutFocus take clicks while the game has focus, the others let the mouse
    // through until the overlay has focus, except over the title line of those in TitleClickable (a click there gives the
    // overlay focus).
    public const string GroupFrameKey = "GroupFrame";
    public const string FightGraphsKey = "FightGraphs";
    public const string SkillMonitorKey = "SkillMonitor";
    public const string TwinLibraryKey = "TwinLibrary";
    public const string PlaybackKey = "Playback";
    public const string MeterKey = "Meter";
    public const string HiddenCubesKey = "HiddenCubes";
    public static readonly string[] ClickableFrames = [MeterKey, GroupFrameKey, FightGraphsKey, SkillMonitorKey, HiddenCubesKey, TwinLibraryKey, PlaybackKey];

    [ObservableProperty]
    public partial IReadOnlyList<string> ClickableWithoutFocus { get; set; } = [TwinLibraryKey, PlaybackKey, MeterKey];

    public bool IsClickableWithoutFocus(string frame) => ClickableWithoutFocus.Contains(frame);

    public void ToggleClickableWithoutFocus(string frame) => ClickableWithoutFocus = WithFrame(ClickableWithoutFocus, frame, !IsClickableWithoutFocus(frame));

    partial void OnClickableWithoutFocusChanged(IReadOnlyList<string> value) => PersistSettings();

    // "Title line always clickable": the meter's at first.
    [ObservableProperty]
    public partial IReadOnlyList<string> TitleClickable { get; set; } = [MeterKey];

    public bool IsTitleClickable(string frame) => TitleClickable.Contains(frame);

    public void ToggleTitleClickable(string frame) => TitleClickable = WithFrame(TitleClickable, frame, !IsTitleClickable(frame));

    partial void OnTitleClickableChanged(IReadOnlyList<string> value) => PersistSettings();

    // The frames with that one in or out; the same list when it already is (no change to report then).
    private static IReadOnlyList<string> WithFrame(IReadOnlyList<string> frames, string frame, bool with) =>
        frames.Contains(frame) == with ? frames : with ? [.. frames, frame] : [.. frames.Where(other => other != frame)];

    // The tutorial ran to its end once: until then the meter shows its "?".
    [ObservableProperty]
    public partial bool TutorialDone { get; set; }

    partial void OnTutorialDoneChanged(bool value) => PersistSettings();

    // Settings → General's "Start the tutorial": the meter runs it.
    public event Action? TutorialWanted;

    public void StartTutorial() => TutorialWanted?.Invoke();

    // While the tour runs no workflow applies a scene, nor does a scene's shortcut.
    public bool TutorialRunning { get; set; }

    // The group frame's loot bag: the player's share of the boss fight (the meter updates it with every live frame).
    public BossParticipationViewModel BossParticipation { get; }

    // Whether the loot bag warns at all.
    [ObservableProperty]
    public partial bool BossParticipationWarning { get; set; } = true;

    partial void OnBossParticipationWarningChanged(bool value)
    {
        BossParticipation.Refresh();
        PersistSettings();
    }

    // The share of a boss its loot needs, in percent; saved once its slider is let go, like the other sliders.
    [ObservableProperty]
    public partial double BossParticipationPercent { get; set; } = 3.5;

    partial void OnBossParticipationPercentChanged(double value) => BossParticipation.Refresh();

    // The frames with an eye: each folds down to its name and the eye; the shortcut does all of them at once.
    public static readonly string[] Frames = [MeterKey, GroupFrameKey, SkillMonitorKey, FightGraphsKey, HiddenCubesKey, TwinLibraryKey];

    [ObservableProperty]
    public partial IReadOnlyList<string> MinimizedFrames { get; set; } = [];

    public bool IsMinimized(string frame) => MinimizedFrames.Contains(frame);

    public void ToggleMinimized(string frame) =>
        MinimizedFrames = IsMinimized(frame) ? [.. MinimizedFrames.Where(other => other != frame)] : [.. MinimizedFrames, frame];

    // With every frame minimized all come back, otherwise all are minimized.
    public void ToggleMinimizeAll() => MinimizedFrames = Frames.All(IsMinimized) ? [] : [.. Frames];

    partial void OnMinimizedFramesChanged(IReadOnlyList<string> value) => PersistSettings();

    // Each frame's own opacity and size (Visuals), saved once their slider is let go (SaveOverlayOpacity).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FightGraphsOpacity))]
    public partial int FightGraphsOpacityPercent { get; set; } = 100;

    public double FightGraphsOpacity => FightGraphsOpacityPercent / 100d;

    // The skill monitor goes by the saved scale (SkillMonitorWindow), so it follows once the slider is let go.
    [ObservableProperty]
    public partial int SkillMonitorScalePercent { get; set; } = 100;

    [ObservableProperty]
    public partial int SkillMonitorOpacityPercent { get; set; } = 100;

    [ObservableProperty]
    public partial int TwinLibraryScalePercent { get; set; } = 100;

    [ObservableProperty]
    public partial int TwinLibraryOpacityPercent { get; set; } = 100;

    // The settings window's tab; a frame's gear opens it at the frame's.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab), nameof(IsVisualsTab), nameof(IsAudioTab), nameof(IsMeterTab), nameof(IsGroupFrameTab),
        nameof(IsSkillMonitorTab), nameof(IsFightGraphsTab), nameof(IsTwinLibraryTab), nameof(IsSystemTab), nameof(IsShortcutsTab), nameof(IsHiddenCubesTab),
        nameof(IsScenesTab), nameof(IsWorkflowsTab))]
    public partial SettingsTab Tab { get; set; }

    public bool IsGeneralTab { get => Tab == SettingsTab.General; set => ShowTab(SettingsTab.General, value); }

    public bool IsVisualsTab { get => Tab == SettingsTab.Visuals; set => ShowTab(SettingsTab.Visuals, value); }

    public bool IsAudioTab { get => Tab == SettingsTab.Audio; set => ShowTab(SettingsTab.Audio, value); }

    public bool IsMeterTab { get => Tab == SettingsTab.Meter; set => ShowTab(SettingsTab.Meter, value); }

    public bool IsGroupFrameTab { get => Tab == SettingsTab.GroupFrame; set => ShowTab(SettingsTab.GroupFrame, value); }

    public bool IsSkillMonitorTab { get => Tab == SettingsTab.SkillMonitor; set => ShowTab(SettingsTab.SkillMonitor, value); }

    public bool IsFightGraphsTab { get => Tab == SettingsTab.FightGraphs; set => ShowTab(SettingsTab.FightGraphs, value); }

    public bool IsTwinLibraryTab { get => Tab == SettingsTab.TwinLibrary; set => ShowTab(SettingsTab.TwinLibrary, value); }

    public bool IsSystemTab { get => Tab == SettingsTab.System; set => ShowTab(SettingsTab.System, value); }

    public bool IsScenesTab { get => Tab == SettingsTab.Scenes; set => ShowTab(SettingsTab.Scenes, value); }

    public bool IsWorkflowsTab { get => Tab == SettingsTab.Workflows; set => ShowTab(SettingsTab.Workflows, value); }

    public bool IsShortcutsTab { get => Tab == SettingsTab.Shortcuts; set => ShowTab(SettingsTab.Shortcuts, value); }

    public bool IsHiddenCubesTab { get => Tab == SettingsTab.HiddenCubes; set => ShowTab(SettingsTab.HiddenCubes, value); }

    // The settings window's top left corner, saved apart from the rest.
    public System.Drawing.Point? SettingsWindowPosition
    {
        get => _settingsService.Current.SettingsWindowPosition;
        set => _settingsService.Update(s => s.SettingsWindowPosition = value);
    }

    // Clicking the tab shown again leaves it shown.
    private void ShowTab(SettingsTab tab, bool show, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (show)
            Tab = tab;
        else if (Tab == tab)
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(property));
    }

    partial void OnFightGraphsBestMetricChanged(Cloris.Aion2Flow.SceneRuntime.Stores.FightMetric value) => PersistSettings();

    [ObservableProperty]
    public partial int UiScalePercent { get; set; }

    // The overlay follows the slider while it moves; SaveOverlayOpacity stores it once the slider is let go. 60% is the
    // original look (see-through panel, full-strength text): above it the panel fills in until it is solid at 100%,
    // below it the whole overlay fades, down to barely visible at 20%.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverlayContentOpacity))]
    [NotifyPropertyChangedFor(nameof(OverlayBackdropFill))]
    public partial int OverlayOpacityPercent { get; set; } = 60;

    public double OverlayContentOpacity => ContentOpacity(OverlayOpacityPercent);

    public double OverlayBackdropFill => BackdropFill(OverlayOpacityPercent);

    // The same for a player's details panel, applied each time it opens.
    [ObservableProperty]
    public partial int DetailOpacityPercent { get; set; } = 60;

    public double DetailContentOpacity => ContentOpacity(DetailOpacityPercent);

    public double DetailBackdropFill => BackdropFill(DetailOpacityPercent);

    // The group frame's own size, apart from the UI scale; the frame follows the slider while it moves.
    [ObservableProperty]
    public partial int GroupFrameScalePercent { get; set; } = 100;

    // Which auras the group frame shows next to each member.
    [ObservableProperty]
    public partial bool GroupFrameShowMyBuffs { get; set; } = true;

    [ObservableProperty]
    public partial bool GroupFrameShowDebuffs { get; set; } = true;

    [ObservableProperty]
    public partial bool GroupFrameShowOtherBuffs { get; set; }

    // Buffs without a timer (no duration, or more than an hour); debuffs always show.
    [ObservableProperty]
    public partial bool GroupFrameShowPermanentBuffs { get; set; } = true;

    // The shield bar from the right instead of the left.
    [ObservableProperty]
    public partial bool GroupFrameShieldOnRight { get; set; }

    // Each shield on its own, blinking for its last 3 s.
    [ObservableProperty]
    public partial bool GroupFrameShieldAnimation { get; set; } = true;

    // The distance beside each member's arrow: green up to this, orange up to the next, red up to the range; past the
    // range the member is dimmed as out of range.
    [ObservableProperty]
    public partial int GroupFrameGreenMeters { get; set; } = 25;

    [ObservableProperty]
    public partial int GroupFrameOrangeMeters { get; set; } = 35;

    [ObservableProperty]
    public partial int GroupFrameRangeMeters { get; set; } = 40;

    // Each member's deaths in the current boss fight beside their name.
    [ObservableProperty]
    public partial bool GroupFrameShowDeaths { get; set; } = true;

    // The Hidden Cube finder (experimental), a frame of its own: a line per cube the server announced, with direction and
    // distance, and with the sound on a blip when a new one comes into range.
    [ObservableProperty]
    public partial bool HiddenCubesEnabled { get; set; }

    [ObservableProperty]
    public partial bool HiddenCubesSound { get; set; }

    [ObservableProperty]
    public partial int HiddenCubesScalePercent { get; set; } = 100;

    [ObservableProperty]
    public partial int HiddenCubesOpacityPercent { get; set; } = 100;

    // Where each member's auras, arrow and distance go: right of the HP bar, mirrored to its left, or off.
    [ObservableProperty]
    public partial GroupFrameSide GroupFrameAuraSide { get; set; }

    [ObservableProperty]
    public partial GroupFrameSide GroupFrameArrowSide { get; set; }

    [ObservableProperty]
    public partial GroupFrameSide GroupFrameDistanceSide { get; set; }

    // How see-through the group frame's HP bars, auras, arrows and distances are: 100% is their own look. The frame
    // follows a slider while it moves; SaveOverlayOpacity stores it once the slider is let go.
    [ObservableProperty]
    public partial int GroupFrameHpOpacityPercent { get; set; } = 100;

    [ObservableProperty]
    public partial int GroupFrameAuraOpacityPercent { get; set; } = 100;

    [ObservableProperty]
    public partial int GroupFrameArrowOpacityPercent { get; set; } = 100;

    [ObservableProperty]
    public partial int GroupFrameDistanceOpacityPercent { get; set; } = 100;

    // The boss alert: a sound when the boss turns to you (the group frame's red banner), the chime (null) or one of the
    // user's own sounds, newest first. The volume is saved once its slider is let go.
    [ObservableProperty]
    public partial bool GroupFrameAlertEnabled { get; set; }

    [ObservableProperty]
    public partial int GroupFrameAlertVolumePercent { get; set; } = 100;

    // No new alert this many seconds after the last (0-10); saved like the volume, once its slider is let go.
    [ObservableProperty]
    public partial int GroupFrameAlertQuietSeconds { get; set; } = 5;

    [ObservableProperty]
    public partial string? GroupFrameAlertSound { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> GroupFrameAlertSounds { get; set; } = [];

    public AlertSounds AlertSounds { get; }

    // The player's character as last named by the game (see AppSettings.LocalPlayerServerId).
    public (int ServerId, string Nickname)? RememberedLocalPlayer =>
        _settingsService.Current is { LocalPlayerServerId: > 0 and var serverId, LocalPlayerNickname: { Length: > 0 } nickname } ? (serverId, nickname) : null;

    public void RememberLocalPlayer(int serverId, string nickname)
    {
        if (RememberedLocalPlayer != (serverId, nickname))
            _settingsService.Update(s => (s.LocalPlayerServerId, s.LocalPlayerNickname) = (serverId, nickname));
    }

    // Updates from the overlay's release page (only an install from a release package has them): on, a check and
    // download at every start and the install when the overlay closes; off, only "Update now".
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateAutomaticallyDisplay))]
    public partial bool UpdateAutomatically { get; set; }

    public string UpdateAutomaticallyDisplay => Localization[UpdateAutomatically ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"];

    // Digital twins: on, party members are looked up on the official character website as they show up in the meter (off,
    // only one at a time from the details' Digital twin tab).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LookUpDigitalTwinsDisplay))]
    public partial bool LookUpDigitalTwins { get; set; }

    public string LookUpDigitalTwinsDisplay => Localization[LookUpDigitalTwins ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"];

    partial void OnLookUpDigitalTwinsChanged(bool value) => PersistSettings();

    // The twin library's "Update myself on overlay start", also a row of its tab here (see MainViewModel.LookUpMyselfOnce).
    [ObservableProperty]
    public partial bool UpdateMyselfOnStart { get; set; }

    partial void OnUpdateMyselfOnStartChanged(bool value) => PersistSettings();

    // "Use last battle's skill levels": a row of the Digital twin library tab here, and a check box at the top of the
    // library's twin and comparison tabs and of the details' Digital twin tab (kept in step by the main view model).
    [ObservableProperty]
    public partial bool UseSeenSkillLevels { get; set; } = true;

    partial void OnUseSeenSkillLevelsChanged(bool value) => PersistSettings();

    // The pause before each call to the official character website, in seconds (see DigitalTwinService.RequestGap); saved
    // once its slider is let go, like the other sliders.
    [ObservableProperty]
    public partial double TwinLookupPauseSeconds { get; set; } = 0.7;

    public void PlayAlertSound() => AlertSounds.Play(GroupFrameAlertSound, GroupFrameAlertVolumePercent);

    // A sound from the alert editor: saved, first in the list, and the one the alert plays from now on.
    public void AddAlertSound(AudioClip clip, string name)
    {
        var sounds = GroupFrameAlertSounds.ToList();
        var added = AlertSounds.Add(clip, name, sounds);
        GroupFrameAlertSounds = sounds;
        GroupFrameAlertSound = added;
    }

    private static double ContentOpacity(int percent) => Math.Clamp((percent - 10) / 50d, 0.2, 1);

    // How far the panel's see-through backdrop is filled in towards solid.
    private static double BackdropFill(int percent) => Math.Clamp((percent - 60) / 40d, 0, 1);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LanguageDisplay))]
    public partial LanguageOption? SelectedLanguage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResetHotkeyDisplay))]
    [NotifyPropertyChangedFor(nameof(HasResetHotkey))]
    public partial HotkeyDefinition? BattleResetHotkey { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResetHotkeyDisplay))]
    [NotifyPropertyChangedFor(nameof(OverlayInteractionHotkeyDisplay))]
    [NotifyPropertyChangedFor(nameof(MinimizeAllHotkeyDisplay))]
    [NotifyPropertyChangedFor(nameof(IsCapturingResetHotkey))]
    [NotifyPropertyChangedFor(nameof(IsCapturingOverlayInteractionHotkey))]
    public partial GlobalHotkeyAction? CapturingHotkeyAction { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverlayInteractionHotkeyDisplay))]
    [NotifyPropertyChangedFor(nameof(HasOverlayInteractionHotkey))]
    public partial HotkeyDefinition? OverlayInteractionHotkey { get; private set; }

    // Minimizes every frame, or brings them all back (see ToggleMinimizeAll).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinimizeAllHotkeyDisplay))]
    [NotifyPropertyChangedFor(nameof(HasMinimizeAllHotkey))]
    public partial HotkeyDefinition? MinimizeAllHotkey { get; private set; }

    public string MinimizeAllHotkeyDisplay => CapturingHotkeyAction == GlobalHotkeyAction.MinimizeAllFrames
        ? Localization["Settings_Hotkey_PressKeys"]
        : MinimizeAllHotkey?.Display ?? Localization["Settings_Hotkey_None"];

    public bool HasMinimizeAllHotkey => MinimizeAllHotkey is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyRegistrationErrorText))]
    public partial bool HasHotkeyRegistrationError { get; private set; }

    public string ResetHotkeyDisplay
    {
        get
        {
            if (IsCapturingResetHotkey)
            {
                return Localization["Settings_Hotkey_PressKeys"];
            }
            return BattleResetHotkey?.Display ?? Localization["Settings_Hotkey_None"];
        }
    }

    public bool HasResetHotkey => BattleResetHotkey is not null;

    public bool IsCapturingResetHotkey => CapturingHotkeyAction == GlobalHotkeyAction.BattleReset;

    public string OverlayInteractionHotkeyDisplay
    {
        get
        {
            if (IsCapturingOverlayInteractionHotkey)
            {
                return Localization["Settings_Hotkey_PressKeys"];
            }
            return OverlayInteractionHotkey?.Display ?? Localization["Settings_Hotkey_None"];
        }
    }

    public bool HasOverlayInteractionHotkey => OverlayInteractionHotkey is not null;

    public bool IsCapturingOverlayInteractionHotkey => CapturingHotkeyAction == GlobalHotkeyAction.CycleOverlayInteraction;

    public string HotkeyRegistrationErrorText => Localization["Settings_Hotkey_RegistrationUnavailable"];

    public bool IsAlwaysOnTop => TopmostMode switch
    {
        TopmostMode.Always => true,
        TopmostMode.Never => false,
        _ => IsTopMost
    };

    public string TopmostModeDisplay => Localization[$"Settings_Topmost_{TopmostMode}"];

    internal void RefreshTargetProcessForegroundState() => IsTopMost = _processForegroundWatcher.IsTargetProcessForeground();

    public string MaxVisibleCombatantRowsDisplay => MaxVisibleCombatantRows.ToString();

    public string CombatantSortMetricDisplay => Localization[$"Settings_CombatantSortMetric_{CombatantSortMetric}"];

    public string CombatantSortOrderDisplay => Localization[CombatantSortAscending ? "Settings_CombatantSortOrder_Ascending" : "Settings_CombatantSortOrder_Descending"];

    public string CombatantStatisticsScopeDisplay => Localization[$"Settings_CombatantStatisticsScope_{CombatantStatisticsScope}"];

    public string SceneKindDisplay => Localization[$"Settings_SceneKind_{SceneKind}"];

    public string UseCompactMainMetricsDisplay => Localization[UseCompactMainMetrics ? "Settings_MainMetricsCompact_On" : "Settings_MainMetricsCompact_Off"];

    public string ShowDamagePerSecondColumnDisplay => ResolveMainMetricVisibilityDisplay(ShowDamagePerSecondColumn);

    public string ShowDamageColumnDisplay => ResolveMainMetricVisibilityDisplay(ShowDamageColumn);

    public string ShowHealingPerSecondColumnDisplay => ResolveMainMetricVisibilityDisplay(ShowHealingPerSecondColumn);

    public string ShowHealingColumnDisplay => ResolveMainMetricVisibilityDisplay(ShowHealingColumn);

    public string IncludeOverhealingDisplay => ResolveMainMetricVisibilityDisplay(IncludeOverhealing);

    public string ShowDamageTakenColumnDisplay => ResolveMainMetricVisibilityDisplay(ShowDamageTakenColumn);

    public string ShowUnhealedHealthColumnDisplay => ResolveMainMetricVisibilityDisplay(ShowUnhealedHealthColumn);

    public string ShowAggroColumnDisplay => AggroColumn == AggroColumnMode.Auto ? Localization["Settings_MainMetricVisibility_Auto"] : ResolveMainMetricVisibilityDisplay(ShowAggroColumn);

    public string ShowRecoveryTimeColumnDisplay => RecoveryTimeColumn == AggroColumnMode.Auto ? Localization["Settings_MainMetricVisibility_Auto"] : ResolveMainMetricVisibilityDisplay(ShowRecoveryTimeColumn);

    public string ShowStaggerColumnDisplay => ResolveMainMetricVisibilityDisplay(ShowStaggerColumn);

    public string ShowBossShareColumnDisplay => ResolveMainMetricVisibilityDisplay(ShowBossShareColumn);

    public string ShowTotalDamagePerSecondDisplay => ResolveMainMetricVisibilityDisplay(ShowTotalDamagePerSecond);

    public string ShowColumnHeadersDisplay => ResolveMainMetricVisibilityDisplay(ShowColumnHeaders);

    public string MainMetricVisibilityDisplay => $"{Convert.ToInt32(ShowDamagePerSecondColumn) + Convert.ToInt32(ShowDamageColumn) + Convert.ToInt32(ShowHealingPerSecondColumn) + Convert.ToInt32(ShowHealingColumn) + Convert.ToInt32(ShowDamageTakenColumn) + Convert.ToInt32(ShowUnhealedHealthColumn) + Convert.ToInt32(ShowRecoveryTimeColumn) + Convert.ToInt32(ShowAggroColumn) + Convert.ToInt32(ShowStaggerColumn) + Convert.ToInt32(ShowBossShareColumn) + Convert.ToInt32(ShowTotalDamagePerSecond)}/11";

    public string BossTimeLimitDisplay => FormatBossTimeLimit(BossTimeLimitMinutes);

    public string FormatBossTimeLimit(int minutes) => minutes > 0 ? $"{minutes}:00" : Localization["Settings_BossTimeLimit_Off"];

    public static IReadOnlyList<int> DummyPullOptions { get; } = [0, 30, 60, 120, 180, 300];

    public Cloris.Aion2Flow.SceneRuntime.Stores.TrainingDummyRules TrainingDummyRules => new(DummyPullSeconds * 1_000L, DummyCutoffSeconds * 1_000L);

    public string FormatDummyPull(int seconds) => seconds > 0 ? $"{seconds / 60}:{seconds % 60:00}" : Localization["Settings_DummyPull_Off"];

    public static string FormatMeters(int meters) => $"{meters} m";

    public string EncounterTimeDisplayFormatDisplay => Localization[$"Settings_TimeDisplayFormat_{EncounterTimeDisplayFormat}"];

    public string ShowFocusStatusBarDisplay => Localization[ShowFocusStatusBar ? "Settings_FocusStatusBar_On" : "Settings_FocusStatusBar_Off"];

    public string ShowPlayerNamesDisplay => Localization[ShowPlayerNames ? "Settings_ShowPlayerNames_On" : "Settings_ShowPlayerNames_Off"];

    public string PlayerSelfMarkerDisplayModeDisplay => Localization[$"Settings_PlayerSelfMarker_{PlayerSelfMarkerDisplayMode}"];

    public string ShowPlayerShortServerNameDisplay => Localization[ShowPlayerShortServerName ? "Settings_PlayerShortServerName_On" : "Settings_PlayerShortServerName_Off"];

    public string ShowPlayerLegionNameDisplay => Localization[ShowPlayerLegionName ? "Settings_PlayerLegionName_On" : "Settings_PlayerLegionName_Off"];

    public string TintPlayerNamesByFactionDisplay => Localization[TintPlayerNamesByFaction ? "Settings_PlayerFactionTint_On" : "Settings_PlayerFactionTint_Off"];

    public string SkillMonitorEnabledDisplay => Localization[SkillMonitorEnabled ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"];

    public string GroupFrameEnabledDisplay => Localization[GroupFrameEnabled ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"];

    public string FightGraphsEnabledDisplay => Localization[FightGraphsEnabled ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"];

    public string PlayerNameSettingsDisplay => ShowPlayerNamesDisplay;

    public string LanguageDisplay => SelectedLanguage?.DisplayName ?? string.Empty;

    public bool IsUpdateSectionVisible => _updateService.IsManagedByVelopack;

    public string? CurrentVersion => _updateService.CurrentVersion ?? BuildVersion;

    // The exe's own version when Velopack didn't install it (a local build's -p:Version), without the commit after the '+'.
    private static readonly string? BuildVersion = typeof(SettingsFlyoutViewModel).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];

    public AppUpdateState UpdateState => _updateService.State;

    public int DownloadProgress => _updateService.DownloadProgress;

    public string? AvailableVersion => _updateService.AvailableVersion;

    public string? UpdateStatusMessage => _updateService.StatusMessage;

    public bool IsCheckingUpdate => UpdateState == AppUpdateState.Checking;

    public bool IsDownloadingUpdate => UpdateState == AppUpdateState.Downloading;

    public bool CanCheckForUpdates => UpdateState is AppUpdateState.Idle or AppUpdateState.UpToDate or AppUpdateState.Failed;

    public bool CanRestartToUpdate => UpdateState == AppUpdateState.ReadyToRestart;

    public string UpdateStatusText => UpdateState switch
    {
        AppUpdateState.Checking => Localization["Settings_Update_Checking"],
        AppUpdateState.Downloading => string.Format(Localization["Settings_Update_DownloadingFormat"], DownloadProgress),
        AppUpdateState.UpToDate => Localization["Settings_Update_UpToDate"],
        AppUpdateState.ReadyToRestart => string.Format(Localization["Settings_Update_ReadyFormat"], AvailableVersion ?? string.Empty),
        AppUpdateState.Failed => Localization["Settings_Update_Failed"],
        _ => string.Empty
    };

    public string CurrentVersionText
    {
        get
        {
            var version = CurrentVersion;
            return string.IsNullOrWhiteSpace(version)
                ? string.Empty
                : string.Format(Localization["Settings_Update_CurrentVersionFormat"], version);
        }
    }

    // A downloaded update restarts into it; otherwise a check, then the download and the restart.
    public bool CanUpdateNow => CanCheckForUpdates || CanRestartToUpdate;

    [RelayCommand(CanExecute = nameof(CanUpdateNow))]
    private Task UpdateNowAsync() => _updateService.UpdateNowAsync();

    partial void OnTopmostModeChanged(TopmostMode value) => PersistSettings();

    partial void OnMaxVisibleCombatantRowsChanged(int value) => PersistSettings();

    partial void OnCombatantSortMetricChanged(CombatantSortMetric value) => PersistSettings();

    partial void OnCombatantSortAscendingChanged(bool value) => PersistSettings();

    partial void OnCombatantStatisticsScopeChanged(CombatantStatisticsScope value) => PersistSettings();

    partial void OnSceneKindChanged(SceneKind value) => PersistSettings();

    partial void OnUseCompactMainMetricsChanged(bool value) => PersistSettings();

    partial void OnShowDamagePerSecondColumnChanged(bool value) => PersistSettings();

    partial void OnShowDamageColumnChanged(bool value) => PersistSettings();

    partial void OnShowHealingPerSecondColumnChanged(bool value) => PersistSettings();

    partial void OnShowHealingColumnChanged(bool value) => PersistSettings();

    partial void OnIncludeOverhealingChanged(bool value) => PersistSettings();

    partial void OnShowDamageTakenColumnChanged(bool value) => PersistSettings();

    partial void OnShowUnhealedHealthColumnChanged(bool value) => PersistSettings();

    partial void OnAggroColumnChanged(AggroColumnMode value) => PersistSettings();

    partial void OnRecoveryTimeColumnChanged(AggroColumnMode value) => PersistSettings();

    partial void OnShowStaggerColumnChanged(bool value) => PersistSettings();

    partial void OnShowBossShareColumnChanged(bool value) => PersistSettings();

    partial void OnBossSectionExpandedChanged(bool value) => PersistSettings();

    partial void OnShowBossSumRowChanged(bool value) => PersistSettings();

    partial void OnBossTimeLimitMinutesChanged(int value) => PersistSettings();

    partial void OnDummyPullSecondsChanged(int value) => PersistSettings();

    partial void OnDummyCutoffSecondsChanged(int value) => PersistSettings();

    partial void OnShowTotalDamagePerSecondChanged(bool value) => PersistSettings();

    partial void OnShowColumnHeadersChanged(bool value) => PersistSettings();

    partial void OnEncounterTimeDisplayFormatChanged(EncounterTimeDisplayFormat value) => PersistSettings();

    partial void OnShowFocusStatusBarChanged(bool value) => PersistSettings();

    partial void OnShowPlayerNamesChanged(bool value)
    {
        _playerNameDisplay.ShowPlayerNames = value;
        PersistSettings();
    }

    partial void OnPlayerSelfMarkerDisplayModeChanged(PlayerSelfMarkerDisplayMode value)
    {
        _playerNameDisplay.SelfMarkerDisplayMode = value;
        PersistSettings();
    }

    partial void OnShowPlayerShortServerNameChanged(bool value)
    {
        _playerNameDisplay.ShowShortServerName = value;
        PersistSettings();
    }

    partial void OnShowPlayerLegionNameChanged(bool value)
    {
        _playerNameDisplay.ShowLegionName = value;
        PersistSettings();
    }

    partial void OnTintPlayerNamesByFactionChanged(bool value)
    {
        _playerNameDisplay.TintPlayerNamesByFaction = value;
        PersistSettings();
    }

    partial void OnSkillMonitorEnabledChanged(bool value) => PersistSettings();

    partial void OnGroupFrameEnabledChanged(bool value) => PersistSettings();

    partial void OnFightGraphsEnabledChanged(bool value) => PersistSettings();

    partial void OnFightGraphsShowMembersChanged(bool value) => PersistSettings();

    partial void OnFightGraphsShowOtherDeathsChanged(bool value) => PersistSettings();

    partial void OnFightGraphsSmoothCurvesChanged(bool value) => PersistSettings();

    partial void OnFightGraphsShowOverviewChanged(bool value) => PersistSettings();

    partial void OnFightGraphsNewBestsInTitleChanged(bool value) => PersistSettings();

    partial void OnGroupFrameShowMyBuffsChanged(bool value) => PersistSettings();

    partial void OnGroupFrameShowDebuffsChanged(bool value) => PersistSettings();

    partial void OnGroupFrameShowOtherBuffsChanged(bool value) => PersistSettings();

    partial void OnGroupFrameShowPermanentBuffsChanged(bool value) => PersistSettings();

    partial void OnGroupFrameShieldOnRightChanged(bool value) => PersistSettings();

    partial void OnGroupFrameShieldAnimationChanged(bool value) => PersistSettings();

    partial void OnGroupFrameGreenMetersChanged(int value) => PersistSettings();

    partial void OnGroupFrameOrangeMetersChanged(int value) => PersistSettings();

    partial void OnGroupFrameRangeMetersChanged(int value) => PersistSettings();

    partial void OnGroupFrameShowDeathsChanged(bool value) => PersistSettings();

    partial void OnHiddenCubesEnabledChanged(bool value) => PersistSettings();

    partial void OnHiddenCubesSoundChanged(bool value) => PersistSettings();

    partial void OnGroupFrameAuraSideChanged(GroupFrameSide value) => PersistSettings();

    partial void OnGroupFrameArrowSideChanged(GroupFrameSide value) => PersistSettings();

    partial void OnGroupFrameDistanceSideChanged(GroupFrameSide value) => PersistSettings();

    partial void OnGroupFrameAlertEnabledChanged(bool value) => PersistSettings();

    partial void OnGroupFrameAlertSoundChanged(string? value) => PersistSettings();

    partial void OnGroupFrameAlertSoundsChanged(IReadOnlyList<string> value) => PersistSettings();

    // Switched on, it starts right away rather than at the next start.
    partial void OnUpdateAutomaticallyChanged(bool value)
    {
        PersistSettings();
        if (value && !_isApplyingPersistedSettings)
            _ = _updateService.Start(automatic: true);
    }

    partial void OnUiScalePercentChanged(int value)
    {
        _uiScale.SetScalePercent(value);
        PersistSettings();
    }

    public void SetUiScalePercent(int percent)
    {
        if (UiScalePercent != percent)
            UiScalePercent = percent;
        else
            _uiScale.SetScalePercent(percent);
    }

    public void SaveOverlayOpacity() => PersistSettings();

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is not null)
        {
            _languageService.SetLanguage(value.Code);
            PersistSettings();
        }
    }

    public void BeginCaptureHotkey(GlobalHotkeyAction action)
    {
        ValidateAction(action);
        CapturingHotkeyAction = action;
    }

    // A deleted scene's row may still let go of the focus: its action needs no scene here.
    public void CancelCaptureHotkey(GlobalHotkeyAction action)
    {
        if (!GlobalHotkeyService.IsSupported(action))
            throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported global hotkey action.");
        if (CapturingHotkeyAction == action)
        {
            CapturingHotkeyAction = null;
        }
    }

    [RelayCommand]
    public void ClearHotkey(GlobalHotkeyAction action)
    {
        CancelCaptureHotkey(action);
        if (GetHotkey(action) is null)
        {
            return;
        }

        if (!_globalHotkeyService.TrySetHotkey(action, null))
        {
            HasHotkeyRegistrationError = true;
            return;
        }

        SetHotkeyValue(action, null);
        RefreshHotkeyRegistrationError();
        PersistSettings();
    }

    public bool ApplyCapturedHotkey(GlobalHotkeyAction action, HotkeyDefinition definition)
    {
        ValidateAction(action);
        ArgumentNullException.ThrowIfNull(definition);
        if (CapturingHotkeyAction != action)
        {
            return false;
        }

        CapturingHotkeyAction = null;
        // The same keys on another action move here (the service does so too).
        GlobalHotkeyAction? conflictingAction = null;
        foreach (var other in AllHotkeyActions())
        {
            if (other != action && definition == GetHotkey(other))
                conflictingAction = other;
        }

        var changed = definition != GetHotkey(action) || conflictingAction is not null;
        if (!_globalHotkeyService.TrySetHotkey(action, definition))
        {
            HasHotkeyRegistrationError = true;
            return false;
        }

        if (!changed)
        {
            RefreshHotkeyRegistrationError();
            return true;
        }

        if (conflictingAction is { } conflicting)
        {
            SetHotkeyValue(conflicting, null);
        }
        SetHotkeyValue(action, definition);
        RefreshHotkeyRegistrationError();
        PersistSettings();
        return true;
    }

    public void RefreshHotkeyRegistrationState(bool isRegistrationWindowAttached)
    {
        _hasObservedHotkeyRegistrationState = true;
        _isHotkeyRegistrationWindowAttached = isRegistrationWindowAttached;
        RefreshHotkeyRegistrationError();
    }

    private void SetHotkeyValue(GlobalHotkeyAction action, HotkeyDefinition? definition)
    {
        switch (action)
        {
            case GlobalHotkeyAction.BattleReset:
                BattleResetHotkey = definition;
                break;
            case GlobalHotkeyAction.CycleOverlayInteraction:
                OverlayInteractionHotkey = definition;
                break;
            case GlobalHotkeyAction.MinimizeAllFrames:
                MinimizeAllHotkey = definition;
                break;
            case GlobalHotkeyAction.SaveScene:
                SaveSceneHotkey = definition;
                break;
            default:
                if (!GlobalHotkeyService.TryGetSceneId(action, out var sceneId) || FindScene(sceneId) is not { } scene)
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported global hotkey action.");
                scene.Hotkey = definition;
                break;
        }
    }

    private HotkeyDefinition? GetHotkey(GlobalHotkeyAction action) => action switch
    {
        GlobalHotkeyAction.BattleReset => BattleResetHotkey,
        GlobalHotkeyAction.CycleOverlayInteraction => OverlayInteractionHotkey,
        GlobalHotkeyAction.MinimizeAllFrames => MinimizeAllHotkey,
        GlobalHotkeyAction.SaveScene => SaveSceneHotkey,
        _ => GlobalHotkeyService.TryGetSceneId(action, out var sceneId) && FindScene(sceneId) is { } scene
            ? scene.Hotkey
            : throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported global hotkey action.")
    };

    // A named action, or a scene's that's still there.
    private void ValidateAction(GlobalHotkeyAction action)
    {
        if (!Enum.IsDefined(action) && !(GlobalHotkeyService.TryGetSceneId(action, out var sceneId) && FindScene(sceneId) is not null))
            throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported global hotkey action.");
    }

    private static HotkeyDefinition? CreateHotkey(uint? modifiers, uint? virtualKey) =>
        modifiers is { } mods && virtualKey is { } vk
            ? HotkeyDefinition.TryCreate((HotkeyModifiers)mods, vk)
            : null;

    private void RefreshHotkeyRegistrationError()
    {
        if (_hasObservedHotkeyRegistrationState)
        {
            HasHotkeyRegistrationError =
                (!_isHotkeyRegistrationWindowAttached && (BattleResetHotkey is not null || OverlayInteractionHotkey is not null)) ||
                HasUnavailableRegisteredHotkey();
        }
        else
        {
            HasHotkeyRegistrationError = false;
        }
    }

    private bool HasUnavailableRegisteredHotkey() =>
        AllHotkeyActions().Any(action => GetHotkey(action) is not null && !_globalHotkeyService.IsRegistered(action));

    private void PersistSettings()
    {
        if (_isApplyingPersistedSettings)
        {
            return;
        }

        _settingsService.Update(s =>
        {
            s.TopmostMode = TopmostMode;
            s.MaxVisibleCombatantRows = MaxVisibleCombatantRows;
            s.CombatantSortMetric = CombatantSortMetric;
            s.CombatantSortAscending = CombatantSortAscending;
            s.CombatantStatisticsScope = CombatantStatisticsScope;
            s.SceneKind = SceneKind;
            s.UseCompactMainMetrics = UseCompactMainMetrics;
            s.ShowDamagePerSecondColumn = ShowDamagePerSecondColumn;
            s.ShowDamageColumn = ShowDamageColumn;
            s.ShowHealingPerSecondColumn = ShowHealingPerSecondColumn;
            s.ShowHealingColumn = ShowHealingColumn;
            s.IncludeOverhealing = IncludeOverhealing;
            s.ShowDamageTakenColumn = ShowDamageTakenColumn;
            s.ShowUnhealedHealthColumn = ShowUnhealedHealthColumn;
            s.ShowAggroColumn = ShowAggroColumn;
            s.AggroColumn = AggroColumn;
            s.RecoveryTimeColumn = RecoveryTimeColumn;
            s.ShowStaggerColumn = ShowStaggerColumn;
            s.ShowBossShareColumn = ShowBossShareColumn;
            s.ShowTotalDamagePerSecond = ShowTotalDamagePerSecond;
            s.ShowColumnHeaders = ShowColumnHeaders;
            s.BossSectionExpanded = BossSectionExpanded;
            s.ShowBossSumRow = ShowBossSumRow;
            s.BossTimeLimitMinutes = BossTimeLimitMinutes;
            s.DummyPullSeconds = DummyPullSeconds;
            s.DummyCutoffSeconds = DummyCutoffSeconds;
            s.EncounterTimeDisplayFormat = EncounterTimeDisplayFormat;
            s.ShowFocusStatusBar = ShowFocusStatusBar;
            s.ShowPlayerNames = ShowPlayerNames;
            s.PlayerSelfMarkerDisplayMode = PlayerSelfMarkerDisplayMode;
            s.ShowPlayerShortServerName = ShowPlayerShortServerName;
            s.ShowPlayerLegionName = ShowPlayerLegionName;
            s.TintPlayerNamesByFaction = TintPlayerNamesByFaction;
            s.SkillMonitorEnabled = SkillMonitorEnabled;
            s.GroupFrameEnabled = GroupFrameEnabled;
            s.FightGraphsEnabled = FightGraphsEnabled;
            s.FightGraphsShowMembers = FightGraphsShowMembers;
            s.FightGraphsShowOtherDeaths = FightGraphsShowOtherDeaths;
            s.FightGraphsSmoothCurves = FightGraphsSmoothCurves;
            s.FightGraphsShowOverview = FightGraphsShowOverview;
            s.FightGraphsNewBestsInTitle = FightGraphsNewBestsInTitle;
            s.FightGraphsScalePercent = FightGraphsScalePercent;
            s.FightGraphsBestMetric = FightGraphsBestMetric;
            s.ClickableWithoutFocus = [.. ClickableWithoutFocus];
            s.MeterClickableAdded = true;
            s.TitleClickable = [.. TitleClickable];
            s.FightGraphsOpacityPercent = FightGraphsOpacityPercent;
            s.SkillMonitorOpacityPercent = SkillMonitorOpacityPercent;
            s.TwinLibraryScalePercent = TwinLibraryScalePercent;
            s.TwinLibraryOpacityPercent = TwinLibraryOpacityPercent;
            s.SkillMonitorScalePercent = SkillMonitorScalePercent;
            s.UiScalePercent = UiScalePercent;
            s.OverlayOpacityPercent = OverlayOpacityPercent;
            s.DetailOpacityPercent = DetailOpacityPercent;
            s.GroupFrameScalePercent = GroupFrameScalePercent;
            s.GroupFrameShowMyBuffs = GroupFrameShowMyBuffs;
            s.GroupFrameShowDebuffs = GroupFrameShowDebuffs;
            s.GroupFrameShowOtherBuffs = GroupFrameShowOtherBuffs;
            s.GroupFrameShowPermanentBuffs = GroupFrameShowPermanentBuffs;
            s.GroupFrameShieldOnRight = GroupFrameShieldOnRight;
            s.GroupFrameShieldAnimation = GroupFrameShieldAnimation;
            s.GroupFrameGreenMeters = GroupFrameGreenMeters;
            s.GroupFrameOrangeMeters = GroupFrameOrangeMeters;
            s.GroupFrameRangeMeters = GroupFrameRangeMeters;
            s.GroupFrameShowDeaths = GroupFrameShowDeaths;
            s.GroupFrameAuraSide = GroupFrameAuraSide;
            s.GroupFrameArrowSide = GroupFrameArrowSide;
            s.GroupFrameDistanceSide = GroupFrameDistanceSide;
            s.GroupFrameHpOpacityPercent = GroupFrameHpOpacityPercent;
            s.GroupFrameAuraOpacityPercent = GroupFrameAuraOpacityPercent;
            s.GroupFrameArrowOpacityPercent = GroupFrameArrowOpacityPercent;
            s.GroupFrameDistanceOpacityPercent = GroupFrameDistanceOpacityPercent;
            s.GroupFrameAlertEnabled = GroupFrameAlertEnabled;
            s.GroupFrameAlertVolumePercent = GroupFrameAlertVolumePercent;
            s.GroupFrameAlertQuietSeconds = GroupFrameAlertQuietSeconds;
            s.GroupFrameAlertSound = GroupFrameAlertSound;
            s.GroupFrameAlertSounds = [.. GroupFrameAlertSounds];
            s.UpdateAutomatically = UpdateAutomatically;
            s.LookUpDigitalTwins = LookUpDigitalTwins;
            s.UpdateMyselfOnStart = UpdateMyselfOnStart;
            s.UseSeenSkillLevels = UseSeenSkillLevels;
            s.TwinLookupPauseSeconds = TwinLookupPauseSeconds;
            s.Language =SelectedLanguage?.Code ?? _languageService.CurrentLanguage;
            s.BattleResetHotkeyModifiers = BattleResetHotkey is null ? null : (uint)BattleResetHotkey.Modifiers;
            s.BattleResetHotkeyVirtualKey = BattleResetHotkey?.VirtualKey;
            s.OverlayInteractionHotkeyModifiers = OverlayInteractionHotkey is null ? null : (uint)OverlayInteractionHotkey.Modifiers;
            s.OverlayInteractionHotkeyVirtualKey = OverlayInteractionHotkey?.VirtualKey;
            s.MinimizeAllHotkeyModifiers = MinimizeAllHotkey is null ? null : (uint)MinimizeAllHotkey.Modifiers;
            s.MinimizeAllHotkeyVirtualKey = MinimizeAllHotkey?.VirtualKey;
            s.MinimizedFrames = [.. MinimizedFrames];
            s.MeterFolded = IsMinimized(MeterKey);
            s.HiddenCubesEnabled = HiddenCubesEnabled;
            s.HiddenCubesSound = HiddenCubesSound;
            s.HiddenCubesScalePercent = HiddenCubesScalePercent;
            s.HiddenCubesOpacityPercent = HiddenCubesOpacityPercent;
            s.GroupFrameCubeFinder = !HiddenCubesEnabled ? GroupFrameCubeFinder.Off : HiddenCubesSound ? GroupFrameCubeFinder.Sound : GroupFrameCubeFinder.Show;
            SaveScenes(s);
            SaveWorkflows(s);
            s.TutorialDone = TutorialDone;
            s.BossParticipationWarning = BossParticipationWarning;
            s.BossParticipationPercent = BossParticipationPercent;
        });
    }

    private void OnForegroundChanged() => Dispatcher.UIThread.Post(RefreshTargetProcessForegroundState);

    private void OnLocalizationLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SaveSceneHotkeyDisplay));
        foreach (var scene in Scenes)
            scene.Refresh();
        RefreshWorkflowTriggerLabels();
        BossParticipation.Refresh();
        OnPropertyChanged(nameof(TopmostModeDisplay));
        OnPropertyChanged(nameof(CombatantSortMetricDisplay));
        OnPropertyChanged(nameof(CombatantSortOrderDisplay));
        OnPropertyChanged(nameof(CombatantStatisticsScopeDisplay));
        OnPropertyChanged(nameof(SceneKindDisplay));
        OnPropertyChanged(nameof(UseCompactMainMetricsDisplay));
        OnPropertyChanged(nameof(ShowDamagePerSecondColumnDisplay));
        OnPropertyChanged(nameof(ShowDamageColumnDisplay));
        OnPropertyChanged(nameof(ShowHealingPerSecondColumnDisplay));
        OnPropertyChanged(nameof(ShowHealingColumnDisplay));
        OnPropertyChanged(nameof(IncludeOverhealingDisplay));
        OnPropertyChanged(nameof(ShowDamageTakenColumnDisplay));
        OnPropertyChanged(nameof(ShowUnhealedHealthColumnDisplay));
        OnPropertyChanged(nameof(ShowAggroColumnDisplay));
        OnPropertyChanged(nameof(ShowRecoveryTimeColumnDisplay));
        OnPropertyChanged(nameof(ShowStaggerColumnDisplay));
        OnPropertyChanged(nameof(ShowBossShareColumnDisplay));
        OnPropertyChanged(nameof(ShowTotalDamagePerSecondDisplay));
        OnPropertyChanged(nameof(ShowColumnHeadersDisplay));
        OnPropertyChanged(nameof(BossTimeLimitDisplay));
        OnPropertyChanged(nameof(MainMetricVisibilityDisplay));
        OnPropertyChanged(nameof(EncounterTimeDisplayFormatDisplay));
        OnPropertyChanged(nameof(ShowFocusStatusBarDisplay));
        OnPropertyChanged(nameof(ShowPlayerNamesDisplay));
        OnPropertyChanged(nameof(PlayerSelfMarkerDisplayModeDisplay));
        OnPropertyChanged(nameof(ShowPlayerShortServerNameDisplay));
        OnPropertyChanged(nameof(ShowPlayerLegionNameDisplay));
        OnPropertyChanged(nameof(TintPlayerNamesByFactionDisplay));
        OnPropertyChanged(nameof(SkillMonitorEnabledDisplay));
        OnPropertyChanged(nameof(GroupFrameEnabledDisplay));
        OnPropertyChanged(nameof(FightGraphsEnabledDisplay));
        OnPropertyChanged(nameof(PlayerNameSettingsDisplay));
        OnPropertyChanged(nameof(LanguageDisplay));
        OnPropertyChanged(nameof(UpdateStatusText));
        OnPropertyChanged(nameof(UpdateAutomaticallyDisplay));
        OnPropertyChanged(nameof(LookUpDigitalTwinsDisplay));
        OnPropertyChanged(nameof(CurrentVersionText));
        OnPropertyChanged(nameof(MinimizeAllHotkeyDisplay));
        OnPropertyChanged(nameof(ResetHotkeyDisplay));
        OnPropertyChanged(nameof(OverlayInteractionHotkeyDisplay));
        OnPropertyChanged(nameof(HotkeyRegistrationErrorText));
    }

    private string ResolveMainMetricVisibilityDisplay(bool isVisible) => Localization[isVisible ? "Settings_MainMetricVisibility_Show" : "Settings_MainMetricVisibility_Hide"];

    private void RebuildLanguageOptions()
    {
        var selectedCode = SelectedLanguage?.Code ?? _languageService.CurrentLanguage;
        Languages.Clear();
        Languages.Add(new LanguageOption(LanguageService.TraditionalChinese, "繁體中文"));
        Languages.Add(new LanguageOption(LanguageService.English, "English"));
        Languages.Add(new LanguageOption(LanguageService.German, "Deutsch"));
        Languages.Add(new LanguageOption(LanguageService.Korean, "한국어"));
        SelectedLanguage = Languages.FirstOrDefault(x => x.Code == selectedCode) ?? Languages.FirstOrDefault();
    }

    private void OnUpdatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppUpdateService.State):
                OnPropertyChanged(nameof(UpdateState));
                OnPropertyChanged(nameof(IsCheckingUpdate));
                OnPropertyChanged(nameof(IsDownloadingUpdate));
                OnPropertyChanged(nameof(IsBusyOrChecking));
                OnPropertyChanged(nameof(CanCheckForUpdates));
                OnPropertyChanged(nameof(CanRestartToUpdate));
                OnPropertyChanged(nameof(CanUpdateNow));
                OnPropertyChanged(nameof(UpdateStatusText));
                UpdateNowCommand.NotifyCanExecuteChanged();
                break;
            case nameof(AppUpdateService.DownloadProgress):
                OnPropertyChanged(nameof(DownloadProgress));
                OnPropertyChanged(nameof(UpdateStatusText));
                break;
            case nameof(AppUpdateService.AvailableVersion):
                OnPropertyChanged(nameof(AvailableVersion));
                OnPropertyChanged(nameof(UpdateStatusText));
                break;
            case nameof(AppUpdateService.StatusMessage):
                OnPropertyChanged(nameof(UpdateStatusMessage));
                break;
        }
    }

    public bool IsBusyOrChecking => IsCheckingUpdate || IsDownloadingUpdate;
}
