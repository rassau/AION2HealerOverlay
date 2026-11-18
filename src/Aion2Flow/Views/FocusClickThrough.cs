using Avalonia.Controls;
using Avalonia.Threading;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Cloris.Aion2Flow.Views;

// For windows that otherwise take every click (the twin library, the playback): while the game has focus they let the
// mouse through as their settings say (FrameClickThrough), like the overlay's frames; this tells them whether the overlay
// has focus. The window has the "interactive" class while the overlay has focus, for the twin library's gear and hand
// (shown only then).
internal static class FocusClickThrough
{
    public static void Attach(Window window, string frame)
    {
        SettingsFlyoutViewModel? settings;
        ProcessForegroundWatcher? watcher;
        try
        {
            settings = Ioc.Default.GetService<SettingsFlyoutViewModel>();
            watcher = Ioc.Default.GetService<ProcessForegroundWatcher>();
        }
        catch (InvalidOperationException)
        {
            return; // no services (tests)
        }

        if (settings is null || watcher is null)
            return;

        // The twin library's came with its chrome (FrameChrome.Attach); the playback has none, so all of it lets the mouse
        // through.
        var clickThrough = FrameClickThrough.Of(window) ?? FrameClickThrough.Attach(window, settings, frame);

        void Apply()
        {
            if (window.PlatformImpl is null)
                return;

            var appFocused = NativeOverlayWindowStyles.IsCurrentProcessForeground() || window.IsActive;
            window.Classes.Set("interactive", appFocused);
            clickThrough.SetAppFocused(appFocused);
        }

        // Focus has only settled once the event is through.
        void FocusChanged(object? sender, EventArgs e)
        {
            Apply();
            Dispatcher.UIThread.Post(Apply);
        }

        void ForegroundChanged() => Dispatcher.UIThread.Post(Apply);

        window.Activated += FocusChanged;
        window.Deactivated += FocusChanged;
        watcher.ForegroundChanged += ForegroundChanged;
        window.Closed += (_, _) =>
        {
            window.Activated -= FocusChanged;
            window.Deactivated -= FocusChanged;
            watcher.ForegroundChanged -= ForegroundChanged;
        };
        Apply();
    }
}
