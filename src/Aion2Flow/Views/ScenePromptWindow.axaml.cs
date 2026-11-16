using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Cloris.Aion2Flow.Views;

// The "save scene" shortcut: the frames as they were when it was pressed, kept under the name typed here. Enter saves,
// Escape or clicking elsewhere drops it; a name that's taken saves that scene anew. Enter and Escape give the focus back
// to the window that had it (the game), which Windows would hand to the meter otherwise.
public partial class ScenePromptWindow : Window
{
    private static ScenePromptWindow? _open;
    private SettingsFlyoutViewModel? _settings;
    private FrameScene? _captured;
    private HWND _previous;
    private bool _closing;

    public ScenePromptWindow()
    {
        InitializeComponent();
        SceneName.AddHandler(KeyDownEvent, NameKeyDown, RoutingStrategies.Tunnel);
        // Closed once the deactivation is through: closing in the middle of it would hand the activation to the meter.
        Deactivated += (_, _) => Dispatcher.UIThread.Post(CloseOnce);
    }

    // The frames are kept as they are now, before the prompt takes the focus.
    public static void Ask(Window owner, SettingsFlyoutViewModel settings, UiScaleService uiScale)
    {
        if (_open is not null)
        {
            _open.Activate();
            return;
        }

        var window = _open = new ScenePromptWindow { _settings = settings, _captured = settings.CaptureFrames(), _previous = PInvoke.GetForegroundWindow() };
        window.Closed += (_, _) => _open = null;
        uiScale.RegisterWindow(window);
        window.Show(owner);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Activate();
        SceneName.Focus();
    }

    private void NameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape))
            return;

        e.Handled = true;
        if (e.Key == Key.Enter)
            _settings?.AddScene(SceneName.Text, _captured);
        if (_previous != HWND.Null && PInvoke.IsWindow(_previous))
            PInvoke.SetForegroundWindow(_previous);
        Dispatcher.UIThread.Post(CloseOnce);
    }

    private void CloseOnce()
    {
        if (_closing)
            return;

        _closing = true;
        Close();
    }
}
