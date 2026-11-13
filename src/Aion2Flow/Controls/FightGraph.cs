using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Cloris.Aion2Flow.Controls;

public enum FightGraphStyle
{
    // The user's own: a line with the area under it filled.
    Area,
    Line,
    Dotted,
}

// One curve of a fight graph: a value per second of the fight. The value of second i (what happened between i and i + 1 s)
// is drawn at i + 1 s; NaN leaves a gap.
public sealed record FightGraphSeries(string Label, double[] Values, Color Color, FightGraphStyle Style, Func<double, string> Format)
{
    // Its own scale on the right (the ping).
    public bool RightAxis { get; init; }

    // Which skills the user's values came from, per second, biggest first: the hover lists the first few.
    public (string Skill, double Amount)[][]? Sources { get; init; }
}

// A death: a grave cross at that moment.
public sealed record FightGraphMark(double AtSeconds, Color Color, string Label);

public sealed record FightGraphModel(int Seconds, IReadOnlyList<FightGraphSeries> Series, IReadOnlyList<FightGraphMark> Marks)
{
    // The overview: each curve scaled to its own peak (0-100 %), as they measure different things.
    public bool Normalized { get; init; }

    // "Smooth curves": each curve drawn as an average of the seconds around it (see FightGraph.Smoothed); the hover card
    // keeps each second's own values.
    public bool Smooth { get; init; }
}

// A line of the hover card: a curve's value then, a skill it came from (Muted), or a death.
public sealed record FightGraphHoverLine(string Text, IBrush? Dot, bool Muted = false);

// A fight's curves over time at a fixed size: the time axis squeezes as the fight goes on. HoverSecond draws a line at
// that moment with a dot on each curve; the window shows the values in a card (Describe), as it follows the cursor itself
// (the frame lets the mouse through while the game has focus).
public sealed class FightGraph : Control
{
    public static readonly StyledProperty<FightGraphModel?> ModelProperty = AvaloniaProperty.Register<FightGraph, FightGraphModel?>(nameof(Model));

    public static readonly StyledProperty<int> HoverSecondProperty =
        AvaloniaProperty.Register<FightGraph, int>(nameof(HoverSecond), -1, defaultBindingMode: BindingMode.TwoWay);

    // A fresh fight shows its first half minute this wide, so it doesn't start out stretched.
    private const int MinimumSeconds = 30;
    private const double LeftAxis = 42;
    private const double RightAxis = 44;
    private const double RightPad = 8;
    private const double Top = 6;
    private const double Bottom = 16;
    private const double LabelSize = 10;
    private const int HoverSkills = 4;
    private static readonly ImmutableSolidColorBrush Surface = new(Color.Parse("#D0101418"));
    private static readonly IPen Grid = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#2EFFFFFF")), 1);
    private static readonly IPen Baseline = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#60FFFFFF")), 1);
    private static readonly IPen HoverPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#A0FFFFFF")), 1);
    private static readonly IBrush Muted = new ImmutableSolidColorBrush(Color.Parse("#898781"));
    private static readonly Typeface LabelFace = new(FontFamily.Default);
    private (FightGraphModel Model, Size Size, List<(FightGraphSeries Series, Geometry? Area, Geometry Line)> Curves)? _cache;
    // The smoothed curves of the model shown, worked out once for it.
    private readonly Dictionary<FightGraphSeries, double[]> _smoothed = new(ReferenceEqualityComparer.Instance);
    private FightGraphModel? _smoothedModel;

    static FightGraph()
    {
        AffectsRender<FightGraph>(ModelProperty, HoverSecondProperty);
        ClipToBoundsProperty.OverrideDefaultValue<FightGraph>(true);
    }

    public FightGraphModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public int HoverSecond
    {
        get => GetValue(HoverSecondProperty);
        set => SetValue(HoverSecondProperty, value);
    }

