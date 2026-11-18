using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.SceneRuntime.Twins;
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

                // Off at first: the frame's settings below its switch fade and take no clicks, but still show their hover texts.
                var faded = (StackPanel)groupFrame.Parent!;
                Assert.False(settings.GroupFrameEnabled);
                Assert.Equal((false, 0.4), (faded.IsEffectivelyEnabled, faded.Opacity));
                Assert.True(ToolTip.GetShowOnDisabled((MenuItem)groupFrame.Items[0]!));
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

    // DPS meter: "Add boss sum row" is off at first, says on hover what the row adds up, and a click turns it on (kept).
    [Fact]
    public void The_Meter_Tab_Turns_The_Boss_Sum_Row_On()
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
                window = SettingsWindow.Open(settings, SettingsTab.Meter, uiScale);
                Dispatcher.UIThread.RunJobs();
                var row = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single().FindControl<Menu>("MeterBossSumMenu")!.Items.Cast<MenuItem>().Single();
                Assert.True(row.IsEffectivelyVisible);
                Assert.False(settings.ShowBossSumRow);
                Assert.Equal(localization["Settings_BossSumRowTip"], ToolTip.GetTip(row));

                row.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.True(settings.ShowBossSumRow);
                Assert.True(new SettingsService(settingsService.FilePath).Current.ShowBossSumRow);
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

    // Digital twin library: the pause before each call to the official website, 0.7 s at first, says on hover what it costs,
    // with a red warning under it; a new pause is kept, also through the settings' other saves.
    [Fact]
    public void The_Twin_Library_Tab_Sets_The_Pause_Between_Website_Calls()
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
                window = SettingsWindow.Open(settings, SettingsTab.TwinLibrary, uiScale);
                Dispatcher.UIThread.RunJobs();
                var view = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single();
                var pause = (MenuItem)view.FindControl<Menu>("TwinLookupMenu")!.Items[^1]!;
                var warning = view.FindControl<TextBlock>("TwinLookupPauseWarning")!;
                Assert.Equal([localization["Settings_TwinLookupPause"], 0.7.ToString("0.0", CultureInfo.CurrentCulture) + " s"], Header(pause));
                Assert.Equal(localization["Settings_TwinLookupPauseTip"], ToolTip.GetTip(pause));
                Assert.Equal((true, localization["Settings_TwinLookupPauseWarning"]), (warning.IsEffectivelyVisible, warning.Text));
                Assert.Equal(Color.Parse("#F07C82"), Assert.IsAssignableFrom<ISolidColorBrush>(warning.Foreground).Color);

                var slider = pause.GetLogicalDescendants().OfType<Slider>().Single();
                slider.Value = 30;
                slider.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Right });
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(3, settings.TwinLookupPauseSeconds);
                Assert.Equal(3.0.ToString("0.0", CultureInfo.CurrentCulture) + " s", Header(pause)[1]);
                // Each save copies the settings first.
                settingsService.Update(static s => s.TutorialDone = true);
                Assert.Equal(3, new SettingsService(settingsService.FilePath).Current.TwinLookupPauseSeconds);
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

    // Digital twin library: "Use last battle's skill levels" is on at first, says on hover what it does, and a click turns
    // it off (kept).
    [Fact]
    public void The_Twin_Library_Tab_Turns_The_Seen_Skill_Levels_Off()
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
                window = SettingsWindow.Open(settings, SettingsTab.TwinLibrary, uiScale);
                Dispatcher.UIThread.RunJobs();
                var row = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single().FindControl<Menu>("TwinLookupMenu")!.Items.Cast<MenuItem>()
                    .Single(item => Header(item)[0] == localization["TwinLibrary_UseSeenLevels"]);
                Assert.True(row.IsEffectivelyVisible);
                Assert.True(settings.UseSeenSkillLevels);
                Assert.Equal(localization["TwinLibrary_UseSeenLevelsTip"], ToolTip.GetTip(row));

                row.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.False(settings.UseSeenSkillLevels);
                Assert.False(new SettingsService(settingsService.FilePath).Current.UseSeenSkillLevels);
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

    // System: the patch notes of every version, the newest first, the installed one (1.0.75) marked and shown: its short
    // version above its details, a line each, a heading bold, a bullet beside its dot, no build marks. A click on the
    // version shown opens the drop-down, which explains itself on hover; the mouse wheel over it leaves the version (a
    // focused box would take the wheel from the page). Another version picked shows its notes, unmarked. Each tab opens
    // at its top. A line under the heading says the notes are in English, in German but not in English. A local build
    // (1.0.0) marks no version and shows the newest.
    [Fact]
    public void The_System_Tab_Shows_The_Patch_Notes_Of_The_Version_Picked()
    {
        AvaloniaTestHost.Run(() =>
        {
            // The XAML's texts come from the views' shared localization (Traditional Chinese), the rows' from the settings'.
            var (shared, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var folder = Path.Combine(Path.GetTempPath(), $"aion2flow-settings-{Guid.NewGuid():N}");
            // The language service sets this thread's UI culture and the process default: both go back afterwards.
            var culture = (CultureInfo.CurrentUICulture, CultureInfo.DefaultThreadCurrentUICulture);
            var language = new LanguageService();
            language.SetLanguage(LanguageService.English);
            using var localization = new LocalizationService(language);
            SettingsWindow? window = null;
            ComboBox? box = null;
            try
            {
                var settingsService = new SettingsService(Path.Combine(folder, "settings.json"));
                using var uiScale = new UiScaleService(settingsService);
                var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                    new AppUpdateService { CurrentVersion = "1.0.75" }, new ProcessForegroundWatcher(), new GlobalHotkeyService());
                window = SettingsWindow.Open(settings, SettingsTab.System, uiScale);
                Dispatcher.UIThread.RunJobs();
                var view = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single();
                box = view.FindControl<ComboBox>("PatchNotesBox")!;
                var (notes, hint) = (view.FindControl<StackPanel>("PatchNotesPanel")!, view.FindControl<TextBlock>("PatchNotesHint")!);

                var versions = box.Items.Cast<PatchNotesOption>().ToArray();
                Assert.Equal(PatchNotes.Sections.Select(static section => section.Label), versions.Select(static version => version.Label));
                Assert.Equal(versions.OrderByDescending(static version => version.Section.ToBuild), versions);
                var installed = Assert.Single(versions, static version => version.IsInstalled);
                Assert.Same(installed, box.SelectedItem);
                Assert.Equal(["1.0.75 · 18 Nov 2026", shared["Settings_PatchNotesInstalled"]],
                    box.GetVisualDescendants().OfType<TextBlock>().Where(static text => text.IsEffectivelyVisible).Select(static text => text.Text));
                Assert.Equal(shared["Settings_PatchNotesTip"], ToolTip.GetTip(box));

                var lines = PatchNotesLines(notes);
                Assert.Equal(installed.Section.Blocks.Select(static block => block.Text), lines);
                var detail = lines.IndexOf("In detail");
                Assert.Equal(("In short", "DPS meter"), (lines[0], lines[detail + 1]));
                Assert.InRange(detail, 4, 8);
                Assert.DoesNotContain(lines, static line => line.EndsWith(']'));
                Assert.True(notes.IsEffectivelyVisible);
                Assert.Equal(FontWeight.Bold, ((SelectableTextBlock)notes.Children[0]).FontWeight);
                var bullet = (DockPanel)notes.Children[detail + 2];
                Assert.Equal("•", ((TextBlock)bullet.Children[0]).Text);
                var text = (SelectableTextBlock)bullet.Children[1];
                Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
                Assert.Equal([FontWeight.Normal, FontWeight.Bold], text.Inlines!.Take(2).Select(static run => run.FontWeight));
                Assert.False(hint.IsEffectivelyVisible);

                // The settings window moves only by its title bar, so a press and release on the version shown open the box.
                Click(window, box.GetVisualDescendants().OfType<TextBlock>().First(static text => text.Text == "1.0.75 · 18 Nov 2026"));
                Assert.True(box.IsDropDownOpen);
                box.IsDropDownOpen = false;
                // The mouse wheel over the box leaves the version, also where a click would have focused the box.
                box.Focus();
                box.RaiseEvent(new PointerWheelEventArgs(box, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true), window, default, 0,
                    new PointerPointProperties(), KeyModifiers.None, new Vector(0, -1)));
                Assert.Same(installed, box.SelectedItem);
                box.SelectedItem = versions.Single(static version => version.Section.Covers(74));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(["1.0.74 · 17 Nov 2026"], box.GetVisualDescendants().OfType<TextBlock>().Where(static text => text.IsEffectivelyVisible).Select(static text => text.Text));
                Assert.Equal("1.0.74 · 17 Nov 2026", settings.SelectedPatchNotes?.Label);
                Assert.Equal(settings.SelectedPatchNotes!.Section.Blocks.Select(static block => block.Text), PatchNotesLines(notes));
                Assert.Equal("DPS meter", PatchNotesLines(notes)[0]);

                // The tabs share the page's scroll, and each opens at its top.
                var page = view.FindControl<ScrollViewer>("PageScroll")!;
                page.Offset = new Vector(0, 400);
                window.UpdateLayout();
                Assert.Equal(400, page.Offset.Y);
                settings.Tab = SettingsTab.Visuals;
                settings.Tab = SettingsTab.System;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, page.Offset.Y);

                language.SetLanguage(LanguageService.German);
                Dispatcher.UIThread.RunJobs();
                Assert.True(hint.IsEffectivelyVisible);
                Assert.Equal(shared["Settings_PatchNotesEnglish"], hint.Text);
                Assert.Equal("Die Patchnotes gibt es nur auf Englisch.", localization["Settings_PatchNotesEnglish"]);
                Assert.Equal("DPS meter", PatchNotesLines(notes)[0]);

                // A build of a version without notes (a local 1.0.0) marks none and shows the newest.
                var local = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                    new AppUpdateService { CurrentVersion = "1.0.0" }, new ProcessForegroundWatcher(), new GlobalHotkeyService());
                Assert.DoesNotContain(local.PatchNotesVersions, static version => version.IsInstalled);
                Assert.Same(local.PatchNotesVersions[0], local.SelectedPatchNotes);
            }
            finally
            {
                box?.IsDropDownOpen = false;
                window?.Close();
                Dispatcher.UIThread.RunJobs();
                (CultureInfo.CurrentUICulture, CultureInfo.DefaultThreadCurrentUICulture) = culture;
                Application.Current.Resources.MergedDictionaries.Remove(icons);
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
    }

    // The patch notes shown, a line each (a bullet's text beside its dot).
    private static List<string> PatchNotesLines(StackPanel notes) =>
        [.. notes.Children.Select(static line => ((SelectableTextBlock)(line is DockPanel bullet ? bullet.Children[1] : line)).Inlines!.Text ?? "")];

    // A left press and release on the middle of a control, as the mouse sends them.
    private static void Click(Window window, Control target)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var at = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        target.RaiseEvent(new PointerPressedEventArgs(target, pointer, window, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        target.RaiseEvent(new PointerReleasedEventArgs(target, pointer, window, at, 1,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
    }

    // Every row and button of the settings explains itself on hover, "Compact main numbers" and "Target status bar" (which had
    // none) among them: the rows built in code and those in the XAML, also inside a submenu, a scene's shortcut under Hotkeys,
    // a scene's buttons and its frames' states, a workflow, and the skill monitor tab's buttons. Not the tabs, nor a submenu's
    // choices (their row says what they choose).
    [Fact]
    public void Every_Setting_And_Button_Has_A_Hover_Text()
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
                settings.AddScene("Farming");
                settings.AddWorkflow();
                window = SettingsWindow.Open(settings, SettingsTab.General, uiScale);
                Dispatcher.UIThread.RunJobs();
                var view = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single();
                var tabs = view.FindControl<StackPanel>("SettingsTabsPanel")!;

                // Each tab shown once, so the rows made from its lists are there; not the skill monitor's, whose lists take the
                // game data from the app's services (its buttons below).
                foreach (var tab in Enum.GetValues<SettingsTab>().Where(static tab => tab != SettingsTab.SkillMonitor))
                {
                    settings.Tab = tab;
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                var rows = Sweep(view).Where(row => !Within(row, tabs)).ToArray();
                Assert.Contains(rows, static row => row is ComboBox);
                Assert.Equal(2, rows.Count(static row => row.Classes.Contains("SettingsHotkeyRow") && row.DataContext is SceneViewModel));
                Assert.Contains(rows, static row => row is ToggleButton && row.DataContext is SceneFrameViewModel);
                Assert.Contains(rows, static row => row.Parent is MenuItem { Name: "MainMetricVisibilityMenuItem" });
                Assert.Empty(rows.Where(static row => ToolTip.GetTip(row) is not string { Length: > 0 }).Select(Describe));
                Assert.Equal(localization["Settings_MainMetricsCompactTip"], ToolTip.GetTip(view.FindControl<MenuItem>("CompactMainMetricsMenuItem")!));
                Assert.Equal(localization["Settings_FocusStatusBarTip"], ToolTip.GetTip(view.FindControl<MenuItem>("FocusStatusBarMenuItem")!));
                // Saving packets (off until a recording exists) and adding a workflow (off until a scene exists) explain
                // themselves while they're off too.
                Assert.All(["Settings_Debug_SavePacketsTip", "Workflows_AddTip"], key =>
                    Assert.True(ToolTip.GetShowOnDisabled(rows.Single(row => Equals(ToolTip.GetTip(row), localization[key])))));

                // The skill monitor tab's lists: Select all and Clear all for the buffs, the cooldowns and the debuffs.
                using var resources = new GameResourceService(language);
                using var monitor = new SkillMonitorSettingsViewModel(resources, settingsService, localization);
                var monitorView = new SkillMonitorSettingsView { DataContext = monitor };
                monitorWindow = new Window { Width = 500, Height = 600, Content = monitorView };
                monitorWindow.Show();
                Dispatcher.UIThread.RunJobs();
                var (selectAll, clearAll) = (localization["Settings_SkillMonitor_SelectAllTip"], localization["Settings_SkillMonitor_ClearAllTip"]);
                Assert.Equal([selectAll, clearAll, selectAll, clearAll, selectAll, clearAll], Sweep(monitorView).Select(static button => ToolTip.GetTip(button)));
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

    // What a view shows of its own (not parts of a control's template) to hover: rows and submenu items that aren't choices,
    // buttons (check boxes and toggles too), drop-downs and hotkey rows.
    private static IEnumerable<Control> Sweep(Control view) =>
        view.GetLogicalDescendants().Concat(view.GetVisualDescendants()).OfType<Control>().Distinct()
            .Where(static control => control.TemplatedParent is null &&
                (control is MenuItem { Tag: null } or Button or ComboBox || control.Classes.Contains("SettingsHotkeyRow")));

    private static bool Within(Control control, Control ancestor) =>
        control.GetLogicalAncestors().Contains(ancestor) || control.GetVisualAncestors().Contains(ancestor);

    // A control by its name or its label, for the message when it has no hover text.
    private static string Describe(Control control) =>
        control.Name ?? control.GetLogicalDescendants().OfType<TextBlock>().Select(static text => text.Text).FirstOrDefault(static text => !string.IsNullOrEmpty(text))
        ?? control.GetType().Name;

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

    // Hovering a skill row in the skill monitor tab shows what the skill does: from level 1 to max until the player's own
    // digital twin comes in, then at their level of it.
    [Fact]
    public void A_Skill_Monitor_Row_Shows_What_The_Skill_Does_At_The_Players_Level()
    {
        AvaloniaTestHost.Run(() =>
        {
            const int light = 17410000;
            var (localization, _) = ViewTestServices.Get();
            var path = Path.Combine(Path.GetTempPath(), $"aion2flow-settings-{Guid.NewGuid():N}.json");
            Window? window = null;
            try
            {
                var settingsService = new SettingsService(path);
                settingsService.Update(static settings => (settings.LocalPlayerServerId, settings.LocalPlayerNickname) = (1301, "Healer A"));
                using var twins = new DigitalTwinService();
                using var resources = new GameResourceService(new LanguageService());
                using var monitor = new SkillMonitorSettingsViewModel(resources, settingsService, localization, twins);
                var view = new SkillMonitorSettingsView { DataContext = monitor };
                window = new Window { Width = 500, Height = 600, Content = view };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                // No class opened: no rows, so no hover texts worked out yet.
                Assert.DoesNotContain(view.GetVisualDescendants(), static control => control.DataContext is SkillMonitorSkillOption);
                monitor.Groups.Single(static group => group.Skills.Any(static skill => skill.SkillId == light)).IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var row = view.GetVisualDescendants().OfType<Grid>().Single(static grid => grid.DataContext is SkillMonitorSkillOption { SkillId: light } && ToolTip.GetTip(grid) is not null);
                Assert.Contains("by 10.5–22.5%", Assert.IsType<SkillTip>(ToolTip.GetTip(row)).Effect);

                twins.Preload([new DigitalTwin("Healer A", 1301, "Server", "Cleric", 45, 0, 0, DateTimeOffset.UnixEpoch, [], [], [new TwinSkill(light, "Light", 21, "Passive", true)], [], [], [])]);
                Dispatcher.UIThread.RunJobs();
                var tip = Assert.IsType<SkillTip>(ToolTip.GetTip(row));
                Assert.Contains("by 20.5% and", tip.Effect);
                Assert.Contains("Healer A", tip.EffectNote);
            }
            finally
            {
                window?.Close();
                Dispatcher.UIThread.RunJobs();
                File.Delete(path);
            }
        });
    }

    // Visuals and each frame's own tab end their frame's rows with "Clickable without focus" and, below it, "Title line always
    // clickable" (the meter's on, the others' off), each turning that frame's setting round; the General tab lists every
    // frame's "Clickable without focus" only.
    [Fact]
    public void Each_Frame_Has_Clickable_Without_Focus_And_Title_Line_Always_Clickable_In_Visuals_And_Its_Tab()
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
                    ("VisualsMeterMenu", SettingsFlyoutViewModel.MeterKey),
                    ("VisualsGroupFrameMenu", SettingsFlyoutViewModel.GroupFrameKey),
                    ("VisualsSkillMonitorMenu", SettingsFlyoutViewModel.SkillMonitorKey),
                    ("VisualsFightGraphsMenu", SettingsFlyoutViewModel.FightGraphsKey),
                    ("VisualsHiddenCubesMenu", SettingsFlyoutViewModel.HiddenCubesKey),
                    ("VisualsTwinLibraryMenu", SettingsFlyoutViewModel.TwinLibraryKey),
                    ("MeterClickableMenu", SettingsFlyoutViewModel.MeterKey),
                    ("GroupFrameMenu", SettingsFlyoutViewModel.GroupFrameKey),
                    ("SkillMonitorMenu", SettingsFlyoutViewModel.SkillMonitorKey),
                    ("FightGraphsMenu", SettingsFlyoutViewModel.FightGraphsKey),
                    ("HiddenCubesMenu", SettingsFlyoutViewModel.HiddenCubesKey),
                    ("TwinLibraryMenu", SettingsFlyoutViewModel.TwinLibraryKey),
                })
                {
                    var items = view.FindControl<Menu>(menu)!.Items;
                    var (clickable, title) = ((MenuItem)items[^2]!, (MenuItem)items[^1]!);
                    Assert.Equal(localization["Settings_ClickableWithoutFocus"], Header(clickable)[0]);
                    Assert.Equal([localization["Settings_TitleClickable"], localization[frame == SettingsFlyoutViewModel.MeterKey ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"]],
                        Header(title));
                    Assert.Equal(localization["Settings_TitleClickableTip"], ToolTip.GetTip(title));

                    var was = (settings.IsClickableWithoutFocus(frame), settings.IsTitleClickable(frame));
                    clickable.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    title.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal((!was.Item1, !was.Item2), (settings.IsClickableWithoutFocus(frame), settings.IsTitleClickable(frame)));
                    Assert.Equal(localization[was.Item2 ? "Settings_SkillMonitor_Off" : "Settings_SkillMonitor_On"], Header(title)[1]);

                    // Back as it was, for the frame's other row.
                    settings.ToggleClickableWithoutFocus(frame);
                    settings.ToggleTitleClickable(frame);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(localization[was.Item2 ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"], Header(title)[1]);
                }

                Assert.Equal(SettingsFlyoutViewModel.ClickableFrames.Length, view.FindControl<Menu>("ClickableMenu")!.Items.Count);
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

    // The Scenes tab: two headed columns of check boxes beside each frame's states. A scene from before they were kept shows
    // the third state (a dash: it leaves the setting as it is); clicks go round off, on and the dash again, each kept.
    [Fact]
    public void A_Scenes_Frames_Have_Three_State_Check_Boxes_For_Their_Clicks()
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
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, "settings.json");
                File.WriteAllText(path, """{"NextSceneId":2,"Scenes":[{"Id":1,"Name":"Farming","Frames":{"Meter":{"State":"Open"}}}]}""");
                var settingsService = new SettingsService(path);
                using var uiScale = new UiScaleService(settingsService);
                var settings = new SettingsFlyoutViewModel(localization, new LanguageService(), settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                    new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
                window = SettingsWindow.Open(settings, SettingsTab.Scenes, uiScale);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var view = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single();

                var headers = view.GetVisualDescendants().OfType<TextBlock>().Where(static text => text.Classes.Contains("SceneColumnHeader")).ToArray();
                Assert.Equal([localization["Scenes_ClickableHeader"], localization["Scenes_TitleClickableHeader"]], headers.Select(static header => header.Text));
                Assert.Equal([localization["Settings_ClickableWithoutFocusTip"], localization["Settings_TitleClickableTip"]], headers.Select(static header => ToolTip.GetTip(header)));

                var meter = settings.Scenes.Single().Frames.Single(static frame => frame.Frame == SettingsFlyoutViewModel.MeterKey);
                var boxes = view.GetVisualDescendants().OfType<CheckBox>().Where(box => box.DataContext == meter).ToArray();
                Assert.Equal([2, 3], boxes.Select(static box => Grid.GetColumn(box)));
                Assert.All(boxes, box => Assert.Equal((true, null, localization["Scenes_SettingTip"]), (box.IsThreeState, box.IsChecked, ToolTip.GetTip(box))));

                var title = ControlAutomationPeer.CreatePeerForElement(boxes[1]).GetProvider<IToggleProvider>()!;
                bool? Kept() => new SettingsService(path).Current.Scenes.Single().Frames[SettingsFlyoutViewModel.MeterKey].TitleClickable;
                title.Toggle();
                Assert.Equal((false, false), (meter.TitleClickable, Kept()));
                title.Toggle();
                Assert.Equal((true, true), (meter.TitleClickable, Kept()));
                title.Toggle();
                Assert.Equal((null, null), (meter.TitleClickable, Kept()));
                Assert.Null(meter.ClickableWithoutFocus);
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

    // A scene's row: Apply now, Overwrite, rename and delete, each explaining itself on hover, as Add current scene, the scene's
    // shortcut and its frames' states do. Overwrite asks once ("Overwrite?"), then keeps the frames as they are now in the
    // scene under its name; the second click of a double-click doesn't answer, for Delete as for Overwrite.
    [Fact]
    public void A_Scenes_Overwrite_Asks_Once_And_A_Double_Click_Only_Asks()
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
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, "settings.json");
                File.WriteAllText(path, """{"NextSceneId":2,"Scenes":[{"Id":1,"Name":"Farming","Frames":{"Meter":{"State":"Open"}}}]}""");
                var settingsService = new SettingsService(path);
                using var uiScale = new UiScaleService(settingsService);
                var settings = new SettingsFlyoutViewModel(localization, new LanguageService(), settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                    new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
                window = SettingsWindow.Open(settings, SettingsTab.Scenes, uiScale);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var view = window.GetLogicalDescendants().OfType<SettingsFlyoutView>().Single();
                var clock = new SteppedClock();
                view.Clock = clock;
                var doubleClick = view.GetPlatformSettings()!.GetDoubleTapTime(PointerType.Mouse);

                var scene = settings.Scenes.Single();
                var row = view.GetVisualDescendants().OfType<Button>().Where(button => button.DataContext == scene && button.Parent is Grid { ColumnDefinitions.Count: 5 }).OrderBy(Grid.GetColumn).ToArray();
                Assert.Equal([localization["Scenes_ApplyTip"], localization["Scenes_OverwriteTip"], localization["Scenes_RenameTip"], localization["Scenes_DeleteTip"]], row.Select(static button => ToolTip.GetTip(button)));
                var add = view.GetVisualDescendants().OfType<Button>().Single(button => button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == localization["Scenes_Add"]));
                Assert.Equal(localization["Scenes_AddTip"], ToolTip.GetTip(add));
                var shortcut = view.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("SettingsHotkeyRow") && border.DataContext == scene && border.IsEffectivelyVisible);
                Assert.Equal(localization["Scenes_ShortcutTip"], ToolTip.GetTip(shortcut));
                var states = view.GetVisualDescendants().OfType<ToggleButton>().Where(static toggle => toggle.Parent is StackPanel panel && panel.Classes.Contains("SceneStates")).ToArray();
                Assert.Equal(3 * SettingsFlyoutViewModel.Frames.Length, states.Length);
                Assert.All(states, toggle => Assert.Equal(localization["Scenes_StateTip"], ToolTip.GetTip(toggle)));

                var (overwrite, delete) = (row[1], row[3]);
                string? Shown(Button button) => button.GetVisualDescendants().OfType<TextBlock>().Single(static text => text.IsEffectivelyVisible).Text;
                bool? Kept() => new SettingsService(path).Current.Scenes.Single().Frames[SettingsFlyoutViewModel.MeterKey].TitleClickable;
                void Click(Button button)
                {
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                // Asked, and the second click of a double-click: still asking, nothing kept.
                Assert.Equal(localization["Scenes_Overwrite"], Shown(overwrite));
                Click(overwrite);
                clock.Advance(doubleClick - TimeSpan.FromTicks(1));
                Click(overwrite);
                Assert.Equal((localization["Scenes_OverwriteAsk"], (bool?)null), (Shown(overwrite), Kept()));

                // Answered after the double-click time: the frames as they are now (the meter's title line is clickable at
                // first), under the scene's name and id.
                clock.Advance(TimeSpan.FromTicks(1));
                Click(overwrite);
                Assert.Equal((localization["Scenes_Overwrite"], (bool?)true), (Shown(overwrite), Kept()));
                Assert.Equal(("Farming", 1), (new SettingsService(path).Current.Scenes.Single().Name, new SettingsService(path).Current.Scenes.Single().Id));

                // Delete the same way: a double-click asks only, a later click deletes.
                Click(delete);
                Click(delete);
                Assert.Equal((true, false, localization["TwinLibrary_Delete"]), (scene.IsDeleteArmed, scene.IsOverwriteArmed, Shown(delete)));
                Assert.Same(scene, settings.Scenes.Single());
                clock.Advance(doubleClick);
                Click(delete);
                Assert.Empty(settings.Scenes);
                Assert.Empty(new SettingsService(path).Current.Scenes);
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

    // A clock that moves only when told to.
    private sealed class SteppedClock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
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
    }

    // A row's label and value.
    private static string?[] Header(object? row) => [.. ((Grid)((MenuItem)row!).Header!).Children.OfType<TextBlock>().Select(static text => text.Text)];

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
