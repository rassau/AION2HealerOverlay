using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// A frame's eye: it folds the frame down to its title line (the hand, the eye and the cross stay), and is kept with the
// settings (SettingsFlyoutViewModel.MinimizedFrames, also what the "minimize all" shortcut sets). The frame's root gets
// the "minimized" class, which hides its FrameBody (OverlayTheme). While the frame is open the scenes know where it is
// (SettingsFlyoutViewModel.AttachFramePlacement). The frame takes the mouse while the game has focus as its settings say
// (FrameClickThrough; its window tells that whether the overlay has focus).
internal static class FrameChrome
{
    // titleLine: a click on it does what the eye does, a drag moves the frame (WindowDrag.AttachTitleLine); with "Title line
    // always clickable" it takes clicks while the rest of the frame lets the mouse through.
    // showing: brings what the body shows up to date as it shows again (frames that skip that work while minimized), so the
    // first layout and paint draw it current: the paint after the window grows can come before the next frame tick.
    // placement: where the frame is for the scenes, when its position setting keeps another corner than its top left one.
    public static void Attach(Window window, Control root, Control titleLine, SettingsFlyoutViewModel settings, string frame, Action? showing = null,
        FramePlacement? placement = null)
    {
        WindowDrag.AttachTitleLine(window, titleLine, () => settings.ToggleMinimized(frame));
        FrameClickThrough.Attach(window, settings, frame, root, titleLine);
        // The click that brings the overlay to the front from the game through the title line of a frame that isn't
        // clickable without focus only gives it focus, on the line's buttons too: the hand, the eye, the cross and the gear
        // show up there only with the focus (FocusPress). A drag from the title line still moves the frame.
        var focusPress = FocusPress.Of(window);
        titleLine.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (focusPress.GaveFocus && !settings.IsClickableWithoutFocus(frame) && (e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is not null)
                e.Handled = true;
        }, RoutingStrategies.Tunnel);
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

    // A frame's hand, a shortcut to its "Clickable without focus" (SettingsFlyoutViewModel.ClickableWithoutFocus): a hand
    // while the frame takes clicks with the game in focus, crossed out while the mouse goes through it to the game then; a
    // click turns it round. The frame shows it only while the overlay has focus (like its eye and cross), when it takes
    // clicks anyway. The click that brings the overlay to the front from the game only does that (FocusPress): Windows
    // gives the overlay focus before the press reaches it, so the press finds the hand already shown. The button gets its
    // glyph and tooltip here.
    public static void AttachHand(Window window, Button hand, SettingsFlyoutViewModel settings, string frame)
    {
        var focusPress = FocusPress.Of(window);
        var glyph = new Avalonia.Controls.Shapes.Path();
        glyph.Classes.AddRange(["Glyph", "GlyphMd"]);
        hand.Content = glyph;
        void Refresh()
        {
            var on = settings.IsClickableWithoutFocus(frame);
            glyph.Data = on ? IconGeometries.Hand : IconGeometries.HandOff;
            ToolTip.SetTip(hand, settings.Localization[on ? "Frame_ClickableOnTip" : "Frame_ClickableOffTip"]);
        }

        void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsFlyoutViewModel.ClickableWithoutFocus))
                Refresh();
        }

        void OnLanguageChanged(object? sender, EventArgs e) => Refresh();

        Refresh();
        hand.Click += (_, _) =>
        {
            if (!focusPress.GaveFocus)
                settings.ToggleClickableWithoutFocus(frame);
        };
        settings.PropertyChanged += OnSettingsChanged;
        settings.Localization.LanguageChanged += OnLanguageChanged;
        window.Closed += (_, _) =>
        {
            settings.PropertyChanged -= OnSettingsChanged;
            settings.Localization.LanguageChanged -= OnLanguageChanged;
        };
    }
}
