using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// The settings in a window of their own, one at a time: the meter's gear opens it, a frame's gear at the frame's tab. It
// goes by the main UI scale and opens where it was last.
public partial class SettingsWindow : Window
{
    private static SettingsWindow? _open;

    public SettingsWindow() => InitializeComponent();

    public static SettingsWindow Open(SettingsFlyoutViewModel settings, SettingsTab? tab = null, UiScaleService? uiScale = null)
    {
        if (tab is { } shown)
            settings.Tab = shown;
        if (_open is not null)
        {
            _open.Activate();
            return _open;
        }

        var window = _open = new SettingsWindow { DataContext = settings };
        window.Closed += (_, _) => _open = null;
        if (settings.SettingsWindowPosition is { } corner && window.Screens.ScreenFromPoint(new PixelPoint(corner.X, corner.Y)) is not null)
            (window.WindowStartupLocation, window.Position) = (WindowStartupLocation.Manual, new PixelPoint(corner.X, corner.Y));
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // Scaled before it shows (the scale wraps the content); owned by the meter, like the twin library, so it stays with
        // the overlay.
        uiScale?.RegisterWindow(window);
        if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is { IsVisible: true } owner)
            window.Show(owner);
        else
            window.Show();
        return window;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        SavePosition();
        base.OnClosing(e);
    }

    private void SavePosition()
    {
        if (DataContext is SettingsFlyoutViewModel settings)
            settings.SettingsWindowPosition = new System.Drawing.Point(Position.X, Position.Y);
    }

    private void CloseClicked(object? sender, RoutedEventArgs e) => Close();

    // The move loop returns once the button is let go.
    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        WindowDrag.Move(this, e);
        SavePosition();
    }
}
