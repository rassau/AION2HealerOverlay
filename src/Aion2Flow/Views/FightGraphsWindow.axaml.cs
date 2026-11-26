using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Cloris.Aion2Flow.Views;

public partial class FightGraphsWindow : Window
{
    private readonly AvaloniaFrameClockService _frameClock;
    private readonly UiScaleService _uiScale;
    private readonly SettingsService _settingsService;
    private readonly DispatcherTimer _persistPositionTimer;
    private bool _frameClockAttached;
    private (FightGraph? Graph, int Second, FightGraphModel? Model) _hover = (null, -1, null);
    // The window's graphs, looked up again once a graph entered or left a visual tree (FightGraph.TreeVersion).
    private List<FightGraph> _graphs = [];
    private int _graphsVersion = -1;

    public FightGraphsWindow()
        : this(
            Ioc.Default.GetRequiredService<AvaloniaFrameClockService>(),
            Ioc.Default.GetRequiredService<UiScaleService>(),
            Ioc.Default.GetRequiredService<SettingsService>())
    {
    }

    public FightGraphsWindow(AvaloniaFrameClockService frameClock, UiScaleService uiScale, SettingsService settingsService)
    {
        _frameClock = frameClock;
        _uiScale = uiScale;
        _settingsService = settingsService;
        _persistPositionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _persistPositionTimer.Tick += (_, _) =>
        {
            _persistPositionTimer.Stop();
            PersistPosition();
        };
        InitializeComponent();
    }

    public FightGraphsWindow(FightGraphsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    public new FightGraphsViewModel DataContext
    {
        get => (FightGraphsViewModel)base.DataContext!;
        set => base.DataContext = value;
    }

    // Lets the mouse through while the game has focus as its settings say (FrameClickThrough, from its chrome); header,
    // legends and gear show in full once the overlay has focus.
    public void SetAppFocusPresentation(bool appFocused)
    {
        FrameRoot.Classes.Set("interactive", appFocused);
        FrameClickThrough.Of(this)?.SetAppFocused(appFocused);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_settingsService.Current.FightGraphsPosition is { } position)
            Position = new PixelPoint(position.X, position.Y);
        _uiScale.RegisterWindow(this, () => _settingsService.Current.FightGraphsScalePercent);
        FrameLook.Attach(this, DataContext.Settings, s => s.FightGraphsOpacityPercent);
        FrameChrome.Attach(this, FrameRoot, TitleLine, DataContext.Settings, SettingsFlyoutViewModel.FightGraphsKey);
        FrameChrome.AttachHand(this, HandButton, DataContext.Settings, SettingsFlyoutViewModel.FightGraphsKey);
        _settingsService.Changed += OnSavedSettingsChanged;
        PositionChanged += (_, _) => SchedulePersistPosition();
        DataContext.Refresh();
        if (_frameClockAttached)
            return;

        _frameClockAttached = true;
        _frameClock.Frame += OnAnimationFrame;
        _frameClock.Attach(this);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _persistPositionTimer.Stop();
        PersistPosition();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _settingsService.Changed -= OnSavedSettingsChanged;
        if (base.DataContext is FightGraphsViewModel viewModel)
            viewModel.Dispose();

        if (_frameClockAttached)
        {
            _frameClock.Frame -= OnAnimationFrame;
            _frameClock.Detach(this);
            _frameClockAttached = false;
        }

        base.OnClosed(e);
    }

    // The fight anew once a second; the hover with every frame.
    private void OnAnimationFrame(object? sender, AvaloniaFrameEventArgs e)
    {
        DataContext.RefreshIfDue(Environment.TickCount64);
        UpdateHover(NativeOverlayWindowStyles.TryGetCursorPosition(out var cursor) ? this.PointToClient(cursor) : null);
    }

    // Where the cursor is (in the frame; null: unknown), looked up rather than heard of, as the frame lets the mouse
    // through while the game has focus: the second under it marked in every graph, its values in the card beside it.
    internal void UpdateHover(Point? cursor)
    {
        // Graphs that left the tree aren't held until the next hover change looks the list up again.
        if (_graphsVersion != FightGraph.TreeVersion)
            _graphs.Clear();

        FightGraph? graph = null;
        var second = -1;
        var point = cursor ?? default;
        if (cursor is not null && new Rect(Bounds.Size).Contains(point) && this.InputHitTest(point) is Visual hit &&
            hit.GetSelfAndVisualAncestors().OfType<FightGraph>().FirstOrDefault() is { } hovered &&
            this.TranslatePoint(point, hovered) is { } inGraph)
        {
            second = hovered.SecondAt(inGraph);
            graph = second >= 0 ? hovered : null;
        }

        if (!ReferenceEquals(graph, _hover.Graph) || second != _hover.Second || !ReferenceEquals(graph?.Model, _hover.Model))
        {
            _hover = (graph, second, graph?.Model);
            if (_graphsVersion != FightGraph.TreeVersion)
                (_graphs, _graphsVersion) = ([.. this.GetVisualDescendants().OfType<FightGraph>()], FightGraph.TreeVersion);
            foreach (var each in _graphs)
                each.HoverSecond = second;
            HoverLines.ItemsSource = graph?.Describe(second);
            HoverCard.IsVisible = graph is not null;
        }

        if (graph is null)
            return;

        // Right of the cursor, or left of it near the frame's right edge; kept inside the frame.
        HoverCard.Measure(Size.Infinity);
        var size = HoverCard.DesiredSize;
        var x = point.X + 16 + size.Width <= Bounds.Width ? point.X + 16 : point.X - 16 - size.Width;
        Canvas.SetLeft(HoverCard, Math.Max(0, x));
        Canvas.SetTop(HoverCard, Math.Clamp(point.Y + 12, 0, Math.Max(0, Bounds.Height - size.Height)));
    }

    // The gear: the settings at the frame's tab.
    private void SettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SettingsWindow.Open(DataContext.Settings, SettingsTab.FightGraphs, _uiScale);

    private void EyeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => DataContext.Settings.ToggleMinimized(SettingsFlyoutViewModel.FightGraphsKey);

    private void CloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => DataContext.Settings.FightGraphsEnabled = false;

    // The frame takes its scale as saved, i.e. once the slider is let go.
    private void OnSavedSettingsChanged(object? sender, EventArgs e) => _uiScale.RefreshWindow(this);

    private void SchedulePersistPosition()
    {
        _persistPositionTimer.Stop();
        _persistPositionTimer.Start();
    }

    // Not once it's closed (see GroupFrameWindow.PersistPosition).
    private void PersistPosition()
    {
        if (PlatformImpl is null)
            return;

        var corner = new System.Drawing.Point(Position.X, Position.Y);
        _settingsService.Update(settings => settings.FightGraphsPosition = corner);
    }

}
