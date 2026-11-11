using System.Numerics;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Streams;
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

    // Nearest first, with distance and direction (north up); past 250 m a cube is dropped; without your own position just
    // the lines; off, none.
    [Fact]
    public void Cube_Lines_Show_The_Nearest_First()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, _) = ViewTestServices.Get();
            var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-cubes-{Guid.NewGuid():N}.json"));
            var language = new LanguageService();
            using var uiScale = new UiScaleService(settingsService);
            var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization), uiScale,
                new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
            var viewModel = new GroupFrameViewModel(new WinDivertCaptureService(new ProcessPortDiscoveryService()), new GameResourceService(language), localization, settings);
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
                Assert.Equal([90d, 180d], viewModel.Cubes.Select(static row => Math.Round(row.ArrowRotation.Angle)));
                Assert.All(viewModel.Cubes, row => Assert.Equal(localization["GroupFrame_HiddenCube"], row.Name));

                viewModel.UpdateCubes(null, false, 2_000);
                Assert.Equal(3, viewModel.Cubes.Count);
                Assert.All(viewModel.Cubes, static row => Assert.False(row.HasArrow));

                settings.GroupFrameCubeFinder = GroupFrameCubeFinder.Off;
                viewModel.UpdateCubes(me, false, 2_000);
                Assert.Empty(viewModel.Cubes);
            }
            finally
            {
                HiddenCubes.Clear();
            }
        });
    }
}
