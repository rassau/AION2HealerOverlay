using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Views;

namespace Cloris.Aion2Flow.Tests.App;

// The fight graphs frame grows with what it shows, the best-pull banner too, also after a drag: Windows' move loop puts back
// the size the drag began with, Avalonia takes that for a resize by the user and stops sizing the height to the content.
[Collection(AvaloniaTestCollection.Name)]
public sealed partial class FightGraphsHeightTests
{
    private const uint WmEnterSizeMove = 0x0231;
    private const uint WmExitSizeMove = 0x0232;
    private const uint SwpNoMoveNoZOrderNoActivate = 0x0002 | 0x0004 | 0x0010;

    [Fact]
    public void Frame_Grows_With_The_Banner_Also_After_A_Drag()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-graphs-{Guid.NewGuid():N}.json"));
            var language = new LanguageService();
            using var uiScale = new UiScaleService(settingsService);
            var names = new PlayerNameDisplayService(settingsService, localization);
            var settings = new SettingsFlyoutViewModel(localization, language, settingsService, names, uiScale, new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
            var capture = new WinDivertCaptureService(new ProcessPortDiscoveryService());
            capture.Scene.Reset(DateTimeOffset.Now);
            var frameBatch = new UiFrameBatchService();
            var main = new MainViewModel(capture, new ProcessPortDiscoveryService(), language, new GameResourceService(language), new EncounterArchiveService(),
                new CombatantDetailsFlyoutViewModel(localization, frameBatch), localization, settings, frameBatch);
            var vm = new FightGraphsViewModel(main, settings, names, settingsService);
            // A small frame, as no window grows past the screen (CI's is 1024x768): the graphs folded, no overview.
            settings.FightGraphsShowOverview = false;
            foreach (var graph in vm.Graphs)
                graph.IsFolded = true;
            var frameClock = new AvaloniaFrameClockService();
            var window = new FightGraphsWindow(frameClock, uiScale, settingsService) { DataContext = vm };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                frameClock.Dispose();
                var height = window.ClientSize.Height;
                Assert.True(height < 500, $"{height}: the frame should be small enough to grow");

                // A size change in a move loop, as at the end of a drag.
                var hwnd = window.TryGetPlatformHandle()!.Handle;
                SendMessage(hwnd, WmEnterSizeMove, 0, 0);
                SetWindowPos(hwnd, 0, 0, 0, (int)(window.ClientSize.Width * window.RenderScaling), (int)((height - 40) * window.RenderScaling), SwpNoMoveNoZOrderNoActivate);
                SendMessage(hwnd, WmExitSizeMove, 0, 0);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(SizeToContent.Manual, window.SizeToContent);

                // Once the drag is over the height follows the content again, and grows with the banner.
                WindowDrag.SizeToContentAgain(window, SizeToContent.Height);
                Dispatcher.UIThread.RunJobs();
                Assert.InRange(window.ClientSize.Height, height - 1, height + 1);
                vm.BannerText = "★ First complete kill of the boss: kept as your best pull.";
                Dispatcher.UIThread.RunJobs();
                Assert.True(window.ClientSize.Height > height + 20, $"{window.ClientSize.Height} after {height}");
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
            }
        });
    }

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
}
