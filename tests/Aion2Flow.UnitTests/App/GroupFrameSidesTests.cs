using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Views;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class GroupFrameSidesTests
{
    // A row left to right: outwards from the HP bar the distance, the arrow and the auras, each on its own side or off
    // ("first" and "last" are the member's first and last aura, mirrored on the left). Whatever the sides, the HP bars
    // line up whatever number of auras each row has, and stay where they are on screen as auras come and go: with the
    // auras on the left the frame grows leftwards, keeping its top right corner where it was saved.
    [Theory]
    [InlineData(GroupFrameSide.Right, GroupFrameSide.Right, GroupFrameSide.Right, "hp distance arrow first last")]
    [InlineData(GroupFrameSide.Left, GroupFrameSide.Left, GroupFrameSide.Left, "last first arrow distance hp")]
    [InlineData(GroupFrameSide.Left, GroupFrameSide.Right, GroupFrameSide.Right, "last first hp distance arrow")]
    [InlineData(GroupFrameSide.Right, GroupFrameSide.Left, GroupFrameSide.Right, "arrow hp distance first last")]
    [InlineData(GroupFrameSide.Off, GroupFrameSide.Right, GroupFrameSide.Left, "distance hp arrow")]
    [InlineData(GroupFrameSide.Left, GroupFrameSide.Off, GroupFrameSide.Off, "last first hp")]
    public void Each_Part_Goes_On_Its_Own_Side(GroupFrameSide auras, GroupFrameSide arrow, GroupFrameSide distance, string order)
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-groupframe-{Guid.NewGuid():N}.json"));
            var corner = new System.Drawing.Point(1_500, 400);
            settingsService.Update(value => value.GroupFramePosition = corner);
            var language = new LanguageService();
            using var uiScale = new UiScaleService(settingsService);
            var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService())
            {
                GroupFrameAuraSide = auras,
                GroupFrameArrowSide = arrow,
                GroupFrameDistanceSide = distance,
            };
            var viewModel = new GroupFrameViewModel(new WinDivertCaptureService(new ProcessPortDiscoveryService()), new GameResourceService(language), localization, settings);
            var frameClock = new AvaloniaFrameClockService();
            var window = new GroupFrameWindow(frameClock, localization, uiScale, settingsService) { DataContext = viewModel };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                // Without capture every frame empties the rows: none from here on.
                frameClock.Dispose();
                var three = Row(1, 3);
                var none = Row(2, 0);
                viewModel.Members.Clear();
                viewModel.Members.Add(three);
                viewModel.Members.Add(none);
                Dispatcher.UIThread.RunJobs();

                var parts = Parts(window, three);
                Assert.Equal(order, string.Join(' ', parts.OrderBy(part => X(window, part.Value)).Select(part => part.Key)));
                var hp = parts["hp"];
                Assert.Equal(X(window, hp), X(window, Parts(window, none)["hp"]));
                Assert.Equal(corner.X, auras == GroupFrameSide.Left ? window.Position.X + (int)Math.Round(window.Bounds.Width * window.DesktopScaling) : window.Position.X);

                var hpOnScreen = ScreenX(window, hp);
                var width = window.Bounds.Width;
                for (var i = 0; i < 5; i++)
                    none.MyAuras.Add(Icon());
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(auras != GroupFrameSide.Off, window.Bounds.Width > width);
                Assert.InRange(ScreenX(window, hp), hpOnScreen - 1, hpOnScreen + 1);

                none.MyAuras.Clear();
                Dispatcher.UIThread.RunJobs();
                Assert.InRange(ScreenX(window, hp), hpOnScreen - 1, hpOnScreen + 1);

                window.Close();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(corner, settingsService.Current.GroupFramePosition);
            }
            finally
            {
                if (window.IsVisible)
                    window.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
            }
        });
    }

    // Each slider of the frame's "Opacity" submenu fades just its part, 100% being the part's own look; out of range
    // still dims the HP bar on top of it, and the value is kept with the settings.
    [Fact]
    public void Each_Part_Fades_As_Its_Slider_Says()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var icons = new IconGeometries();
            Application.Current!.Resources.MergedDictionaries.Add(icons);
            var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-groupframe-{Guid.NewGuid():N}.json"));
            var language = new LanguageService();
            using var uiScale = new UiScaleService(settingsService);
            var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService())
            {
                GroupFrameHpOpacityPercent = 50,
                GroupFrameAuraOpacityPercent = 60,
                GroupFrameArrowOpacityPercent = 70,
                GroupFrameDistanceOpacityPercent = 80,
            };
            var viewModel = new GroupFrameViewModel(new WinDivertCaptureService(new ProcessPortDiscoveryService()), new GameResourceService(language), localization, settings);
            var frameClock = new AvaloniaFrameClockService();
            var window = new GroupFrameWindow(frameClock, localization, uiScale, settingsService) { DataContext = viewModel };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                frameClock.Dispose();
                var row = Row(1, 1);
                viewModel.Members.Clear();
                viewModel.Members.Add(row);
                Dispatcher.UIThread.RunJobs();

                var parts = Parts(window, row);
                var hp = (Panel)parts["hp"];
                var bar = hp.Children[0];
                var auras = ((Grid)hp.Parent!).Children.OfType<ReversibleStackPanel>().Single();
                var arrow = ((Panel)parts["arrow"]).Children[0];
                var distance = ((Panel)parts["distance"]).Children[0];
                Assert.Equal(new[] { 1, 0.5, 0.6, 0.7, 0.8 }, new[] { hp.Opacity, bar.Opacity, auras.Opacity, arrow.Opacity, distance.Opacity });

                var menu = new MenuItem();
                _ = new GroupFrameSettingsMenu(settings, menu.Items);
                var sliders = ((MenuItem)menu.Items[^1]!).Items.Cast<MenuItem>().Select(item => ((Grid)item.Header!).Children.OfType<Slider>().Single()).ToList();
                Assert.Equal(new[] { 50d, 60, 70, 80 }, sliders.Select(slider => slider.Value));
                sliders[0].Value = 30;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(30, settings.GroupFrameHpOpacityPercent);
                Assert.Equal(0.3, bar.Opacity);

                row.IsOutOfRange = true;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal((0.45, 0.3), (hp.Opacity, bar.Opacity));

                settings.SaveOverlayOpacity();
                Assert.Equal(30, new SettingsService(settingsService.FilePath).Current.GroupFrameHpOpacityPercent);
            }
            finally
            {
                if (window.IsVisible)
                    window.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Resources.MergedDictionaries.Remove(icons);
            }
        });
    }

    private static GroupFrameMemberRow Row(int entityId, int auras)
    {
        var row = new GroupFrameMemberRow(entityId) { Name = "Member" };
        for (var i = 0; i < auras; i++)
            row.MyAuras.Add(Icon());
        return row;
    }

    private static GroupFrameAuraIcon Icon() => new(1, null, "Aura", "A", GroupFrameAuraKind.Mine);

    // The parts of a member's row that show, by name.
    private static Dictionary<string, Control> Parts(Window window, GroupFrameMemberRow row)
    {
        var grid = window.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Classes.Contains("GroupFrameRow") && ReferenceEquals(grid.DataContext, row));
        var mine = grid.GetVisualDescendants().OfType<ItemsControl>().Single(items => ReferenceEquals(items.ItemsSource, row.MyAuras));
        var parts = new Dictionary<string, Control?>
        {
            ["hp"] = grid.Children[0],
            ["distance"] = grid.Children.Single(child => child.Classes.Contains("GroupFrameDistanceSlot")),
            ["arrow"] = grid.Children.Single(child => child.Classes.Contains("GroupFrameArrowSlot")),
            ["first"] = row.MyAuras.Count > 0 ? mine.ContainerFromIndex(0) : null,
            ["last"] = row.MyAuras.Count > 0 ? mine.ContainerFromIndex(row.MyAuras.Count - 1) : null,
        };
        return parts.Where(part => part.Value is { IsEffectivelyVisible: true }).ToDictionary(part => part.Key, part => part.Value!);
    }

    private static double X(Window window, Control part) => part.TranslatePoint(default, window)!.Value.X;

    private static int ScreenX(Window window, Control part) => window.PointToScreen(part.TranslatePoint(default, window)!.Value).X;
}