    // 950, 12.3K, 1.25M.
    public static string Compact(double value) => Math.Abs(value) switch
    {
        >= 1e9 => (value / 1e9).ToString("0.##", CultureInfo.CurrentCulture) + "B",
        >= 1e6 => (value / 1e6).ToString("0.##", CultureInfo.CurrentCulture) + "M",
        >= 1e4 => (value / 1e3).ToString("0.#", CultureInfo.CurrentCulture) + "K",
        >= 1e3 => (value / 1e3).ToString("0.##", CultureInfo.CurrentCulture) + "K",
        _ => value.ToString("0.#", CultureInfo.CurrentCulture),
    };

    public static string Time(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

    // The second whose value is drawn nearest to a point (this graph's coordinates); -1 off the curves.
    public int SecondAt(Point point)
    {
        if (Model is not { Seconds: > 0 } model || !new Rect(Bounds.Size).Contains(point))
            return -1;

        var plot = PlotOf(model, Bounds.Size);
        var seconds = (point.X - plot.Left) / plot.Width * Math.Max(model.Seconds, MinimumSeconds);
        return seconds < 0.5 || seconds > model.Seconds + 0.5 ? -1 : Math.Clamp((int)Math.Round(seconds) - 1, 0, model.Seconds - 1);
    }

    // The hover card for a second: its time, each curve's value then (with the skills the user's came from), who died in it.
    public IReadOnlyList<FightGraphHoverLine> Describe(int second)
    {
        if (Model is not { } model || second < 0 || second >= model.Seconds)
            return [];

        var lines = new List<FightGraphHoverLine> { new(Time(second + 1), null) };
        foreach (var series in model.Series)
        {
            var value = second < series.Values.Length ? series.Values[second] : double.NaN;
            if (double.IsNaN(value))
                continue;

            lines.Add(new FightGraphHoverLine($"{series.Label}  {series.Format(value)}", new ImmutableSolidColorBrush(series.Color)));
            if (series.Sources is { } perSecond && second < perSecond.Length && perSecond[second] is { Length: > 0 } sources)
            {
                foreach (var (skill, amount) in sources.Take(HoverSkills))
                    lines.Add(new FightGraphHoverLine($"{skill}  {series.Format(amount)}", null, Muted: true));
                if (sources.Length > HoverSkills)
                    lines.Add(new FightGraphHoverLine($"+{sources.Length - HoverSkills}", null, Muted: true));
            }
        }

        foreach (var mark in model.Marks)
        {
            if (mark.AtSeconds >= second && mark.AtSeconds < second + 1)
                lines.Add(new FightGraphHoverLine($"† {mark.Label}", new ImmutableSolidColorBrush(mark.Color)));
        }

        return lines;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.DrawRectangle(Surface, null, new Rect(size), 3, 3);
        if (Model is not { Seconds: > 0 } model || size.Width <= LeftAxis + RightAxis || size.Height <= Top + Bottom)
            return;

        var plot = PlotOf(model, size);
        var range = Math.Max(model.Seconds, MinimumSeconds);
        var (leftMax, rightMax) = Scales(model);
        var left = model.Series.FirstOrDefault(static series => !series.RightAxis);
        var right = model.Series.FirstOrDefault(static series => series.RightAxis);
        for (var tick = 0; tick <= 4; tick++)
        {
            var y = plot.Bottom - tick / 4d * plot.Height;
            context.DrawLine(tick == 0 ? Baseline : Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            var leftLabel = model.Normalized ? $"{tick * 25}%" : left?.Format(leftMax * tick / 4) ?? string.Empty;
            DrawLabel(context, leftLabel, new Point(plot.Left - 4, y), alignRight: true);
            if (right is not null)
                DrawLabel(context, right.Format(rightMax * tick / 4), new Point(plot.Right + 4, y), alignRight: false);
        }

        var step = TimeStep(range, plot.Width);
        for (var seconds = step; seconds <= range; seconds += step)
        {
            var x = plot.Left + seconds / (double)range * plot.Width;
            context.DrawLine(Grid, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 3));
            DrawLabel(context, Time(seconds), new Point(x, plot.Bottom + 3 + LabelSize / 2 + 1), center: true);
        }

        foreach (var (series, area, line) in Curves(model, size, plot, range, leftMax, rightMax))
        {
            if (area is not null)
                context.DrawGeometry(new ImmutableSolidColorBrush(series.Color, 0.35), null, area);
            context.DrawGeometry(null, new ImmutablePen(new ImmutableSolidColorBrush(series.Color), series.Style == FightGraphStyle.Area ? 2 : 1.5,
                series.Style == FightGraphStyle.Dotted ? new ImmutableDashStyle([2, 2], 0) : null, PenLineCap.Round, PenLineJoin.Round), line);
        }

        foreach (var mark in model.Marks)
        {
            var x = plot.Left + mark.AtSeconds / range * plot.Width;
            var pen = new ImmutablePen(new ImmutableSolidColorBrush(mark.Color), 2);
            context.DrawLine(new ImmutablePen(new ImmutableSolidColorBrush(mark.Color, 0.35), 1), new Point(x, plot.Top + 12), new Point(x, plot.Bottom));
            context.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Top + 11));
            context.DrawLine(pen, new Point(x - 3.5, plot.Top + 3.5), new Point(x + 3.5, plot.Top + 3.5));
        }

