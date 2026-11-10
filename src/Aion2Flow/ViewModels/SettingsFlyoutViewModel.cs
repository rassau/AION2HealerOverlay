using System.Collections.ObjectModel;
using System.ComponentModel;
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
            ShowAggroColumn = persisted.ShowAggroColumn;
            ShowStaggerColumn = persisted.ShowStaggerColumn;
            ShowBossShareColumn = persisted.ShowBossShareColumn;
            ShowTotalDamagePerSecond = persisted.ShowTotalDamagePerSecond;
            ShowColumnHeaders = persisted.ShowColumnHeaders;
            BossSectionExpanded = persisted.BossSectionExpanded;
            BossTimeLimitMinutes = persisted.BossTimeLimitMinutes;
            EncounterTimeDisplayFormat = persisted.EncounterTimeDisplayFormat;
            ShowFocusStatusBar = persisted.ShowFocusStatusBar;
            HideHeaderWhenClickThrough = persisted.HideHeaderWhenClickThrough;
            ShowPlayerNames = persisted.ShowPlayerNames;
            PlayerSelfMarkerDisplayMode = persisted.PlayerSelfMarkerDisplayMode;
            ShowPlayerShortServerName = persisted.ShowPlayerShortServerName;
            ShowPlayerLegionName = persisted.ShowPlayerLegionName;
            TintPlayerNamesByFaction = persisted.TintPlayerNamesByFaction;
            SkillMonitorEnabled = persisted.SkillMonitorEnabled;
            GroupFrameEnabled = persisted.GroupFrameEnabled;
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
            BattleResetHotkey = CreateHotkey(persisted.BattleResetHotkeyModifiers, persisted.BattleResetHotkeyVirtualKey);
            OverlayInteractionHotkey = CreateHotkey(persisted.OverlayInteractionHotkeyModifiers, persisted.OverlayInteractionHotkeyVirtualKey);
            if (BattleResetHotkey is not null && BattleResetHotkey == OverlayInteractionHotkey)
            {
                OverlayInteractionHotkey = null;
            }
        }
        finally
        {
            _isApplyingPersistedSettings = false;
        }

        if (!_globalHotkeyService.TrySetHotkey(GlobalHotkeyAction.BattleReset, BattleResetHotkey))
        {
            BattleResetHotkey = null;
        }
        if (!_globalHotkeyService.TrySetHotkey(GlobalHotkeyAction.CycleOverlayInteraction, OverlayInteractionHotkey))
        {
            OverlayInteractionHotkey = null;
        }

        RebuildLanguageOptions();
        SelectedLanguage = Languages.FirstOrDefault(x => string.Equals(x.Code, _languageService.CurrentLanguage, StringComparison.Ordinal));

        _languageService.LanguageChanged += OnLanguageServiceLanguageChanged;
        _processForegroundWatcher.ForegroundChanged += OnForegroundChanged;
        _updateService.PropertyChanged += OnUpdatePropertyChanged;
        Localization.LanguageChanged += OnLocalizationLanguageChanged;
    }

    public LocalizationService Localization { get; }

    public ObservableCollection<LanguageOption> Languages { get; } = [];

    public IReadOnlyList<TopmostMode> TopmostModeOptions { get; } = [TopmostMode.GameForeground, TopmostMode.Always, TopmostMode.Never];

    public IReadOnlyList<int> RowCountOptions { get; } = [5, 6, 7, 8, 9, 10];

    public IReadOnlyList<CombatantSortMetric> CombatantSortMetricOptions { get; } = [CombatantSortMetric.DamagePerSecond, CombatantSortMetric.TotalDamage, CombatantSortMetric.HealingPerSecond, CombatantSortMetric.TotalHealing];

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
    [NotifyPropertyChangedFor(nameof(ShowAggroColumnDisplay))]
    [NotifyPropertyChangedFor(nameof(MainMetricVisibilityDisplay))]
    public partial bool ShowAggroColumn { get; set; }

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BossTimeLimitDisplay))]
    public partial int BossTimeLimitMinutes { get; set; }

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
    [NotifyPropertyChangedFor(nameof(HideHeaderWhenClickThroughDisplay))]
    public partial bool HideHeaderWhenClickThrough { get; set; }

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

    // Updates from the overlay's release page (only an install from a release package has them): on, a check and
    // download at every start and the install when the overlay closes; off, only "Update now".
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateAutomaticallyDisplay))]
    public partial bool UpdateAutomatically { get; set; }

    public string UpdateAutomaticallyDisplay => Localization[UpdateAutomatically ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"];

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
    [NotifyPropertyChangedFor(nameof(IsCapturingResetHotkey))]
    [NotifyPropertyChangedFor(nameof(IsCapturingOverlayInteractionHotkey))]
    public partial GlobalHotkeyAction? CapturingHotkeyAction { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverlayInteractionHotkeyDisplay))]
    [NotifyPropertyChangedFor(nameof(HasOverlayInteractionHotkey))]
    public partial HotkeyDefinition? OverlayInteractionHotkey { get; private set; }

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

    public string ShowAggroColumnDisplay => ResolveMainMetricVisibilityDisplay(ShowAggroColumn);

    public string ShowStaggerColumnDisplay => ResolveMainMetricVisibilityDisplay(ShowStaggerColumn);

    public string ShowBossShareColumnDisplay => ResolveMainMetricVisibilityDisplay(ShowBossShareColumn);

    public string ShowTotalDamagePerSecondDisplay => ResolveMainMetricVisibilityDisplay(ShowTotalDamagePerSecond);

    public string ShowColumnHeadersDisplay => ResolveMainMetricVisibilityDisplay(ShowColumnHeaders);

    public string MainMetricVisibilityDisplay => $"{Convert.ToInt32(ShowDamagePerSecondColumn) + Convert.ToInt32(ShowDamageColumn) + Convert.ToInt32(ShowHealingPerSecondColumn) + Convert.ToInt32(ShowHealingColumn) + Convert.ToInt32(ShowDamageTakenColumn) + Convert.ToInt32(ShowUnhealedHealthColumn) + Convert.ToInt32(ShowAggroColumn) + Convert.ToInt32(ShowStaggerColumn) + Convert.ToInt32(ShowBossShareColumn) + Convert.ToInt32(ShowTotalDamagePerSecond)}/10";

    public string BossTimeLimitDisplay => FormatBossTimeLimit(BossTimeLimitMinutes);

    public string FormatBossTimeLimit(int minutes) => minutes > 0 ? $"{minutes}:00" : Localization["Settings_BossTimeLimit_Off"];

    public static string FormatMeters(int meters) => $"{meters} m";

    public string EncounterTimeDisplayFormatDisplay => Localization[$"Settings_TimeDisplayFormat_{EncounterTimeDisplayFormat}"];

    public string ShowFocusStatusBarDisplay => Localization[ShowFocusStatusBar ? "Settings_FocusStatusBar_On" : "Settings_FocusStatusBar_Off"];

    public string HideHeaderWhenClickThroughDisplay => Localization[HideHeaderWhenClickThrough ? "Settings_HideHeaderWhenClickThrough_On" : "Settings_HideHeaderWhenClickThrough_Off"];

    public string ShowPlayerNamesDisplay => Localization[ShowPlayerNames ? "Settings_ShowPlayerNames_On" : "Settings_ShowPlayerNames_Off"];

    public string PlayerSelfMarkerDisplayModeDisplay => Localization[$"Settings_PlayerSelfMarker_{PlayerSelfMarkerDisplayMode}"];

    public string ShowPlayerShortServerNameDisplay => Localization[ShowPlayerShortServerName ? "Settings_PlayerShortServerName_On" : "Settings_PlayerShortServerName_Off"];

    public string ShowPlayerLegionNameDisplay => Localization[ShowPlayerLegionName ? "Settings_PlayerLegionName_On" : "Settings_PlayerLegionName_Off"];

    public string TintPlayerNamesByFactionDisplay => Localization[TintPlayerNamesByFaction ? "Settings_PlayerFactionTint_On" : "Settings_PlayerFactionTint_Off"];

    public string SkillMonitorEnabledDisplay => Localization[SkillMonitorEnabled ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"];

    public string GroupFrameEnabledDisplay => Localization[GroupFrameEnabled ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"];

    public string PlayerNameSettingsDisplay => ShowPlayerNamesDisplay;

    public string LanguageDisplay => SelectedLanguage?.DisplayName ?? string.Empty;

    public bool IsUpdateSectionVisible => _updateService.IsManagedByVelopack;

    public string? CurrentVersion => _updateService.CurrentVersion;

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

    partial void OnShowAggroColumnChanged(bool value) => PersistSettings();

    partial void OnShowStaggerColumnChanged(bool value) => PersistSettings();

    partial void OnShowBossShareColumnChanged(bool value) => PersistSettings();

    partial void OnBossSectionExpandedChanged(bool value) => PersistSettings();

    partial void OnBossTimeLimitMinutesChanged(int value) => PersistSettings();

    partial void OnShowTotalDamagePerSecondChanged(bool value) => PersistSettings();

    partial void OnShowColumnHeadersChanged(bool value) => PersistSettings();

    partial void OnEncounterTimeDisplayFormatChanged(EncounterTimeDisplayFormat value) => PersistSettings();

    partial void OnShowFocusStatusBarChanged(bool value) => PersistSettings();

    partial void OnHideHeaderWhenClickThroughChanged(bool value) => PersistSettings();

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

    public void CancelCaptureHotkey(GlobalHotkeyAction action)
    {
        ValidateAction(action);
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
        var conflictingAction = OtherAction(action);
        var hasConflict = definition == GetHotkey(conflictingAction);
        var changed = definition != GetHotkey(action) || hasConflict;
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

        if (hasConflict)
        {
            SetHotkeyValue(conflictingAction, null);
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
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported global hotkey action.");
        }
    }

    private HotkeyDefinition? GetHotkey(GlobalHotkeyAction action) => action switch
    {
        GlobalHotkeyAction.BattleReset => BattleResetHotkey,
        GlobalHotkeyAction.CycleOverlayInteraction => OverlayInteractionHotkey,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported global hotkey action.")
    };

    private static GlobalHotkeyAction OtherAction(GlobalHotkeyAction action) => action switch
    {
        GlobalHotkeyAction.BattleReset => GlobalHotkeyAction.CycleOverlayInteraction,
        GlobalHotkeyAction.CycleOverlayInteraction => GlobalHotkeyAction.BattleReset,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported global hotkey action.")
    };

    private static void ValidateAction(GlobalHotkeyAction action) => _ = OtherAction(action);

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
        (BattleResetHotkey is not null && !_globalHotkeyService.IsRegistered(GlobalHotkeyAction.BattleReset)) ||
        (OverlayInteractionHotkey is not null && !_globalHotkeyService.IsRegistered(GlobalHotkeyAction.CycleOverlayInteraction));

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
            s.ShowStaggerColumn = ShowStaggerColumn;
            s.ShowBossShareColumn = ShowBossShareColumn;
            s.ShowTotalDamagePerSecond = ShowTotalDamagePerSecond;
            s.ShowColumnHeaders = ShowColumnHeaders;
            s.BossSectionExpanded = BossSectionExpanded;
            s.BossTimeLimitMinutes = BossTimeLimitMinutes;
            s.EncounterTimeDisplayFormat = EncounterTimeDisplayFormat;
            s.ShowFocusStatusBar = ShowFocusStatusBar;
            s.HideHeaderWhenClickThrough = HideHeaderWhenClickThrough;
            s.ShowPlayerNames = ShowPlayerNames;
            s.PlayerSelfMarkerDisplayMode = PlayerSelfMarkerDisplayMode;
            s.ShowPlayerShortServerName = ShowPlayerShortServerName;
            s.ShowPlayerLegionName = ShowPlayerLegionName;
            s.TintPlayerNamesByFaction = TintPlayerNamesByFaction;
            s.SkillMonitorEnabled = SkillMonitorEnabled;
            s.GroupFrameEnabled = GroupFrameEnabled;
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
            s.Language = SelectedLanguage?.Code ?? _languageService.CurrentLanguage;
            s.BattleResetHotkeyModifiers = BattleResetHotkey is null ? null : (uint)BattleResetHotkey.Modifiers;
            s.BattleResetHotkeyVirtualKey = BattleResetHotkey?.VirtualKey;
            s.OverlayInteractionHotkeyModifiers = OverlayInteractionHotkey is null ? null : (uint)OverlayInteractionHotkey.Modifiers;
            s.OverlayInteractionHotkeyVirtualKey = OverlayInteractionHotkey?.VirtualKey;
        });
    }

    private void OnForegroundChanged() => Dispatcher.UIThread.Post(RefreshTargetProcessForegroundState);

    private void OnLanguageServiceLanguageChanged(object? sender, string language)
    {
        Dispatcher.UIThread.Post(RebuildLanguageOptions);
    }

    private void OnLocalizationLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TopmostModeDisplay));
        OnPropertyChanged(nameof(CombatantSortMetricDisplay));
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
        OnPropertyChanged(nameof(ShowStaggerColumnDisplay));
        OnPropertyChanged(nameof(ShowBossShareColumnDisplay));
        OnPropertyChanged(nameof(ShowTotalDamagePerSecondDisplay));
        OnPropertyChanged(nameof(ShowColumnHeadersDisplay));
        OnPropertyChanged(nameof(BossTimeLimitDisplay));
        OnPropertyChanged(nameof(MainMetricVisibilityDisplay));
        OnPropertyChanged(nameof(EncounterTimeDisplayFormatDisplay));
        OnPropertyChanged(nameof(ShowFocusStatusBarDisplay));
        OnPropertyChanged(nameof(HideHeaderWhenClickThroughDisplay));
        OnPropertyChanged(nameof(ShowPlayerNamesDisplay));
        OnPropertyChanged(nameof(PlayerSelfMarkerDisplayModeDisplay));
        OnPropertyChanged(nameof(ShowPlayerShortServerNameDisplay));
        OnPropertyChanged(nameof(ShowPlayerLegionNameDisplay));
        OnPropertyChanged(nameof(TintPlayerNamesByFactionDisplay));
        OnPropertyChanged(nameof(SkillMonitorEnabledDisplay));
        OnPropertyChanged(nameof(GroupFrameEnabledDisplay));
        OnPropertyChanged(nameof(PlayerNameSettingsDisplay));
        OnPropertyChanged(nameof(LanguageDisplay));
        OnPropertyChanged(nameof(UpdateStatusText));
        OnPropertyChanged(nameof(UpdateAutomaticallyDisplay));
        OnPropertyChanged(nameof(CurrentVersionText));
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
