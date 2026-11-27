using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Cloris.Aion2Flow.Services;

namespace Cloris.Aion2Flow.Controls;

// The user's ping over the last hour (PingHistory), ten minutes at a time, with what 99 % of the two minutes' pings before
// each one stayed at or below (dashed: the footer's figure in brackets as it went). Or the game's frame rate
// (ShowsFrameRate), two minutes at a time, with each one's 1 % low dashed. The mouse wheel or a drag moves along what's
// kept, the pointer reads off a moment; shown up to now, it takes in each new value.
public sealed class PingGraph : Control
{
    public static readonly StyledProperty<PingHistory?> HistoryProperty = AvaloniaProperty.Register<PingGraph, PingHistory?>(nameof(History));

    public static readonly StyledProperty<string> PingLabelProperty = AvaloniaProperty.Register<PingGraph, string>(nameof(PingLabel), "Ping");

    public static readonly StyledProperty<string> PercentileLabelProperty =
        AvaloniaProperty.Register<PingGraph, string>(nameof(PercentileLabel), "99 % (2 min)");

    public static readonly StyledProperty<string> EmptyTextProperty = AvaloniaProperty.Register<PingGraph, string>(nameof(EmptyText), string.Empty);

    // "Smooth curve" (Settings → DPS meter): both lines drawn as an average of the values around each, as the fight graphs'
    // Smooth curves do (FightGraph.Smoothed), up to a gap; hovering still reads each value's own.
    public static readonly StyledProperty<bool> SmoothProperty = AvaloniaProperty.Register<PingGraph, bool>(nameof(Smooth));

    internal const long SpanMilliseconds = 600_000;
    internal const long FrameRateSpanMilliseconds = 120_000;
    // Pings further apart get a gap between them (no capture, a loading screen), as the footer's figure lapses then; frame
    // rates (2 s apart at most) where the game was closed.
    private const long GapMilliseconds = 30_000;
    private const long FrameRateGapMilliseconds = 5_000;
    private const double LeftAxis = 40;
    private const double RightPad = 8;
    private const double Top = 6;
    private const double Bottom = 16;
    private const double LabelSize = 10;
    // A pair checked against the dark surface (colour-blind safe); the percentile is dashed as well.
    private static readonly Color PingColor = Color.Parse("#1FA093");
    private static readonly Color PercentileColor = Color.Parse("#C98500");
    private static readonly IPen PingPen = new ImmutablePen(new ImmutableSolidColorBrush(PingColor), 2, null, PenLineCap.Round, PenLineJoin.Round);
    private static readonly IPen PercentilePen = new ImmutablePen(new ImmutableSolidColorBrush(PercentileColor), 1.5, new ImmutableDashStyle([3, 2], 0));
    private static readonly IBrush LegendText = new ImmutableSolidColorBrush(Color.Parse("#E6E4DF"));
    // The meter's muted text (OverlayBrush.TextMuted), with its halo (OverlayTheme), as the graph sits over the game.
    private static readonly IBrush AxisText = new ImmutableSolidColorBrush(Color.Parse("#B0C1D0"));
    private static readonly IBrush LegendBackground = new ImmutableSolidColorBrush(Color.Parse("#C0101418"));
    private static readonly Typeface LabelFace = new(FontFamily.Default);
    // The view's right end (Unix ms) once moved back in time; null: now.
    private long? _end;
    private (double X, long End)? _drag;
    private Point? _pointer;
    private PingHistory? _watched;
    private bool _attached;

    static PingGraph()
    {
        AffectsRender<PingGraph>(HistoryProperty, PingLabelProperty, PercentileLabelProperty, EmptyTextProperty, SmoothProperty);
        ClipToBoundsProperty.OverrideDefaultValue<PingGraph>(true);
    }

    public PingGraph() => Cursor = new Cursor(StandardCursorType.SizeWestEast);

