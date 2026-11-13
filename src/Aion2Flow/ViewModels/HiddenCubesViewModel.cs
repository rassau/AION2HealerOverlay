using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Capture.Streams;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Audio;

namespace Cloris.Aion2Flow.ViewModels;

// The Hidden Cube finder (experimental), a frame of its own: a line per cube the server announced (HiddenCubes), nearest
// first, with the distance in gold and the arrow like a group frame member's (see OwnHeading); without your own position
// just the line, as the cube is within ~170 m. With the sound on, a cube new to this session plays the blip once.
public sealed class HiddenCubesViewModel(WinDivertCaptureService captureService, LocalizationService localization, SettingsFlyoutViewModel settings)
{
    // Cubes are dropped past this distance (the server announces them within ~170 m) or, while your own position isn't
    // known, once not announced again for this long.
    private const double CubeDropMeters = 250;
    private const long CubeStaleMilliseconds = 600_000;
    internal static readonly IBrush CubeBrush = new ImmutableSolidColorBrush(Color.Parse("#E6C65A"));
    private readonly OwnHeading _heading = new();
    private readonly List<HiddenCubes.Cube> _cubes = [];
    private readonly List<(HiddenCubes.Cube Cube, double Meters)> _shown = [];
    private readonly HashSet<int> _announced = [];

    public ObservableCollection<GroupFrameMemberRow> Cubes { get; } = [];

    // The frame's own gear opens its part of these.
    public SettingsFlyoutViewModel Settings => settings;

    public void ProcessUiFrame()
    {
        if (!captureService.IsDriverActive)
        {
            Cubes.Clear();
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (me, guessed) = _heading.Locate(captureService.Scene.Owner.MetadataRegistry.LocalPlayerEntityId, now);
        UpdateCubes(me, guessed, now);
    }

    internal void TrackHeading(PlayerPositions.Fix fix) => _heading.Track(fix);

    internal void UpdateCubes(Vector3? me, bool guessed, long now)
    {
        HiddenCubes.CopyTo(_cubes);
        _shown.Clear();
        if (settings.HiddenCubesEnabled)
        {
            foreach (var cube in _cubes)
            {
                var meters = me is { } mine ? PlayerPositions.Between(mine, cube.Position).Meters : (double?)null;
                if (meters > CubeDropMeters || meters is null && now - cube.SeenAtMilliseconds > CubeStaleMilliseconds)
                    continue;
                _shown.Add((cube, meters ?? double.MaxValue));
            }
        }

        _shown.Sort(static (left, right) => left.Meters.CompareTo(right.Meters));
        if (!Cubes.Select(static row => row.EntityId).SequenceEqual(_shown.Select(static shown => shown.Cube.EntityId)))
        {
            Cubes.Clear();
            foreach (var (cube, _) in _shown)
                Cubes.Add(new GroupFrameMemberRow(cube.EntityId) { Name = localization["GroupFrame_HiddenCube"] });
        }

        var sound = false;
        for (var i = 0; i < Cubes.Count; i++)
        {
            var row = Cubes[i];
            sound |= _announced.Add(row.EntityId) && settings.HiddenCubesSound;
            if (me is not { } mine)
            {
                row.HasArrow = false;
                continue;
            }

            var (meters, bearing, rise) = PlayerPositions.Between(mine, _shown[i].Cube.Position);
            row.HasArrow = true;
            row.IsPlacementGuessed = guessed;
            row.DistanceText = string.Create(CultureInfo.InvariantCulture, $"{(int)Math.Round(meters)} m");
            row.DistanceBrush = CubeBrush;
            _heading.Point(row, meters, bearing, rise);
        }

        if (sound)
            settings.AlertSounds.Play(AlertSounds.BlipName, settings.GroupFrameAlertVolumePercent);
    }
}
