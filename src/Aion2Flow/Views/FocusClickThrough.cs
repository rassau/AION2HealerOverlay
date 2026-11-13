using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Cloris.Aion2Flow.Views;

// For windows that otherwise take every click (the twin library, the playback): while the game has focus they let the
// mouse through, unless the user wants them clickable then ("Clickable without focus"), like the overlay's frames.
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

        void Apply()
        {
            if (window.PlatformImpl is null)
                return;

            var appFocused = NativeOverlayWindowStyles.IsCurrentProcessForeground() || window.IsActive;
            NativeOverlayWindowStyles.SetInputTransparent(window, !appFocused && !settings.IsClickableWithoutFocus(frame));
        }

        // Focus has only settled once the event is through.
        void FocusChanged(object? sender, EventArgs e)
        {
            Apply();
            Dispatcher.UIThread.Post(Apply);
        }

        void ForegroundChanged() => Dispatcher.UIThread.Post(Apply);

        void SettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsFlyoutViewModel.ClickableWithoutFocus))
                Apply();
        }

        window.Activated += FocusChanged;
        window.Deactivated += FocusChanged;
        watcher.ForegroundChanged += ForegroundChanged;
        settings.PropertyChanged += SettingsChanged;
        window.Closed += (_, _) =>
        {
            window.Activated -= FocusChanged;
            window.Deactivated -= FocusChanged;
            watcher.ForegroundChanged -= ForegroundChanged;
            settings.PropertyChanged -= SettingsChanged;
        };
        Apply();
    }
}
