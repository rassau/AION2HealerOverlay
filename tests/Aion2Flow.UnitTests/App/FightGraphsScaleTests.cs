using Avalonia;
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

// The fight graphs frame keeps its size while its scale slider moves and takes the new scale once it's let go (saved):
// scaled while dragged, it moved its gear's menu, and the slider with it, under the mouse.
[Collection(AvaloniaTestCollection.Name)]
public sealed class FightGraphsScaleTests
{
    [Fact]
    public void Frame_Takes_Its_Scale_Once_The_Slider_Is_Let_Go()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            Application.Current!.Resources.MergedDictionaries.Add(new IconGeometries());
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
            var frameClock = new AvaloniaFrameClockService();
            var window = new FightGraphsWindow(frameClock, uiScale, settingsService) { DataContext = vm };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            frameClock.Dispose();
            Assert.Equal(780, window.ClientSize.Width);

            settings.FightGraphsScalePercent = 85;
            settings.FightGraphsScalePercent = 80;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(780, window.ClientSize.Width);

            settings.SaveOverlayOpacity();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(624, window.ClientSize.Width);
            window.Close();
        });
    }
}
