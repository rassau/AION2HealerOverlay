using System.Numerics;
using Avalonia;
using Avalonia.Media;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.Tests.Capture;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class HiddenCubeFinderTests
{
    // The cube from the 11 Nov 2026 recording: announced (0x3436) about 172 m away, opened, then its state (0x3536) went to
    // 04 03 00 and 0x058D took it away.
    private static readonly byte[] Spawn = TransportTestData.BuildFrame(0x34, 0x36, Convert.FromHexString(
        "fa ff 01 04 00 cf da 13 00 6c 0f a5 47 74 36 39 48 00 6c 51 46 00 00 00 00 00 50 20 43 01 01 01 7b 44 29 cc a1 01 00 00 00 00 00 00 00 00 00 00 30 9f 28 93 07 00 00".Replace(" ", "")));
    private static readonly byte[] GoneState = TransportTestData.BuildFrame(0x35, 0x36, [0xfa, 0xff, 0x01, 0x04, 0x03, 0x00]);
    private static readonly byte[] Removed = TransportTestData.BuildFrame(0x05, 0x8d, [0xfa, 0xff, 0x01]);

    [Fact]
    public void A_Cube_Is_Known_From_Its_Spawn_Until_It_Is_Used_Up()
    {
        Assert.True(PacketObjectHandler.TryReadSpawn(Spawn, out var entityId, out var code, out var position));
        Assert.Equal((32_762, 1_301_199), (entityId, code));
        Assert.Equal(new Vector3(84_510.84375f, 189_657.8125f, 13_403f), position);

        var cubes = new List<HiddenCubes.Cube>();
        HiddenCubes.Saw(entityId, code, position, 1_000);
        HiddenCubes.Saw(900_009, 1_234_567, position, 1_000);
        HiddenCubes.CopyTo(cubes);
        Assert.Contains(cubes, static cube => cube.EntityId == 32_762);
        Assert.DoesNotContain(cubes, static cube => cube.EntityId == 900_009);

        Assert.True(PacketObjectHandler.TryReadState(GoneState, out var stateId, out var state));
        Assert.Equal((32_762, (byte)4), (stateId, state));
        Assert.True(PacketObjectHandler.TryReadRemoved(Removed, out var removedId));
        Assert.Equal(32_762, removedId);
        Assert.False(PacketObjectHandler.TryReadRemoved(GoneState, out _));

        HiddenCubes.Remove(removedId);
        HiddenCubes.CopyTo(cubes);
        Assert.DoesNotContain(cubes, static cube => cube.EntityId == 32_762);
    }

    // Nearest first, with the distance; past 250 m a cube is dropped; without your own position just the lines; off, none.
    [Fact]
    public void Cube_Lines_Show_The_Nearest_First()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var (viewModel, settings, uiScale) = CreateViewModel(localization);
            using var _ = uiScale;
            var me = new Vector3(50_000, 50_000, 1_000);
            HiddenCubes.Clear();
            try
            {
                HiddenCubes.Saw(900_001, 1_301_199, me + new Vector3(0, 17_200, 0), 1_000);
                HiddenCubes.Saw(900_002, 1_301_199, me + new Vector3(5_000, 0, 0), 1_000);
                HiddenCubes.Saw(900_003, 1_301_199, me + new Vector3(0, -30_000, 0), 1_000);

                viewModel.UpdateCubes(me, false, 2_000);
                Assert.Equal([900_002, 900_001], viewModel.Cubes.Select(static row => row.EntityId));
                Assert.Equal(["50 m", "172 m"], viewModel.Cubes.Select(static row => row.DistanceText));
                Assert.All(viewModel.Cubes, row => Assert.Equal(localization["GroupFrame_HiddenCube"], row.Name));

                viewModel.UpdateCubes(null, false, 2_000);
                Assert.Equal(3, viewModel.Cubes.Count);
                Assert.All(viewModel.Cubes, static row => Assert.False(row.HasArrow));

                settings.HiddenCubesEnabled = false;
                viewModel.UpdateCubes(me, false, 2_000);
                Assert.Empty(viewModel.Cubes);
            }
            finally
            {
                HiddenCubes.Clear();
            }
        });
    }

    // The arrow is turned to the way you head: none until you've gone 3 m, then where the cube is from there, tipped up as
    // steeply as the cube lies above you; a jump no one can run (a teleport) forgets the way again.
    [Fact]
    public void Cube_Arrows_Point_From_The_Way_You_Head()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var (viewModel, _, uiScale) = CreateViewModel(localization);
            using var _ = uiScale;
            var start = new Vector3(50_000, 50_000, 1_000);
            var north = start - new Vector3(0, 400, 0);
            var east = north + new Vector3(1_000, 0, 0);
            HiddenCubes.Clear();
            try
            {
                HiddenCubes.Saw(900_001, 1_301_199, north + new Vector3(5_000, 0, 0), 1_000);
                HiddenCubes.Saw(900_002, 1_301_199, north + new Vector3(0, 8_000, 4_000), 1_000);

                viewModel.TrackHeading(new PlayerPositions.Fix(start, 1_000));
                viewModel.TrackHeading(new PlayerPositions.Fix(start - new Vector3(0, 200, 0), 2_000));
                viewModel.UpdateCubes(start, false, 2_000);
                Assert.All(viewModel.Cubes, static row => Assert.True(double.IsNaN(row.ArrowYaw)));

                // Heading north: the cube east of you is to your right, the other behind you and up.
                viewModel.TrackHeading(new PlayerPositions.Fix(north, 3_000));
                viewModel.UpdateCubes(north, false, 3_000);
                Assert.Equal([90d, 180d], viewModel.Cubes.Select(static row => Math.Round(row.ArrowYaw)));
                Assert.Equal([0d, 26.6], viewModel.Cubes.Select(static row => Math.Round(row.ArrowPitch, 1)));

                // Heading east: that cube is straight ahead.
                viewModel.TrackHeading(new PlayerPositions.Fix(east, 4_000));
                viewModel.UpdateCubes(east, false, 4_000);
                Assert.Equal(0, viewModel.Cubes[0].ArrowYaw, 3);

                viewModel.TrackHeading(new PlayerPositions.Fix(east + new Vector3(100_000, 0, 0), 5_000));
                viewModel.UpdateCubes(east, false, 5_000);
                Assert.All(viewModel.Cubes, static row => Assert.True(double.IsNaN(row.ArrowYaw)));
            }
            finally
            {
                HiddenCubes.Clear();
            }
        });
    }

    // Seen from behind and above: ahead points up the box, right to the right, behind down; a cube above tips the point
    // up, one below down; shaded, and always inside its box.
    [Fact]
    public void The_Cube_Arrow_Points_The_Way_It_Is_Turned()
    {
        var size = new Size(28, 24);
        Point Farthest(double yaw, double pitch, Func<Point, double> by) =>
            Arrow3D.Project(yaw, pitch, size, Colors.SteelBlue).SelectMany(static face => face.Points).MaxBy(by);

        Assert.Equal(14, Farthest(0, 0, static point => -point.Y).X, 1);
        Assert.Equal(12, Farthest(90, 0, static point => point.X).Y, 1);
        Assert.Equal(14, Farthest(180, 0, static point => point.Y).X, 1);
        Assert.True(Farthest(90, 30, static point => point.X).Y < 11);
        Assert.True(Farthest(90, -30, static point => point.X).Y > 13);
        Assert.True(Arrow3D.Project(0, 0, size, Colors.SteelBlue).Select(static face => face.Color).Distinct().Count() > 1);
        Assert.Empty(Arrow3D.Project(double.NaN, 0, size, Colors.SteelBlue));
        for (var yaw = 0; yaw < 360; yaw += 15)
        {
            foreach (var pitch in new[] { -90, -30, 0, 30, 90 })
            {
                Assert.All(Arrow3D.Project(yaw, pitch, size, Colors.SteelBlue).SelectMany(static face => face.Points),
                    point => Assert.True(point.X is >= 0 and <= 28 && point.Y is >= 0 and <= 24, $"{yaw}/{pitch}: {point}"));
            }
        }
    }

    private static (HiddenCubesViewModel ViewModel, SettingsFlyoutViewModel Settings, UiScaleService UiScale) CreateViewModel(LocalizationService localization)
    {
        var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-cubes-{Guid.NewGuid():N}.json"));
        var language = new LanguageService();
        var uiScale = new UiScaleService(settingsService);
        var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
            new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService()) { HiddenCubesEnabled = true };
        return (new HiddenCubesViewModel(new WinDivertCaptureService(new ProcessPortDiscoveryService()), localization, settings), settings, uiScale);
    }
}
