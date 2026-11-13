using System.Numerics;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Cloris.Aion2Flow.Capture.Streams;

namespace Cloris.Aion2Flow.ViewModels;

// Where you are and which way you're heading, for the arrows to party members (group frame) and Hidden Cubes. The game
// doesn't say where you or your camera face, so the heading is the way you went from the last place 3 m or more behind
// you (your positions come about once a second while you move, see PlayerPositions), kept while you stand. A jump faster
// than anyone moves (a teleport, the next map) leaves it unknown until you move again.
internal sealed class OwnHeading
{
    private const double HeadingMinMeters = 3;
    private const double JumpMetersPerSecond = 60;
    private const double SameLevelMeters = 3;
    // Ours comes only from the auras we cast. Up to 2 s old it's where we are; on the move (7-25 m/s) an older one is soon
    // 15 m and more off, so up to 10 s it's a guess (arrow and distance faded), then nothing: no arrow, no range.
    private const long OwnPositionFreshMilliseconds = 2_000;
    private const long OwnPositionGuessMilliseconds = 10_000;
    private static readonly IBrush AboveBrush = new ImmutableSolidColorBrush(Color.Parse("#2E8B3A"));
    private static readonly IBrush BelowBrush = new ImmutableSolidColorBrush(Color.Parse("#B3261E"));
    private static readonly IBrush LevelBrush = new ImmutableSolidColorBrush(Color.Parse("#1F5FBF"));
    // Degrees clockwise from north (null until you've moved), where you were when it was taken, and your last position.
    private double? _heading;
    private Vector3 _from;
    private PlayerPositions.Fix? _last;

    // Your position as of now (a guess when older than 2 s, none past 10 s); takes the heading from it as it goes.
    public (Vector3? Me, bool Guessed) Locate(int self, long now)
    {
        if (self > 0 && PlayerPositions.TryGet(self, out var fix))
            Track(fix);
        return (Fresh(self, now, OwnPositionGuessMilliseconds, holdInSight: false), Fresh(self, now, OwnPositionFreshMilliseconds, holdInSight: false) is null);
    }

    public void Track(PlayerPositions.Fix fix)
    {
        if (fix == _last)
            return;

        var last = _last;
        _last = fix;
        if (last is not { } previous
            || Vector3.Distance(previous.Position, fix.Position) / 100 > JumpMetersPerSecond * Math.Max(1, (fix.AtMilliseconds - previous.AtMilliseconds) / 1_000d))
        {
            (_heading, _from) = (null, fix.Position);
            return;
        }

        var (meters, bearing, rise) = PlayerPositions.Between(_from, fix.Position);
        if (meters * meters - rise * rise >= HeadingMinMeters * HeadingMinMeters)
            (_heading, _from) = (bearing, fix.Position);
    }

    // The 3D arrow (Arrow3D) to someone or something: as seen facing the way you're heading (none until you've moved), tipped
    // up or down as steeply as it lies above or below you, green above, red below, blue about level.
    public void Point(GroupFrameMemberRow row, double meters, double bearing, double rise)
    {
        row.ArrowYaw = _heading is { } heading ? (bearing - heading + 360) % 360 : double.NaN;
        row.ArrowPitch = Math.Atan2(rise, Math.Sqrt(Math.Max(0, meters * meters - rise * rise))) * 180 / Math.PI;
        row.ArrowBrush = rise > SameLevelMeters ? AboveBrush : rise < -SameLevelMeters ? BelowBrush : LevelBrush;
    }

    // Ours never holds just for being "in sight": the server only now and then sends our own moves, never all of them.
    public static Vector3? Fresh(int entityId, long now, long staleAfter, bool holdInSight) =>
        entityId > 0 && PlayerPositions.TryGet(entityId, out var fix) && (holdInSight && fix.InSight || now - fix.AtMilliseconds <= staleAfter) ? fix.Position : null;
}
