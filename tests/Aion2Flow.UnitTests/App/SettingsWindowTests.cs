using Avalonia;
using Avalonia.Controls;
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
}