        if (HoverSecond is var hover && hover >= 0 && hover < model.Seconds)
        {
            var x = plot.Left + (hover + 1d) / range * plot.Width;
            context.DrawLine(HoverPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            foreach (var series in model.Series)
            {
                // On the curve as drawn; the card says what the second itself had.
                var drawn = Drawn(model, series);
                var value = hover < drawn.Length ? drawn[hover] : double.NaN;
                if (!double.IsNaN(value))
                    context.DrawEllipse(new ImmutableSolidColorBrush(series.Color), new ImmutablePen(Surface, 1.5), new Point(x, Y(plot, value, ScaleOf(series, model, leftMax, rightMax))), 3, 3);
            }
        }
    }

    // A Gaussian average over the seconds around each one (σ 2 s, ±6 s), the same on both sides so a peak stays where it
    // was (how long a spike lasts, how far healing trails damage taken); at the ends and beside gaps only the seconds
    // there count, and a gap (NaN) stays one.
    internal static double[] Smoothed(double[] values)
    {
        const double Sigma = 2;
        const int Radius = 6;
        Span<double> weights = stackalloc double[Radius + 1];
        for (var offset = 0; offset <= Radius; offset++)
            weights[offset] = Math.Exp(-offset * offset / (2 * Sigma * Sigma));

        var smoothed = new double[values.Length];
        for (var second = 0; second < values.Length; second++)
        {
            if (double.IsNaN(values[second]))
            {
                smoothed[second] = double.NaN;
                continue;
            }

            var (sum, total) = (0d, 0d);
            for (var at = Math.Max(0, second - Radius); at <= Math.Min(values.Length - 1, second + Radius); at++)
            {
                if (double.IsNaN(values[at]))
                    continue;

                var weight = weights[Math.Abs(at - second)];
                sum += values[at] * weight;
                total += weight;
            }

            smoothed[second] = sum / total;
        }

        return smoothed;
    }

    // What a curve draws: its values, or smoothed while the model wants it (once per model).
    private double[] Drawn(FightGraphModel model, FightGraphSeries series)
    {
        if (!model.Smooth)
            return series.Values;

        if (!ReferenceEquals(_smoothedModel, model))
        {
            _smoothedModel = model;
            _smoothed.Clear();
        }

        if (!_smoothed.TryGetValue(series, out var values))
            _smoothed[series] = values = Smoothed(series.Values);
        return values;
    }

    private static Rect PlotOf(FightGraphModel model, Size size)
    {
        var right = model.Series.Any(static series => series.RightAxis) ? RightAxis : RightPad;
        return new Rect(LeftAxis, Top, Math.Max(1, size.Width - LeftAxis - right), Math.Max(1, size.Height - Top - Bottom));
    }

