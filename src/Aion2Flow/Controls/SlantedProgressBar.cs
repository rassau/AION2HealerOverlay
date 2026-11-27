using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Cloris.Aion2Flow.Presentation;

namespace Cloris.Aion2Flow.Controls;

public sealed class SlantedProgressBar : Control
{
    public static readonly DirectProperty<SlantedProgressBar, ProgressSegment?> SegmentProperty =
        AvaloniaProperty.RegisterDirect<SlantedProgressBar, ProgressSegment?>(
            nameof(Segment),
            control => control.Segment,
            (control, value) => control.Segment = value);

    public static readonly StyledProperty<double> SlantWidthProperty =
        AvaloniaProperty.Register<SlantedProgressBar, double>(nameof(SlantWidth), 7d);

    // The fill's left end at this share of its colour's strength, rising to full at its right end (the meter's rows);
    // 1, at first, fills it evenly.
    public static readonly StyledProperty<double> FadeFromProperty =
        AvaloniaProperty.Register<SlantedProgressBar, double>(nameof(FadeFrom), 1d);

    private IImmutableBrush _fillBrush = new ImmutableSolidColorBrush(Colors.Transparent);
    private SlantedProgressBarVisualState _cachedState;
    private bool _hasCachedState;

    static SlantedProgressBar()
    {
        AffectsRender<SlantedProgressBar>(SlantWidthProperty, FadeFromProperty);
    }

    public ProgressSegment? Segment
    {
        get;
        set => SetAndRaise(SegmentProperty, ref field, value);
    }

    public double SlantWidth
    {
        get => GetValue(SlantWidthProperty);
        set => SetValue(SlantWidthProperty, value);
    }

    public double FadeFrom
    {
        get => GetValue(FadeFromProperty);
        set => SetValue(FadeFromProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var state = CreateVisualState();
        EnsureFillBrush(state);
        var fill = SlantedProgressBarFill.Create(Bounds.Size, state.SlantWidth, state.Ratio);
        if (!fill.IsVisible)
            return;

        using (context.PushTransform(fill.Transform))
            context.FillRectangle(_fillBrush, fill.LocalBounds);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentProperty)
            InvalidateVisual();
    }

    internal SlantedProgressBarVisualState CreateVisualStateForDiagnostics() => CreateVisualState();

    private SlantedProgressBarVisualState CreateVisualState()
    {
        var segment = Segment;
        var ratio = segment.HasValue ? Math.Clamp(segment.Value.Ratio, 0d, 1d) : 0d;
        var fillColor = segment.HasValue
            ? HudBrushColor.Resolve(segment.Value.Brush, Colors.Transparent)
            : Colors.Transparent;
        return new SlantedProgressBarVisualState(
            (float)ratio,
            fillColor,
            (float)Math.Max(0d, SlantWidth),
            (float)Math.Clamp(FadeFrom, 0d, 1d));
    }

    // Across the fill drawn (its own bounds), so a short bar fades in as a long one does.
    private void EnsureFillBrush(SlantedProgressBarVisualState state)
    {
        if (!_hasCachedState || state.FillColor != _cachedState.FillColor || state.FadeFrom != _cachedState.FadeFrom)
        {
            var color = state.FillColor;
            _fillBrush = state.FadeFrom >= 1f
                ? new ImmutableSolidColorBrush(color)
                : new ImmutableLinearGradientBrush(
                    [new ImmutableGradientStop(0, Color.FromArgb((byte)Math.Round(color.A * state.FadeFrom), color.R, color.G, color.B)), new ImmutableGradientStop(1, color)],
                    startPoint: new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    endPoint: new RelativePoint(1, 0.5, RelativeUnit.Relative));
        }

        _cachedState = state;
        _hasCachedState = true;
    }
}

internal readonly record struct SlantedProgressBarVisualState(
    float Ratio,
    Color FillColor,
    float SlantWidth,
    float FadeFrom = 1f);

internal readonly record struct SlantedProgressBarFill(
    Matrix Transform,
    Rect LocalBounds)
{
    internal bool IsVisible => LocalBounds.Width > 0d && LocalBounds.Height > 0d;

    internal static SlantedProgressBarFill Create(Size size, float slantWidth, float ratio)
    {
        var width = Math.Max(0d, size.Width);
        var height = Math.Max(0d, size.Height);
        var slant = Math.Clamp(slantWidth, 0d, width);
        var fillWidth = Math.Max(0d, width - slant) * Math.Clamp(ratio, 0f, 1f);
        if (height <= 0d || fillWidth <= 0d)
            return default;

        var transform = new Matrix(1d, 0d, -slant / height, 1d, slant, 0d);
        return new SlantedProgressBarFill(transform, new Rect(0d, 0d, fillWidth, height));
    }
}
