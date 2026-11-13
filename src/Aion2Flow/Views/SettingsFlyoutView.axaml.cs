using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Cloris.Aion2Flow.Capture.Diagnostics;
using Cloris.Aion2Flow.Presentation;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Logging;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Cloris.Aion2Flow.Views;

public partial class SettingsFlyoutView : UserControl
{
    private MenuItem? _topmostMenuItem;
    private MenuItem? _visibleRowsMenuItem;
    private MenuItem? _combatantStatisticsScopeMenuItem;
    private MenuItem? _combatantSortMetricMenuItem;
    private MenuItem? _mainMetricVisibilityMenuItem;
    private MenuItem? _showDamagePerSecondColumnMenuItem;
    private MenuItem? _showDamageColumnMenuItem;
    private MenuItem? _showHealingPerSecondColumnMenuItem;
    private MenuItem? _showHealingColumnMenuItem;
    private MenuItem? _includeOverhealingMenuItem;
    private MenuItem? _showDamageTakenColumnMenuItem;
    private MenuItem? _showUnhealedHealthColumnMenuItem;
    private MenuItem? _showAggroColumnMenuItem;
    private MenuItem? _showStaggerColumnMenuItem;
    private MenuItem? _showBossShareColumnMenuItem;
    private MenuItem? _bossTimeLimitMenuItem;
    private MenuItem? _showTotalDamagePerSecondMenuItem;
    private MenuItem? _showColumnHeadersMenuItem;
    // The rows built in code, in the menus named in the XAML (see BuildSections).
    private readonly List<SettingsRows> _sections = [];
    private MenuItem? _encounterTimeDisplayFormatMenuItem;
    private MenuItem? _sceneKindMenuItem;
    private MenuItem? _compactMainMetricsMenuItem;
    private MenuItem? _focusStatusBarMenuItem;
    private MenuItem? _hideHeaderWhenClickThroughMenuItem;
    private MenuItem? _updateAutomaticallyMenuItem;
    private MenuItem? _playerNameSettingsMenuItem;
    private MenuItem? _showPlayerNamesMenuItem;
    private MenuItem? _playerSelfMarkerMenuItem;
    private MenuItem? _showPlayerShortServerNameMenuItem;
    private MenuItem? _showPlayerLegionNameMenuItem;
    private MenuItem? _tintPlayerNamesByFactionMenuItem;
    private MenuItem? _languageMenuItem;
    private SettingsFlyoutViewModel? _viewModel;
    private Services.LocalizationService? _localization;
    private SkillMonitorSettingsViewModel? _skillMonitorSettingsViewModel;

