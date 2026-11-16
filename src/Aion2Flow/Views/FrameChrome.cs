using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// A frame's eye: it folds the frame down to its title line (the eye and the cross stay), and is kept with the settings
// (SettingsFlyoutViewModel.MinimizedFrames, also what the "minimize all" shortcut sets). The frame's root gets the
// "minimized" class, which hides its FrameBody (OverlayTheme). While the frame is open the scenes know where it is
// (SettingsFlyoutViewModel.AttachFramePlacement).
internal static class FrameChrome
{
    // titleLine: a click on it does what the eye does, a drag moves the frame (WindowDrag.AttachTitleLine).
    // showing: brings what the body shows up to date as it shows again (frames that skip that work while minimized), so the
    // first layout and paint draw it current: the paint after the window grows can come before the next frame tick.
    // placement: where the frame is for the scenes, when its position setting keeps another corner than its top left one.
    public static void Attach(Window window, Control root, Control titleLine, SettingsFlyoutViewModel settings, string frame, Action? showing = null,
        FramePlacement? placement = null)
    {
        WindowDrag.AttachTitleLine(window, titleLine, () => settings.ToggleMinimized(frame));
        // Back to the window's own sizing each time: a drag or a resize may have stopped it (see WindowDrag).
        var sizeToContent = window.SizeToContent;
        void Apply()
        {
            var minimized = settings.IsMinimized(frame);
            if (root.Classes.Contains("minimized") == minimized)
                return;

            if (!minimized)
                showing?.Invoke();
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
        placement ??= new FramePlacement(() => new System.Drawing.Point(window.Position.X, window.Position.Y),
            corner => window.Position = new PixelPoint(corner.X, corner.Y));
        settings.AttachFramePlacement(frame, placement);
        window.Closed += (_, _) =>
        {
            settings.PropertyChanged -= OnSettingsChanged;
            settings.DetachFramePlacement(frame, placement);
        };
    }
}
