using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Cloris.Aion2Flow.Views;

public partial class MainWindow : Window
{
    private const uint WmEnterSizeMove = 0x0231;
    private const uint WmExitSizeMove = 0x0232;
    private const int MaxPinNativeRetries = 3;
    private const int MaxNativeStyleRetries = 3;
    private static readonly TimeSpan CursorProbeInterval = TimeSpan.FromMilliseconds(33);
    private static readonly object LiveSceneMenuItemTag = new();

    private Task _exportTask = Task.CompletedTask;
    private readonly GlobalHotkeyService _globalHotkeyService;
    private readonly OverlayInteractionController _overlayInteractionController;
    private readonly SettingsService _settingsService;
    private readonly AvaloniaFrameClockService _frameClock;
    private readonly UiScaleService _uiScale;
    private readonly LocalizationService _localization;
    private readonly SkillMonitorWindowController _skillMonitorWindowController;
    private readonly GroupFrameWindowController _groupFrameWindowController;
    private readonly FightGraphsWindowController _fightGraphsWindowController;
    private readonly HiddenCubesWindowController _hiddenCubesWindowController;
    private readonly PlaybackWindowController _playbackWindowController = new();
    private readonly Control? _overlayRoot;
    private OverlayPinWindow? _pinWindow;
    private bool _hotkeyAttached;
    private bool _frameClockAttached;
    private bool _isNativeSizeMoveActive;
    private bool _autoHeightRefreshQueued;
    private bool _overlayGeometryRefreshQueued;
    private bool _nativeStyleRefreshQueued;
    private bool _hasAppliedOverlayInteractionMode;
    private bool _hasCursorProbeTimestamp;
    private bool _hasOverlayScreenBounds;
    private int _pinNativeRetryCount;
    private int _nativeStyleRetryCount;
    private TimeSpan _lastCursorProbeTimestamp;
    private OverlayInteractionMode _appliedOverlayInteractionMode;
    private OverlayWindowInputState _windowInputState;
    private int _overlayScreenLeft;
    private int _overlayScreenTop;
    private int _overlayScreenRight;
    private int _overlayScreenBottom;

    public new MainViewModel DataContext { get => (MainViewModel)base.DataContext!; set => base.DataContext = value; }

    public MainWindow()
    {
        DataContext = Ioc.Default.GetRequiredService<MainViewModel>();
        _globalHotkeyService = Ioc.Default.GetRequiredService<GlobalHotkeyService>();
        _overlayInteractionController = Ioc.Default.GetRequiredService<OverlayInteractionController>();
        _settingsService = Ioc.Default.GetRequiredService<SettingsService>();
        _frameClock = Ioc.Default.GetRequiredService<AvaloniaFrameClockService>();
        _uiScale = Ioc.Default.GetRequiredService<UiScaleService>();
        _localization = Ioc.Default.GetRequiredService<LocalizationService>();
        _skillMonitorWindowController = Ioc.Default.GetRequiredService<SkillMonitorWindowController>();
        _groupFrameWindowController = Ioc.Default.GetRequiredService<GroupFrameWindowController>();
        _fightGraphsWindowController = Ioc.Default.GetRequiredService<FightGraphsWindowController>();
        _hiddenCubesWindowController = Ioc.Default.GetRequiredService<HiddenCubesWindowController>();
        DataContext.InitializeAsync().ConfigureAwait(false);
        InitializeComponent();
        _overlayRoot = Content as Control;
        DataContext.EncounterHistory.CollectionChanged += OnEncounterHistoryCollectionChanged;
        DataContext.SettingsFlyout.PropertyChanged += OnSettingsFlyoutPropertyChanged;
        DataContext.CombatantColumns.PropertyChanged += OnCombatantColumnsPropertyChanged;
        Width = DataContext.CombatantColumns.OverlayWidth;
        DataContext.IsMeterFolded = DataContext.SettingsFlyout.IsMinimized(SettingsFlyoutViewModel.MeterKey);
        RefreshHeaderVisibilityPreference();
        RebuildEncounterHistoryMenuItems();
        _globalHotkeyService.Triggered += OnGlobalHotkeyTriggered;
        DigitalTwinLibraryWindow.OpenChanged += OnTwinLibraryOpenChanged;
        _overlayInteractionController.ModeChanged += OnOverlayInteractionModeChanged;
        PositionChanged += OnWindowPositionChanged;
        ScalingChanged += OnWindowScalingChanged;
        if (_settingsService.Current.MainWindowPosition.HasValue)
        {
            Position = new(_settingsService.Current.MainWindowPosition.Value.X, _settingsService.Current.MainWindowPosition.Value.Y);
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        DataContext.SettingsFlyout.RefreshTargetProcessForegroundState();
        _uiScale.RegisterWindow(this);
        RebuildEncounterHistoryMenuItems();
        AttachGlobalHotkeyHook();
        AttachFrameClock();
        ShowPinWindow();
        _skillMonitorWindowController.ShowOrActivate(this);
        _groupFrameWindowController.ShowOrActivate(this);
        _fightGraphsWindowController.ShowOrActivate(this);
        _hiddenCubesWindowController.ShowOrActivate(this);
        ApplyBackdropFill();
        ApplyOverlayInteractionMode(_overlayInteractionController.Mode);
    }

    private void AttachFrameClock()
    {
        if (_frameClockAttached)
        {
            return;
        }

        _frameClockAttached = true;
        _frameClock.Frame += OnAnimationFrame;
        _frameClock.Attach(this);
    }

    private void OnAnimationFrame(object? sender, AvaloniaFrameEventArgs e)
    {
        DataContext.ProcessUiFrame(e.Timestamp);
        UpdateClickThroughCursorState(e.Timestamp);
    }

    private void AttachGlobalHotkeyHook()
    {
        if (_hotkeyAttached)
        {
            return;
        }

        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        Win32Properties.AddWndProcHookCallback(this, WndProcHook);
        _globalHotkeyService.AttachWindow(hwnd);
        DataContext.SettingsFlyout.RefreshHotkeyRegistrationState(_globalHotkeyService.IsAttachedTo(hwnd));
        _hotkeyAttached = true;
    }

    private nint WndProcHook(nint hWnd, uint msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == GlobalHotkeyService.WmHotkey)
        {
            _globalHotkeyService.HandleWindowMessage(msg, wParam);
        }
        else if (msg == WmEnterSizeMove)
        {
            _isNativeSizeMoveActive = true;
        }
        else if (msg == WmExitSizeMove)
        {
            _isNativeSizeMoveActive = false;
            ScheduleOverlayAutoHeightRefresh();
        }

        return default;
    }

