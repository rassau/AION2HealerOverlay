using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Views;

namespace Cloris.Aion2Flow.Tests.App;

// Where a frame takes the mouse while the game has focus (FrameClickThrough). The windows sit far off the screen, away from
// the cursor, which the tests leave where it is: a body under it would let the mouse through at once.
[Collection(AvaloniaTestCollection.Name)]
public sealed class FrameClickThroughTests
{
    private static readonly PixelPoint OffScreen = new(-30_000, -30_000);

    // The meter's way with its settings as they start out, on a window laid out like it (a title line over a body): it
    // takes clicks while the game has focus until it's no longer clickable without focus. Then its title line takes them
    // (the window doesn't let the mouse through) until the cursor comes over the body, which lets the mouse through while
    // the cursor is watched; with the cursor gone from the body the window takes clicks again. With "Title line always
    // clickable" off the mouse goes through all of the meter at once; with the overlay in focus it takes clicks. Closed,
    // the cursor is no longer watched.
    [Fact]
    public void The_Meter_Takes_Clicks_On_Its_Title_Line_Until_Its_Setting_Is_Off()
    {
        AvaloniaTestHost.Run(() =>
        {
            using var services = new FrameServices();
            var settings = services.Settings;
            var (window, root, titleLine) = Frame();
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var meter = FrameClickThrough.Attach(window, settings, SettingsFlyoutViewModel.MeterKey, root, titleLine);

                meter.SetAppFocused(false);
                Assert.Equal((OverlayWindowInputState.Interactive, false), (meter.State, IsInputTransparent(window)));

                settings.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.MeterKey);
                Assert.Equal((OverlayWindowInputState.ClickThroughArmed, false), (meter.State, IsInputTransparent(window)));

                MoveOver(root, new Point(10, 40));
                Assert.Equal((OverlayWindowInputState.ClickThroughActive, true, true), (meter.State, IsInputTransparent(window), meter.WatchesCursor));
                // Told again that the game has focus, it stays so.
                meter.SetAppFocused(false);
                Assert.Equal(OverlayWindowInputState.ClickThroughActive, meter.State);

                meter.WatchCursor();
                Assert.Equal((OverlayWindowInputState.ClickThroughArmed, false, false), (meter.State, IsInputTransparent(window), meter.WatchesCursor));
                MoveOver(root, new Point(10, 10));
                Assert.Equal(OverlayWindowInputState.ClickThroughArmed, meter.State);

                settings.ToggleTitleClickable(SettingsFlyoutViewModel.MeterKey);
                Assert.Equal((OverlayWindowInputState.ClickThrough, true), (meter.State, IsInputTransparent(window)));
                MoveOver(root, new Point(10, 40));
                Assert.Equal((OverlayWindowInputState.ClickThrough, false), (meter.State, meter.WatchesCursor));

                settings.ToggleTitleClickable(SettingsFlyoutViewModel.MeterKey);
                Assert.Equal((OverlayWindowInputState.ClickThroughArmed, false), (meter.State, IsInputTransparent(window)));
                meter.SetAppFocused(true);
                Assert.Equal((OverlayWindowInputState.Interactive, false), (meter.State, IsInputTransparent(window)));

                meter.SetAppFocused(false);
                MoveOver(root, new Point(10, 40));
                Assert.True(meter.WatchesCursor);
                window.Close();
                Assert.False(meter.WatchesCursor);
            }
            finally
            {
                if (window.IsVisible)
                    window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    // Where the body is on the screen goes by the frame as it's laid out, with the screen's DPI and the frame's scale (here
    // 150%): its title line ends at 30 rather than 20, and it's 300 by 180. A folded frame (FrameChrome's "minimized") has
    // no body.
    [Fact]
    public void The_Body_On_The_Screen_Follows_The_Frames_Scale_And_Folding()
    {
        AvaloniaTestHost.Run(() =>
        {
            using var services = new FrameServices();
            var (window, root, titleLine) = Frame();
            try
            {
                window.Show();
                services.UiScale.RegisterWindow(window, static () => 150);
                Dispatcher.UIThread.RunJobs();
                var frame = FrameClickThrough.Attach(window, services.Settings, SettingsFlyoutViewModel.GroupFrameKey, root, titleLine);
                var origin = window.PointToScreen(default);
                PixelPoint At(double x, double y) =>
                    new(origin.X + (int)Math.Round(x * window.RenderScaling), origin.Y + (int)Math.Round(y * window.RenderScaling));

                Assert.False(frame.IsOverBody(At(10, 25)));
                Assert.True(frame.IsOverBody(At(10, 35)));
                Assert.True(frame.IsOverBody(At(295, 175)));
                Assert.False(frame.IsOverBody(At(305, 100)));
                Assert.False(frame.IsOverBody(At(10, 185)));

                root.Classes.Add("minimized");
                Assert.False(frame.IsOverBody(At(10, 35)));
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    // Each frame as the overlay shows it, with the game in focus (as its controller or FocusClickThrough passes that on)
    // and the frame not clickable without focus (the twin library is at first): the mouse goes through all of it while its
    // "Title line always clickable" is off, as it is at first; on, its title line takes clicks (the window doesn't let the
    // mouse through while the cursor is elsewhere); either way at once. Clickable without focus, it takes clicks all over.
    [Theory]
    [InlineData(SettingsFlyoutViewModel.GroupFrameKey)]
    [InlineData(SettingsFlyoutViewModel.SkillMonitorKey)]
    [InlineData(SettingsFlyoutViewModel.FightGraphsKey)]
    [InlineData(SettingsFlyoutViewModel.HiddenCubesKey)]
    [InlineData(SettingsFlyoutViewModel.TwinLibraryKey)]
    public void Each_Frame_Takes_Clicks_On_Its_Title_Line_While_Its_Setting_Is_On(string frame)
    {
        AvaloniaTestHost.Run(() =>
        {
            using var services = new FrameServices();
            var settings = services.Settings;
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var owner = new Window { Width = 40, Height = 40, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Position = OffScreen };
            var capture = new WinDivertCaptureService(new ProcessPortDiscoveryService());
            var resources = new GameResourceService(services.Language);
            var frameClock = new AvaloniaFrameClockService();
            var twinFolder = Path.Combine(Path.GetTempPath(), $"aion2flow-clickthrough-twins-{Guid.NewGuid():N}");
            using var twins = new DigitalTwinService();
            SkillMonitorWindowController? skillMonitor = null;
            try
            {
                owner.Show();
                Window window;
                Action gameFocused;
                switch (frame)
                {
                    case SettingsFlyoutViewModel.GroupFrameKey:
                        var group = new GroupFrameWindow(frameClock, services.Localization, services.UiScale, services.SettingsService)
                        {
                            DataContext = new GroupFrameViewModel(capture, resources, services.Localization, settings)
                        };
                        group.Show(owner);
                        (window, gameFocused) = (group, () => group.SetAppFocusPresentation(false));
                        break;
                    case SettingsFlyoutViewModel.SkillMonitorKey:
                        skillMonitor = new SkillMonitorWindowController(capture, resources, services.Localization, services.SettingsService, services.UiScale, settings,
                            services.Watcher);
                        skillMonitor.ShowOrActivate(owner);
                        var monitor = Assert.Single(owner.OwnedWindows.OfType<SkillMonitorWindow>());
                        (window, gameFocused) = (monitor, () => monitor.SetAppFocusPresentation(false));
                        break;
                    case SettingsFlyoutViewModel.FightGraphsKey:
                        capture.Scene.Reset(DateTimeOffset.Now);
                        var frameBatch = new UiFrameBatchService();
                        var main = new MainViewModel(capture, new ProcessPortDiscoveryService(), services.Language, resources, new EncounterArchiveService(),
                            new CombatantDetailsFlyoutViewModel(services.Localization, frameBatch), services.Localization, settings, frameBatch);
                        var graphs = new FightGraphsWindow(frameClock, services.UiScale, services.SettingsService)
                        {
                            DataContext = new FightGraphsViewModel(main, settings, new PlayerNameDisplayService(services.SettingsService, services.Localization), services.SettingsService)
                        };
                        graphs.Show(owner);
                        (window, gameFocused) = (graphs, () => graphs.SetAppFocusPresentation(false));
                        break;
                    case SettingsFlyoutViewModel.HiddenCubesKey:
                        var cubes = new HiddenCubesWindow { DataContext = new HiddenCubesViewModel(capture, services.Localization, settings) };
                        cubes.Show(owner);
                        (window, gameFocused) = (cubes, () => cubes.SetAppFocusPresentation(false));
                        break;
                    default:
                        DigitalTwinLibraryWindow.Open(new DigitalTwinLibrary(twinFolder), twins, services.Localization, new Dictionary<int, string>(), null, settings,
                            services.UiScale, owner, activate: false);
                        var library = Assert.Single(owner.OwnedWindows.OfType<DigitalTwinLibraryWindow>());
                        // FocusClickThrough's part, which has no services here.
                        (window, gameFocused) = (library, () => FrameClickThrough.Of(library)!.SetAppFocused(false));
                        break;
                }

                window.Position = OffScreen;
                // No frame ticks from here on.
                frameClock.Dispose();
                Dispatcher.UIThread.RunJobs();
                gameFocused();
                if (settings.IsClickableWithoutFocus(frame))
                    settings.ToggleClickableWithoutFocus(frame);
                var clickThrough = FrameClickThrough.Of(window)!;
                Assert.False(settings.IsTitleClickable(frame));
                Assert.Equal((OverlayWindowInputState.ClickThrough, true), (clickThrough.State, IsInputTransparent(window)));

                settings.ToggleTitleClickable(frame);
                Assert.Equal((OverlayWindowInputState.ClickThroughArmed, false), (clickThrough.State, IsInputTransparent(window)));

                settings.ToggleTitleClickable(frame);
                Assert.Equal((OverlayWindowInputState.ClickThrough, true), (clickThrough.State, IsInputTransparent(window)));

                settings.ToggleClickableWithoutFocus(frame);
                Assert.Equal((OverlayWindowInputState.Interactive, false), (clickThrough.State, IsInputTransparent(window)));
            }
            finally
            {
                skillMonitor?.Close();
                DigitalTwinLibraryWindow.CloseOpen();
                foreach (var owned in owner.OwnedWindows.ToArray())
                    owned.Close();
                owner.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
                if (Directory.Exists(twinFolder))
                    Directory.Delete(twinFolder, recursive: true);
            }
        });
    }

    // A frame not clickable without focus whose title line takes clicks: the click there that brings the overlay to the
    // front from the game only gives it focus, also on the eye, which shows up with that focus (Windows activates the frame
    // before the press reaches it). The next press on the eye presses it.
    [Fact]
    public void The_Click_On_A_Title_Line_That_Gives_Focus_Presses_No_Button()
    {
        AvaloniaTestHost.Run(() =>
        {
            using var services = new FrameServices();
            var settings = services.Settings;
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var frameClock = new AvaloniaFrameClockService();
            var window = new GroupFrameWindow(frameClock, services.Localization, services.UiScale, services.SettingsService)
            {
                DataContext = new GroupFrameViewModel(new WinDivertCaptureService(new ProcessPortDiscoveryService()), new GameResourceService(services.Language),
                    services.Localization, settings)
            };
            try
            {
                window.Show();
                window.Position = OffScreen;
                frameClock.Dispose();
                settings.ToggleTitleClickable(SettingsFlyoutViewModel.GroupFrameKey);
                window.SetAppFocusPresentation(false);
                Assert.Equal(OverlayWindowInputState.ClickThroughArmed, FrameClickThrough.Of(window)!.State);

                // WM_MOUSEACTIVATE with the game in front, the frame activated, then the button-down.
                var focusPress = FocusPress.Of(window);
                focusPress.Message(0x0021, static () => false);
                window.SetAppFocusPresentation(true);
                Dispatcher.UIThread.RunJobs();
                var eye = window.GetVisualDescendants().OfType<Button>()
                    .Single(button => button.Classes.Contains("GroupFrameChrome") && !button.Classes.Contains("CloseBtn") && button != window.HandButton);
                focusPress.Message(0x0201, static () => true);
                Press(window, eye);
                Assert.False(eye.IsPressed);

                focusPress.Message(0x0201, static () => true);
                Press(window, eye);
                Assert.True(eye.IsPressed);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
            }
        });
    }

    // A window laid out like a frame, far off the screen: a title line of 20 over a body of 100, 200 wide.
    private static (Window Window, StackPanel Root, Border TitleLine) Frame()
    {
        var titleLine = new Border { Height = 20, Background = Brushes.Transparent };
        var root = new StackPanel { Width = 200, Children = { titleLine, new Border { Height = 100, Background = Brushes.Transparent } } };
        var window = new Window
        {
            Content = root,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = OffScreen,
        };
        return (window, root, titleLine);
    }

    // The mouse moving over the frame at a point of it, as Avalonia passes that on while the window takes the mouse.
    private static void MoveOver(Control root, Point at)
    {
        var window = TopLevel.GetTopLevel(root)!;
        root.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, root, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true), window,
            root.TranslatePoint(at, window)!.Value, 0, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None));
    }

    // A left press in the middle of the target, as the mouse sends it.
    private static void Press(Window window, Control target)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var at = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        target.RaiseEvent(new PointerPressedEventArgs(target, pointer, window, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
    }

    private static bool IsInputTransparent(Window window)
    {
        Assert.True(NativeOverlayWindowStyles.TryGetInputTransparentStyles(window, out _, out var isTransparent));
        return isTransparent;
    }

    // A frame's settings, in a file of their own.
    private sealed class FrameServices : IDisposable
    {
        public FrameServices()
        {
            (Localization, _) = ViewTestServices.Get();
            SettingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-clickthrough-{Guid.NewGuid():N}.json"));
            UiScale = new UiScaleService(SettingsService);
            Settings = new SettingsFlyoutViewModel(Localization, Language, SettingsService, new PlayerNameDisplayService(SettingsService, Localization), UiScale,
                new AppUpdateService(), Watcher, new GlobalHotkeyService());
        }

        public LocalizationService Localization { get; }

        public LanguageService Language { get; } = new();

        public SettingsService SettingsService { get; }

        public UiScaleService UiScale { get; }

        public ProcessForegroundWatcher Watcher { get; } = new();

        public SettingsFlyoutViewModel Settings { get; }

        public void Dispose()
        {
            UiScale.Dispose();
            Watcher.Dispose();
            File.Delete(SettingsService.FilePath);
        }
    }
}
