using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Cloris.Aion2Flow.Services;

namespace Cloris.Aion2Flow.Controls;

// The user's ping over the last hour (PingHistory), ten minutes at a time, with what 99 % of the two minutes' pings before
// each one stayed at or below (dashed: the footer's figure in brackets as it went). The mouse wheel or a drag moves along
// the hour, the pointer reads off a moment; shown up to now, it takes in each new ping.
public sealed class PingGraph : Control
{
    public static readonly StyledProperty<PingHistory?> HistoryProperty = AvaloniaProperty.Register<PingGraph, PingHistory?>(nameof(History));

    public static readonly StyledProperty<string> PingLabelProperty = AvaloniaProperty.Register<PingGraph, string>(nameof(PingLabel), "Ping");

    public static readonly StyledProperty<string> PercentileLabelProperty =
        AvaloniaProperty.Register<PingGraph, string>(nameof(PercentileLabel), "99 % (2 min)");

    public static readonly StyledProperty<string> EmptyTextProperty = AvaloniaProperty.Register<PingGraph, string>(nameof(EmptyText), string.Empty);

    internal const long SpanMilliseconds = 600_000;
    // A wheel notch moves a tenth of the view.
    private const long WheelStepMilliseconds = SpanMilliseconds / 10;
    // Pings further apart get a gap between them (no capture, a loading screen), as the footer's figure lapses then.
    private const long GapMilliseconds = 30_000;
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
        AffectsRender<PingGraph>(HistoryProperty, PingLabelProperty, PercentileLabelProperty, EmptyTextProperty);
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

    internal TimeProvider Clock { get; set; } = TimeProvider.System;

    // The view's right end: now, or where the user moved it.
    internal long ViewEnd => Math.Min(_end ?? Now(), Now());

    // Within the hour kept: not past now, not further back than the first ping's ten minutes; back at now it follows again.
    internal void MoveTo(long end)
    {
        var now = Now();
        var first = History is { Samples.Count: > 0 } history ? history.Samples[0].At : now;
        end = Math.Clamp(end, Math.Min(now, first + SpanMilliseconds), now);
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

    // Wheel up (or left) goes back in time, down (or right) towards now.
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        MoveTo(ViewEnd - (long)((e.Delta.Y + e.Delta.X) * WheelStepMilliseconds));
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
            MoveTo(drag.End - (long)((_pointer.Value.X - drag.X) / Plot(Bounds.Size).Width * SpanMilliseconds));
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
        context.DrawRectangle(FightGraph.Surface, null, new Rect(size), 3, 3);
        var plot = Plot(size);
        if (size.Width <= LeftAxis + RightPad || size.Height <= Top + Bottom)
            return;

        var end = ViewEnd;
        var start = end - SpanMilliseconds;
        var shown = Shown(start, end);
        var peak = 0d;
        foreach (var (sample, percentile) in shown)
            peak = Math.Max(peak, Math.Max(sample.Milliseconds, percentile));
        var max = FightGraph.NiceMax(Math.Max(peak, 40));

        for (var tick = 0; tick <= 4; tick++)
        {
            var y = plot.Bottom - tick / 4d * plot.Height;
            context.DrawLine(tick == 0 ? FightGraph.Baseline : FightGraph.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            FightGraph.DrawLabel(context, Milliseconds(max * tick / 4), new Point(plot.Left - 4, y), alignRight: true);
        }

        // Clock times at round minutes, at least ~50 px apart; none so near an edge that it would be cut off.
        var offset = (long)TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.FromUnixTimeMilliseconds(end)).TotalMilliseconds;
        var step = (long)TimeSpan.FromMinutes(((int[])[1, 2, 5, 10]).FirstOrDefault(minutes => minutes * 60_000d / SpanMilliseconds * plot.Width >= 50, 10)).TotalMilliseconds;
        for (var at = ((start + offset) / step + 1) * step - offset; at <= end; at += step)
        {
            var x = X(plot, start, at);
            if (x < plot.Left + 12 || x > plot.Right - 12)
                continue;

            context.DrawLine(FightGraph.Grid, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 3));
            FightGraph.DrawLabel(context, ClockTime(at, "t"), new Point(x, plot.Bottom + 3 + LabelSize / 2 + 1), center: true);
        }

