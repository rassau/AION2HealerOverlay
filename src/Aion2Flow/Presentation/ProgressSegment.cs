using Avalonia.Media;

namespace Cloris.Aion2Flow.Presentation;

public readonly record struct ProgressSegment(double Ratio, IBrush Brush);

// A piece of a bar from Start to End, as parts (0 to 1) of its width.
public readonly record struct BarSegment(double Start, double End, IBrush Brush);