    public PingHistory? History
    {
        get => GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    public string PingLabel
    {
        get => GetValue(PingLabelProperty);
        set => SetValue(PingLabelProperty, value);
    }

    public string PercentileLabel
    {
        get => GetValue(PercentileLabelProperty);
        set => SetValue(PercentileLabelProperty, value);
    }

    public string EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public bool Smooth
    {
        get => GetValue(SmoothProperty);
        set => SetValue(SmoothProperty, value);
    }

    // Set once, where the graph is made.
    public bool ShowsFrameRate { get; set; }

    internal TimeProvider Clock { get; set; } = TimeProvider.System;

    // The view's right end: now, or where the user moved it.
    internal long ViewEnd => Math.Min(_end ?? Now(), Now());

    private long Span => ShowsFrameRate ? FrameRateSpanMilliseconds : SpanMilliseconds;

    private long Gap => ShowsFrameRate ? FrameRateGapMilliseconds : GapMilliseconds;

    // Within what's kept: not past now, not further back than the first value's view; back at now it follows again.
    internal void MoveTo(long end)
    {
        var now = Now();
        var first = History is { Samples.Count: > 0 } history ? history.Samples[0].At : now;
        end = Math.Clamp(end, Math.Min(now, first + Span), now);
        _end = end >= now - 1_000 ? null : end;
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Opened anew: up to now.
        (_attached, _end, _pointer) = (true, null, null);
        Watch(History);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        Watch(null);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HistoryProperty && _attached)
            Watch(History);
    }

