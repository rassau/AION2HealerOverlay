using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Cloris.Aion2Flow.Presentation;

namespace Cloris.Aion2Flow.Controls;

// A bar made of pieces anywhere along it, each in its own brush, drawn in order.
public sealed class SegmentedBar : Control
{
    public static readonly DirectProperty<SegmentedBar, IReadOnlyList<BarSegment>?> SegmentsProperty =
        AvaloniaProperty.RegisterDirect<SegmentedBar, IReadOnlyList<BarSegment>?>(
            nameof(Segments),
            bar => bar.Segments,
            (bar, value) => bar.Segments = value);

    public IReadOnlyList<BarSegment>? Segments
    {
        get;
        set => SetAndRaise(SegmentsProperty, ref field, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Segments is not { } segments)
            return;

        var width = Bounds.Width;
        foreach (var segment in segments)
        {
            var start = Math.Clamp(segment.Start, 0, 1) * width;
            var end = Math.Clamp(segment.End, 0, 1) * width;
            if (end > start)
                context.FillRectangle(segment.Brush, new Rect(start, 0, end - start, Bounds.Height));
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentsProperty)
            InvalidateVisual();
    }
}