        if (shown.Count == 0)
        {
            FightGraph.DrawLabel(context, EmptyText, plot.Center, center: true);
            return;
        }

        // No clip pushed: text drawn after one came out at twice its size.
        DrawCurve(context, plot, start, max, shown, static item => item.Percentile, PercentilePen, PercentileColor);
        DrawCurve(context, plot, start, max, shown, static item => item.Sample.Milliseconds, PingPen, PingColor);

        // The pointer's moment: the ping nearest to it, with its percentile; otherwise what the lines are.
        (PingSample Sample, double Percentile)? hovered = null;
        if (_pointer is { } pointer && plot.Contains(pointer))
        {
            var at = start + (long)((pointer.X - plot.Left) / plot.Width * SpanMilliseconds);
            var nearest = shown.MinBy(item => Math.Abs(item.Sample.At - at));
            if (Math.Abs(nearest.Sample.At - at) <= GapMilliseconds && nearest.Sample.At >= start && nearest.Sample.At <= end)
                hovered = nearest;
        }

        if (hovered is { } moment)
        {
            var x = X(plot, start, moment.Sample.At);
            context.DrawLine(FightGraph.HoverPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            context.DrawEllipse(new ImmutableSolidColorBrush(PercentileColor), new ImmutablePen(FightGraph.Surface, 1.5), new Point(x, Y(plot, moment.Percentile, max)), 3, 3);
            context.DrawEllipse(new ImmutableSolidColorBrush(PingColor), new ImmutablePen(FightGraph.Surface, 1.5), new Point(x, Y(plot, moment.Sample.Milliseconds, max)), 3, 3);
        }

        DrawLegend(context, plot, hovered is { } shownMoment
            ? [(null, ClockTime(shownMoment.Sample.At, "T")), (PingPen, Milliseconds(shownMoment.Sample.Milliseconds)), (PercentilePen, Milliseconds(shownMoment.Percentile))]
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

    // The pings in view and one on either side (the lines run to the edges), each with its percentile.
    private List<(PingSample Sample, double Percentile)> Shown(long start, long end)
    {
        var shown = new List<(PingSample, double)>();
        if (History is not { } history)
            return shown;

        var samples = history.Samples;
        var first = 0;
        while (first < samples.Count && samples[first].At < start)
            first++;
        var last = first;
        while (last < samples.Count && samples[last].At <= end)
            last++;
        for (var i = Math.Max(0, first - 1); i < Math.Min(samples.Count, last + 1); i++)
            shown.Add((samples[i], history.Percentile99(samples[i].At) ?? samples[i].Milliseconds));
        return shown;
    }

    // A line through the pings, broken where they're over GapMilliseconds apart; a ping alone between gaps as a dot. The
    // pings just outside the view only take the line to its edges.
    private static void DrawCurve(DrawingContext context, Rect plot, long start, double max, List<(PingSample Sample, double Percentile)> shown,
        Func<(PingSample Sample, double Percentile), double> value, IPen pen, Color color)
    {
        var points = shown.Select(item => (item.Sample.At, Point: new Point(X(plot, start, item.Sample.At), Y(plot, value(item), max)))).ToList();
        if (points.Count > 0 && points[0].Point.X < plot.Left)
        {
            if (points.Count > 1 && points[1].At - points[0].At <= GapMilliseconds)
                points[0] = (points[0].At, AtEdge(points[0].Point, points[1].Point, plot.Left));
            else
                points.RemoveAt(0);
        }

        if (points.Count > 0 && points[^1].Point.X > plot.Right)
        {
            if (points.Count > 1 && points[^1].At - points[^2].At <= GapMilliseconds)
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
                if (open && at - last > GapMilliseconds)
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

    private static double X(Rect plot, long start, long at) => plot.Left + (at - start) / (double)SpanMilliseconds * plot.Width;

    private static double Y(Rect plot, double value, double max) => plot.Bottom - Math.Clamp(value / max, 0, 1) * plot.Height;

    private static string Milliseconds(double value) => value >= 1_000
        ? (value / 1_000).ToString("0.0", CultureInfo.CurrentCulture) + " s"
        : value.ToString("0", CultureInfo.CurrentCulture) + " ms";

    private static string ClockTime(long at, string format) =>
        DateTimeOffset.FromUnixTimeMilliseconds(at).ToLocalTime().ToString(format, CultureInfo.CurrentCulture);
}
