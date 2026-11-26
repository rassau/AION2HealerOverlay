using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Views;
using SkiaSharp;

namespace Cloris.Aion2Flow.Tests.App;

// The fight graphs' hover: a line at the hovered second with a dot on each curve, on top of the graph, in every graph of
// the frame.
[Collection(AvaloniaTestCollection.Name)]
public sealed class FightGraphHoverTests
{
    private const int Width = 400;
    private const int Height = 120;

    // The line and its dots change only the columns around the hovered second, and leave no trace once the hover is gone;
    // a second the model doesn't have draws nothing.
    [Fact]
    public void The_Hover_Line_Is_Drawn_On_Top_Of_The_Graph_At_Its_Second()
    {
        AvaloniaTestHost.Run(() =>
        {
            var graph = new FightGraph { Model = Model(60) };
            graph.Measure(new Size(Width, Height));
            graph.Arrange(new Rect(0, 0, Width, Height));
            var plain = Pixels(graph);

            graph.HoverSecond = 19;
            var hovered = Pixels(graph);
            // As FightGraph.SecondAt reads it: 42 px of axis on the left, 8 on the right, second 19 drawn at 20 s of 60.
            var x = 42 + 20d / 60 * (Width - 42 - 8);
            var changed = Enumerable.Range(0, Width).Where(column => Enumerable.Range(0, Height).Any(row => plain[row * Width + column] != hovered[row * Width + column])).ToList();
            Assert.NotEmpty(changed);
            Assert.All(changed, column => Assert.InRange(column, x - 6, x + 6));

            graph.HoverSecond = -1;
            Assert.Equal(plain, Pixels(graph));

            graph.HoverSecond = 45;
            graph.Model = Model(30);
            Assert.Equal(Pixels(new FightGraph { Model = graph.Model }), Pixels(graph));
        });
    }