    // The left scale: the highest value of its curves as drawn, rounded up to a nice number (the overview: 0-1 of each
    // one's peak); the right one likewise, at least 100 (ms).
    private (double Left, double Right) Scales(FightGraphModel model)
    {
        var (left, right) = (0d, 0d);
        foreach (var series in model.Series)
        {
            var peak = Peak(Drawn(model, series));
            if (series.RightAxis)
                right = Math.Max(right, peak);
            else
                left = Math.Max(left, peak);
        }

        return (model.Normalized ? 1 : NiceMax(left), NiceMax(Math.Max(right, 100)));
    }

    private double ScaleOf(FightGraphSeries series, FightGraphModel model, double leftMax, double rightMax) =>
        series.RightAxis ? rightMax : model.Normalized ? Math.Max(Peak(Drawn(model, series)), double.Epsilon) : leftMax;

    private static double Peak(double[] values)
    {
        var peak = 0d;
        foreach (var value in values)
        {
            if (value > peak)
                peak = value;
        }

        return peak;
    }

    private static double NiceMax(double max)
    {
        if (max <= 0)
            return 1;

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(max)));
        foreach (var step in (double[])[1, 1.2, 1.6, 2, 2.4, 3, 4, 5, 6, 8, 10])
        {
            if (step * magnitude >= max * 0.999)
                return step * magnitude;
        }

        return 10 * magnitude;
    }

    // Time labels at least ~60 px apart.
    private static int TimeStep(int range, double width)
    {
        foreach (var step in (int[])[5, 10, 15, 30, 60, 120, 180, 300, 600, 900, 1_800, 3_600])
        {
            if (step / (double)range * width >= 60)
                return step;
        }

        return 3_600;
    }

    private static double Y(Rect plot, double value, double max) => plot.Bottom - Math.Clamp(value / max, 0, 1) * plot.Height;

    // Each curve's line (and the user's area), worked out once per model and size, as hovering redraws all the time.
    private List<(FightGraphSeries Series, Geometry? Area, Geometry Line)> Curves(FightGraphModel model, Size size, Rect plot, int range, double leftMax, double rightMax)
    {
        if (_cache is { } cache && ReferenceEquals(cache.Model, model) && cache.Size == size)
            return cache.Curves;

        var curves = new List<(FightGraphSeries, Geometry?, Geometry)>();
        // The ping behind everything, the user's area under the lines, the user's own line on top.
        foreach (var series in model.Series.OrderBy(static series => series.RightAxis ? 0 : series.Style == FightGraphStyle.Area ? 1 : 2))
        {
            var max = ScaleOf(series, model, leftMax, rightMax);
            var values = Drawn(model, series);
            var line = new StreamGeometry();
            var area = series.Style == FightGraphStyle.Area ? new StreamGeometry() : null;
            using (var lineContext = line.Open())
            using (var areaContext = area?.Open())
            {
                var open = false;
                var lastX = 0d;
                for (var second = 0; second < values.Length && second < model.Seconds; second++)
                {
                    var value = values[second];
                    var x = plot.Left + (second + 1d) / range * plot.Width;
                    if (double.IsNaN(value))
                    {
                        Close();
                        continue;
                    }

                    var point = new Point(x, Y(plot, value, max));
                    if (!open)
                    {
                        lineContext.BeginFigure(point, false);
                        areaContext?.BeginFigure(new Point(x, plot.Bottom), true);
                        open = true;
                    }
                    else
                    {
                        lineContext.LineTo(point);
                    }

                    areaContext?.LineTo(point);
                    lastX = x;
                }

                Close();

                void Close()
                {
                    if (!open)
                        return;

                    lineContext.EndFigure(false);
                    areaContext?.LineTo(new Point(lastX, plot.Bottom));
                    areaContext?.EndFigure(true);
                    open = false;
                }
            }

            curves.Add((series, area, line));
        }

        _cache = (model, size, curves);
        return curves;
    }

    private static void DrawLabel(DrawingContext context, string text, Point anchor, bool alignRight = false, bool center = false)
    {
        if (text.Length == 0)
            return;

        var label = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelFace, LabelSize, Muted);
        var x = center ? anchor.X - label.Width / 2 : alignRight ? anchor.X - label.Width : anchor.X;
        context.DrawText(label, new Point(x, anchor.Y - label.Height / 2));
    }
}