    private void OnGlobalHotkeyTriggered(GlobalHotkeyAction action)
    {
        if (DataContext.SettingsFlyout.CapturingHotkeyAction is not null)
        {
            return;
        }

        if (action == GlobalHotkeyAction.CycleOverlayInteraction)
        {
            _overlayInteractionController.Cycle();
            return;
        }

        if (action == GlobalHotkeyAction.MinimizeAllFrames)
        {
            DataContext.SettingsFlyout.ToggleMinimizeAll();
            return;
        }

        if (action == GlobalHotkeyAction.BattleReset && DataContext.IsCapturing && DataContext.ResetCommand.CanExecute(null))
        {
            DataContext.ResetCommand.Execute(null);
        }
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (!_exportTask.IsCompleted)
        {
            e.Cancel = true;
            try
            {
                await _exportTask;
            }
            catch
            {
                // The export shows its own failure.
            }

            Close();
            return;
        }

        DataContext.EncounterHistory.CollectionChanged -= OnEncounterHistoryCollectionChanged;
        DataContext.SettingsFlyout.PropertyChanged -= OnSettingsFlyoutPropertyChanged;
        DataContext.CombatantColumns.PropertyChanged -= OnCombatantColumnsPropertyChanged;
        _globalHotkeyService.Triggered -= OnGlobalHotkeyTriggered;
        _overlayInteractionController.ModeChanged -= OnOverlayInteractionModeChanged;
        PositionChanged -= OnWindowPositionChanged;
        ScalingChanged -= OnWindowScalingChanged;
        _globalHotkeyService.DetachWindow();
        _settingsService.Update(settings => settings.MainWindowPosition = new(Position.X, Position.Y));
        _playbackWindowController.Close();
        _skillMonitorWindowController.Close();
        _groupFrameWindowController.Close();
        _fightGraphsWindowController.Close();
        _hiddenCubesWindowController.Close();
        ClosePinWindow();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        DigitalTwinLibraryWindow.OpenChanged -= OnTwinLibraryOpenChanged;
        if (_frameClockAttached)
        {
            _frameClock.Frame -= OnAnimationFrame;
            _frameClock.Detach(this);
            _frameClockAttached = false;
        }

        if (_hotkeyAttached)
        {
            Win32Properties.RemoveWndProcHookCallback(this, WndProcHook);
            _hotkeyAttached = false;
        }

        base.OnClosed(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ClientSizeProperty)
        {
            InvalidateOverlayScreenGeometry();
        }
        else if (change.Property == WindowStateProperty)
        {
            InvalidateOverlayScreenGeometry();
            if (WindowState != WindowState.Minimized)
            {
                ScheduleNativeStyleRefresh();
            }
        }
    }

    private void ShowPinWindow()
    {
        if (_pinWindow is not null)
        {
            return;
        }

        var pinWindow = new OverlayPinWindow(_overlayInteractionController, _localization, _uiScale);
        pinWindow.PlacementInvalidated += OnPinWindowPlacementInvalidated;
        _pinWindow = pinWindow;
        pinWindow.Show(this);
        if (!SynchronizeOverlayTopmostBand())
        {
            ScheduleNativeStyleRefresh();
        }
        ScheduleOverlayGeometryRefresh();
    }

    private void ClosePinWindow()
    {
        var pinWindow = _pinWindow;
        if (pinWindow is null)
        {
            return;
        }

        _pinWindow = null;
        pinWindow.PlacementInvalidated -= OnPinWindowPlacementInvalidated;
        pinWindow.Close();
    }

