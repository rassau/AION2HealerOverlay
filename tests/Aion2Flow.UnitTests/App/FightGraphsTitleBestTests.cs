using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Views;

namespace Cloris.Aion2Flow.Tests.App;

// A pull that set new personal bests names their metrics after the folded fight graphs' name, as the banner names them; a
// first kill beat no earlier best and names none.
[Collection(AvaloniaTestCollection.Name)]
public sealed class FightGraphsTitleBestTests
{
    // In the banner's order, a cleric's group values marked "*"; a metric without an earlier value (kept before it was) and
    // the healing the meter doesn't show are left out. None with the setting off, which is kept.
    [Fact]
    public void The_Title_Line_Names_The_Metrics_The_Banner_Names()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aion2flow-title-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var language = new LanguageService();
            language.SetLanguage(LanguageService.English);
            var settingsService = new SettingsService(Path.Combine(directory, "settings.json"));
            var (settings, graphs) = Graphs(language, new LocalizationService(language), settingsService, new UiScaleService(settingsService));
            using (graphs)
            {
                BestRecord kill = new("Boss",
                [
                    new("Damage", 2_000, 1_000), new("Healing", 900, 800), new(BestRunKeys.EffectiveHealing, 700, 600),
                    new(BestRunKeys.GroupUnhealedHealth, 100, 200), new(BestRunKeys.GroupRecoveryTime, 1.5, null), new("Stagger", 20, 10),
                ]);
                Assert.True(settings.FightGraphsNewBestsInTitle);
                Assert.Equal("★ DPS · HPS · UHS* · Stagger", graphs.TitleBest(kill, includeOverhealing: true));

                // Only the HP restored beat its best: named while the meter leaves out overhealing, as it shows that then.
                BestRecord restored = new("Boss", [new("Damage", 2_000, 1_000), new(BestRunKeys.EffectiveHealing, 700, 600)]);
                Assert.Equal("★ DPS", graphs.TitleBest(restored, includeOverhealing: true));
                Assert.Equal("★ DPS · HPS", graphs.TitleBest(restored, includeOverhealing: false));

                BestRecord first = new("Boss", [new("Damage", 2_000, null), new("Healing", 900, null)]);
                Assert.True(first.IsFirst);
                Assert.Null(graphs.TitleBest(first, includeOverhealing: true));

                settings.FightGraphsNewBestsInTitle = false;
                Assert.Null(graphs.TitleBest(kill, includeOverhealing: true));
                Assert.False(new SettingsService(settingsService.FilePath).Current.FightGraphsNewBestsInTitle);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Folded, the new bests follow the frame's name, the banner on hover; a long list is trimmed before the hand, the eye
    // and the cross rather than pushing them off the line. Unfolded, the tabs take the line again.
    [Fact]
    public void Folded_A_Long_List_Is_Trimmed_Before_The_Hand_The_Eye_And_The_Cross()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var directory = Path.Combine(Path.GetTempPath(), $"aion2flow-title-{Guid.NewGuid():N}");
            var settingsService = new SettingsService(Path.Combine(directory, "settings.json"));
            using var uiScale = new UiScaleService(settingsService);
            var (settings, vm) = Graphs(new LanguageService(), localization, settingsService, uiScale);
            var frameClock = new AvaloniaFrameClockService();
            var window = new FightGraphsWindow(frameClock, uiScale, settingsService) { DataContext = vm };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                // No refresh from here on, which would find no pull and clear the list.
                frameClock.Dispose();
                vm.TitleBestText = "★ " + string.Join(" · ", Enumerable.Repeat("DPS", 300));
                vm.BannerText = "★ New personal best on Boss: DPS 2K/s (was 1K/s)";
                Dispatcher.UIThread.RunJobs();
                var best = window.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == vm.TitleBestText);
                Assert.False(best.IsEffectivelyVisible);

                settings.ToggleMinimized(SettingsFlyoutViewModel.FightGraphsKey);
                Dispatcher.UIThread.RunJobs();
                Assert.True(best.IsEffectivelyVisible);
                Assert.Equal(vm.BannerText, ToolTip.GetTip(best));
                var chrome = window.GetVisualDescendants().OfType<Button>().Where(static button => button.Classes.Contains("FightGraphsChrome")).ToArray();
                Assert.Equal(3, chrome.Length);
                Assert.All(chrome, static button => Assert.True(button.IsEffectivelyVisible));
                var hand = chrome.Single(static button => button.Name == "HandButton");
                var cross = chrome.Single(static button => button.Classes.Contains("CloseBtn"));
                var (end, handStart) = (best.TranslatePoint(new Point(best.Bounds.Width, 0), window)!.Value.X, hand.TranslatePoint(default, window)!.Value.X);
                Assert.True(end <= handStart, $"the list ends at {end}, the hand starts at {handStart}");
                Assert.Contains(best.TextLayout.TextLines, static line => line.HasCollapsed);
                var crossEnd = cross.TranslatePoint(new Point(cross.Bounds.Width, 0), window)!.Value.X;
                Assert.True(crossEnd <= window.Bounds.Width, $"the cross ends at {crossEnd} of {window.Bounds.Width}");

                settings.ToggleMinimized(SettingsFlyoutViewModel.FightGraphsKey);
                Dispatcher.UIThread.RunJobs();
                Assert.False(best.IsEffectivelyVisible);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        });
    }

    // The fight graphs over an empty live scene.
    private static (SettingsFlyoutViewModel Settings, FightGraphsViewModel Graphs) Graphs(LanguageService language, LocalizationService localization,
        SettingsService settingsService, UiScaleService uiScale)
    {
        var names = new PlayerNameDisplayService(settingsService, localization);
        var settings = new SettingsFlyoutViewModel(localization, language, settingsService, names, uiScale, new AppUpdateService(), new ProcessForegroundWatcher(),
            new GlobalHotkeyService());
        var ports = new ProcessPortDiscoveryService();
        var capture = new WinDivertCaptureService(ports);
        capture.Scene.Reset(DateTimeOffset.Now);
        var frameBatch = new UiFrameBatchService();
        var main = new MainViewModel(capture, ports, language, new GameResourceService(language), new EncounterArchiveService(),
            new CombatantDetailsFlyoutViewModel(localization, frameBatch), localization, settings, frameBatch);
        return (settings, new FightGraphsViewModel(main, settings, names, settingsService));
    }
}
