using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Twins;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Logging;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Cloris.Aion2Flow.Views;

// The tutorial (the meter's "?", or Settings → General): a tour of the overlay with a made-up fight. HappyCat, a cleric
// ("you" for the tour), her group and Vakron are a history entry of their own while it runs, HappyCat a twin in the
// library, and the group frame and the loot bag show the group at the fight's end. Each step brings up what it's about,
// rings it and puts the card beside it. At the end (Done, End tour, or the meter closing) all goes back: the frames as
// they were, the overlay's mode and the history entry shown; the demo entry and twin are gone. Should the overlay end
// mid-tour, the next start puts the frames back (AppSettings.TutorialRestore).
public partial class MainWindow
{
    private TutorialRun? _tutorial;
    private TutorialCardWindow? _tutorialCard;
    private Control? _tutorialRinged;
    private TutorialStep[]? _tutorialSteps;

    private sealed record TutorialStep(string Key, Func<TutorialRun, Task<Control?>> Show);

    private sealed class TutorialRun(TutorialDemo demo, DigitalTwin twin, FrameScene frames, IReadOnlyList<string> minimized, OverlayInteractionMode mode,
        ArchivedEncounterRecord? shown)
    {
        public TutorialDemo Demo { get; } = demo;

        public DigitalTwin Twin { get; } = twin;

        public FrameScene Frames { get; } = frames;

        // Folded at the start (the frames that were off too, which Frames leaves as they are).
        public IReadOnlyList<string> Minimized { get; } = minimized;

        public OverlayInteractionMode Mode { get; } = mode;

        // The history entry shown before; null: the live fight.
        public ArchivedEncounterRecord? Shown { get; } = shown;

        public int Step { get; set; }

        // Counts the steps shown, so one still bringing up its frame doesn't overrule a newer one.
        public int Showing { get; set; }
    }

    private TutorialStep[] TutorialSteps => _tutorialSteps ??=
    [
        new("Welcome", run => OnMeter(run, () => MainHudShell)),
        new("Rows", run => OnMeter(run, () => HappyCatRow())),
        new("Sorting", run => OnMeter(run, () => ColumnHeader(CombatantSortMetric.HealingPerSecond))),
        new("Uhs", run => OnMeter(run, () => ColumnHeader(CombatantSortMetric.UnhealedHealth))),
        new("Details", TutorialDetails),
        new("BossSection", run => OnMeter(run, () => BossSection)),
        new("History", run => OnMeter(run, () => EncounterHistoryButton)),
        new("Playback", TutorialPlayback),
        new("Ping", run => OnMeter(run, () => PingButton)),
        new("Pin", run => OnMeter(run, () => _pinWindow?.PinButton)),
        new("Frames", run => OnMeter(run, () => TitleBarLeft)),
        new("GroupFrame", run => OnGroupFrame(run, static window => window.FrameRoot)),
        new("LootBag", run => OnGroupFrame(run, static window => window.GetVisualDescendants().OfType<Border>().FirstOrDefault(static border => border.Classes.Contains("GroupFrameLoot")))),
        new("TitleClick", run => OnGroupFrame(run, static window => window.TitleLine)),
        new("SkillMonitor", run => OnFrame<SkillMonitorWindow>(run, SettingsFlyoutViewModel.SkillMonitorKey, static settings => settings.SkillMonitorEnabled = true, static window => window.MonitorRoot)),
        new("FightGraphs", run => OnFrame<FightGraphsWindow>(run, SettingsFlyoutViewModel.FightGraphsKey, static settings => settings.FightGraphsEnabled = true, static window => window.FrameRoot)),
        new("TwinLibrary", TutorialTwinLibrary),
        new("Scenes", run => OnSettings(SettingsTab.Scenes)),
        new("Workflows", run => OnSettings(SettingsTab.Workflows)),
        new("Shortcuts", run => OnSettings(SettingsTab.Shortcuts)),
        new("Done", run => OnSettings(SettingsTab.General, static view => view.StartTutorialButton)),
    ];

    private void TutorialClicked(object? sender, RoutedEventArgs e) => StartTutorial();

