using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Cloris.Aion2Flow.Views;

// Moving a window with the mouse. Windows' move loop puts back the size the window had when the drag began, though the click
// that began it (the overlay taking focus) may have changed what it shows; Avalonia takes that for a resize by the user and
// stops sizing the window to its content, so a frame no longer grew or shrank with it (the fight graphs with their banner).
// Once the drag is over (the loop returns when the button is let go) the window sizes to its content again. The meter sees
// to its height itself (MainWindow.RefreshOverlayAutoHeight).
internal static class WindowDrag
{
    public static void Move(Window window, PointerPressedEventArgs e)
    {
        var sizeToContent = window.SizeToContent;
        window.BeginMoveDrag(e);
        SizeToContentAgain(window, sizeToContent);
    }

    // A frame's title line: a click does what the frame's eye does (folds the frame or brings it back), a drag moves the
    // frame. Avalonia only posts the move loop (it starts once the press is handled), so the window's position can't tell
    // the two apart: the line keeps the pointer, a move of more than a few pixels before the button comes up starts the
    // drag (with the press: Windows' move loop wants the button still down), and a release before that is the click.
    // move: how the window moves (WindowDrag.Move unless given).
    public static void AttachTitleLine(Window window, Control titleLine, Action eye, Action<PointerPressedEventArgs>? move = null)
    {
        const double dragDistance = 4;
        PointerPressedEventArgs? press = null;
        var start = default(Point);
        titleLine.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(window).Properties.IsLeftButtonPressed
                || titleLine.InputHitTest(e.GetPosition(titleLine), enabledElementsOnly: false) is InputElement { IsEffectivelyEnabled: false })
                return;

            press = e;
            start = e.GetPosition(window);
            e.Pointer.Capture(titleLine);
            e.Handled = true;
        });
        titleLine.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            var travel = e.GetPosition(window) - start;
            if (press is not { } pressed || Math.Abs(travel.X) <= dragDistance && Math.Abs(travel.Y) <= dragDistance)
                return;

            press = null;
            if (move is null)
                Move(window, pressed);
            else
                move(pressed);
        });
        titleLine.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            if (press is null)
                return;

            press = null;
            e.Pointer.Capture(null);
            eye();
        });
        titleLine.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => press = null);
    }

    internal static void SizeToContentAgain(Window window, SizeToContent sizeToContent)
    {
        if (window.SizeToContent == sizeToContent)
            return;

        window.SizeToContent = sizeToContent;
        window.InvalidateMeasure();
    }
}
