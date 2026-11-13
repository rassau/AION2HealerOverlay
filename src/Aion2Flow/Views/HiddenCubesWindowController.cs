using Avalonia.Controls;
using Avalonia.Threading;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// Shows the Hidden Cube frame while it's turned on (settings, the meter's navigation); on top and click-through like the
// group frame.
internal sealed class HiddenCubesWindowController
{
    private readonly WinDivertCaptureService _captureService;
    private readonly LocalizationService _localization;
    private readonly SettingsFlyoutViewModel _settings;
    private HiddenCubesWindow? _window;
    private Window? _owner;

    public HiddenCubesWindowController(WinDivertCaptureService captureService, LocalizationService localization, SettingsFlyoutViewModel settings,
        ProcessForegroundWatcher foregroundWatcher)
    {
        _captureService = captureService;
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

        if (!_settings.HiddenCubesEnabled)
        {
            CloseWindow();
            return;
        }

        if (_window is { IsVisible: true })
            return;

        var window = new HiddenCubesWindow { DataContext = new HiddenCubesViewModel(_captureService, _localization, _settings) };
        _window = window;
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
        if (sender is HiddenCubesWindow window)
            Untrack(window);
    }

    private void Untrack(HiddenCubesWindow window)
    {
        window.Closed -= WindowClosed;
        window.Activated -= WindowFocusChanged;
        window.Deactivated -= WindowFocusChanged;
        if (ReferenceEquals(_window, window))
            _window = null;
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsFlyoutViewModel.HiddenCubesEnabled))
        {
            if (_settings.HiddenCubesEnabled && _owner is { } owner)
                ShowOrActivate(owner);
            else
                CloseWindow();
        }
        else if (e.PropertyName is nameof(SettingsFlyoutViewModel.IsAlwaysOnTop) or nameof(SettingsFlyoutViewModel.ClickableWithoutFocus))
        {
            SynchronizeWindowState();
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
        window.SetAppFocusPresentation(NativeOverlayWindowStyles.IsCurrentProcessForeground() || _owner?.IsActive == true || window.IsActive,
            _settings.IsClickableWithoutFocus(SettingsFlyoutViewModel.HiddenCubesKey));
    }
}