    private void StartTutorial()
    {
        if (_tutorial is { } running)
        {
            ShowTutorialStep(running.Step);
            return;
        }

        var settings = DataContext.SettingsFlyout;
        var frames = settings.CaptureFrames();
        var minimized = settings.MinimizedFrames;
        _settingsService.Update(saved =>
        {
            saved.TutorialRestore = frames.Clone();
            saved.TutorialRestoreMinimized = [.. minimized];
        });
        var shown = DataContext.IsViewingArchivedEncounter ? DataContext.SelectedEncounterHistory?.Record : null;
        // Ending now, so the playback and the frames read it as just over.
        var demo = TutorialDemoScene.Build(DateTimeOffset.Now - TutorialDemoScene.Length);
        var twin = TutorialTwin.Create(DateTimeOffset.Now);
        Ioc.Default.GetRequiredService<DigitalTwinLibrary>().AddTemporary(twin);
        Ioc.Default.GetRequiredService<DigitalTwinService>().Preload([twin]);
        var run = _tutorial = new TutorialRun(demo, twin, frames, minimized, _overlayInteractionController.Mode, shown);
        settings.TutorialRunning = true;
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        _overlayInteractionController.SetMode(OverlayInteractionMode.Interactive);
        settings.BossParticipation.ShowDemo(demo.Record.ScenePayload.BossFights, TutorialDemoScene.HappyCatId);

        var card = _tutorialCard = new TutorialCardWindow();
        // Closed on its own (Alt+F4): the same as End tour. EndTutorial and the meter closing get here with the tour
        // already over, so nothing runs twice.
        card.Closed += (_, _) =>
        {
            if (!ReferenceEquals(_tutorialCard, card))
                return;

            _tutorialCard = null;
            EndTutorial(done: true);
        };
        card.PreviousRequested += () => ShowTutorialStep(run.Step - 1);
        card.NextRequested += () =>
        {
            if (run.Step >= TutorialSteps.Length - 1)
                EndTutorial(done: true);
            else
                ShowTutorialStep(run.Step + 1);
        };
        card.EndRequested += () => EndTutorial(done: true);
        _uiScale.RegisterWindow(card);
        card.Show(this);
        ShowTutorialStep(0);
    }

    private async void ShowTutorialStep(int index)
    {
        if (_tutorial is not { } run || _tutorialCard is not { } card)
            return;

        var steps = TutorialSteps;
        index = Math.Clamp(index, 0, steps.Length - 1);
        run.Step = index;
        var showing = ++run.Showing;
        var step = steps[index];
        Control? target;
        try
        {
            target = await step.Show(run);
            // Laid out first, so that the ring and the card find it where it shows.
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Tutorial step {step.Key}: {ex.Message}");
            target = null;
        }

        if (!ReferenceEquals(_tutorial, run) || run.Showing != showing)
            return;

        if (target is { IsEffectivelyVisible: true })
            target.BringIntoView();
        else
            target = null;
        RingTutorialTarget(target);
        var localization = DataContext.Localization;
        card.ShowStep(
            string.Format(localization["Tutorial_StepFormat"], index + 1, steps.Length),
            localization[$"Tutorial_{step.Key}_Title"],
            localization[$"Tutorial_{step.Key}_Text"],
            localization[index == steps.Length - 1 ? "Tutorial_Done" : "Tutorial_Next"],
            index > 0);
        card.Activate();
        Dispatcher.UIThread.Post(() => PlaceTutorialCard(target), DispatcherPriority.Loaded);
    }

    // done: it ran (Done or End tour); false when the meter closes mid-tour.
    private void EndTutorial(bool done)
    {
        if (_tutorial is not { } run)
            return;

        _tutorial = null;
        RingTutorialTarget(null);
        _tutorialCard?.Close();
        _tutorialCard = null;
        var settings = DataContext.SettingsFlyout;
        settings.TutorialRunning = false;
        CloseTransientSurfaces();
        _playbackWindowController.Close();
        SettingsWindow.CloseOpen();
        settings.BossParticipation.EndDemo();
        foreach (var window in Windows<GroupFrameWindow>())
            window.DataContext.DemoScene = null;
        foreach (var window in Windows<SkillMonitorWindow>())
        {
            if (window.DataContext is SkillMonitorViewModel monitor)
                monitor.DemoScene = null;
        }

        // The demo entry and HappyCat go; the library closes, so that it doesn't keep showing her (it opens again below if
        // it was open before).
        if (DataContext.EncounterHistory.FirstOrDefault(item => ReferenceEquals(item.Record, run.Demo.Record)) is { } entry)
            DataContext.RemoveEncounterHistory(entry);
        if (Ioc.Default.GetRequiredService<DigitalTwinLibrary>().RemoveTemporary() is { } twin)
            Ioc.Default.GetRequiredService<DigitalTwinService>().Remove(twin);
        DigitalTwinLibraryWindow.CloseOpen();

        settings.MinimizedFrames = run.Minimized;
        settings.ApplyFrames(run.Frames);
        // The mode before the entry: click-through goes back to live, which would drop it again.
        _overlayInteractionController.SetMode(run.Mode);
        if (run.Shown is { } shown && DataContext.EncounterHistory.Any(item => ReferenceEquals(item.Record, shown)))
            DataContext.ShowHistoryEntry(shown);
        else
            DataContext.ReturnToLiveCommand.Execute(null);
        _settingsService.Update(static saved =>
        {
            saved.TutorialRestore = null;
            saved.TutorialRestoreMinimized = null;
        });
        if (done)
            settings.TutorialDone = true;
    }

