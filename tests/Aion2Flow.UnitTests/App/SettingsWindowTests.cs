using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Views;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class SettingsWindowTests
{
    [Fact]
    public void A_Frame_Tab_Fades_While_The_Frame_Is_Off_And_A_Gear_Opens_Its_Tab()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var folder = Path.Combine(Path.GetTempPath(), $"aion2flow-settings-{Guid.NewGuid():N}");
            SettingsWindow? window = null;
            try
            {
                var settingsService = new SettingsService(Path.Combine(folder, "settings.json"));
                using var uiScale = new UiScaleService(settingsService);
                var settings = new SettingsFlyoutViewModel(localization, new LanguageService(), settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                    new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());

                // The group frame's gear: its tab, the rest hidden.
                window = SettingsWindow.Open(settings, SettingsTab.GroupFrame, uiScale);
                Dispatcher.UIThread.RunJobs();
                var view = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single();
                var groupFrame = view.FindControl<Menu>("GroupFrameMenu")!;
                var fightGraphs = view.FindControl<Menu>("FightGraphsMenu")!;
                Assert.True(groupFrame.IsEffectivelyVisible);
                Assert.False(fightGraphs.IsEffectivelyVisible);

                // Off at first: the frame's settings below its switch fade and take no clicks.
                var faded = (StackPanel)groupFrame.Parent!;
                Assert.False(settings.GroupFrameEnabled);
                Assert.Equal((false, 0.4), (faded.IsEffectivelyEnabled, faded.Opacity));
                var enable = (MenuItem)view.FindControl<Menu>("GroupFrameEnableMenu")!.Items[0]!;
                enable.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.True(settings.GroupFrameEnabled);
                Assert.Equal((true, 1d), (faded.IsEffectivelyEnabled, faded.Opacity));

                // The fight graphs' gear: the same window, at their tab.
                Assert.Same(window, SettingsWindow.Open(settings, SettingsTab.FightGraphs, uiScale));
                Dispatcher.UIThread.RunJobs();
                Assert.True(fightGraphs.IsEffectivelyVisible);
                Assert.False(groupFrame.IsEffectivelyVisible);
            }
            finally
            {
                window?.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
    }

    // General: how long a training dummy pull runs (a choice) and the pause that ends one (a slider, set once let go).
    [Fact]
    public void The_General_Tab_Sets_How_Training_Dummy_Pulls_Run()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var folder = Path.Combine(Path.GetTempPath(), $"aion2flow-settings-{Guid.NewGuid():N}");
            SettingsWindow? window = null;
            try
            {
                var settingsService = new SettingsService(Path.Combine(folder, "settings.json"));
                using var uiScale = new UiScaleService(settingsService);
                var settings = new SettingsFlyoutViewModel(localization, new LanguageService(), settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                    new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
                window = SettingsWindow.Open(settings, SettingsTab.General, uiScale);
                Dispatcher.UIThread.RunJobs();
                var rows = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single().FindControl<Menu>("TrainingDummyMenu")!.Items.Cast<MenuItem>().ToArray();
                Assert.Equal((0, 5), (settings.DummyPullSeconds, settings.DummyCutoffSeconds));

                var minute = rows[0].Items.Cast<MenuItem>().Single(static option => option.Tag is 60);
                minute.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                var slider = rows[1].GetLogicalDescendants().OfType<Slider>().Single();
                slider.Value = 8;
                slider.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Right });
                Dispatcher.UIThread.RunJobs();

                Assert.Equal((60, 8), (settings.DummyPullSeconds, settings.DummyCutoffSeconds));
                Assert.Equal((60, 8), (settingsService.Current.DummyPullSeconds, settingsService.Current.DummyCutoffSeconds));
                Assert.Equal(new Cloris.Aion2Flow.SceneRuntime.Stores.TrainingDummyRules(60_000, 8_000), settings.TrainingDummyRules);
            }
            finally
            {
                window?.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
    }

    [Fact]
    public void The_Visuals_Tab_Has_Each_Frames_Clickable_Without_Focus_Under_Its_Sliders()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var folder = Path.Combine(Path.GetTempPath(), $"aion2flow-settings-{Guid.NewGuid():N}");
            SettingsWindow? window = null;
            try
            {
                var settingsService = new SettingsService(Path.Combine(folder, "settings.json"));
                using var uiScale = new UiScaleService(settingsService);
                var settings = new SettingsFlyoutViewModel(localization, new LanguageService(), settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                    new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
                window = SettingsWindow.Open(settings, SettingsTab.Visuals, uiScale);
                Dispatcher.UIThread.RunJobs();
                var view = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single();

                foreach (var (menu, frame) in new[]
                {
                    ("VisualsGroupFrameMenu", SettingsFlyoutViewModel.GroupFrameKey),
                    ("VisualsSkillMonitorMenu", SettingsFlyoutViewModel.SkillMonitorKey),
                    ("VisualsFightGraphsMenu", SettingsFlyoutViewModel.FightGraphsKey),
                    ("VisualsHiddenCubesMenu", SettingsFlyoutViewModel.HiddenCubesKey),
                    ("VisualsTwinLibraryMenu", SettingsFlyoutViewModel.TwinLibraryKey),
                })
                {
                    // The section's last row turns that frame's setting round, the same one General and its tab show.
                    var row = (MenuItem)view.FindControl<Menu>(menu)!.Items[^1]!;
                    var was = settings.IsClickableWithoutFocus(frame);
                    row.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Dispatcher.UIThread.RunJobs();
                    Assert.NotEqual(was, settings.IsClickableWithoutFocus(frame));
                }
            }
            finally
            {
                window?.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
    }
}