    // Wheel up (or left) goes back in time, down (or right) towards now: a notch by a tenth of the view.
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        MoveTo(ViewEnd - (long)((e.Delta.Y + e.Delta.X) * Span / 10));
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _drag = (e.GetPosition(this).X, ViewEnd);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    // Dragging to the right pulls the line along it, back in time.
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointer = e.GetPosition(this);
        if (_drag is { } drag)
            MoveTo(drag.End - (long)((_pointer.Value.X - drag.X) / Plot(Bounds.Size).Width * Span));
        else
            InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is null)
            return;

        _drag = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _drag = null;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointer = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        // See-through like the meter (the popup's backdrop shows), with its halo (OverlayTheme); still drawn so the wheel
        // and drags reach the whole graph.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(size), 3, 3);
        var plot = Plot(size);
        if (size.Width <= LeftAxis + RightPad || size.Height <= Top + Bottom)
            return;

        var end = ViewEnd;
        var start = end - Span;
        var shown = Shown(start, end);
        // As the fight graphs do: the scale goes by the lines as drawn.
        var peak = 0d;
        foreach (var item in shown)
            peak = Math.Max(peak, Math.Max(item.Line, item.PercentileLine));
        var max = FightGraph.NiceMax(Math.Max(peak, ShowsFrameRate ? 60 : 40));

        for (var tick = 0; tick <= 4; tick++)
        {
            var y = plot.Bottom - tick / 4d * plot.Height;
            context.DrawLine(tick == 0 ? FightGraph.Baseline : FightGraph.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            FightGraph.DrawLabel(context, Format(max * tick / 4), new Point(plot.Left - 4, y), alignRight: true, brush: AxisText);
        }

        // Clock times at round steps (seconds shown below a minute), at least ~50 px apart; none so near an edge that it
        // would be cut off.
        var offset = (long)TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.FromUnixTimeMilliseconds(end)).TotalMilliseconds;
        var step = 1_000L * ((int[])[15, 30, 60, 120, 300, 600]).FirstOrDefault(seconds => seconds * 1_000d / Span * plot.Width >= 50, 600);
        for (var at = ((start + offset) / step + 1) * step - offset; at <= end; at += step)
        {
            var x = X(plot, start, at);
            if (x < plot.Left + 12 || x > plot.Right - 12)
                continue;

            context.DrawLine(FightGraph.Grid, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 3));
            FightGraph.DrawLabel(context, ClockTime(at, step < 60_000 ? "T" : "t"), new Point(x, plot.Bottom + 3 + LabelSize / 2 + 1), center: true, brush: AxisText);
        }

        if (shown.Count == 0)
        {
            FightGraph.DrawLabel(context, EmptyText, plot.Center, center: true, brush: AxisText);
            return;
        }

        // No clip pushed: text drawn after one came out at twice its size.
        DrawCurve(context, plot, start, max, shown, static item => item.PercentileLine, PercentilePen, PercentileColor);
        DrawCurve(context, plot, start, max, shown, static item => item.Line, PingPen, PingColor);

        // The pointer's moment: the value nearest to it, with its percentile (the dots on the lines as drawn, the numbers
        // its own); otherwise what the lines are.
        ShownValue? hovered = null;
        if (_pointer is { } pointer && plot.Contains(pointer))
        {
            var at = start + (long)((pointer.X - plot.Left) / plot.Width * Span);
            var nearest = shown.MinBy(item => Math.Abs(item.Sample.At - at));
            if (Math.Abs(nearest.Sample.At - at) <= Gap && nearest.Sample.At >= start && nearest.Sample.At <= end)
                hovered = nearest;
        }

        if (hovered is { } moment)
        {
            var x = X(plot, start, moment.Sample.At);
            context.DrawLine(FightGraph.HoverPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            context.DrawEllipse(new ImmutableSolidColorBrush(PercentileColor), new ImmutablePen(FightGraph.Surface, 1.5), new Point(x, Y(plot, moment.PercentileLine, max)), 3, 3);
            context.DrawEllipse(new ImmutableSolidColorBrush(PingColor), new ImmutablePen(FightGraph.Surface, 1.5), new Point(x, Y(plot, moment.Line, max)), 3, 3);
        }

        DrawLegend(context, plot, hovered is { } shownMoment
            ? [(null, ClockTime(shownMoment.Sample.At, "T")), (PingPen, Format(shownMoment.Sample.Value)), (PercentilePen, Format(shownMoment.Percentile))]
            : [(PingPen, PingLabel), (PercentilePen, PercentileLabel)]);
    }

    private long Now() => Clock.GetUtcNow().ToUnixTimeMilliseconds();

    private void Watch(PingHistory? history)
    {
        if (_watched is not null)
            _watched.Changed -= InvalidateVisual;
        _watched = history;
        if (history is not null)
            history.Changed += InvalidateVisual;
    }

    // The values in view and one on either side (the lines run to the edges), each with its percentile (a frame rate with
    // its 1 % low). Smoothed, the lines take in the 6 values on either side too.
    internal List<ShownValue> Shown(long start, long end)
    {
        var shown = new List<ShownValue>();
        if (History is not { } history)
            return shown;

        var samples = history.Samples;
        var first = 0;
        while (first < samples.Count && samples[first].At < start)
            first++;
        var last = first;
        while (last < samples.Count && samples[last].At <= end)
            last++;
        var (from, to) = (Math.Max(0, first - 1), Math.Min(samples.Count, last + 1));
        var margin = Smooth ? 6 : 0;
        var (lo, hi) = (Math.Max(0, from - margin), Math.Min(samples.Count, to + margin));
        var values = new double[hi - lo];
        var percentiles = new double[hi - lo];
        for (var i = lo; i < hi; i++)
            (values[i - lo], percentiles[i - lo]) = (samples[i].Value, ShowsFrameRate ? samples[i].Low : history.Percentile99(samples[i].At) ?? samples[i].Value);
        var (lines, percentileLines) = Smooth ? (Smoothed(samples, lo, values), Smoothed(samples, lo, percentiles)) : (values, percentiles);
        for (var i = from; i < to; i++)
            shown.Add(new ShownValue(samples[i], percentiles[i - lo], lines[i - lo], percentileLines[i - lo]));
        return shown;
    }

    // FightGraph.Smoothed run by run: a gap (values over Gap apart) breaks the average.
    private double[] Smoothed(IReadOnlyList<PingSample> samples, int offset, double[] values)
    {
        var smoothed = new double[values.Length];
        for (var run = 0; run < values.Length;)
        {
            var next = run + 1;
            while (next < values.Length && samples[offset + next].At - samples[offset + next - 1].At <= Gap)
                next++;
            FightGraph.Smoothed(values[run..next]).CopyTo(smoothed, run);
            run = next;
        }

        return smoothed;
    }

    // A value in view: the sample (its own value, as hovering reads it), its percentile, and both as the lines draw them.
    internal readonly record struct ShownValue(PingSample Sample, double Percentile, double Line, double PercentileLine);

    // A line through the values, broken where they're over Gap apart; a value alone between gaps as a dot. The values just
    // outside the view only take the line to its edges.
    private void DrawCurve(DrawingContext context, Rect plot, long start, double max, List<ShownValue> shown, Func<ShownValue, double> value, IPen pen, Color color)
    {
        var points = shown.Select(item => (item.Sample.At, Point: new Point(X(plot, start, item.Sample.At), Y(plot, value(item), max)))).ToList();
        if (points.Count > 0 && points[0].Point.X < plot.Left)
        {
            if (points.Count > 1 && points[1].At - points[0].At <= Gap)
                points[0] = (points[0].At, AtEdge(points[0].Point, points[1].Point, plot.Left));
            else
                points.RemoveAt(0);
        }

        if (points.Count > 0 && points[^1].Point.X > plot.Right)
        {
            if (points.Count > 1 && points[^1].At - points[^2].At <= Gap)
                points[^1] = (points[^1].At, AtEdge(points[^1].Point, points[^2].Point, plot.Right));
            else
                points.RemoveAt(points.Count - 1);
        }

        var line = new StreamGeometry();
        var lone = new List<Point>();
        using (var figure = line.Open())
        {
            var (open, count, last, first) = (false, 0, 0L, default(Point));
            foreach (var (at, point) in points)
            {
                if (open && at - last > Gap)
                    End();
                if (!open)
                {
                    figure.BeginFigure(point, false);
                    (open, count, first) = (true, 0, point);
                }
                else
                {
                    figure.LineTo(point);
                }

                count++;
                last = at;
            }

            End();

            void End()
            {
                if (!open)
                    return;

                figure.EndFigure(false);
                if (count == 1)
                    lone.Add(first);
                open = false;
            }
        }

        context.DrawGeometry(null, pen, line);
        foreach (var point in lone)
            context.DrawEllipse(new ImmutableSolidColorBrush(color), null, point, 2, 2);
    }

    // A row at the top left: a short stroke of each line's pen before its text, on a dark box.
    private static void DrawLegend(DrawingContext context, Rect plot, (IPen? Pen, string Text)[] parts)
    {
        const double Swatch = 12;
        const double Gap = 4;
        const double Spacing = 10;
        var texts = parts.Select(part => new FormattedText(part.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelFace, LabelSize, LegendText)).ToArray();
        var width = parts.Select((part, i) => (part.Pen is null ? 0 : Swatch + Gap) + texts[i].Width).Sum() + Spacing * (parts.Length - 1);
        var height = texts.Max(static text => text.Height);
        var origin = new Point(plot.Left + 4, plot.Top + 3);
        context.DrawRectangle(LegendBackground, null, new Rect(origin.X - 3, origin.Y - 1, width + 6, height + 2), 2, 2);
        var x = origin.X;
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Pen is { } pen)
            {
                context.DrawLine(pen, new Point(x, origin.Y + height / 2), new Point(x + Swatch, origin.Y + height / 2));
                x += Swatch + Gap;
            }

            context.DrawText(texts[i], new Point(x, origin.Y));
            x += texts[i].Width + Spacing;
        }
    }

    // Where the line from a point outside to one inside crosses the edge at x.
    private static Point AtEdge(Point outside, Point inside, double x) =>
        new(x, outside.Y + (inside.Y - outside.Y) * (x - outside.X) / (inside.X - outside.X));

    private static Rect Plot(Size size) =>
        new(LeftAxis, Top, Math.Max(1, size.Width - LeftAxis - RightPad), Math.Max(1, size.Height - Top - Bottom));

    private double X(Rect plot, long start, long at) => plot.Left + (at - start) / (double)Span * plot.Width;

    private static double Y(Rect plot, double value, double max) => plot.Bottom - Math.Clamp(value / max, 0, 1) * plot.Height;

    private string Format(double value) => ShowsFrameRate
        ? value.ToString("0", CultureInfo.CurrentCulture) + " fps"
        : value >= 1_000
            ? (value / 1_000).ToString("0.0", CultureInfo.CurrentCulture) + " s"
            : value.ToString("0", CultureInfo.CurrentCulture) + " ms";

    private static string ClockTime(long at, string format) =>
        DateTimeOffset.FromUnixTimeMilliseconds(at).ToLocalTime().ToString(format, CultureInfo.CurrentCulture);
}
