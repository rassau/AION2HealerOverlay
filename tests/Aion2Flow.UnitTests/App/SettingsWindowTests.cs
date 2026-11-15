using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

    // The settings views have no theme of their own: their setters find OverlayTheme's brushes and sizes in the window's.
    [Fact]
    public void The_Settings_Views_Take_Their_Brushes_From_The_Windows_Theme()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var folder = Path.Combine(Path.GetTempPath(), $"aion2flow-settings-{Guid.NewGuid():N}");
            SettingsWindow? window = null;
            Window? monitorWindow = null;
            try
            {
                var settingsService = new SettingsService(Path.Combine(folder, "settings.json"));
                using var uiScale = new UiScaleService(settingsService);
                var language = new LanguageService();
                var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                    new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
                window = SettingsWindow.Open(settings, SettingsTab.General, uiScale);
                Dispatcher.UIThread.RunJobs();
                var view = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single();
                var tabs = view.GetLogicalDescendants().OfType<StackPanel>().First(static panel => panel.Classes.Contains("SettingsTabs"));
                var section = view.GetLogicalDescendants().OfType<TextBlock>().First(static text => text.Classes.Contains("SettingsSection"));
                Assert.Equal(Color.Parse("#3205080B"), Assert.IsAssignableFrom<ISolidColorBrush>(tabs.Background).Color);
                Assert.Equal(Color.Parse("#B0C1D0"), Assert.IsAssignableFrom<ISolidColorBrush>(section.Foreground).Color);
                Assert.Equal(11d, section.FontSize);
                Assert.DoesNotContain(view.Styles, static style => style is Styles);

                // The skill monitor's lists (made for its tab), in a window with the theme: a class group and a skill's name.
                using var resources = new GameResourceService(language);
                using var monitor = new SkillMonitorSettingsViewModel(resources, settingsService, localization);
                var monitorView = new SkillMonitorSettingsView { DataContext = monitor };
                monitorWindow = new Window { Width = 500, Height = 600, Content = monitorView };
                monitorWindow.Styles.Add(new StyleInclude(new Uri("avares://Aion2Heal/")) { Source = new Uri("avares://Aion2Heal/Styles/OverlayTheme.axaml") });
                monitorWindow.Show();
                monitor.Groups[0].IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                monitorWindow.UpdateLayout();
                var group = monitorView.GetVisualDescendants().OfType<Expander>().First(static expander => expander.Classes.Contains("MonitorClassGroup"));
                var skill = monitorView.GetVisualDescendants().OfType<TextBlock>().First(static text => text.Classes.Contains("MonitorSkillName"));
                Assert.Equal(Color.Parse("#52060A0D"), Assert.IsAssignableFrom<ISolidColorBrush>(group.Background).Color);
                Assert.Equal(Color.Parse("#4A7D9AAE"), Assert.IsAssignableFrom<ISolidColorBrush>(group.BorderBrush).Color);
                Assert.Equal(Color.Parse("#F1F6FB"), Assert.IsAssignableFrom<ISolidColorBrush>(skill.Foreground).Color);
                Assert.DoesNotContain(monitorView.Styles, static style => style is Styles);
            }
            finally
            {
                monitorWindow?.Close();
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

    // The settings live as long as the app: a closed window lets go of them, its rows go, and closed windows don't pile up.
    [Fact]
    public void A_Closed_Window_Lets_Go_Of_The_Settings()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var folder = Path.Combine(Path.GetTempPath(), $"aion2flow-settings-{Guid.NewGuid():N}");
            try
            {
                var settingsService = new SettingsService(Path.Combine(folder, "settings.json"));
                using var uiScale = new UiScaleService(settingsService);
                var settings = new SettingsFlyoutViewModel(localization, new LanguageService(), settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                    new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());

                WeakReference[] closed = [OpenAndClose(settings, uiScale), OpenAndClose(settings, uiScale), OpenAndClose(settings, uiScale)];
                for (var i = 0; i < 3; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                // Avalonia may hold on to the last one closed for a while (here until the next window), not to more.
                Assert.True(closed.Count(static window => window.IsAlive) <= 1);
            }
            finally
            {
                Application.Current.Resources.MergedDictionaries.Remove(icons);
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
    }

    // A hotkey row still waiting for keys when the window closes stops waiting (while one waits, the hotkeys don't work):
    // its focus goes before the window lets go of the settings.
    [Fact]
    public void Closing_The_Window_Ends_A_Hotkey_Capture()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var folder = Path.Combine(Path.GetTempPath(), $"aion2flow-settings-{Guid.NewGuid():N}");
            try
            {
                var settingsService = new SettingsService(Path.Combine(folder, "settings.json"));
                using var uiScale = new UiScaleService(settingsService);
                var settings = new SettingsFlyoutViewModel(localization, new LanguageService(), settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                    new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
                var window = SettingsWindow.Open(settings, SettingsTab.Shortcuts, uiScale);
                Dispatcher.UIThread.RunJobs();
                var row = window.GetLogicalDescendants().OfType<Border>().Single(static row => row.Tag is GlobalHotkeyAction.MinimizeAllFrames);
                Assert.True(row.Focus());
                settings.BeginCaptureHotkey(GlobalHotkeyAction.MinimizeAllFrames);

                window.Close();
                Dispatcher.UIThread.RunJobs();

                Assert.Null(settings.CapturingHotkeyAction);
            }
            finally
            {
                Application.Current.Resources.MergedDictionaries.Remove(icons);
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OpenAndClose(SettingsFlyoutViewModel settings, UiScaleService uiScale)
    {
        var window = SettingsWindow.Open(settings, SettingsTab.GroupFrame, uiScale);
        Dispatcher.UIThread.RunJobs();
        var view = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single();
        var rows = view.FindControl<Menu>("GroupFrameMenu")!;
        Assert.NotEmpty(rows.Items);
        // UiScaleService's Closed handler puts the view back into the window: the view must not get the settings again
        // during the close (it would rebuild its rows and listen again), whichever Closed handler clears them.
        object? regained = null;
        view.DataContextChanged += (_, _) => regained ??= view.DataContext;

        window.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(regained);
        Assert.Null(window.DataContext);
        Assert.Empty(rows.Items);
        return new WeakReference(window);
    }

    // A language change while the window is open: its rows, the language row and the hotkeys' "None" in the new language.
    [Fact]
    public void An_Open_Window_Follows_A_Language_Change()
    {
        AvaloniaTestHost.Run(() =>
        {
            ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var folder = Path.Combine(Path.GetTempPath(), $"aion2flow-settings-{Guid.NewGuid():N}");
            // The language service sets this thread's UI culture and the process default: both go back afterwards.
            var culture = (CultureInfo.CurrentUICulture, CultureInfo.DefaultThreadCurrentUICulture);
            var language = new LanguageService();
            language.SetLanguage(LanguageService.English);
            using var localization = new LocalizationService(language);
            SettingsWindow? window = null;
            try
            {
                var settingsService = new SettingsService(Path.Combine(folder, "settings.json"));
                using var uiScale = new UiScaleService(settingsService);
                var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                    new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
                window = SettingsWindow.Open(settings, SettingsTab.General, uiScale);
                Dispatcher.UIThread.RunJobs();
                var view = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single();
                var languages = view.FindControl<MenuItem>("LanguageMenuItem")!;
                var dummies = view.FindControl<Menu>("TrainingDummyMenu")!;
                var minimizeAll = view.GetLogicalDescendants().OfType<Border>().Single(static row => row.Tag is GlobalHotkeyAction.MinimizeAllFrames)
                    .GetLogicalDescendants().OfType<TextBlock>().Single(static text => text.Classes.Contains("SettingsRowValue"));
                var (pull, none) = (Header(dummies.Items[0])[0], minimizeAll.Text);

                languages.Items.Cast<MenuItem>().Single(static option => option.Tag is LanguageService.German).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(LanguageService.German, language.CurrentLanguage);
                Assert.NotEqual(pull, Header(dummies.Items[0])[0]);
                Assert.Equal(localization["Settings_DummyPull"], Header(dummies.Items[0])[0]);
                Assert.NotEqual(none, minimizeAll.Text);
                Assert.Equal(localization["Settings_Hotkey_None"], minimizeAll.Text);
                Assert.Equal([localization["Settings_Language"], "Deutsch"], Header(languages));
                Assert.Equal([LanguageService.German], languages.Items.Cast<MenuItem>().Where(static option => option.Icon is not null).Select(static option => option.Tag));
            }
            finally
            {
                window?.Close();
                Dispatcher.UIThread.RunJobs();
                (CultureInfo.CurrentUICulture, CultureInfo.DefaultThreadCurrentUICulture) = culture;
                Application.Current.Resources.MergedDictionaries.Remove(icons);
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });

        // A row's label and value.
        static string?[] Header(object? row) => [.. ((Grid)((MenuItem)row!).Header!).Children.OfType<TextBlock>().Select(static text => text.Text)];
    }

    // The tick of a choice moves with it, and a refresh leaves it where it is (no new one each time).
    [Fact]
    public void A_Choice_Keeps_Its_Tick_Until_It_Changes()
    {
        AvaloniaTestHost.Run(() =>
        {
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            try
            {
                var row = new MenuItem();
                SettingsFlyoutView.RebuildTaggedMenuItems(row, [5, 10], static minutes => $"{minutes}", static minutes => minutes, static minutes => minutes == 5, static (_, _) => { });
                var (five, ten) = ((MenuItem)row.Items[0]!, (MenuItem)row.Items[1]!);
                var tick = five.Icon;
                Assert.NotNull(tick);

                SettingsFlyoutView.RefreshTaggedMenuCheckmarks<int>(row, static minutes => minutes == 5);
                Assert.Same(tick, five.Icon);
                Assert.Null(ten.Icon);

                SettingsFlyoutView.RefreshTaggedMenuCheckmarks<int>(row, static minutes => minutes == 10);
                Assert.Null(five.Icon);
                Assert.NotNull(ten.Icon);
            }
            finally
            {
                Application.Current.Resources.MergedDictionaries.Remove(icons);
            }
        });
    }
}
