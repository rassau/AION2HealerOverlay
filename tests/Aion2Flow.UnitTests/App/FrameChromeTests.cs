using Avalonia;
using Avalonia.Controls;
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
    // "minimize all" shortcut does the same; its cross turns the frame off.
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
                var chrome = window.GetVisualDescendants().OfType<Button>().Where(static button => button.Classes.Contains("GroupFrameChrome")).ToArray();
                var eye = chrome.Single(static button => !button.Classes.Contains("CloseBtn"));
                var cross = chrome.Single(static button => button.Classes.Contains("CloseBtn"));
                var rows = window.GetVisualDescendants().OfType<ItemsControl>().First(items => ReferenceEquals(items.ItemsSource, viewModel.Members));

                eye.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.True(settings.IsMinimized(SettingsFlyoutViewModel.GroupFrameKey));
                Assert.False(rows.IsEffectivelyVisible);
                Assert.True(eye.IsEffectivelyVisible);
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
}