    // The same on screen, i.e. in the compositor's picture (RenderTargetBitmap records every visual anew): the line follows
    // the hover, leaves no trace, and follows a model shown under it.
    [Fact]
    public void The_Screen_Shows_The_Hover_Line_Where_It_Is_Now()
    {
        AvaloniaTestHost.Run(() =>
        {
            var graph = new FightGraph { Model = Model(60) };
            var window = new Window { Width = Width, Height = Height, Content = graph };
            try
            {
                window.Show();
                var plain = Snapshot(window);
                graph.HoverSecond = 19;
                var hovered = Snapshot(window);
                Assert.NotEqual(plain, hovered);
                graph.HoverSecond = -1;
                Assert.Equal(plain, Snapshot(window));

                graph.HoverSecond = 19;
                Assert.Equal(hovered, Snapshot(window));
                graph.Model = Model(90);
                var moved = Snapshot(window);
                window.Content = new FightGraph { Model = Model(90), HoverSecond = 19 };
                Assert.Equal(Snapshot(window), moved);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    // Every graph of the frame marks the hovered second: the overview, the metrics', and a comparison's made after the
    // first hover; off the graphs every one lets go. Graphs that leave the frame aren't held until the next hover change.
    [Fact]
    public void The_Hover_Marks_Every_Graph_Of_The_Frame_Also_Ones_Added_Later()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-hover-{Guid.NewGuid():N}.json"));
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
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                // The window follows the real cursor on each frame; here the test moves the hover.
                frameClock.Dispose();
                var model = Model(60);
                vm.HasFight = true;
                vm.OverviewModel = model;
                foreach (var graph in vm.Graphs)
                    graph.Model = model;
                NextFrame(window);

                var graphs = window.GetVisualDescendants().OfType<FightGraph>().ToList();
                var hovered = graphs.First(static graph => graph.IsEffectivelyVisible);
                Point At(int second) => hovered.TranslatePoint(new Point(42 + (second + 1d) / 60 * (hovered.Bounds.Width - 42 - 8), hovered.Bounds.Height / 2), window)!.Value;
                HoverAt(window, hovered, At(20), 20);
                Assert.Equal(8, graphs.Count);
                Assert.All(graphs, static graph => Assert.Equal(20, graph.HoverSecond));
                Assert.True(window.HoverCard.IsVisible);

                vm.HasComparison = true;
                vm.Tab = FightGraphsTab.Compare;
                foreach (var graph in vm.Graphs)
                    vm.CompareMetrics.Add(new FightCompareMetric(graph, [new FightCompareGraph { Title = "Run", Model = model }]));
                Dispatcher.UIThread.RunJobs();
                vm.Tab = FightGraphsTab.Fight;
                NextFrame(window);
                var all = window.GetVisualDescendants().OfType<FightGraph>().ToList();
                Assert.Equal(15, all.Count);
                HoverAt(window, hovered, At(30), 30);
                Assert.All(all, static graph => Assert.Equal(30, graph.HoverSecond));

                // The comparison's graphs leave while the hover stays off the graphs (no hover change): none of them is held.
                window.UpdateHover(null);
                vm.CompareMetrics.Clear();
                NextFrame(window);
                window.UpdateHover(null);
                var held = (List<FightGraph>)typeof(FightGraphsWindow).GetField("_graphs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
                Assert.All(held, graph => Assert.Contains(graph, graphs));
                HoverAt(window, hovered, At(40), 40);
                Assert.All(graphs, static graph => Assert.Equal(40, graph.HoverSecond));
                window.UpdateHover(null);
                Assert.All(graphs, static graph => Assert.Equal(-1, graph.HoverSecond));
                Assert.False(window.HoverCard.IsVisible);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
            }
        });
    }

    // Hovering anywhere on a death's cross lists that death: Abcd at 1.5 s and the user at 2 s of a 4 s pull (the user's
    // cross got the card of the second before, with only Abcd's death), and one mid-pull of a 5-minute one, where a cross
    // spans seconds.
    [Fact]
    public void Hovering_Over_A_Cross_Names_Who_Died_There()
    {
        AvaloniaTestHost.Run(() =>
        {
            foreach (var seconds in (int[])[4, 300])
            {
                FightGraphMark[] marks = [new(1.5, Colors.SlateGray, "Abcd died"), new(2, Colors.White, "You died"), new(seconds / 2d + 0.3, Colors.SlateGray, "Efgh died")];
                var graph = new FightGraph { Model = new FightGraphModel(seconds, [], marks) };
                graph.Measure(new Size(680, 150));
                graph.Arrange(new Rect(0, 0, 680, 150));
                foreach (var mark in marks)
                {
                    // The plot runs from 42 to 672 px; the cross's bar reaches 3.5 px either side.
                    var x = 42 + mark.AtSeconds / Math.Max(seconds, 30) * 630;
                    for (var dx = -3; dx <= 3; dx++)
                    {
                        if (graph.SecondAt(new Point(x + dx, 75)) is var second and >= 0)
                            Assert.Contains(graph.Describe(second), line => line.Text == "† " + mark.Label);
                    }
                }
            }
        });
    }

    // The window's hit test finds a graph where the render thread has put it: frames until that has caught up.
    private static void HoverAt(FightGraphsWindow window, FightGraph graph, Point point, int second)
    {
        var until = Stopwatch.StartNew();
        do
        {
            NextFrame(window);
            window.UpdateHover(point);
        }
        while (graph.HoverSecond != second && until.Elapsed < TimeSpan.FromSeconds(10));
    }

    // The window as the compositor (render thread) holds it, a frame after the last change.
    private static byte[] Snapshot(Window window)
    {
        NextFrame(window);
        var visual = ElementComposition.GetElementVisual(window)!;
        var task = visual.Compositor.CreateCompositionVisualSnapshot(visual, 1);
        var until = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            Assert.True(until.Elapsed < TimeSpan.FromSeconds(10), "no snapshot");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }

        using var bitmap = task.Result;
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    // A frame laid out and drawn.
    private static void NextFrame(Window window)
    {
        var drawn = false;
        window.RequestAnimationFrame(_ => drawn = true);
        var until = Stopwatch.StartNew();
        while (!drawn)
        {
            Assert.True(until.Elapsed < TimeSpan.FromSeconds(10), "no frame was drawn");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
    }

    // The user's area, the party's line and a death, over the given seconds.
    private static FightGraphModel Model(int seconds) => new(seconds,
    [
        new FightGraphSeries("Party", [.. Enumerable.Range(0, seconds).Select(static second => 2_000 + 900 * Math.Sin(second / 5d))], Colors.White, FightGraphStyle.Line, FightGraph.Compact),
        new FightGraphSeries("You", [.. Enumerable.Range(0, seconds).Select(static second => 1_000 + 500 * Math.Cos(second / 7d))], Colors.Teal, FightGraphStyle.Area, FightGraph.Compact),
    ], [new FightGraphMark(seconds / 3d, Colors.Red, "Healer")]);

    private static uint[] Pixels(FightGraph graph)
    {
        if (graph.Bounds.Width == 0)
        {
            graph.Measure(new Size(Width, Height));
            graph.Arrange(new Rect(0, 0, Width, Height));
        }

        using var target = new RenderTargetBitmap(new PixelSize(Width, Height));
        target.Render(graph);
        using var stream = new MemoryStream();
        target.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        using var bitmap = SKBitmap.Decode(stream);
        return [.. bitmap.Pixels.Select(static color => (uint)color)];
    }
}
