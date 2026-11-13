using Avalonia.Controls;
using Avalonia.Threading;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// Shows the fight graphs while they're turned on in the settings; kept on top and click-through like the group frame.
internal sealed class FightGraphsWindowController
{
    private readonly MainViewModel _main;
    private readonly SettingsFlyoutViewModel _settings;
    private readonly PlayerNameDisplayService _names;
    private readonly SettingsService _settingsService;
    private readonly BestRunRecorder _bestRuns;
    private readonly GameResourceService _resources;
    private FightGraphsWindow? _window;
    private Window? _owner;

    public FightGraphsWindowController(
        MainViewModel main,
        SettingsFlyoutViewModel settings,
        PlayerNameDisplayService names,
        SettingsService settingsService,
        BestRunRecorder bestRuns,
        GameResourceService resources,
        ProcessForegroundWatcher foregroundWatcher)
    {
        _main = main;
        _settings = settings;
        _names = names;
        _settingsService = settingsService;
        _bestRuns = bestRuns;
        _resources = resources;
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

        if (!_settings.FightGraphsEnabled)
        {
            CloseWindow();
            return;
        }

        if (_window is { IsVisible: true })
            return;

        var window = new FightGraphsWindow(new FightGraphsViewModel(_main, _settings, _names, _settingsService, _bestRuns, _resources));
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
        if (sender is FightGraphsWindow window)
            Untrack(window);
    }

    private void Untrack(FightGraphsWindow window)
    {
        window.Closed -= WindowClosed;
        window.Activated -= WindowFocusChanged;
        window.Deactivated -= WindowFocusChanged;
        if (ReferenceEquals(_window, window))
            _window = null;
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsFlyoutViewModel.FightGraphsEnabled))
        {
            if (_settings.FightGraphsEnabled && _owner is { } owner)
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
            _settings.IsClickableWithoutFocus(SettingsFlyoutViewModel.FightGraphsKey));
    }
}