    public SettingsFlyoutView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private SettingsFlyoutViewModel? ViewModel => DataContext as SettingsFlyoutViewModel;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ShowSkillMonitorSettings();
    }

    // The skill monitor's lists go with the window (their view model follows the game data and the settings).
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SkillMonitorSettingsHost.Content = null;
        _skillMonitorSettingsViewModel?.Dispose();
        _skillMonitorSettingsViewModel = null;
    }

    // The skill monitor's buff and cooldown lists, made the first time its tab shows.
    private void ShowSkillMonitorSettings()
    {
        if (ViewModel?.Tab != SettingsTab.SkillMonitor || _skillMonitorSettingsViewModel is not null)
            return;

        _skillMonitorSettingsViewModel = new SkillMonitorSettingsViewModel(
            Ioc.Default.GetRequiredService<GameResourceService>(),
            Ioc.Default.GetRequiredService<SettingsService>(),
            Ioc.Default.GetRequiredService<Services.LocalizationService>());
        // As wide as the page, which scrolls, so the lists take the height they need.
        SkillMonitorSettingsHost.Content = new SkillMonitorSettingsView { DataContext = _skillMonitorSettingsViewModel, Width = double.NaN, Height = double.NaN };
    }

    // The rows built in code, by tab and section; built anew with the language.
    private void BuildSections()
    {
        Menu[] menus =
        [
            ClickableMenu, VisualsMeterMenu, VisualsGroupFrameMenu, VisualsSkillMonitorMenu, VisualsFightGraphsMenu, VisualsTwinLibraryMenu, AudioMenu,
            MeterSortMenu, MeterVisualsMenu, GroupFrameEnableMenu, GroupFrameMenu, GroupFrameAlertMenu, GroupFrameVisualsMenu, SkillMonitorEnableMenu, SkillMonitorMenu,
            SkillMonitorVisualsMenu, FightGraphsEnableMenu, FightGraphsMenu, FightGraphsVisualsMenu, TwinLibraryMenu, TwinLibraryVisualsMenu, DebuggingMenu,
        ];
        foreach (var menu in menus)
            menu.Items.Clear();
        _sections.Clear();
        if (ViewModel is not { } vm)
            return;

        SettingsRows Rows(Menu menu)
        {
            var rows = new SettingsRows(vm, menu.Items);
            _sections.Add(rows);
            return rows;
        }

        // The meter goes by the main UI scale, which the settings window follows too, so it's set once the slider is let go.
        void MeterVisuals(Menu menu)
        {
            var rows = Rows(menu);
            rows.Slider("Settings_UiScale", () => vm.UiScalePercent, vm.SetUiScalePercent, min: 50, max: 200, step: 1, live: false);
            rows.Slider("Settings_OverlayOpacity", () => vm.OverlayOpacityPercent, percent => vm.OverlayOpacityPercent = percent, min: 20);
            rows.Slider("Settings_DetailOpacity", () => vm.DetailOpacityPercent, percent => vm.DetailOpacityPercent = percent, min: 20);
        }

        void SkillMonitorVisuals(Menu menu) => Rows(menu).Visuals(() => vm.SkillMonitorScalePercent, percent => vm.SkillMonitorScalePercent = percent,
            () => vm.SkillMonitorOpacityPercent, percent => vm.SkillMonitorOpacityPercent = percent);

        void TwinLibraryVisuals(Menu menu) => Rows(menu).Visuals(() => vm.TwinLibraryScalePercent, percent => vm.TwinLibraryScalePercent = percent,
            () => vm.TwinLibraryOpacityPercent, percent => vm.TwinLibraryOpacityPercent = percent);

        Rows(ClickableMenu).Clickable(SettingsFlyoutViewModel.ClickableFrames);

        MeterVisuals(VisualsMeterMenu);
        _sections.Add(new GroupFrameSettingsMenu(vm, VisualsGroupFrameMenu.Items, GroupFrameRows.Visuals));
        SkillMonitorVisuals(VisualsSkillMonitorMenu);
        _sections.Add(new FightGraphsSettingsMenu(vm, VisualsFightGraphsMenu.Items, FightGraphsRows.Visuals));
        TwinLibraryVisuals(VisualsTwinLibraryMenu);

        _sections.Add(new GroupFrameSettingsMenu(vm, AudioMenu.Items, GroupFrameRows.Alert | GroupFrameRows.Cubes));

        // The meter's order: the highest first or the lowest (a click on the sorted column's head turns it round too).
        Rows(MeterSortMenu).Toggle("Settings_CombatantSortOrder", () => vm.CombatantSortAscending = !vm.CombatantSortAscending,
            () => vm.CombatantSortOrderDisplay, static () => false);
        MeterVisuals(MeterVisualsMenu);

        _sections.Add(new GroupFrameSettingsMenu(vm, GroupFrameEnableMenu.Items, GroupFrameRows.Enable));
        _sections.Add(new GroupFrameSettingsMenu(vm, GroupFrameMenu.Items, GroupFrameRows.Display | GroupFrameRows.Cubes | GroupFrameRows.Clickable));
        _sections.Add(new GroupFrameSettingsMenu(vm, GroupFrameAlertMenu.Items, GroupFrameRows.Alert));
        _sections.Add(new GroupFrameSettingsMenu(vm, GroupFrameVisualsMenu.Items, GroupFrameRows.Visuals));

        Rows(SkillMonitorEnableMenu).OnOff("Settings_SkillMonitor_Enable", () => vm.SkillMonitorEnabled, on => vm.SkillMonitorEnabled = on);
        Rows(SkillMonitorMenu).Clickable(SettingsFlyoutViewModel.SkillMonitorKey);
        SkillMonitorVisuals(SkillMonitorVisualsMenu);

        _sections.Add(new FightGraphsSettingsMenu(vm, FightGraphsEnableMenu.Items, FightGraphsRows.Enable));
        _sections.Add(new FightGraphsSettingsMenu(vm, FightGraphsMenu.Items, FightGraphsRows.Display | FightGraphsRows.Clickable));
        _sections.Add(new FightGraphsSettingsMenu(vm, FightGraphsVisualsMenu.Items, FightGraphsRows.Visuals));

        var twins = Rows(TwinLibraryMenu);
        ToolTip.SetTip(twins.OnOff("Settings_DigitalTwins", () => vm.LookUpDigitalTwins, on => vm.LookUpDigitalTwins = on), vm.Localization["Settings_DigitalTwinsTip"]);
        twins.Clickable(SettingsFlyoutViewModel.TwinLibraryKey);
        TwinLibraryVisuals(TwinLibraryVisualsMenu);

        // Recording all packets, and saving them where the user picks.
        var debugging = Rows(DebuggingMenu);
        var record = SettingsRows.Toggle(ToggleRecording);
        debugging.Add(record, () =>
        {
            var recording = PacketRecording.IsRecording;
            RefreshToggleMenuItem(record, vm.Localization[recording ? "Settings_Debug_StopRecording" : "Settings_Debug_StartRecording"],
                recording ? vm.Localization["Settings_Debug_Recording"] : string.Empty, recording);
        });
        var save = SettingsRows.Row();
        save.Click += SavePacketsClicked;
        debugging.Add(save, () =>
        {
            save.Header = vm.Localization["Settings_Debug_SavePackets"];
            save.IsEnabled = PacketRecording.HasRecording;
        });

        RefreshSections();
    }

    private void RefreshSections()
    {
        foreach (var section in _sections)
            section.Refresh();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Languages.CollectionChanged -= OnLanguagesCollectionChanged;
        }
        _localization?.LanguageChanged -= OnLocalizationLanguageChanged;

        _viewModel = ViewModel;
        _localization = _viewModel?.Localization;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Languages.CollectionChanged += OnLanguagesCollectionChanged;
        }
        _localization?.LanguageChanged += OnLocalizationLanguageChanged;

        RebuildTopmostMenuItems();
        RebuildVisibleRowsMenuItems();
        RebuildCombatantStatisticsScopeMenuItems();
        RebuildCombatantSortMetricMenuItems();
        RebuildMainMetricVisibilityMenuItems();
        RebuildEncounterTimeDisplayFormatMenuItems();
        RebuildSceneKindMenuItems();
        RefreshColumnHeadersMenuItem();
        RefreshOverhealingMenuItem();
        RefreshCompactMainMetricsMenuItem();
        RefreshFocusStatusBarMenuItem();
        RebuildBossTimeLimitMenuItems();
        RefreshHideHeaderWhenClickThroughMenuItem();
        RefreshUpdateAutomaticallyMenuItem();
        RebuildPlayerNameSettingsMenuItems();
        BuildSections();
        RebuildLanguageMenuItems();
        ShowSkillMonitorSettings();
    }

    private void OnLocalizationLanguageChanged(object? sender, EventArgs e)
    {
        RebuildTopmostMenuItems();
        RebuildVisibleRowsMenuItems();
        RebuildCombatantStatisticsScopeMenuItems();
        RebuildCombatantSortMetricMenuItems();
        RebuildMainMetricVisibilityMenuItems();
        RebuildEncounterTimeDisplayFormatMenuItems();
        RebuildSceneKindMenuItems();
        RefreshColumnHeadersMenuItem();
        RefreshOverhealingMenuItem();
        RefreshCompactMainMetricsMenuItem();
        RefreshFocusStatusBarMenuItem();
        RebuildBossTimeLimitMenuItems();
        RefreshHideHeaderWhenClickThroughMenuItem();
        RefreshUpdateAutomaticallyMenuItem();
        RebuildPlayerNameSettingsMenuItems();
        BuildSections();
        RebuildLanguageMenuItems();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Cheap: a row whose texts and tick haven't changed stays as it is.
        RefreshSections();
        switch (e.PropertyName)
        {
            case nameof(SettingsFlyoutViewModel.Tab):
                ShowSkillMonitorSettings();
                break;
            case nameof(SettingsFlyoutViewModel.TopmostMode):
            case nameof(SettingsFlyoutViewModel.TopmostModeDisplay):
                RefreshTopmostHeader();
                RefreshTopmostCheckmarks();
                break;
            case nameof(SettingsFlyoutViewModel.MaxVisibleCombatantRows):
            case nameof(SettingsFlyoutViewModel.MaxVisibleCombatantRowsDisplay):
                RefreshVisibleRowsHeader();
                RefreshVisibleRowsCheckmarks();
                break;
            case nameof(SettingsFlyoutViewModel.CombatantStatisticsScope):
            case nameof(SettingsFlyoutViewModel.CombatantStatisticsScopeDisplay):
                RefreshCombatantStatisticsScopeHeader();
                RefreshCombatantStatisticsScopeCheckmarks();
                break;
            case nameof(SettingsFlyoutViewModel.CombatantSortMetric):
            case nameof(SettingsFlyoutViewModel.CombatantSortMetricDisplay):
                RefreshCombatantSortMetricHeader();
                RefreshCombatantSortMetricCheckmarks();
                break;
            case nameof(SettingsFlyoutViewModel.ShowDamagePerSecondColumn):
            case nameof(SettingsFlyoutViewModel.ShowDamagePerSecondColumnDisplay):
            case nameof(SettingsFlyoutViewModel.ShowDamageColumn):
            case nameof(SettingsFlyoutViewModel.ShowDamageColumnDisplay):
            case nameof(SettingsFlyoutViewModel.ShowHealingPerSecondColumn):
            case nameof(SettingsFlyoutViewModel.ShowHealingPerSecondColumnDisplay):
            case nameof(SettingsFlyoutViewModel.ShowHealingColumn):
            case nameof(SettingsFlyoutViewModel.ShowHealingColumnDisplay):
            case nameof(SettingsFlyoutViewModel.ShowDamageTakenColumn):
            case nameof(SettingsFlyoutViewModel.ShowDamageTakenColumnDisplay):
            case nameof(SettingsFlyoutViewModel.ShowUnhealedHealthColumn):
            case nameof(SettingsFlyoutViewModel.ShowUnhealedHealthColumnDisplay):
            case nameof(SettingsFlyoutViewModel.ShowAggroColumn):
            case nameof(SettingsFlyoutViewModel.ShowAggroColumnDisplay):
            case nameof(SettingsFlyoutViewModel.ShowStaggerColumn):
            case nameof(SettingsFlyoutViewModel.ShowStaggerColumnDisplay):
            case nameof(SettingsFlyoutViewModel.ShowBossShareColumn):
            case nameof(SettingsFlyoutViewModel.ShowBossShareColumnDisplay):
            case nameof(SettingsFlyoutViewModel.ShowTotalDamagePerSecond):
            case nameof(SettingsFlyoutViewModel.ShowTotalDamagePerSecondDisplay):
            case nameof(SettingsFlyoutViewModel.MainMetricVisibilityDisplay):
                RefreshMainMetricVisibilityMenuItems();
                break;
            case nameof(SettingsFlyoutViewModel.ShowColumnHeaders):
            case nameof(SettingsFlyoutViewModel.ShowColumnHeadersDisplay):
                RefreshColumnHeadersMenuItem();
                break;
            case nameof(SettingsFlyoutViewModel.IncludeOverhealing):
            case nameof(SettingsFlyoutViewModel.IncludeOverhealingDisplay):
                RefreshOverhealingMenuItem();
                break;
            case nameof(SettingsFlyoutViewModel.EncounterTimeDisplayFormat):
            case nameof(SettingsFlyoutViewModel.EncounterTimeDisplayFormatDisplay):
                RefreshEncounterTimeDisplayFormatHeader();
                RefreshEncounterTimeDisplayFormatCheckmarks();
                break;
            case nameof(SettingsFlyoutViewModel.SceneKind):
            case nameof(SettingsFlyoutViewModel.SceneKindDisplay):
                RefreshSceneKindHeader();
                RefreshSceneKindCheckmarks();
                break;
            case nameof(SettingsFlyoutViewModel.UseCompactMainMetrics):
            case nameof(SettingsFlyoutViewModel.UseCompactMainMetricsDisplay):
                RefreshCompactMainMetricsMenuItem();
                break;
            case nameof(SettingsFlyoutViewModel.ShowFocusStatusBar):
            case nameof(SettingsFlyoutViewModel.ShowFocusStatusBarDisplay):
                RefreshFocusStatusBarMenuItem();
                break;
            case nameof(SettingsFlyoutViewModel.BossTimeLimitMinutes):
            case nameof(SettingsFlyoutViewModel.BossTimeLimitDisplay):
                RefreshBossTimeLimitHeader();
                RefreshTaggedMenuCheckmarks<int>(_bossTimeLimitMenuItem, minutes => minutes == ViewModel?.BossTimeLimitMinutes);
                break;
            case nameof(SettingsFlyoutViewModel.HideHeaderWhenClickThrough):
            case nameof(SettingsFlyoutViewModel.HideHeaderWhenClickThroughDisplay):
                RefreshHideHeaderWhenClickThroughMenuItem();
                break;
            case nameof(SettingsFlyoutViewModel.UpdateAutomatically):
            case nameof(SettingsFlyoutViewModel.UpdateAutomaticallyDisplay):
                RefreshUpdateAutomaticallyMenuItem();
                break;
            case nameof(SettingsFlyoutViewModel.ShowPlayerNames):
            case nameof(SettingsFlyoutViewModel.ShowPlayerNamesDisplay):
            case nameof(SettingsFlyoutViewModel.PlayerSelfMarkerDisplayMode):
            case nameof(SettingsFlyoutViewModel.PlayerSelfMarkerDisplayModeDisplay):
            case nameof(SettingsFlyoutViewModel.ShowPlayerShortServerName):
            case nameof(SettingsFlyoutViewModel.ShowPlayerShortServerNameDisplay):
            case nameof(SettingsFlyoutViewModel.ShowPlayerLegionName):
            case nameof(SettingsFlyoutViewModel.ShowPlayerLegionNameDisplay):
            case nameof(SettingsFlyoutViewModel.TintPlayerNamesByFaction):
            case nameof(SettingsFlyoutViewModel.TintPlayerNamesByFactionDisplay):
            case nameof(SettingsFlyoutViewModel.PlayerNameSettingsDisplay):
                RefreshPlayerNameSettingsMenuItems();
                break;
            case nameof(SettingsFlyoutViewModel.SelectedLanguage):
            case nameof(SettingsFlyoutViewModel.LanguageDisplay):
                RefreshLanguageHeader();
                RefreshLanguageCheckmarks();
                break;
        }
    }

    private void OnLanguagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildLanguageMenuItems();
        RebuildTopmostMenuItems();
        RebuildCombatantStatisticsScopeMenuItems();
        RebuildCombatantSortMetricMenuItems();
        RebuildMainMetricVisibilityMenuItems();
        RebuildEncounterTimeDisplayFormatMenuItems();
        RebuildSceneKindMenuItems();
        RefreshColumnHeadersMenuItem();
        RefreshOverhealingMenuItem();
        RefreshCompactMainMetricsMenuItem();
        RefreshFocusStatusBarMenuItem();
        RebuildBossTimeLimitMenuItems();
        RefreshHideHeaderWhenClickThroughMenuItem();
        RefreshUpdateAutomaticallyMenuItem();
        RebuildPlayerNameSettingsMenuItems();
        BuildSections();
    }

    private void TopmostMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _topmostMenuItem != mi)
        {
            _topmostMenuItem = mi;
            RebuildTopmostMenuItems();
        }
    }

    private void VisibleRowsMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _visibleRowsMenuItem != mi)
        {
            _visibleRowsMenuItem = mi;
            RebuildVisibleRowsMenuItems();
        }
    }

    private void LanguageMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _languageMenuItem != mi)
        {
            _languageMenuItem = mi;
            RebuildLanguageMenuItems();
        }
    }

    private void CombatantStatisticsScopeMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _combatantStatisticsScopeMenuItem != mi)
        {
            _combatantStatisticsScopeMenuItem = mi;
            RebuildCombatantStatisticsScopeMenuItems();
        }
    }

    private void CombatantSortMetricMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _combatantSortMetricMenuItem != mi)
        {
            _combatantSortMetricMenuItem = mi;
            RebuildCombatantSortMetricMenuItems();
        }
    }

    private void SceneKindMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _sceneKindMenuItem != mi)
        {
            _sceneKindMenuItem = mi;
            RebuildSceneKindMenuItems();
        }
    }

    private void MainMetricVisibilityMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _mainMetricVisibilityMenuItem != mi)
        {
            _mainMetricVisibilityMenuItem = mi;
            RebuildMainMetricVisibilityMenuItems();
        }
    }

    private void EncounterTimeDisplayFormatMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _encounterTimeDisplayFormatMenuItem != mi)
        {
            _encounterTimeDisplayFormatMenuItem = mi;
            RebuildEncounterTimeDisplayFormatMenuItems();
        }
    }

    private void PlayerNameSettingsMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _playerNameSettingsMenuItem != mi)
        {
            _playerNameSettingsMenuItem = mi;
            RebuildPlayerNameSettingsMenuItems();
        }
    }

    private void ColumnHeadersMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _showColumnHeadersMenuItem != mi)
        {
            _showColumnHeadersMenuItem = mi;
            RefreshColumnHeadersMenuItem();
        }
    }

    private void OverhealingMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _includeOverhealingMenuItem != mi)
        {
            _includeOverhealingMenuItem = mi;
            RefreshOverhealingMenuItem();
        }
    }

    private void CompactMainMetricsMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _compactMainMetricsMenuItem != mi)
        {
            _compactMainMetricsMenuItem = mi;
            RefreshCompactMainMetricsMenuItem();
        }
    }

    private void FocusStatusBarMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _focusStatusBarMenuItem != mi)
        {
            _focusStatusBarMenuItem = mi;
            RefreshFocusStatusBarMenuItem();
        }
    }

    private void BossTimeLimitMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _bossTimeLimitMenuItem != mi)
        {
            _bossTimeLimitMenuItem = mi;
            RebuildBossTimeLimitMenuItems();
        }
    }

    private void RebuildBossTimeLimitMenuItems()
    {
        RefreshBossTimeLimitHeader();
        if (ViewModel is not { } vm)
            return;

        RebuildTaggedMenuItems(
            _bossTimeLimitMenuItem,
            vm.BossTimeLimitOptions,
            vm.FormatBossTimeLimit,
            static minutes => minutes,
            minutes => minutes == vm.BossTimeLimitMinutes,
            BossTimeLimitItemClicked);
    }

    private void RefreshBossTimeLimitHeader()
    {
        if (_bossTimeLimitMenuItem is null || ViewModel is not { } vm) return;
        _bossTimeLimitMenuItem.Header = CreateRowHeader(vm.Localization["Settings_BossTimeLimit"], vm.BossTimeLimitDisplay);
    }

    private void BossTimeLimitItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && sender is MenuItem { Tag: int minutes })
            vm.BossTimeLimitMinutes = minutes;
    }

    private void HideHeaderWhenClickThroughMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _hideHeaderWhenClickThroughMenuItem != mi)
        {
            _hideHeaderWhenClickThroughMenuItem = mi;
            RefreshHideHeaderWhenClickThroughMenuItem();
        }
    }

    private void UpdateAutomaticallyMenuItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && _updateAutomaticallyMenuItem != mi)
        {
            _updateAutomaticallyMenuItem = mi;
            RefreshUpdateAutomaticallyMenuItem();
        }
    }

    private void UpdateAutomaticallyMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
            vm.UpdateAutomatically = !vm.UpdateAutomatically;
    }

    private void RefreshUpdateAutomaticallyMenuItem()
    {
        if (ViewModel is { } vm)
            RefreshToggleMenuItem(_updateAutomaticallyMenuItem, vm.Localization["Settings_Update_Automatically"], vm.UpdateAutomaticallyDisplay, vm.UpdateAutomatically);
    }

    private void RebuildTopmostMenuItems()
    {
        RefreshTopmostHeader();
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RebuildTaggedMenuItems(_topmostMenuItem, vm.TopmostModeOptions, mode => vm.Localization[$"Settings_Topmost_{mode}"], mode => mode, mode => mode == vm.TopmostMode, TopmostModeItemClicked);
    }

    private void RefreshTopmostCheckmarks()
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RefreshTaggedMenuCheckmarks<TopmostMode>(_topmostMenuItem, mode => mode == vm.TopmostMode);
    }

    private void RebuildVisibleRowsMenuItems()
    {
        RefreshVisibleRowsHeader();
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RebuildTaggedMenuItems(_visibleRowsMenuItem, vm.RowCountOptions, static count => count.ToString(), static count => count, count => count == vm.MaxVisibleCombatantRows, VisibleRowsItemClicked);
    }

    private void RefreshVisibleRowsCheckmarks()
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RefreshTaggedMenuCheckmarks<int>(_visibleRowsMenuItem, count => count == vm.MaxVisibleCombatantRows);
    }

    private void RebuildCombatantStatisticsScopeMenuItems()
    {
        RefreshCombatantStatisticsScopeHeader();
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RebuildTaggedMenuItems(_combatantStatisticsScopeMenuItem, vm.CombatantStatisticsScopeOptions, scope => vm.Localization[$"Settings_CombatantStatisticsScope_{scope}"], scope => scope, scope => scope == vm.CombatantStatisticsScope, CombatantStatisticsScopeItemClicked);
    }

    private void RefreshCombatantStatisticsScopeCheckmarks()
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RefreshTaggedMenuCheckmarks<CombatantStatisticsScope>(_combatantStatisticsScopeMenuItem, scope => scope == vm.CombatantStatisticsScope);
    }

    private void RebuildCombatantSortMetricMenuItems()
    {
        RefreshCombatantSortMetricHeader();
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RebuildTaggedMenuItems(_combatantSortMetricMenuItem, vm.CombatantSortMetricOptions, metric => vm.Localization[$"Settings_CombatantSortMetric_{metric}"], metric => metric, metric => metric == vm.CombatantSortMetric, CombatantSortMetricItemClicked);
    }

    private void RefreshCombatantSortMetricCheckmarks()
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RefreshTaggedMenuCheckmarks<CombatantSortMetric>(_combatantSortMetricMenuItem, metric => metric == vm.CombatantSortMetric);
    }

    private void RebuildMainMetricVisibilityMenuItems()
    {
        var vm = ViewModel;
        RefreshMainMetricVisibilityHeader();
        if (_mainMetricVisibilityMenuItem is null || vm is null)
        {
            return;
        }

        _mainMetricVisibilityMenuItem.Items.Clear();
        _showDamagePerSecondColumnMenuItem = CreateToggleMenuItem(vm.Localization["Settings_MainMetricVisibility_DamagePerSecond"], vm.ShowDamagePerSecondColumnDisplay, vm.ShowDamagePerSecondColumn, ShowDamagePerSecondColumnMenuItemClicked);
        _showDamageColumnMenuItem = CreateToggleMenuItem(vm.Localization["Settings_MainMetricVisibility_Damage"], vm.ShowDamageColumnDisplay, vm.ShowDamageColumn, ShowDamageColumnMenuItemClicked);
        _showHealingPerSecondColumnMenuItem = CreateToggleMenuItem(vm.Localization["Settings_MainMetricVisibility_HealingPerSecond"], vm.ShowHealingPerSecondColumnDisplay, vm.ShowHealingPerSecondColumn, ShowHealingPerSecondColumnMenuItemClicked);
        _showHealingColumnMenuItem = CreateToggleMenuItem(vm.Localization["Settings_MainMetricVisibility_Healing"], vm.ShowHealingColumnDisplay, vm.ShowHealingColumn, ShowHealingColumnMenuItemClicked);
        _showDamageTakenColumnMenuItem = CreateToggleMenuItem(vm.Localization["Settings_MainMetricVisibility_DamageTaken"], vm.ShowDamageTakenColumnDisplay, vm.ShowDamageTakenColumn, ShowDamageTakenColumnMenuItemClicked);
        _showUnhealedHealthColumnMenuItem = CreateToggleMenuItem(vm.Localization["Settings_MainMetricVisibility_UnhealedHealth"], vm.ShowUnhealedHealthColumnDisplay, vm.ShowUnhealedHealthColumn, ShowUnhealedHealthColumnMenuItemClicked);
        _showAggroColumnMenuItem = CreateToggleMenuItem(vm.Localization["Settings_MainMetricVisibility_Aggro"], vm.ShowAggroColumnDisplay, vm.ShowAggroColumn, ShowAggroColumnMenuItemClicked);
        _showStaggerColumnMenuItem = CreateToggleMenuItem(vm.Localization["Settings_MainMetricVisibility_Stagger"], vm.ShowStaggerColumnDisplay, vm.ShowStaggerColumn, ShowStaggerColumnMenuItemClicked);
        _showBossShareColumnMenuItem = CreateToggleMenuItem(vm.Localization["Settings_MainMetricVisibility_BossShare"], vm.ShowBossShareColumnDisplay, vm.ShowBossShareColumn, ShowBossShareColumnMenuItemClicked);
        _showTotalDamagePerSecondMenuItem = CreateToggleMenuItem(vm.Localization["Settings_MainMetricVisibility_TotalDamagePerSecond"], vm.ShowTotalDamagePerSecondDisplay, vm.ShowTotalDamagePerSecond, ShowTotalDamagePerSecondMenuItemClicked);
        _mainMetricVisibilityMenuItem.Items.Add(_showDamagePerSecondColumnMenuItem);
        _mainMetricVisibilityMenuItem.Items.Add(_showDamageColumnMenuItem);
        _mainMetricVisibilityMenuItem.Items.Add(_showHealingPerSecondColumnMenuItem);
        _mainMetricVisibilityMenuItem.Items.Add(_showHealingColumnMenuItem);
        _mainMetricVisibilityMenuItem.Items.Add(_showDamageTakenColumnMenuItem);
        _mainMetricVisibilityMenuItem.Items.Add(_showUnhealedHealthColumnMenuItem);
        _mainMetricVisibilityMenuItem.Items.Add(_showAggroColumnMenuItem);
        _mainMetricVisibilityMenuItem.Items.Add(_showStaggerColumnMenuItem);
        _mainMetricVisibilityMenuItem.Items.Add(_showBossShareColumnMenuItem);
        _mainMetricVisibilityMenuItem.Items.Add(_showTotalDamagePerSecondMenuItem);
    }

    private void RefreshMainMetricVisibilityMenuItems()
    {
        var vm = ViewModel;
        RefreshMainMetricVisibilityHeader();
        if (vm is null)
        {
            return;
        }

        RefreshToggleMenuItem(_showDamagePerSecondColumnMenuItem, vm.Localization["Settings_MainMetricVisibility_DamagePerSecond"], vm.ShowDamagePerSecondColumnDisplay, vm.ShowDamagePerSecondColumn);
        RefreshToggleMenuItem(_showDamageColumnMenuItem, vm.Localization["Settings_MainMetricVisibility_Damage"], vm.ShowDamageColumnDisplay, vm.ShowDamageColumn);
        RefreshToggleMenuItem(_showHealingPerSecondColumnMenuItem, vm.Localization["Settings_MainMetricVisibility_HealingPerSecond"], vm.ShowHealingPerSecondColumnDisplay, vm.ShowHealingPerSecondColumn);
        RefreshToggleMenuItem(_showHealingColumnMenuItem, vm.Localization["Settings_MainMetricVisibility_Healing"], vm.ShowHealingColumnDisplay, vm.ShowHealingColumn);
        RefreshToggleMenuItem(_showDamageTakenColumnMenuItem, vm.Localization["Settings_MainMetricVisibility_DamageTaken"], vm.ShowDamageTakenColumnDisplay, vm.ShowDamageTakenColumn);
        RefreshToggleMenuItem(_showUnhealedHealthColumnMenuItem, vm.Localization["Settings_MainMetricVisibility_UnhealedHealth"], vm.ShowUnhealedHealthColumnDisplay, vm.ShowUnhealedHealthColumn);
        RefreshToggleMenuItem(_showAggroColumnMenuItem, vm.Localization["Settings_MainMetricVisibility_Aggro"], vm.ShowAggroColumnDisplay, vm.ShowAggroColumn);
        RefreshToggleMenuItem(_showStaggerColumnMenuItem, vm.Localization["Settings_MainMetricVisibility_Stagger"], vm.ShowStaggerColumnDisplay, vm.ShowStaggerColumn);
        RefreshToggleMenuItem(_showBossShareColumnMenuItem, vm.Localization["Settings_MainMetricVisibility_BossShare"], vm.ShowBossShareColumnDisplay, vm.ShowBossShareColumn);
        RefreshToggleMenuItem(_showTotalDamagePerSecondMenuItem, vm.Localization["Settings_MainMetricVisibility_TotalDamagePerSecond"], vm.ShowTotalDamagePerSecondDisplay, vm.ShowTotalDamagePerSecond);
    }

    private void RebuildEncounterTimeDisplayFormatMenuItems()
    {
        RefreshEncounterTimeDisplayFormatHeader();
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RebuildTaggedMenuItems(
            _encounterTimeDisplayFormatMenuItem,
            vm.EncounterTimeDisplayFormatOptions,
            format => vm.Localization[$"Settings_TimeDisplayFormat_{format}"],
            static format => format,
            format => format == vm.EncounterTimeDisplayFormat,
            EncounterTimeDisplayFormatItemClicked);
    }

    private void RefreshEncounterTimeDisplayFormatCheckmarks()
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RefreshTaggedMenuCheckmarks<EncounterTimeDisplayFormat>(
            _encounterTimeDisplayFormatMenuItem,
            format => format == vm.EncounterTimeDisplayFormat);
    }

    private void RebuildSceneKindMenuItems()
    {
        RefreshSceneKindHeader();
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RebuildTaggedMenuItems(_sceneKindMenuItem, vm.SceneKindOptions, kind => vm.Localization[$"Settings_SceneKind_{kind}"], kind => kind, kind => kind == vm.SceneKind, SceneKindItemClicked);
    }

    private void RefreshSceneKindCheckmarks()
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RefreshTaggedMenuCheckmarks<SceneKind>(_sceneKindMenuItem, kind => kind == vm.SceneKind);
    }

    private void RebuildPlayerNameSettingsMenuItems()
    {
        var vm = ViewModel;
        RefreshPlayerNameSettingsHeader();
        if (_playerNameSettingsMenuItem is null || vm is null)
        {
            return;
        }

        _playerNameSettingsMenuItem.Items.Clear();
        _showPlayerNamesMenuItem = CreateToggleMenuItem(vm.Localization["Settings_ShowPlayerNames"], vm.ShowPlayerNamesDisplay, vm.ShowPlayerNames, ShowPlayerNamesMenuItemClicked);
        _playerSelfMarkerMenuItem = new MenuItem();
        _playerSelfMarkerMenuItem.Classes.Add("FlyoutMenuItem");
        _playerSelfMarkerMenuItem.Classes.Add("FlyoutPanelRow");
        _playerSelfMarkerMenuItem.Classes.Add("SettingsRowItem");
        RebuildTaggedMenuItems(_playerSelfMarkerMenuItem, vm.PlayerSelfMarkerDisplayModeOptions, mode => vm.Localization[$"Settings_PlayerSelfMarker_{mode}"], mode => mode, mode => mode == vm.PlayerSelfMarkerDisplayMode, PlayerSelfMarkerDisplayModeItemClicked, staysOpenOnClick: true);

        _showPlayerShortServerNameMenuItem = CreateToggleMenuItem(vm.Localization["Settings_PlayerShortServerName"], vm.ShowPlayerShortServerNameDisplay, vm.ShowPlayerShortServerName, ShowPlayerShortServerNameMenuItemClicked);
        _showPlayerLegionNameMenuItem = CreateToggleMenuItem(vm.Localization["Settings_PlayerLegionName"], vm.ShowPlayerLegionNameDisplay, vm.ShowPlayerLegionName, ShowPlayerLegionNameMenuItemClicked);
        _tintPlayerNamesByFactionMenuItem = CreateToggleMenuItem(vm.Localization["Settings_PlayerFactionTint"], vm.TintPlayerNamesByFactionDisplay, vm.TintPlayerNamesByFaction, TintPlayerNamesByFactionMenuItemClicked);

        _playerNameSettingsMenuItem.Items.Add(_showPlayerNamesMenuItem);
        _playerNameSettingsMenuItem.Items.Add(_playerSelfMarkerMenuItem);
        _playerNameSettingsMenuItem.Items.Add(_showPlayerShortServerNameMenuItem);
        _playerNameSettingsMenuItem.Items.Add(_showPlayerLegionNameMenuItem);
        _playerNameSettingsMenuItem.Items.Add(_tintPlayerNamesByFactionMenuItem);
        RefreshPlayerNameSettingsMenuItems();
    }

    private void RefreshPlayerNameSettingsMenuItems()
    {
        RefreshPlayerNameSettingsHeader();
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        if (_showPlayerNamesMenuItem is not null)
        {
            RefreshToggleMenuItem(_showPlayerNamesMenuItem, vm.Localization["Settings_ShowPlayerNames"], vm.ShowPlayerNamesDisplay, vm.ShowPlayerNames);
        }

        if (_playerSelfMarkerMenuItem is not null)
        {
            _playerSelfMarkerMenuItem.Header = CreateRowHeader(vm.Localization["Settings_PlayerSelfMarker"], vm.PlayerSelfMarkerDisplayModeDisplay);
            RefreshTaggedMenuCheckmarks<PlayerSelfMarkerDisplayMode>(_playerSelfMarkerMenuItem, mode => mode == vm.PlayerSelfMarkerDisplayMode);
        }

        if (_showPlayerShortServerNameMenuItem is not null)
        {
            RefreshToggleMenuItem(_showPlayerShortServerNameMenuItem, vm.Localization["Settings_PlayerShortServerName"], vm.ShowPlayerShortServerNameDisplay, vm.ShowPlayerShortServerName);
        }

        if (_showPlayerLegionNameMenuItem is not null)
        {
            RefreshToggleMenuItem(_showPlayerLegionNameMenuItem, vm.Localization["Settings_PlayerLegionName"], vm.ShowPlayerLegionNameDisplay, vm.ShowPlayerLegionName);
        }

        if (_tintPlayerNamesByFactionMenuItem is not null)
        {
            RefreshToggleMenuItem(_tintPlayerNamesByFactionMenuItem, vm.Localization["Settings_PlayerFactionTint"], vm.TintPlayerNamesByFactionDisplay, vm.TintPlayerNamesByFaction);
        }
    }

    private void ToggleRecording()
    {
        try
        {
            if (PacketRecording.IsRecording)
                PacketRecording.Stop();
            else
                PacketRecording.Start(LogDirectoryResolver.GetDefaultLogDirectory());
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Packet recording could not start: {ex}");
        }

        RefreshSections();
    }

    // Stops the recording and saves it where the user picks; cancelling the dialog leaves it running.
    private async void SavePacketsClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage || ViewModel is not { } vm)
            return;

        try
        {
            using var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = vm.Localization["Settings_Debug_SavePackets"],
                SuggestedFileName = $"Aion2Heal packets {DateTime.Now:yyyy-MM-dd HH-mm}{PacketRecording.Extension}",
                DefaultExtension = PacketRecording.Extension.TrimStart('.'),
                FileTypeChoices = [new FilePickerFileType(vm.Localization["Settings_Debug_FileType"]) { Patterns = ["*" + PacketRecording.Extension] }],
                ShowOverwritePrompt = true
            });
            if (file is null)
                return;

            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek)
                stream.SetLength(0);
            await PacketRecording.SaveToAsync(stream);
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Saving the packet recording failed: {ex}");
        }
        finally
        {
            RefreshSections();
        }
    }

    private void RefreshColumnHeadersMenuItem()
    {
        var vm = ViewModel;
        if (_showColumnHeadersMenuItem is null || vm is null)
        {
            return;
        }

        RefreshToggleMenuItem(_showColumnHeadersMenuItem, vm.Localization["Settings_MainMetricVisibility_ColumnHeaders"], vm.ShowColumnHeadersDisplay, vm.ShowColumnHeaders);
    }

    private void RefreshOverhealingMenuItem()
    {
        var vm = ViewModel;
        if (_includeOverhealingMenuItem is null || vm is null)
        {
            return;
        }

        RefreshToggleMenuItem(_includeOverhealingMenuItem, vm.Localization["Settings_MainMetricVisibility_Overhealing"], vm.IncludeOverhealingDisplay, vm.IncludeOverhealing);
    }

    private void RefreshCompactMainMetricsMenuItem()
    {
        var vm = ViewModel;
        if (_compactMainMetricsMenuItem is null || vm is null)
        {
            return;
        }

        RefreshToggleMenuItem(_compactMainMetricsMenuItem, vm.Localization["Settings_MainMetricsCompact"], vm.UseCompactMainMetricsDisplay, vm.UseCompactMainMetrics);
    }

    private void RefreshFocusStatusBarMenuItem()
    {
        var vm = ViewModel;
        if (_focusStatusBarMenuItem is null || vm is null)
        {
            return;
        }

        RefreshToggleMenuItem(_focusStatusBarMenuItem, vm.Localization["Settings_FocusStatusBar"], vm.ShowFocusStatusBarDisplay, vm.ShowFocusStatusBar);
    }

    private void RefreshHideHeaderWhenClickThroughMenuItem()
    {
        var vm = ViewModel;
        if (_hideHeaderWhenClickThroughMenuItem is null || vm is null)
        {
            return;
        }

        RefreshToggleMenuItem(
            _hideHeaderWhenClickThroughMenuItem,
            vm.Localization["Settings_ClickThroughTitleBar"],
            vm.HideHeaderWhenClickThroughDisplay,
            vm.HideHeaderWhenClickThrough);
    }

    private void RebuildLanguageMenuItems()
    {
        RefreshLanguageHeader();
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RebuildTaggedMenuItems(_languageMenuItem, vm.Languages, static option => option.DisplayName, static option => option.Code, option => string.Equals(option.Code, vm.SelectedLanguage?.Code, StringComparison.Ordinal), LanguageItemClicked);
    }

    private void RefreshLanguageCheckmarks()
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        RefreshTaggedMenuCheckmarks<string>(_languageMenuItem, code => string.Equals(code, vm.SelectedLanguage?.Code, StringComparison.Ordinal));
    }

    private void TopmostModeItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && sender is MenuItem { Tag: TopmostMode mode })
        {
            vm.TopmostMode = mode;
        }
    }

    private void VisibleRowsItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && sender is MenuItem { Tag: int count })
        {
            vm.MaxVisibleCombatantRows = count;
        }
    }

    private void CombatantSortMetricItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && sender is MenuItem { Tag: CombatantSortMetric metric })
        {
            vm.CombatantSortMetric = metric;
        }
    }

    private void CombatantStatisticsScopeItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && sender is MenuItem { Tag: CombatantStatisticsScope scope })
        {
            vm.CombatantStatisticsScope = scope;
        }
    }

    private void SceneKindItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && sender is MenuItem { Tag: SceneKind kind })
            vm.SceneKind = kind;
    }

    private void EncounterTimeDisplayFormatItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm &&
            sender is MenuItem { Tag: EncounterTimeDisplayFormat displayFormat })
        {
            vm.EncounterTimeDisplayFormat = displayFormat;
        }
    }

    private void ShowPlayerNamesMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowPlayerNames = !vm.ShowPlayerNames;
        }

        e.Handled = true;
    }

    private void PlayerSelfMarkerDisplayModeItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && sender is MenuItem { Tag: PlayerSelfMarkerDisplayMode mode })
        {
            vm.PlayerSelfMarkerDisplayMode = mode;
        }

        e.Handled = true;
    }

    private void ShowPlayerShortServerNameMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowPlayerShortServerName = !vm.ShowPlayerShortServerName;
        }

        e.Handled = true;
    }

    private void ShowPlayerLegionNameMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowPlayerLegionName = !vm.ShowPlayerLegionName;
        }

        e.Handled = true;
    }

    private void TintPlayerNamesByFactionMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.TintPlayerNamesByFaction = !vm.TintPlayerNamesByFaction;
        }

        e.Handled = true;
    }

    private void CompactMainMetricsMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.UseCompactMainMetrics = !vm.UseCompactMainMetrics;
        }
    }

    private void FocusStatusBarMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowFocusStatusBar = !vm.ShowFocusStatusBar;
        }
    }

    private void HideHeaderWhenClickThroughMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.HideHeaderWhenClickThrough = !vm.HideHeaderWhenClickThrough;
        }
    }

    private void ShowDamagePerSecondColumnMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowDamagePerSecondColumn = !vm.ShowDamagePerSecondColumn;
        }

        e.Handled = true;
    }

    private void ShowDamageColumnMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowDamageColumn = !vm.ShowDamageColumn;
        }

        e.Handled = true;
    }

    private void ShowHealingPerSecondColumnMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowHealingPerSecondColumn = !vm.ShowHealingPerSecondColumn;
        }

        e.Handled = true;
    }

    private void ShowHealingColumnMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowHealingColumn = !vm.ShowHealingColumn;
        }

        e.Handled = true;
    }

    private void IncludeOverhealingMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.IncludeOverhealing = !vm.IncludeOverhealing;
        }

        e.Handled = true;
    }

    private void ShowDamageTakenColumnMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowDamageTakenColumn = !vm.ShowDamageTakenColumn;
        }

        e.Handled = true;
    }

    private void ShowAggroColumnMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowAggroColumn = !vm.ShowAggroColumn;
        }

        e.Handled = true;
    }

    private void ShowStaggerColumnMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowStaggerColumn = !vm.ShowStaggerColumn;
        }

        e.Handled = true;
    }

    private void ShowBossShareColumnMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowBossShareColumn = !vm.ShowBossShareColumn;
        }

        e.Handled = true;
    }

    private void ShowUnhealedHealthColumnMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowUnhealedHealthColumn = !vm.ShowUnhealedHealthColumn;
        }

        e.Handled = true;
    }

    private void ShowTotalDamagePerSecondMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowTotalDamagePerSecond = !vm.ShowTotalDamagePerSecond;
        }

        e.Handled = true;
    }

    private void ShowColumnHeadersMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowColumnHeaders = !vm.ShowColumnHeaders;
        }

        e.Handled = true;
    }

    private void LanguageItemClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && sender is MenuItem { Tag: string code })
        {
            var option = vm.Languages.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.Ordinal));
            if (option is not null)
            {
                vm.SelectedLanguage = option;
            }
        }
    }

    private void RefreshTopmostHeader()
    {
        var vm = ViewModel;
        if (_topmostMenuItem is null || vm is null) return;
        _topmostMenuItem.Header = CreateRowHeader(vm.Localization["Settings_Topmost"], vm.TopmostModeDisplay);
    }

    private void RefreshVisibleRowsHeader()
    {
        var vm = ViewModel;
        if (_visibleRowsMenuItem is null || vm is null) return;
        _visibleRowsMenuItem.Header = CreateRowHeader(vm.Localization["Settings_VisibleRows"], vm.MaxVisibleCombatantRowsDisplay);
    }

    private void RefreshCombatantStatisticsScopeHeader()
    {
        var vm = ViewModel;
        if (_combatantStatisticsScopeMenuItem is null || vm is null) return;
        _combatantStatisticsScopeMenuItem.Header = CreateRowHeader(vm.Localization["Settings_CombatantStatisticsScope"], vm.CombatantStatisticsScopeDisplay);
    }

    private void RefreshCombatantSortMetricHeader()
    {
        var vm = ViewModel;
        if (_combatantSortMetricMenuItem is null || vm is null) return;
        _combatantSortMetricMenuItem.Header = CreateRowHeader(vm.Localization["Settings_CombatantSortMetric"], vm.CombatantSortMetricDisplay);
    }

    private void RefreshSceneKindHeader()
    {
        var vm = ViewModel;
        if (_sceneKindMenuItem is null || vm is null) return;
        _sceneKindMenuItem.Header = CreateRowHeader(vm.Localization["Settings_SceneKind"], vm.SceneKindDisplay);
    }

    private void RefreshMainMetricVisibilityHeader()
    {
        var vm = ViewModel;
        if (_mainMetricVisibilityMenuItem is null || vm is null) return;
        _mainMetricVisibilityMenuItem.Header = CreateRowHeader(vm.Localization["Settings_MainMetricVisibility"], vm.MainMetricVisibilityDisplay);
    }

    private void RefreshEncounterTimeDisplayFormatHeader()
    {
        var vm = ViewModel;
        if (_encounterTimeDisplayFormatMenuItem is null || vm is null) return;
        _encounterTimeDisplayFormatMenuItem.Header = CreateRowHeader(
            vm.Localization["Settings_TimeDisplayFormat"],
            vm.EncounterTimeDisplayFormatDisplay);
    }

    private void RefreshLanguageHeader()
    {
        var vm = ViewModel;
        if (_languageMenuItem is null || vm is null) return;
        _languageMenuItem.Header = CreateRowHeader(vm.Localization["Settings_Language"], vm.LanguageDisplay);
    }

    private void RefreshPlayerNameSettingsHeader()
    {
        var vm = ViewModel;
        if (_playerNameSettingsMenuItem is null || vm is null) return;
        _playerNameSettingsMenuItem.Header = CreateRowHeader(vm.Localization["Settings_PlayerNameSettings"], vm.PlayerNameSettingsDisplay);
    }

    internal static void RebuildTaggedMenuItems<TOption, TTag>(
        MenuItem? menuItem,
        IEnumerable<TOption> options,
        Func<TOption, string> resolveHeader,
        Func<TOption, TTag> resolveTag,
        Func<TOption, bool> isSelected,
        EventHandler<RoutedEventArgs> click,
        bool staysOpenOnClick = false)
    {
        if (menuItem is null)
            return;

        menuItem.Items.Clear();
        foreach (var option in options)
        {
            var item = new MenuItem
            {
                Header = resolveHeader(option),
                Tag = resolveTag(option),
                StaysOpenOnClick = staysOpenOnClick
            };
            item.Classes.Add("FlyoutMenuItem");
            item.Classes.Add("SettingsOptionItem");
            item.Icon = CreateCheckmark(isSelected(option));
            item.Click += click;
            menuItem.Items.Add(item);
        }
    }

    internal static void RefreshTaggedMenuCheckmarks<TTag>(MenuItem? menuItem, Func<TTag, bool> isSelected)
    {
        if (menuItem is null)
            return;

        foreach (var child in menuItem.Items)
        {
            if (child is MenuItem { Tag: TTag value } mi)
                mi.Icon = CreateCheckmark(isSelected(value));
        }
    }

    internal static void RefreshToggleMenuItem(MenuItem? menuItem, string label, string value, bool isChecked)
    {
        if (menuItem is null)
            return;

        SetRowHeader(menuItem, label, value);
        if ((menuItem.Icon is not null) != isChecked)
            menuItem.Icon = CreateCheckmark(isChecked);
    }

    // A row's label and value: its header stays when it has one (the same text again changes nothing), so a refresh of
    // every row costs little.
    internal static void SetRowHeader(MenuItem menuItem, string label, string value)
    {
        if (menuItem.Header is Grid { Children: [TextBlock labelText, TextBlock valueText] })
            (labelText.Text, valueText.Text) = (label, value);
        else
            menuItem.Header = CreateRowHeader(label, value);
    }

    internal static MenuItem CreateToggleMenuItem(string label, string value, bool isChecked, EventHandler<RoutedEventArgs> click)
    {
        var item = new MenuItem
        {
            Header = CreateRowHeader(label, value),
            Icon = CreateCheckmark(isChecked),
            StaysOpenOnClick = true
        };
        item.Classes.Add("FlyoutMenuItem");
        item.Classes.Add("FlyoutPanelRow");
        item.Classes.Add("SettingsRowItem");
        item.Click += click;
        return item;
    }

    internal static Grid CreateRowHeader(string label, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var labelText = new TextBlock { Text = label };
        labelText.Classes.Add("SettingsRowLabel");
        Grid.SetColumn(labelText, 0);
        grid.Children.Add(labelText);

        var valueText = new TextBlock { Text = value };
        valueText.Classes.Add("SettingsRowValue");
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);

        return grid;
    }

    private static Avalonia.Controls.Shapes.Path? CreateCheckmark(bool visible)
    {
        if (!visible)
        {
            return null;
        }

        var resources = Avalonia.Application.Current?.Resources;
        if (resources is null || !resources.TryGetResource("check", null, out var resource) || resource is not Avalonia.Media.Geometry geometry)
        {
            return null;
        }

        var path = new Avalonia.Controls.Shapes.Path { Data = geometry };
        path.Classes.Add("Glyph");
        path.Classes.Add("GlyphLg");
        return path;
    }

    private void HotkeyRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || ViewModel is not { } vm || !TryGetHotkeyAction(border, out var action))
        {
            return;
        }

        border.Focus();
        vm.BeginCaptureHotkey(action);
        e.Handled = true;
    }

    private void HotkeyRowKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Border border
            || ViewModel is not { } vm
            || !TryGetHotkeyAction(border, out var action))
        {
            return;
        }

        if (vm.CapturingHotkeyAction != action)
        {
            if ((e.Key is Key.Enter or Key.Space) && e.KeyModifiers == KeyModifiers.None)
            {
                vm.BeginCaptureHotkey(action);
                e.Handled = true;
            }
            return;
        }

        if (e.Key is Key.Escape)
        {
            vm.CancelCaptureHotkey(action);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin)
        {
            return;
        }

        var definition = HotkeyDefinition.FromKeyEvent(e.KeyModifiers, e.Key);
        if (definition is null)
        {
            return;
        }

        vm.ApplyCapturedHotkey(action, definition);
        e.Handled = true;
    }

    private void HotkeyRowLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is Border border
            && ViewModel is { } vm
            && TryGetHotkeyAction(border, out var action))
        {
            vm.CancelCaptureHotkey(action);
        }
    }

    private static bool TryGetHotkeyAction(Border border, out GlobalHotkeyAction action)
    {
        if (border.Tag is GlobalHotkeyAction value)
        {
            action = value;
            return true;
        }

        action = default;
        return false;
    }
}
