using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Cloris.Aion2Flow.Services.Overlay;

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
        if (InComboBox(e.Source))
            return;

        var sizeToContent = window.SizeToContent;
        window.BeginMoveDrag(e);
        SizeToContentAgain(window, sizeToContent);
    }

    // A drop-down box takes a press itself only on its arrow (Simple theme); a press anywhere else on it goes on to the
    // window, and the box opens on the release, which a move loop begun on that press would swallow (the twin library's
    // boxes opened only from their arrow). Such a press doesn't move the window.
    internal static bool InComboBox(object? source) => (source as Visual)?.FindAncestorOfType<ComboBox>(includeSelf: true) is not null;

    // A frame's title line: a click does what the frame's eye does (folds the frame or brings it back), a drag moves the
    // frame. Avalonia only posts the move loop (it starts once the press is handled), so the window's position can't tell
    // the two apart: the line keeps the pointer, a move of more than a few pixels before the button comes up starts the
    // drag (with the press: Windows' move loop wants the button still down), and a release before that is the click. The
    // click that brings the overlay to the front from another program (the game) only does that (FocusPress): the meter's
    // title line is where players give the overlay focus.
    // move: how the window moves (WindowDrag.Move unless given).
    public static void AttachTitleLine(Window window, Control titleLine, Action eye, Action<PointerPressedEventArgs>? move = null)
    {
        const double dragDistance = 4;
        PointerPressedEventArgs? press = null;
        var start = default(Point);
        var focusPress = FocusPress.Of(window);
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
            if (!focusPress.GaveFocus)
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

// Whether the press under way brought the overlay to the front from another program, from its window's messages. Windows
// activates a window before the press reaches it (WM_MOUSEACTIVATE, WM_ACTIVATE, then the button-down), so by the press the
// overlay has the foreground either way. WM_MOUSEACTIVATE comes first, only to a window that isn't active yet, while the
// foreground is still where it was: whether that was another program is noted then and taken over by the button-down that
// follows. A press on the active window gets no WM_MOUSEACTIVATE, so it didn't.
internal sealed class FocusPress
{
    private const uint WmMouseActivate = 0x0021;
    private static readonly ConditionalWeakTable<Window, FocusPress> ByWindow = new();
    private bool _activating;

    public bool GaveFocus { get; private set; }

    // The window's, from its messages until it closes: its title line and its hand go by the same one.
    public static FocusPress Of(Window window) => ByWindow.GetValue(window, static window =>
    {
        var focusPress = new FocusPress();
        Win32Properties.CustomWndProcHookCallback hook = (nint _, uint msg, nint _, nint _, ref bool _) =>
        {
            focusPress.Message(msg, NativeOverlayWindowStyles.IsCurrentProcessForeground);
            return 0;
        };
        Win32Properties.AddWndProcHookCallback(window, hook);
        window.Closed += (_, _) => Win32Properties.RemoveWndProcHookCallback(window, hook);
        return focusPress;
    });

    // msg: a message to the window, before Avalonia handles it. overlayInFront: whether one of the overlay's windows has the
    // foreground; asked only at WM_MOUSEACTIVATE.
    public void Message(uint msg, Func<bool> overlayInFront)
    {
        if (msg == WmMouseActivate)
            _activating = !overlayInFront();
        // WM_LBUTTONDOWN, WM_RBUTTONDOWN, WM_MBUTTONDOWN, WM_XBUTTONDOWN (the ones Avalonia makes mouse presses of).
        // ponytail: mouse only; a touch or pen tap (WM_POINTERACTIVATE, WM_POINTERDOWN) still folds or switches the hand,
        // add those if it matters.
        else if (msg is 0x0201 or 0x0204 or 0x0207 or 0x020B)
            (GaveFocus, _activating) = (_activating, false);
    }
}
