using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Cloris.Aion2Flow.Services.Audio;

namespace Cloris.Aion2Flow.Controls;

// The alert editor's waveform: what lies between the two edges is the alert. A press moves the nearer edge there and
// drags it; the arrow keys nudge the edge moved last by 10 ms (Shift: 100 ms). Zoomed in, it shows the selection with a
// little around it, for fine cuts.
public sealed class AlertClipView : Control
{
    private static readonly IBrush BackBrush = new ImmutableSolidColorBrush(Color.Parse("#C0101418"));
    private static readonly IBrush OutsideBrush = new ImmutableSolidColorBrush(Color.Parse("#B0101418"));
    private static readonly IPen WavePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#8CD8FF")), 1);
    private static readonly IPen EdgePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#E6C65A")), 2);
    private AudioClip? _clip;
    private int _minLength;
    private int _maxLength;
    private bool _zoomed;
    private int _viewStart;
    private int _viewEnd;
    private float[] _low = [];
    private float[] _high = [];
    private (int Start, int End, int Width) _peaksFor;
    private bool? _draggingEnd;
    private bool _lastMovedEnd;

    public AlertClipView()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
    }

    // The selection, in samples.
    public int Start { get; private set; }

    public int End { get; private set; }

    public bool Zoomed
    {
        get => _zoomed;
        set
        {
            _zoomed = value;
            FitView();
        }
    }

    public event EventHandler? SelectionChanged;

    // An edge was let go, true for the end one: the editor plays a bit from there.
    public event EventHandler<bool>? EdgeReleased;

    // A new sound, its first maxLength selected.
    public void Load(AudioClip clip, TimeSpan maxLength)
    {
        _clip = clip;
        _minLength = clip.SampleRate / 20;
        _maxLength = (int)(maxLength.TotalSeconds * clip.SampleRate);
        Start = 0;
        End = Math.Min(clip.Samples.Length, _maxLength);
        _zoomed = false;
        FitView();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // Moves one edge to `at`, keeping the selection inside the sound and between min and max long: the other edge
    // follows when it has to.
    internal static (int Start, int End) MoveEdge(int start, int end, bool movingEnd, int at, int length, int min, int max)
    {
        if (movingEnd)
        {
            end = Math.Clamp(at, Math.Min(min, length), length);
            start = Math.Clamp(start, Math.Max(0, end - max), Math.Max(0, end - min));
        }
        else
        {
            start = Math.Clamp(at, 0, Math.Max(0, length - min));
            end = Math.Clamp(end, Math.Min(length, start + min), Math.Min(length, start + max));
        }

        return (start, end);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_clip is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Focus();
        var at = SampleAt(e.GetPosition(this).X);
        _draggingEnd = Math.Abs(at - End) < Math.Abs(at - Start);
        Move(_draggingEnd.Value, at);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_draggingEnd is { } end)
            Move(end, SampleAt(e.GetPosition(this).X));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_draggingEnd is not { } end)
            return;

        _draggingEnd = null;
        e.Pointer.Capture(null);
        if (_zoomed)
            FitView();
        EdgeReleased?.Invoke(this, end);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _draggingEnd = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_clip is null || e.Key is not (Key.Left or Key.Right))
            return;

        var step = _clip.SampleRate / (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 100);
        Move(_lastMovedEnd, (_lastMovedEnd ? End : Start) + (e.Key == Key.Left ? -step : step));
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_clip is not null && e.Key is Key.Left or Key.Right)
        {
            if (_zoomed)
                FitView();
            EdgeReleased?.Invoke(this, _lastMovedEnd);
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(BackBrush, new Rect(size));
        if (_clip is null || _viewEnd <= _viewStart)
            return;

        var width = Math.Max(1, (int)size.Width);
        if (_peaksFor != (_viewStart, _viewEnd, width))
            FindPeaks(width);
        var middle = size.Height / 2;
        for (var x = 0; x < width; x++)
            context.DrawLine(WavePen, new Point(x + 0.5, middle - _high[x] * middle), new Point(x + 0.5, middle - _low[x] * middle + 1));

        var start = X(Start);
        var end = X(End);
        context.FillRectangle(OutsideBrush, new Rect(0, 0, Math.Max(0, start), size.Height));
        context.FillRectangle(OutsideBrush, new Rect(end, 0, Math.Max(0, size.Width - end), size.Height));
        Edge(start);
        Edge(end);

        // A pixel inside the view at least, so an edge at the very start or end still shows whole.
        void Edge(double x)
        {
            x = Math.Max(1, Math.Min(x, size.Width - 1));
            context.DrawLine(EdgePen, new Point(x, 0), new Point(x, size.Height));
        }
    }

    private void Move(bool end, int at)
    {
        _lastMovedEnd = end;
        (Start, End) = MoveEdge(Start, End, end, at, _clip!.Samples.Length, _minLength, _maxLength);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    // All of the sound, or zoomed in: the selection and half a second (or a quarter of it) on each side.
    private void FitView()
    {
        if (_clip is null)
            return;

        var pad = Math.Max(_clip.SampleRate / 2, (End - Start) / 4);
        (_viewStart, _viewEnd) = _zoomed ? (Math.Max(0, Start - pad), Math.Min(_clip.Samples.Length, End + pad)) : (0, _clip.Samples.Length);
        InvalidateVisual();
    }

    private int SampleAt(double x) => _viewStart + (int)(Math.Clamp(x / Math.Max(1, Bounds.Width), 0, 1) * (_viewEnd - _viewStart));

    private double X(int sample) => (sample - _viewStart) / (double)(_viewEnd - _viewStart) * Bounds.Width;

    // The loudest and quietest value under each pixel column; only again when the view or the width changes.
    private void FindPeaks(int width)
    {
        _peaksFor = (_viewStart, _viewEnd, width);
        _low = new float[width];
        _high = new float[width];
        var samples = _clip!.Samples;
        var span = (long)(_viewEnd - _viewStart);
        for (var x = 0; x < width; x++)
        {
            var from = _viewStart + (int)(span * x / width);
            var to = Math.Min(samples.Length, Math.Max(from + 1, _viewStart + (int)(span * (x + 1) / width)));
            short low = 0, high = 0;
            for (var i = from; i < to; i++)
            {
                low = Math.Min(low, samples[i]);
                high = Math.Max(high, samples[i]);
            }

            _low[x] = low / 32768f;
            _high[x] = high / 32767f;
        }
    }
}
