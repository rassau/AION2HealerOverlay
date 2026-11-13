using System.ComponentModel;
using Avalonia.Controls;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// A frame's own opacity from the settings' Visuals, and its size for a frame that doesn't see to that itself (scale);
// followed as their sliders move.
internal static class FrameLook
{
    public static void Attach(Window window, SettingsFlyoutViewModel settings, Func<SettingsFlyoutViewModel, int> opacity,
        Func<SettingsFlyoutViewModel, int>? scale = null, UiScaleService? uiScale = null)
    {
        if (scale is not null)
            uiScale?.RegisterWindow(window, () => scale(settings));
        // The content as shown, i.e. inside the scale's wrapper.
        if (window.Content is not Control content)
            return;

        void Apply(object? sender, PropertyChangedEventArgs? e)
        {
            content.Opacity = opacity(settings) / 100d;
            if (scale is not null)
                uiScale?.RefreshWindow(window);
        }

        settings.PropertyChanged += Apply;
        window.Closed += (_, _) => settings.PropertyChanged -= Apply;
        Apply(null, null);
    }
}
