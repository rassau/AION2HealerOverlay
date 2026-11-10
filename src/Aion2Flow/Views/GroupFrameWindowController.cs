using Avalonia.Controls;
using Avalonia.Threading;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// Shows the group frame while it's turned on in the settings; kept on top and click-through like the skill monitor.
internal sealed class GroupFrameWindowController
{
    private readonly WinDivertCaptureService _captureService;
    private readonly GameResourceService _resources;
    private readonly LocalizationService _localization;
    private readonly SettingsFlyoutViewModel _settings;
    private GroupFrameWindow? _window;
    private Window? _owner;

    public GroupFrameWindowController(
        WinDivertCaptureService captureService,
        GameResourceService resources,
        LocalizationService localization,
        SettingsFlyoutViewModel settings,
        ProcessForegroundWatcher foregroundWatcher)
    {
        _captureService = captureService;
        _resources = resources;
        _localization = localization;
        _settings = settings;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        foregroundWatcher.ForegroundChanged += () => Dispatcher.UIThread.Post(SynchronizeWindowState);
    }

    public void ShowOrActivate(Window owner)
    {
        if (!ReferenceEquals(_owner, owner))
        {
            DetachOwner();
            _owner = owner;
            owner.Activated += WindowFocusChanged;
            owner.Deactivated += WindowFocusChanged;
        }

        if (!_settings.GroupFrameEnabled)
        {
            CloseWindow();
            return;
        }

        if (_window is { IsVisible: true })
            return;

        var window = new GroupFrameWindow(new GroupFrameViewModel(_captureService, _resources, _localization, _settings));
        _window = window;
        window.SetScalePercent(_settings.GroupFrameScalePercent);
        window.Closed += WindowClosed;
        window.Activated += WindowFocusChanged;
        window.Deactivated += WindowFocusChanged;
        window.Show(owner);
        SynchronizeWindowState();
    }

    public void Close()
    {
        CloseWindow();
        DetachOwner();
    }

    private void CloseWindow()
    {
        if (_window is not { } window)
            return;

        Untrack(window);
        if (window.IsVisible)
            window.Close();
    }

    private void DetachOwner()
    {
        if (_owner is { } owner)
        {
            owner.Activated -= WindowFocusChanged;
            owner.Deactivated -= WindowFocusChanged;
        }

        _owner = null;
    }

    private void WindowClosed(object? sender, EventArgs e)
    {
        if (sender is GroupFrameWindow window)
            Untrack(window);
    }

    private void Untrack(GroupFrameWindow window)
    {
        window.Closed -= WindowClosed;
        window.Activated -= WindowFocusChanged;
        window.Deactivated -= WindowFocusChanged;
        if (ReferenceEquals(_window, window))
            _window = null;
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsFlyoutViewModel.GroupFrameEnabled))
        {
            if (_settings.GroupFrameEnabled && _owner is { } owner)
                ShowOrActivate(owner);
            else
                CloseWindow();
        }
        else if (e.PropertyName == nameof(SettingsFlyoutViewModel.IsAlwaysOnTop))
        {
            SynchronizeWindowState();
        }
        else if (e.PropertyName == nameof(SettingsFlyoutViewModel.GroupFrameScalePercent))
        {
            _window?.SetScalePercent(_settings.GroupFrameScalePercent);
        }
    }

    // Focus has only settled once the event is through.
    private void WindowFocusChanged(object? sender, EventArgs e)
    {
        SynchronizeWindowState();
        Dispatcher.UIThread.Post(SynchronizeWindowState);
    }

    private void SynchronizeWindowState()
    {
        if (_window is not { } window)
            return;

        NativeOverlayWindowStyles.SetTopmostBand(window, _settings.IsAlwaysOnTop);
        window.SetAppFocusPresentation(NativeOverlayWindowStyles.IsCurrentProcessForeground() || _owner?.IsActive == true || window.IsActive);
    }
}
