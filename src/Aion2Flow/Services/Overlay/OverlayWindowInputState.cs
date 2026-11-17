using Avalonia;

namespace Cloris.Aion2Flow.Services.Overlay;

// A frame's window (Views/FrameClickThrough): it takes clicks (Interactive) while the overlay has focus or the frame is
// clickable without focus. Otherwise the whole window lets the mouse through to the game (ClickThrough), or with "Title
// line always clickable" all but its title line: armed while the cursor is elsewhere (the title line takes clicks, a click
// there gives the overlay focus), active (the window input-transparent) while the cursor is over the body below it.
internal enum OverlayWindowInputState
{
    Interactive,
    ClickThrough,
    ClickThroughArmed,
    ClickThroughActive
}

internal static class OverlayWindowInputStateLogic
{
    public static OverlayWindowInputState Enter(bool clickThrough, bool titleClickable, bool isCursorOverBody) =>
        !clickThrough ? OverlayWindowInputState.Interactive
        : !titleClickable ? OverlayWindowInputState.ClickThrough
        : isCursorOverBody ? OverlayWindowInputState.ClickThroughActive
        : OverlayWindowInputState.ClickThroughArmed;

    public static bool RequiresInputTransparency(this OverlayWindowInputState state) =>
        state is OverlayWindowInputState.ClickThrough or OverlayWindowInputState.ClickThroughActive;

    // An input-transparent window gets no pointer events: while its title line takes clicks, the cursor is watched for
    // leaving the body.
    public static bool ShouldPollCursor(this OverlayWindowInputState state) => state == OverlayWindowInputState.ClickThroughActive;

    // The body: the frame (its size) below the end of its title line, in the frame's own units; its right and bottom edge
    // are outside.
    public static bool IsOverBody(Size frame, double titleBottom, Point point) =>
        point.X >= 0 && point.X < frame.Width && point.Y >= titleBottom && point.Y < frame.Height;
}
