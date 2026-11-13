using System.ComponentModel;
using Avalonia.Controls;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// A frame's eye: it folds the frame down to its title line (the eye and the cross stay), and is kept with the settings
// (SettingsFlyoutViewModel.MinimizedFrames, also what the "minimize all" shortcut sets). The frame's root gets the
// "minimized" class, which hides its FrameBody (OverlayTheme).
internal static class FrameChrome
{
    public static void Attach(Window window, Control root, SettingsFlyoutViewModel settings, string frame)
    {
        // Back to the window's own sizing each time: a drag or a resize may have stopped it (see WindowDrag).
        var sizeToContent = window.SizeToContent;
        void Apply()
        {
            var minimized = settings.IsMinimized(frame);
            if (root.Classes.Contains("minimized") == minimized)
                return;

            root.Classes.Set("minimized", minimized);
            WindowDrag.SizeToContentAgain(window, sizeToContent);
        }

        void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsFlyoutViewModel.MinimizedFrames))
                Apply();
        }

        Apply();
        settings.PropertyChanged += OnSettingsChanged;
        window.Closed += (_, _) => settings.PropertyChanged -= OnSettingsChanged;
    }
}