    // A tour the overlay closed in the middle of: its frames back as they were (the frames' settings were saved as it went).
    private void RestoreAfterTutorial()
    {
        if (_settingsService.Current.TutorialRestore is not { } frames)
            return;

        if (_settingsService.Current.TutorialRestoreMinimized is { } minimized)
            DataContext.SettingsFlyout.MinimizedFrames = [.. minimized];
        DataContext.SettingsFlyout.ApplyFrames(frames);
        _settingsService.Update(static saved =>
        {
            saved.TutorialRestore = null;
            saved.TutorialRestoreMinimized = null;
        });
    }

    // The meter as the tour shows it: the demo entry, unfolded, nothing of another step left open.
    private Task<Control?> OnMeter(TutorialRun run, Func<Control?> target)
    {
        ShowTutorialDemo(run);
        _playbackWindowController.Close();
        SettingsWindow.CloseOpen();
        CloseTransientSurfaces();
        return Task.FromResult(target());
    }

    private void ShowTutorialDemo(TutorialRun run)
    {
        var settings = DataContext.SettingsFlyout;
        if (settings.IsMinimized(SettingsFlyoutViewModel.MeterKey))
            settings.ToggleMinimized(SettingsFlyoutViewModel.MeterKey);
        // Seen and clickable again after the pin's step (EndTutorial puts the mode back).
        _overlayInteractionController.SetMode(OverlayInteractionMode.Interactive);
        if (!DataContext.IsViewingArchivedEncounter || !ReferenceEquals(DataContext.SelectedEncounterHistory?.Record, run.Demo.Record))
            DataContext.ShowHistoryEntry(run.Demo.Record);
    }

    private Control? HappyCatRow() =>
        MainHudShell.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(static border => border.Name == "RowRoot" && border.DataContext is CombatantRowViewModel { Id: TutorialDemoScene.HappyCatId });

    // The column's heading, or (with that column or the headings off) any heading shown, else HappyCat's row.
    private Control? ColumnHeader(CombatantSortMetric metric)
    {
        var headings = MainHudShell.GetVisualDescendants().OfType<Button>()
            .Where(static button => button.IsEffectivelyVisible && button.CommandParameter is CombatantSortMetric).ToList();
        return headings.FirstOrDefault(button => (CombatantSortMetric)button.CommandParameter! == metric) ?? headings.FirstOrDefault() ?? HappyCatRow();
    }

    private async Task<Control?> TutorialDetails(TutorialRun run)
    {
        await OnMeter(run, static () => null);
        if (DataContext.Combatants.FirstOrDefault(static row => row.Id == TutorialDemoScene.HappyCatId) is not { } row)
            return null;

        // The card first: the details close when the meter loses the focus (as they would to the card after them).
        _tutorialCard?.Activate();
        ShowCombatantDetails(row);
        return CombatantDetailsFlyoutView;
    }

    private async Task<Control?> TutorialPlayback(TutorialRun run)
    {
        await OnMeter(run, static () => null);
        if (DataContext.EncounterHistory.FirstOrDefault(item => ReferenceEquals(item.Record, run.Demo.Record)) is not { } entry)
            return null;

        OpenPlayback(MainViewModel.CreatePlaybackContext(entry));
        return Windows<ScenePlaybackWindow>().FirstOrDefault()?.RootLayout;
    }

    // A frame on and unfolded for its step (the frames go back as they were at the end).
    private Task<Control?> OnFrame<TWindow>(TutorialRun run, string frame, Action<SettingsFlyoutViewModel> turnOn, Func<TWindow, Control?> target)
        where TWindow : Window
    {
        ShowTutorialDemo(run);
        _playbackWindowController.Close();
        SettingsWindow.CloseOpen();
        CloseTransientSurfaces();
        var settings = DataContext.SettingsFlyout;
        turnOn(settings);
        if (settings.IsMinimized(frame))
            settings.ToggleMinimized(frame);
        foreach (var monitor in Windows<SkillMonitorWindow>())
        {
            if (monitor.DataContext is SkillMonitorViewModel model)
                model.DemoScene = run.Demo.Scene;
        }

        return Task.FromResult(Windows<TWindow>().FirstOrDefault() is { } window ? target(window) : null);
    }