    private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e) => InvalidateOverlayScreenGeometry();

    private void OnWindowScalingChanged(object? sender, EventArgs e) => InvalidateOverlayScreenGeometry();

    private void OnPinWindowPlacementInvalidated(object? sender, EventArgs e)
    {
        _pinNativeRetryCount = 0;
        ScheduleOverlayGeometryRefresh();
    }

    private void InvalidateOverlayScreenGeometry()
    {
        _hasOverlayScreenBounds = false;
        _pinNativeRetryCount = 0;
        ScheduleOverlayGeometryRefresh();
    }

    private void ScheduleOverlayGeometryRefresh()
    {
        if (_overlayGeometryRefreshQueued)
        {
            return;
        }

        _overlayGeometryRefreshQueued = true;
        Dispatcher.UIThread.Post(RefreshOverlayScreenGeometry, DispatcherPriority.Loaded);
    }

    private void RefreshOverlayScreenGeometry()
    {
        _overlayGeometryRefreshQueued = false;
        if (PlatformImpl is null || WindowState == WindowState.Minimized)
        {
            return;
        }

        var overlayTopLeft = MainHudShell.PointToScreen(default);
        var overlayBottomRight = MainHudShell.PointToScreen(new Point(MainHudShell.Bounds.Width, MainHudShell.Bounds.Height));
        _overlayScreenLeft = overlayTopLeft.X;
        _overlayScreenTop = overlayTopLeft.Y;
        _overlayScreenRight = overlayBottomRight.X;
        _overlayScreenBottom = overlayBottomRight.Y;
        _hasOverlayScreenBounds = MainHudShell.Bounds.Width > 0 && MainHudShell.Bounds.Height > 0;
        if (_windowInputState == OverlayWindowInputState.ClickThroughArmed && IsCursorCurrentlyInsideOverlay())
        {
            TryApplyWindowInputState(OverlayWindowInputState.ClickThroughActive);
        }

        var pinWindow = _pinWindow;
        if (pinWindow is null || pinWindow.PlatformImpl is null)
        {
            return;
        }

        var pinTopLeft = OverlayPinSlot.PointToScreen(default);
        var pinBottomRight = OverlayPinSlot.PointToScreen(new Point(OverlayPinSlot.Bounds.Width, OverlayPinSlot.Bounds.Height));
        var styleApplied = pinWindow.ApplyNativeWindowStyle();
        var boundsApplied = pinWindow.SetScreenBounds(new PixelRect(
            pinTopLeft.X,
            pinTopLeft.Y,
            Math.Max(1, pinBottomRight.X - pinTopLeft.X),
            Math.Max(1, pinBottomRight.Y - pinTopLeft.Y)));
        if (styleApplied && boundsApplied)
        {
            _pinNativeRetryCount = 0;
        }
        else
        {
            SchedulePinNativeRetry();
        }
    }

    private void ScheduleNativeStyleRefresh()
    {
        _nativeStyleRetryCount = 0;
        QueueNativeStyleRefresh();
    }

    private void QueueNativeStyleRefresh()
    {
        if (_nativeStyleRefreshQueued)
        {
            return;
        }

        _nativeStyleRefreshQueued = true;
        Dispatcher.UIThread.Post(RefreshNativeWindowStyles, DispatcherPriority.Loaded);
    }

    private void RefreshNativeWindowStyles()
    {
        _nativeStyleRefreshQueued = false;
        if (PlatformImpl is null || WindowState == WindowState.Minimized)
        {
            return;
        }

        var mode = _overlayInteractionController.Mode;
        if (!NativeOverlayWindowStyles.SetInputTransparent(this, _windowInputState.RequiresInputTransparency()))
        {
            if (mode != OverlayInteractionMode.Interactive)
            {
                _overlayInteractionController.SetMode(OverlayInteractionMode.Interactive);
            }
            ScheduleNativeStyleRetry();
            return;
        }

        var pinStyleApplied = _pinWindow is not { } pinWindow || pinWindow.ApplyNativeWindowStyle();
        var topmostApplied = SynchronizeOverlayTopmostBand();
        if (!pinStyleApplied || !topmostApplied)
        {
            ScheduleNativeStyleRetry();
            return;
        }

        _nativeStyleRetryCount = 0;
    }

    private void ScheduleNativeStyleRetry()
    {
        if (_nativeStyleRetryCount >= MaxNativeStyleRetries)
        {
            return;
        }

        _nativeStyleRetryCount++;
        QueueNativeStyleRefresh();
    }

    private void SchedulePinNativeRetry()
    {
        if (_pinNativeRetryCount >= MaxPinNativeRetries)
        {
            return;
        }

        _pinNativeRetryCount++;
        ScheduleOverlayGeometryRefresh();
    }

    private void OnOverlayInteractionModeChanged(OverlayInteractionMode mode) => ApplyOverlayInteractionMode(mode);

    private void ApplyOverlayInteractionMode(OverlayInteractionMode mode)
    {
        var isPointerInside = mode == OverlayInteractionMode.ClickThrough && IsCursorCurrentlyInsideOverlay();
        var nextInputState = OverlayWindowInputStateLogic.EnterMode(mode, isPointerInside);
        if (!NativeOverlayWindowStyles.SetInputTransparent(this, nextInputState.RequiresInputTransparency()))
        {
            if (_hasAppliedOverlayInteractionMode && _overlayInteractionController.Mode != _appliedOverlayInteractionMode)
            {
                _overlayInteractionController.SetMode(_appliedOverlayInteractionMode);
            }
            return;
        }

        if (_hasAppliedOverlayInteractionMode)
        {
            CloseTransientSurfaces();
        }
        MainHudShell.Classes.Set("click-through", mode == OverlayInteractionMode.ClickThrough);
        MainHudShell.Opacity = mode == OverlayInteractionMode.Hidden ? 0d : DataContext.SettingsFlyout.OverlayContentOpacity;
        CommitWindowInputState(nextInputState);
        _appliedOverlayInteractionMode = mode;
        _hasAppliedOverlayInteractionMode = true;
        if (mode == OverlayInteractionMode.ClickThrough && DataContext.IsViewingArchivedEncounter)
        {
            DataContext.ReturnToLiveCommand.Execute(null);
        }
    }

    private bool SynchronizeOverlayTopmostBand()
    {
        if (PlatformImpl is null)
        {
            return false;
        }

        var isTopmost = DataContext.SettingsFlyout.IsAlwaysOnTop;
        if (!NativeOverlayWindowStyles.SetTopmostBand(this, isTopmost))
        {
            return false;
        }

        if (_pinWindow is not { PlatformImpl: not null } pinWindow)
        {
            return true;
        }

        return NativeOverlayWindowStyles.SetTopmostBand(pinWindow, isTopmost)
            && NativeOverlayWindowStyles.IsImmediatelyAbove(pinWindow, this);
    }

    private void OnSettingsFlyoutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsFlyoutViewModel.IsAlwaysOnTop))
        {
            if (!SynchronizeOverlayTopmostBand())
            {
                ScheduleNativeStyleRefresh();
            }
            return;
        }

        if (e.PropertyName == nameof(SettingsFlyoutViewModel.HideHeaderWhenClickThrough))
        {
            RefreshHeaderVisibilityPreference();
            return;
        }

        // Folded with its eye, or with every frame by the "minimize all" shortcut.
        if (e.PropertyName == nameof(SettingsFlyoutViewModel.MinimizedFrames))
        {
            var folded = DataContext.SettingsFlyout.IsMinimized(SettingsFlyoutViewModel.MeterKey);
            if (DataContext.IsMeterFolded != folded)
            {
                DataContext.IsMeterFolded = folded;
                ScheduleOverlayAutoHeightRefresh();
            }
            return;
        }

        if (e.PropertyName == nameof(SettingsFlyoutViewModel.OverlayOpacityPercent))
        {
            ApplyBackdropFill();
            if (_appliedOverlayInteractionMode != OverlayInteractionMode.Hidden)
                MainHudShell.Opacity = DataContext.SettingsFlyout.OverlayContentOpacity;
        }
    }

    // The opacity slider fills the see-through panel in until it is solid; its pin button and flyouts are left alone
    // (the details panel has its own slider).
    private void ApplyBackdropFill()
    {
        var fill = DataContext.SettingsFlyout.OverlayBackdropFill;
        FillBackdrop(HeaderBackdrop, "OverlayBrush.MainHudBackdrop", fill);
        FillBackdrop(ContentBackdrop, "OverlayBrush.MainHudContentBackdrop", fill);
    }

    private void FillBackdrop(Border backdrop, string brushKey, double fill)
    {
        if (this.TryFindResource(brushKey, out var value) && value is ISolidColorBrush { Color: var color })
            backdrop.Background = new SolidColorBrush(Color.FromArgb((byte)Math.Round(color.A + (255 - color.A) * fill), color.R, color.G, color.B));
    }

    // The details opacity slider works the same way on the details panel's card.
    private void ApplyDetailOpacity(CombatantDetailsFlyoutView flyoutView)
    {
        var settings = DataContext.SettingsFlyout;
        flyoutView.Opacity = settings.DetailContentOpacity;
        if (flyoutView.Content is Border card)
            FillBackdrop(card, "OverlayBrush.DetailBackdrop", settings.DetailBackdropFill);
    }

    private void OnCombatantColumnsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CombatantColumnLayoutViewModel.OverlayWidth))
            _uiScale.SetWindowBaseWidth(this, DataContext.CombatantColumns.OverlayWidth);
    }

    private void RefreshHeaderVisibilityPreference() =>
        MainHudShell.Classes.Set("hide-header-when-click-through", DataContext.SettingsFlyout.HideHeaderWhenClickThrough);

    private void UpdateClickThroughCursorState(TimeSpan timestamp)
    {
        if (!_windowInputState.ShouldPollCursor())
        {
            return;
        }

        if (_hasCursorProbeTimestamp && timestamp - _lastCursorProbeTimestamp < CursorProbeInterval)
        {
            return;
        }

        _hasCursorProbeTimestamp = true;
        _lastCursorProbeTimestamp = timestamp;
        if (!_hasOverlayScreenBounds || !NativeOverlayWindowStyles.TryGetCursorPosition(out var cursor))
        {
            return;
        }

        if (!IsInsideOverlayScreenBounds(cursor))
        {
            TryApplyWindowInputState(OverlayWindowInputState.ClickThroughArmed);
        }
    }

    private void MainHudShellPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_windowInputState == OverlayWindowInputState.ClickThroughArmed)
        {
            TryApplyWindowInputState(OverlayWindowInputState.ClickThroughActive);
        }
    }

    private bool TryApplyWindowInputState(OverlayWindowInputState state)
    {
        if (_windowInputState == state)
        {
            return true;
        }

        if (!NativeOverlayWindowStyles.SetInputTransparent(this, state.RequiresInputTransparency()))
        {
            return false;
        }

        CommitWindowInputState(state);
        return true;
    }

    private void CommitWindowInputState(OverlayWindowInputState state)
    {
        _windowInputState = state;
        _hasCursorProbeTimestamp = false;
        MainHudShell.Classes.Set("cursor-over", state == OverlayWindowInputState.ClickThroughActive);
    }

    private bool IsCursorCurrentlyInsideOverlay()
    {
        if (!_hasOverlayScreenBounds)
        {
            RefreshOverlayScreenGeometry();
        }

        return _hasOverlayScreenBounds &&
               NativeOverlayWindowStyles.TryGetCursorPosition(out var cursor) &&
               IsInsideOverlayScreenBounds(cursor);
    }

    private bool IsInsideOverlayScreenBounds(PixelPoint cursor) =>
        cursor.X >= _overlayScreenLeft && cursor.X < _overlayScreenRight &&
        cursor.Y >= _overlayScreenTop && cursor.Y < _overlayScreenBottom;

    private void CloseTransientSurfaces()
    {
        EncounterHistoryButton.Flyout?.Hide();
        if (GetValue(FlyoutBase.AttachedFlyoutProperty) is Flyout detailsFlyout)
        {
            detailsFlyout.Hide();
        }
    }

    private void Minimize(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Exit(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    // The title bar's navigation: a frame on or off, as in its tab of the settings.
    private void NavGroupFrameClicked(object? sender, RoutedEventArgs e) => DataContext.SettingsFlyout.GroupFrameEnabled ^= true;

    private void NavSkillMonitorClicked(object? sender, RoutedEventArgs e) => DataContext.SettingsFlyout.SkillMonitorEnabled ^= true;

    private void NavFightGraphsClicked(object? sender, RoutedEventArgs e) => DataContext.SettingsFlyout.FightGraphsEnabled ^= true;

    private void NavHiddenCubesClicked(object? sender, RoutedEventArgs e) => DataContext.SettingsFlyout.HiddenCubesEnabled ^= true;

    private void OnTwinLibraryOpenChanged() => DataContext.IsTwinLibraryOpen = DigitalTwinLibraryWindow.IsOpen;

    private void TwinLibraryClicked(object? sender, RoutedEventArgs e) =>
        DigitalTwinLibraryWindow.Open(
            Ioc.Default.GetRequiredService<DigitalTwinLibrary>(),
            Ioc.Default.GetRequiredService<DigitalTwinService>(),
            DataContext.Localization,
            Ioc.Default.GetRequiredService<GameResourceService>().ServerNames.ToDictionary(static server => server.Key, static server => server.Value.ServerName),
            DataContext.LocalPlayerServerId,
            DataContext.SettingsFlyout,
            _uiScale,
            this);

    // The eye: the meter folded to its title and bottom bar, or back.
    private void MeterEyeClicked(object? sender, RoutedEventArgs e) => DataContext.SettingsFlyout.ToggleMinimized(SettingsFlyoutViewModel.MeterKey);

    // The gear: the settings window (a frame's gear opens it at the frame's tab).
    private void SettingsClicked(object? sender, RoutedEventArgs e) => SettingsWindow.Open(DataContext.SettingsFlyout, uiScale: _uiScale);

    private void TitleBarDragRegionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
            ScheduleOverlayAutoHeightRefresh();
        }
    }

    private void ScheduleOverlayAutoHeightRefresh()
    {
        if (_autoHeightRefreshQueued || WindowState == WindowState.Minimized)
        {
            return;
        }

        _autoHeightRefreshQueued = true;
        Dispatcher.UIThread.Post(RefreshOverlayAutoHeight, DispatcherPriority.Loaded);
    }

    private void RefreshOverlayAutoHeight()
    {
        _autoHeightRefreshQueued = false;
        if (_isNativeSizeMoveActive || WindowState == WindowState.Minimized || PlatformImpl is null)
        {
            return;
        }

        InvalidateOverlayLayout();

        if (TryMeasureOverlayContentHeight(out var contentHeight)
            && Math.Abs(Bounds.Height - contentHeight) > 0.5d)
        {
            SizeToContent = Avalonia.Controls.SizeToContent.Manual;
            Height = contentHeight;
        }

        SizeToContent = Avalonia.Controls.SizeToContent.Height;
    }

    private bool TryMeasureOverlayContentHeight(out double height)
    {
        height = 0;

        if (Content is not Control content)
        {
            return TryUseFiniteHeight(_overlayRoot?.DesiredSize.Height * _uiScale.Scale, out height);
        }

        var availableWidth = Bounds.Width > 0
            ? Bounds.Width
            : Width;
        if (!double.IsFinite(availableWidth) || availableWidth <= 0)
        {
            availableWidth = double.PositiveInfinity;
        }

        content.Measure(new Size(availableWidth, double.PositiveInfinity));
        if (!TryUseFiniteHeight(content.DesiredSize.Height, out height))
        {
            return TryUseFiniteHeight(content.Bounds.Height, out height);
        }

        return true;
    }

    private bool TryUseFiniteHeight(double? candidate, out double height)
    {
        height = 0;
        if (candidate is not { } value || !double.IsFinite(value) || value <= 0)
        {
            return false;
        }

        if (double.IsFinite(MinHeight) && MinHeight > 0)
        {
            value = Math.Max(MinHeight, value);
        }

        if (double.IsFinite(MaxHeight) && MaxHeight > 0)
        {
            value = Math.Min(MaxHeight, value);
        }

        height = value;
        return true;
    }

    private void InvalidateOverlayLayout()
    {
        InvalidateMeasure();
        InvalidateArrange();

        if (Content is Control content)
        {
            content.InvalidateMeasure();
            content.InvalidateArrange();
        }

        if (_overlayRoot is not null && !ReferenceEquals(Content, _overlayRoot))
        {
            _overlayRoot.InvalidateMeasure();
            _overlayRoot.InvalidateArrange();
        }
    }

    // A row of the main table, or of a boss fight section (its detail then covers that fight).
    private void CombatantRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: CombatantRowViewModel combatant })
        {
            DataContext.SelectCombatantRow(combatant);
            if (TryGetCombatantDetailsFlyout(out var flyout, out var flyoutView))
            {
                ConfigureCombatantDetailsFlyout(flyout, flyoutView);
                ApplyDetailOpacity(flyoutView);
                flyout.ShowAt(_overlayRoot ?? this);
            }
        }
    }

    private void FlyoutClosed(object? sender, EventArgs e)
    {
        DataContext.SelectCombatantCommand.Execute(null);
    }

    private void OnEncounterHistoryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildEncounterHistoryMenuItems();
    }

    private void RebuildEncounterHistoryMenuItems()
    {
        if (this.FindControl<Button>("EncounterHistoryButton")?.Flyout is not MenuFlyout menu)
        {
            return;
        }

        menu.Items.Clear();
        menu.Items.Add(CreateLiveSceneMenuItem());
        if (DataContext.EncounterHistory.Count == 0)
        {
            var placeholder = new MenuItem
            {
                Header = DataContext.Localization["Empty_History"],
                IsEnabled = false
            };
            placeholder.Classes.Add("FlyoutMenuItem");
            placeholder.Classes.Add("FlyoutPanelRow");
            placeholder.Classes.Add("FlyoutMenuItemPlaceholder");
            menu.Items.Add(placeholder);
        }

        foreach (var item in DataContext.EncounterHistory)
        {
            var header = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                ],
                ColumnSpacing = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };
            DisplayContextProvider.SetDisplayContext(header, item.DisplayContext);
            var sceneName = new TextBlock
            {
                Text = item.Title,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            sceneName.Classes.Add("SettingsRowLabel");
            Grid.SetColumn(sceneName, 0);
            header.Children.Add(sceneName);

            var archivedAt = new TextBlock
            {
                Text = item.ArchivedAtText,
                VerticalAlignment = VerticalAlignment.Center
            };
            archivedAt.Classes.Add("SettingsRowValue");
            Grid.SetColumn(archivedAt, 1);
            header.Children.Add(archivedAt);

            var renameButton = CreateInlineButton(item, IconGeometries.Pencil, "History_Rename", EncounterHistoryRenameButtonClicked);
            Grid.SetColumn(renameButton, 2);
            header.Children.Add(renameButton);

            var exportButton = CreateInlineButton(item, IconGeometries.Download, "History_Export", EncounterHistoryExportButtonClicked);
            Grid.SetColumn(exportButton, 3);
            header.Children.Add(exportButton);

            var playbackButton = CreatePlaybackButton(item);
            Grid.SetColumn(playbackButton, 4);
            header.Children.Add(playbackButton);

            var removeButton = CreateInlineButton(item, IconGeometries.Trash, "History_Remove", EncounterHistoryRemoveButtonClicked);
            Grid.SetColumn(removeButton, 5);
            header.Children.Add(removeButton);

            var menuItem = new MenuItem
            {
                Header = header,
                Tag = item
            };
            menuItem.Classes.Add("FlyoutMenuItem");
            menuItem.Classes.Add("FlyoutPanelRow");
            menuItem.Click += EncounterHistoryMenuItemClicked;
            menu.Items.Add(menuItem);
        }

        menu.Items.Add(CreateHistoryFileMenuItem("History_Import", ImportEncounterHistoryClicked));
        if (DataContext.EncounterHistory.Count > 0)
        {
            menu.Items.Add(CreateHistoryFileMenuItem("History_ExportAll", ExportAllEncounterHistoryClicked));
            menu.Items.Add(CreateHistoryFileMenuItem("History_ClearAll", (_, _) => DataContext.ClearEncounterHistory()));
        }
    }

    private MenuItem CreateHistoryFileMenuItem(string headerKey, EventHandler<RoutedEventArgs> click)
    {
        var menuItem = new MenuItem { Header = DataContext.Localization[headerKey] };
        menuItem.Classes.Add("FlyoutMenuItem");
        menuItem.Classes.Add("FlyoutPanelRow");
        menuItem.Click += click;
        return menuItem;
    }

    private MenuItem CreateLiveSceneMenuItem()
    {
        var header = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };
        DisplayContextProvider.SetDisplayContext(header, DataContext.DisplayContext);

        var sceneName = new TextBlock
        {
            Text = DataContext.Localization["Action_Live"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        sceneName.Classes.Add("SettingsRowLabel");
        Grid.SetColumn(sceneName, 0);
        header.Children.Add(sceneName);

        var playbackButton = CreatePlaybackButton(LiveSceneMenuItemTag);
        Grid.SetColumn(playbackButton, 1);
        header.Children.Add(playbackButton);

        var menuItem = new MenuItem
        {
            Header = header,
            Tag = LiveSceneMenuItemTag
        };
        menuItem.Classes.Add("FlyoutMenuItem");
        menuItem.Classes.Add("FlyoutPanelRow");
        menuItem.Click += EncounterHistoryMenuItemClicked;
        return menuItem;
    }

    private Button CreatePlaybackButton(object tag)
        => CreateInlineButton(tag, IconGeometries.Play, "Playback_Open", EncounterHistoryPlaybackButtonClicked);

    private Button CreateInlineButton(object tag, Geometry icon, string toolTipKey, EventHandler<RoutedEventArgs> click)
    {
        var glyph = new Avalonia.Controls.Shapes.Path
        {
            Data = icon
        };
        glyph.Classes.Add("Glyph");
        glyph.Classes.Add("GlyphSm");
        var button = new Button
        {
            Tag = tag,
            VerticalAlignment = VerticalAlignment.Center,
            Content = glyph
        };
        ToolTip.SetTip(button, DataContext.Localization[toolTipKey]);
        button.Classes.Add("FlyoutInlineButton");
        button.Click += click;
        return button;
    }

    private void EncounterHistoryMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        if (ReferenceEquals(menuItem.Tag, LiveSceneMenuItemTag))
        {
            if (DataContext.ReturnToLiveCommand.CanExecute(null))
            {
                DataContext.ReturnToLiveCommand.Execute(null);
            }
            return;
        }

        if (menuItem.Tag is EncounterHistoryItemViewModel item)
        {
            DataContext.SelectedEncounterHistory = item;
        }
    }

    private void EncounterHistoryPlaybackButtonClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: var tag })
        {
            return;
        }

        if (ReferenceEquals(tag, LiveSceneMenuItemTag))
        {
            OpenPlayback(DataContext.CreateLivePlaybackContext());
            return;
        }

        if (tag is EncounterHistoryItemViewModel item)
        {
            OpenPlayback(MainViewModel.CreatePlaybackContext(item));
        }
    }

    private void EncounterHistoryRenameButtonClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: EncounterHistoryItemViewModel item })
        {
            return;
        }

        EncounterHistoryButton.Flyout?.Hide();
        // Clicking elsewhere keeps the new name like Enter does; only Escape drops it. An empty name restores the scene name.
        CardFlyout.EditText(EncounterHistoryButton, item.Record.Name ?? item.SceneName, 80, name => DataContext.RenameEncounterHistory(item, name));
    }

    private void EncounterHistoryRemoveButtonClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: EncounterHistoryItemViewModel item })
            DataContext.RemoveEncounterHistory(item);
    }

    private async void EncounterHistoryExportButtonClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: EncounterHistoryItemViewModel item })
        {
            await ExportEncounterHistoryAsync([item], $"{item.Record.Name ?? item.SceneName} {item.Record.ArchivedAt.ToLocalTime():yyyy-MM-dd HH-mm}");
        }
    }

    private async void ExportAllEncounterHistoryClicked(object? sender, RoutedEventArgs e)
        => await ExportEncounterHistoryAsync([.. DataContext.EncounterHistory], $"Aion2Heal {DateTime.Now:yyyy-MM-dd HH-mm}");

    private async Task ExportEncounterHistoryAsync(IReadOnlyList<EncounterHistoryItemViewModel> items, string fileName)
    {
        EncounterHistoryButton.Flyout?.Hide();
        try
        {
            using var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = DataContext.Localization["History_Export"],
                SuggestedFileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars())) + SceneArchiveFile.Extension,
                DefaultExtension = SceneArchiveFile.Extension.TrimStart('.'),
                FileTypeChoices = [CreateHistoryFileType(SceneArchiveFile.Extension)],
                ShowOverwritePrompt = true
            });
            if (file is null)
            {
                return;
            }

            var records = items.Select(static item => item.Record).ToArray();
            DataContext.AttachDigitalTwins(records);
            _exportTask = WriteHistoryFileAsync(file, records);
            await _exportTask;
        }
        catch (Exception ex)
        {
            ShowEncounterHistoryMessage(string.Format(DataContext.Localization["History_ExportFailed"], ex.Message));
        }
    }

    // Covers closing the stream too: OnClosing waits for it, so quitting mid-export doesn't leave a cut-off file.
    private static async Task WriteHistoryFileAsync(IStorageFile file, ArchivedEncounterRecord[] records)
    {
        await using var stream = await file.OpenWriteAsync();
        if (stream.CanSeek)
        {
            stream.SetLength(0);
        }

        await Task.Run(() => SceneArchiveFile.WriteAsync(stream, records));
    }

    private async void ImportEncounterHistoryClicked(object? sender, RoutedEventArgs e)
    {
        EncounterHistoryButton.Flyout?.Hide();
        var records = new List<ArchivedEncounterRecord>();
        var failures = new List<string>();
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = DataContext.Localization["History_Import"],
                AllowMultiple = true,
                FileTypeFilter = [CreateHistoryFileType(SceneArchiveFile.Extension, SceneArchiveFile.LegacyExtension)]
            });
            foreach (var file in files)
            {
                using (file)
                {
                    try
                    {
                        await using var stream = await file.OpenReadAsync();
                        records.AddRange(await Task.Run(() => SceneArchiveFile.ReadAsync(stream)));
                    }
                    catch (SceneArchiveCutOffException ex)
                    {
                        records.AddRange(ex.Records);
                        failures.Add($"{file.Name}: {string.Format(DataContext.Localization["History_ImportCutOff"], ex.Records.Count)}");
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{file.Name}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            failures.Add(ex.Message);
        }

        DataContext.ImportEncounterHistory(records);
        if (failures.Count > 0)
        {
            ShowEncounterHistoryMessage(string.Join(Environment.NewLine, [DataContext.Localization["History_ImportFailed"], .. failures]));
        }
    }

    // Exports are .aion2heal; imports also take the .aion2flow files from before the rename.
    private FilePickerFileType CreateHistoryFileType(params string[] extensions)
        => new(DataContext.Localization["History_FileType"]) { Patterns = [.. extensions.Select(static extension => "*" + extension)] };

    private void ShowEncounterHistoryMessage(string message)
    {
        CardFlyout.Create(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 320
        }).ShowAt(EncounterHistoryButton);
    }

    private void OpenPlayback(ScenePlaybackOpenContext context)
    {
        if (_playbackWindowController.TryActivate())
        {
            context.Source.Dispose();
            return;
        }

        ScenePlaybackViewModel viewModel;
        try
        {
            viewModel = new ScenePlaybackViewModel(
                context.Source,
                context.DisplayContext,
                DataContext.Localization,
                DataContext.SettingsFlyout);
        }
        catch
        {
            context.Source.Dispose();
            throw;
        }

        var window = new ScenePlaybackWindow(viewModel);
        _playbackWindowController.Show(window, this);
    }

    private void ConfigureCombatantDetailsFlyout(Flyout flyout, CombatantDetailsFlyoutView flyoutView)
    {
        var screen = Screens.ScreenFromWindow(this);
        if (screen is null)
        {
            return;
        }

        var topLeft = this.PointToScreen(new Point(0, 0));
        var bottomRight = this.PointToScreen(new Point(Bounds.Width, Bounds.Height));
        var workArea = screen.WorkingArea;

        var leftSpace = Math.Max(0, topLeft.X - workArea.X);
        var rightSpace = Math.Max(0, workArea.Right - bottomRight.X);
        var topSpace = Math.Max(0, topLeft.Y - workArea.Y);
        var bottomSpace = Math.Max(0, workArea.Bottom - bottomRight.Y);

        var placeRight = rightSpace >= leftSpace;
        var alignTop = bottomSpace >= topSpace;

        flyout.Placement = (placeRight, alignTop) switch
        {
            (true, true) => PlacementMode.RightEdgeAlignedTop,
            (true, false) => PlacementMode.RightEdgeAlignedBottom,
            (false, true) => PlacementMode.LeftEdgeAlignedTop,
            _ => PlacementMode.LeftEdgeAlignedBottom
        };

        var renderScale = (RenderScaling <= 0 ? 1d : RenderScaling) * _uiScale.Scale;
        var availableWidth = Math.Max(0d, (placeRight ? rightSpace : leftSpace) / renderScale - 16d);
        var availableHeight = Math.Max(
            0d,
            (alignTop ? workArea.Bottom - topLeft.Y : bottomRight.Y - workArea.Y) / renderScale - 16d);

        flyoutView.ConfigureViewport(availableWidth, availableHeight);
    }

    private bool TryGetCombatantDetailsFlyout(out Flyout flyout, out CombatantDetailsFlyoutView flyoutView)
    {
        if (GetValue(FlyoutBase.AttachedFlyoutProperty) is Flyout { Content: CombatantDetailsFlyoutView content } attachedFlyout)
        {
            flyout = attachedFlyout;
            flyoutView = content;
            return true;
        }

        flyout = null!;
        flyoutView = null!;
        return false;
    }
}
