using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Cloris.Aion2Flow.Controls;

// An arrowhead with a raised spine, drawn in perspective as if it hovered in front of you, seen from behind and above as
// the game's camera sees your character. Yaw turns it (degrees clockwise, 0 = straight ahead), Pitch tips its point up (+)
// or down (-). Lit from above left, so its sides shade apart; a light rim keeps it readable on any ground. Nothing is
// drawn while Yaw is NaN (no way ahead known).
public sealed class Arrow3D : Control
{
    public static readonly StyledProperty<double> YawProperty = AvaloniaProperty.Register<Arrow3D, double>(nameof(Yaw), double.NaN);

    public static readonly StyledProperty<double> PitchProperty = AvaloniaProperty.Register<Arrow3D, double>(nameof(Pitch));

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<Arrow3D, IBrush?>(nameof(Fill));

    // The camera looks down this steeply, from this many arrow half-lengths away (nearer: stronger perspective). An arrow
    // pointing along the camera's line shrinks to a dot, so its point tips up or down no more than MaxPitchDegrees: at
    // least 30° off that line, it still reads as an arrow (steeper is up or down all the same, and the colour says which).
    private const float CameraTiltDegrees = 60;
    private const float CameraDistance = 4;
    private const double MaxPitchDegrees = 30;
    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-0.4f, -0.5f, 1));
    private static readonly IPen Rim = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#D0FFFFFF")), 2, lineJoin: PenLineJoin.Round);

    // x right, y ahead, z up, about one unit each way from the middle: the tip, the right and left barb, the notch at the
    // top of the spine and below it.
    private static readonly Vector3[] Corners = [new(0, 1, 0), new(0.62f, -0.85f, 0), new(-0.62f, -0.85f, 0), new(0, -0.42f, 0.3f), new(0, -0.42f, 0)];

    // Counter-clockwise seen from outside: the two sides of the spine, the two walls of the notch, the underside.
    private static readonly int[][] Faces = [[0, 3, 1], [0, 2, 3], [1, 3, 4], [2, 4, 3], [0, 1, 4, 2]];

    static Arrow3D()
    {
        AffectsRender<Arrow3D>(YawProperty, PitchProperty, FillProperty);
    }

    public double Yaw
    {
        get => GetValue(YawProperty);
        set => SetValue(YawProperty, value);
    }

    public double Pitch
    {
        get => GetValue(PitchProperty);
        set => SetValue(PitchProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Fill is not ISolidColorBrush fill)
            return;

        var faces = Project(Yaw, Pitch, Bounds.Size, fill.Color);
        var shapes = faces.Select(static face => new PolylineGeometry(face.Points, isFilled: true)).ToArray();
        // The rim first: the faces cover all of it but its outer half along the outline.
        foreach (var shape in shapes)
            context.DrawGeometry(null, Rim, shape);
        for (var i = 0; i < shapes.Length; i++)
        {
            var brush = new ImmutableSolidColorBrush(faces[i].Color);
            context.DrawGeometry(brush, new ImmutablePen(brush, 0.6), shapes[i]);
        }
    }

    // The faces turned towards the camera as points in a box of that size, the farthest first, each lit by how much it
    // faces the light.
    internal static List<(Point[] Points, Color Color)> Project(double yawDegrees, double pitchDegrees, Size size, Color color)
    {
        var faces = new List<(Point[] Points, Color Color, float Depth)>();
        if (!double.IsFinite(yawDegrees) || size.Width <= 0 || size.Height <= 0)
            return [];

        var (sinYaw, cosYaw) = MathF.SinCos((float)(yawDegrees * Math.PI / 180));
        var (sinPitch, cosPitch) = MathF.SinCos((float)(Math.Clamp(double.IsFinite(pitchDegrees) ? pitchDegrees : 0, -MaxPitchDegrees, MaxPitchDegrees) * Math.PI / 180));
        var (sinTilt, cosTilt) = MathF.SinCos(CameraTiltDegrees * MathF.PI / 180);
        var camera = new Vector3(0, -cosTilt, sinTilt) * CameraDistance;
        var ahead = new Vector3(0, cosTilt, -sinTilt);
        var up = new Vector3(0, sinTilt, cosTilt);
        // A unit as large as fits the box with the nearest end magnified by the perspective.
        var scale = (float)Math.Min(size.Width, size.Height) / 2 * (CameraDistance - 1) / CameraDistance * 0.92f;
        var middle = new Vector2((float)size.Width / 2, (float)size.Height / 2);

        // Tipped up about the crosswise axis, then turned clockwise about the upright one.
        var world = new Vector3[Corners.Length];
        for (var i = 0; i < Corners.Length; i++)
        {
            var corner = Corners[i];
            var y = corner.Y * cosPitch - corner.Z * sinPitch;
            var z = corner.Y * sinPitch + corner.Z * cosPitch;
            world[i] = new Vector3(corner.X * cosYaw + y * sinYaw, y * cosYaw - corner.X * sinYaw, z);
        }

        foreach (var face in Faces)
        {
            // Newell's normal: outwards for corners counter-clockwise from outside, also on the notched underside.
            var normal = Vector3.Zero;
            var center = Vector3.Zero;
            for (var j = 0; j < face.Length; j++)
            {
                var (a, b) = (world[face[j]], world[face[(j + 1) % face.Length]]);
                normal += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
                center += a / face.Length;
            }

            normal = Vector3.Normalize(normal);
            if (Vector3.Dot(normal, camera - center) <= 0)
                continue;

            var points = new Point[face.Length];
            for (var j = 0; j < face.Length; j++)
            {
                var relative = world[face[j]] - camera;
                var perspective = scale * CameraDistance / Vector3.Dot(relative, ahead);
                points[j] = new Point(middle.X + relative.X * perspective, middle.Y - Vector3.Dot(relative, up) * perspective);
            }

            var light = 0.6f + 0.6f * MathF.Max(0, Vector3.Dot(normal, Light));
            faces.Add((points, Color.FromArgb(color.A, Lit(color.R, light), Lit(color.G, light), Lit(color.B, light)), Vector3.Dot(center - camera, ahead)));
        }

        faces.Sort(static (left, right) => right.Depth.CompareTo(left.Depth));
        return [.. faces.Select(static face => (face.Points, face.Color))];
    }

    private static byte Lit(byte channel, float light) => (byte)Math.Clamp(channel * light, 0, 255);
}
