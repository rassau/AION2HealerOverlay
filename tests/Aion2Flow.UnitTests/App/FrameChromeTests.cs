using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Views;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class FrameChromeTests
{
    // A frame's eye folds it down to its title line (the members go, the window gets smaller), again brings it back; the
    // "minimize all" shortcut does the same; a click on the title line does what the eye does (a press that moves a pixel
    // or two still counts as a click); its cross turns the frame off.
    [Fact]
    public void The_Eye_Minimizes_The_Frame_And_The_Cross_Turns_It_Off()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-chrome-{Guid.NewGuid():N}.json"));
            var language = new LanguageService();
            using var uiScale = new UiScaleService(settingsService);
            var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService())
            {
                GroupFrameEnabled = true,
            };
            var viewModel = new GroupFrameViewModel(new WinDivertCaptureService(new ProcessPortDiscoveryService()), new GameResourceService(language), localization, settings);
            var frameClock = new AvaloniaFrameClockService();
            var window = new GroupFrameWindow(frameClock, localization, uiScale, settingsService) { DataContext = viewModel };
            var screen = window.Screens.Primary!.WorkingArea;
            settingsService.Update(value => value.GroupFramePosition = new System.Drawing.Point(screen.X + screen.Width / 2, screen.Y + 100));
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                // Without capture every frame empties the rows: none from here on.
                frameClock.Dispose();
                viewModel.Members.Clear();
                for (var i = 1; i <= 4; i++)
                    viewModel.Members.Add(new GroupFrameMemberRow(i) { Name = "Member" });
                window.SetAppFocusPresentation(true);
                Dispatcher.UIThread.RunJobs();
                var height = window.Bounds.Height;
                var chrome = window.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("GroupFrameChrome") && button != window.HandButton).ToArray();
                var eye = chrome.Single(static button => !button.Classes.Contains("CloseBtn"));
                var cross = chrome.Single(static button => button.Classes.Contains("CloseBtn"));
                var rows = window.GetVisualDescendants().OfType<ItemsControl>().First(items => ReferenceEquals(items.ItemsSource, viewModel.Members));

                eye.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.True(settings.IsMinimized(SettingsFlyoutViewModel.GroupFrameKey));
                Assert.False(rows.IsEffectivelyVisible);
                Assert.True(eye.IsEffectivelyVisible);
                Assert.True(window.HandButton.IsEffectivelyVisible);
                Assert.True(window.Bounds.Height < height - 50, $"{window.Bounds.Height} after {height}");

                eye.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.True(rows.IsEffectivelyVisible);

                settings.ToggleMinimizeAll();
                Dispatcher.UIThread.RunJobs();
                Assert.False(rows.IsEffectivelyVisible);
                settings.ToggleMinimizeAll();
                Dispatcher.UIThread.RunJobs();
                Assert.True(rows.IsEffectivelyVisible);

                Click(window, window.TitleLine, new Point(4, 4), new Point(4, 4));
                Dispatcher.UIThread.RunJobs();
                Assert.True(settings.IsMinimized(SettingsFlyoutViewModel.GroupFrameKey));
                Click(window, window.TitleLine, new Point(4, 4), new Point(6, 7));
                Dispatcher.UIThread.RunJobs();
                Assert.False(settings.IsMinimized(SettingsFlyoutViewModel.GroupFrameKey));
                Assert.True(rows.IsEffectivelyVisible);

                cross.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.False(settings.GroupFrameEnabled);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
            }
        });
    }

    // A frame's hand: a hand while the frame is clickable without focus, crossed out while it isn't, with a tooltip saying
    // so; a click turns the setting round (not the click that brought the overlay to the front from the game, which finds
    // the hand shown as Windows activates the overlay before the press), the settings and a new language show on it, and
    // the window closing lets go.
    [Fact]
    public void The_Hand_Shows_And_Turns_Clickable_Without_Focus()
    {
        AvaloniaTestHost.Run(() =>
        {
            var language = new LanguageService();
            language.SetLanguage(LanguageService.English);
            using var localization = new LocalizationService(language);
            var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-hand-{Guid.NewGuid():N}.json"));
            using var uiScale = new UiScaleService(settingsService);
            using var watcher = new ProcessForegroundWatcher();
            var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                new AppUpdateService(), watcher, new GlobalHotkeyService());
            var hand = new Button();
            var window = new Window { Content = hand };
            FrameChrome.AttachHand(window, hand, settings, SettingsFlyoutViewModel.GroupFrameKey);
            var glyph = Assert.IsType<Avalonia.Controls.Shapes.Path>(hand.Content);
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Same(IconGeometries.HandOff, glyph.Data);
                Assert.Equal(localization["Frame_ClickableOffTip"], ToolTip.GetTip(hand));

                // WM_MOUSEACTIVATE while the game has the foreground, then the button-down.
                var focusPress = FocusPress.Of(window);
                focusPress.Message(0x0021, static () => false);
                focusPress.Message(0x0201, static () => true);
                hand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.False(settings.IsClickableWithoutFocus(SettingsFlyoutViewModel.GroupFrameKey));
                focusPress.Message(0x0201, static () => true);

                hand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(settings.IsClickableWithoutFocus(SettingsFlyoutViewModel.GroupFrameKey));
                Assert.Same(IconGeometries.Hand, glyph.Data);
                Assert.Equal(localization["Frame_ClickableOnTip"], ToolTip.GetTip(hand));

                settings.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.GroupFrameKey);
                Assert.Same(IconGeometries.HandOff, glyph.Data);

                language.SetLanguage(LanguageService.German);
                Assert.Equal(localization["Frame_ClickableOffTip"], ToolTip.GetTip(hand));
                Assert.StartsWith("Klickbar ohne Fokus", (string)ToolTip.GetTip(hand)!);
            }
            finally
            {
                language.SetLanguage(LanguageService.English);
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            settings.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.GroupFrameKey);
            Assert.Same(IconGeometries.HandOff, glyph.Data);
            File.Delete(settingsService.FilePath);
        });
    }

    // A click on a title line that brings the overlay to the front from the game doesn't fold the frame: whether another
    // program had the foreground is asked as Windows activates the window (WM_MOUSEACTIVATE), and only then, and goes with
    // the press after it until the next press. A press on the active window, or one that came from another of the
    // overlay's windows, folds.
    [Fact]
    public void Only_The_Press_That_Brings_The_Overlay_To_The_Front_Gave_Focus()
    {
        const uint mouseActivate = 0x0021, activate = 0x0006, setFocus = 0x0007, mouseMove = 0x0200, leftDown = 0x0201, leftUp = 0x0202, rightDown = 0x0204;
        var focus = new FocusPress();
        void Send(Func<bool> overlayInFront, params uint[] messages)
        {
            foreach (var message in messages)
                focus.Message(message, overlayInFront);
        }

        static bool Game() => false;
        static bool Overlay() => true;
        static bool NotAsked() => throw new InvalidOperationException("asked outside WM_MOUSEACTIVATE");

        Send(NotAsked, mouseMove, leftDown, leftUp);
        Assert.False(focus.GaveFocus);

        Send(Game, mouseActivate);
        Send(NotAsked, activate, setFocus, leftDown);
        Assert.True(focus.GaveFocus);
        Send(NotAsked, mouseMove, leftUp);
        Assert.True(focus.GaveFocus);
        Send(NotAsked, leftDown);
        Assert.False(focus.GaveFocus);

        Send(Overlay, mouseActivate);
        Send(NotAsked, activate, leftDown);
        Assert.False(focus.GaveFocus);

        // The right button's press that gave focus is its own; the left press after it folds.
        Send(Game, mouseActivate);
        Send(NotAsked, rightDown);
        Assert.True(focus.GaveFocus);
        Send(NotAsked, leftDown);
        Assert.False(focus.GaveFocus);
    }

    // A drop-down box takes a press itself only on its arrow; a press anywhere else on it (the name shown in it, say) must
    // not move the window, or the move loop swallows the release the box opens on (the twin library's Reference box opened
    // only from its arrow). A press beside the box still moves the window.
    [Fact]
    public void A_Press_On_A_Drop_Down_Box_Doesn_t_Move_The_Window()
    {
        AvaloniaTestHost.Run(() =>
        {
            var box = new ComboBox { ItemsSource = new[] { "Aerwyn", "Dremora" }, SelectedIndex = 0, Width = 200 };
            var root = new Border { Padding = new Thickness(10), Child = box };
            var window = new Window { Content = root, SizeToContent = SizeToContent.WidthAndHeight };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                var name = box.GetVisualDescendants().OfType<TextBlock>().Single(static text => text.Text == "Aerwyn");
                Assert.True(WindowDrag.InComboBox(name));
                Assert.True(WindowDrag.InComboBox(box));
                Assert.False(WindowDrag.InComboBox(root));
                Assert.False(WindowDrag.InComboBox(null));
            }
            finally
            {
                window.Close();
            }
        });
    }

    // A left press at one point of the target and its release at another, with a move between (as the mouse sends them).
    private static void Click(Window window, Control target, Point at, Point releasedAt)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var start = target.TranslatePoint(at, window)!.Value;
        var end = target.TranslatePoint(releasedAt, window)!.Value;
        target.RaiseEvent(new PointerPressedEventArgs(target, pointer, window, start, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        target.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, target, pointer, window, end, 1,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
        target.RaiseEvent(new PointerReleasedEventArgs(target, pointer, window, end, 2,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
    }
}
