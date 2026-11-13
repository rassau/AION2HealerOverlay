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

    internal static void SizeToContentAgain(Window window, SizeToContent sizeToContent)
    {
        if (window.SizeToContent == sizeToContent)
            return;

        window.SizeToContent = sizeToContent;
        window.InvalidateMeasure();
    }
}
