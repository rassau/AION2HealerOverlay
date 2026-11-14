using System.ComponentModel;
using Avalonia.Controls;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// The overlay's other windows (the settings, the twin library, the alert sound editor) go by the Topmost mode like its
// frames: on top while the game is in front (by default), so they don't stay in front of every program you switch to.
internal static class TopmostFollower
{
    public static void Attach(Window window, SettingsFlyoutViewModel settings)
    {
        void Changed(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsFlyoutViewModel.IsAlwaysOnTop))
                NativeOverlayWindowStyles.SetTopmostBand(window, settings.IsAlwaysOnTop);
        }

        settings.PropertyChanged += Changed;
        window.Closed += (_, _) => settings.PropertyChanged -= Changed;
        NativeOverlayWindowStyles.SetTopmostBand(window, settings.IsAlwaysOnTop);
    }
}