    // The group frame shows the demo group at the fight's end.
    private Task<Control?> OnGroupFrame(TutorialRun run, Func<GroupFrameWindow, Control?> target) =>
        OnFrame<GroupFrameWindow>(run, SettingsFlyoutViewModel.GroupFrameKey, static settings => settings.GroupFrameEnabled = true, window =>
        {
            window.DataContext.DemoScene = run.Demo.Scene;
            window.DataContext.ProcessUiFrame();
            return target(window);
        });

    private async Task<Control?> TutorialTwinLibrary(TutorialRun run)
    {
        await OnMeter(run, static () => null);
        var settings = DataContext.SettingsFlyout;
        if (settings.IsMinimized(SettingsFlyoutViewModel.TwinLibraryKey))
            settings.ToggleMinimized(SettingsFlyoutViewModel.TwinLibraryKey);
        OpenTwinLibrary();
        var twins = Ioc.Default.GetRequiredService<DigitalTwinService>();
        var own = DataContext.LocalCharacter is { } me ? twins.Get(me.ServerId, me.Name).Twin : null;
        if (DigitalTwinLibraryWindow.Shown is not { } library)
            return null;

        library.ShowTutorial(run.Twin, own);
        return Windows<DigitalTwinLibraryWindow>().FirstOrDefault()?.LibraryRoot;
    }

    private Task<Control?> OnSettings(SettingsTab tab, Func<SettingsFlyoutView, Control?>? target = null)
    {
        _playbackWindowController.Close();
        CloseTransientSurfaces();
        var window = SettingsWindow.Open(DataContext.SettingsFlyout, tab, _uiScale);
        var view = window.GetVisualDescendants().OfType<SettingsFlyoutView>().FirstOrDefault();
        if (view is null)
            return Task.FromResult<Control?>(null);

        return Task.FromResult(target?.Invoke(view) ?? (view.SettingsTabsPanel.Children.Count > (int)tab ? view.SettingsTabsPanel.Children[(int)tab] : null));
    }

    // A gold ring around what the step is about, in its own window's adorner layer (it follows the window, takes no clicks).
    private void RingTutorialTarget(Control? target)
    {
        if (_tutorialRinged is { } ringed)
            AdornerLayer.SetAdorner(ringed, null);
        _tutorialRinged = target;
        if (target is null)
            return;

        // Around it by 3 px where its window has the room, else just inside its edge (a frame that fills its window).
        var top = TopLevel.GetTopLevel(target);
        var near = top is null ? null : target.TranslatePoint(default, top);
        var far = top is null ? null : target.TranslatePoint(new Point(target.Bounds.Width, target.Bounds.Height), top);
        static double Out(double room) => -Math.Clamp(room, 0, 3);
        var margin = top is not null && near is { } a && far is { } b
            ? new Thickness(Out(a.X), Out(a.Y), Out(top.ClientSize.Width - b.X), Out(top.ClientSize.Height - b.Y))
            : new Thickness(-3);
        var ring = new Border
        {
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Margin = margin,
            IsHitTestVisible = false,
            BorderBrush = this.TryFindResource("OverlayBrush.MainHudGold", out var gold) && gold is IBrush brush ? brush : Brushes.Gold,
        };
        AdornerLayer.SetIsClipEnabled(ring, false);
        AdornerLayer.SetAdorner(target, ring);
    }

    // Beside the target: right of it where the screen has room, else left; at its top, kept on the screen.
    private void PlaceTutorialCard(Control? target)
    {
        if (_tutorialCard is not { } card)
            return;

        // A target gone meanwhile (a flyout that closed) has no place on the screen: beside the meter then.
        var anchor = target is not null && TopLevel.GetTopLevel(target) is not null ? target : MainHudShell;
        var rect = new PixelRect(anchor.PointToScreen(default), anchor.PointToScreen(new Point(anchor.Bounds.Width, anchor.Bounds.Height)));
        var work = (Screens.ScreenFromPoint(rect.Center) ?? Screens.Primary)?.WorkingArea ?? rect;
        var size = PixelSize.FromSize(card.ClientSize, card.DesktopScaling);
        var x = work.Right - rect.Right >= size.Width + 12 ? rect.Right + 12 : rect.X - 12 - size.Width;
        card.Position = new PixelPoint(
            Math.Clamp(x, work.X, Math.Max(work.X, work.Right - size.Width)),
            Math.Clamp(rect.Y, work.Y, Math.Max(work.Y, work.Bottom - size.Height)));
    }

    private static IEnumerable<TWindow> Windows<TWindow>() where TWindow : Window =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows.OfType<TWindow>() ?? [];
}
