using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Cloris.Aion2Flow.Services.Overlay;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class OverlayWindowInputStateTests
{
    // A frame takes clicks unless the game has focus and it isn't clickable without focus. Then the mouse goes through all of
    // its window, or with its title line taking clicks only while the cursor is over its body (watched for leaving it).
    [Theory]
    [InlineData(false, false, false, (int)OverlayWindowInputState.Interactive, false, false)]
    [InlineData(false, true, true, (int)OverlayWindowInputState.Interactive, false, false)]
    [InlineData(true, false, false, (int)OverlayWindowInputState.ClickThrough, true, false)]
    [InlineData(true, false, true, (int)OverlayWindowInputState.ClickThrough, true, false)]
    [InlineData(true, true, false, (int)OverlayWindowInputState.ClickThroughArmed, false, false)]
    [InlineData(true, true, true, (int)OverlayWindowInputState.ClickThroughActive, true, true)]
    public void WindowInputState_MapsClickThroughTitleLineAndCursorOverBody(
        bool clickThrough,
        bool titleClickable,
        bool isCursorOverBody,
        int expected,
        bool requiresInputTransparency,
        bool shouldPollCursor)
    {
        var state = OverlayWindowInputStateLogic.Enter(clickThrough, titleClickable, isCursorOverBody);

        Assert.Equal((OverlayWindowInputState)expected, state);
        Assert.Equal(requiresInputTransparency, state.RequiresInputTransparency());
        Assert.Equal(shouldPollCursor, state.ShouldPollCursor());
    }

    // The body starts where the title line ends and ends before the frame's right and bottom edge (a frame 300 by 120 with a
    // title line of 20, in its own units).
    [Theory]
    [InlineData(50, 19.9, false)] // the title line's last row
    [InlineData(50, 20, true)] // the body's first row
    [InlineData(0, 60, true)]
    [InlineData(299.9, 119.9, true)]
    [InlineData(300, 60, false)] // right of it
    [InlineData(50, 120, false)] // below it
    [InlineData(-0.1, 60, false)] // left of it
    public void The_Body_Is_The_Frame_Below_Its_Title_Line(double x, double y, bool expected) =>
        Assert.Equal(expected, OverlayWindowInputStateLogic.IsOverBody(new Size(300, 120), titleBottom: 20, new Point(x, y)));

    [Fact]
    public void NativeWindowStyles_ToggleInputTransparencyOnRealHwnd()
    {
        AvaloniaTestHost.Run(() =>
        {
            var window = new Window
            {
                Width = 120,
                Height = 80,
                ShowActivated = false,
                ShowInTaskbar = false
            };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Assert.NotEqual(0, window.TryGetPlatformHandle()?.Handle ?? 0);
                Assert.True(NativeOverlayWindowStyles.TryGetInputTransparentStyles(window, out var wasLayered, out _));
                Assert.True(NativeOverlayWindowStyles.SetInputTransparent(window, true));
                Assert.True(NativeOverlayWindowStyles.TryGetInputTransparentStyles(window, out var isLayered, out var isTransparent));
                Assert.True(isLayered);
                Assert.True(isTransparent);
                Assert.True(NativeOverlayWindowStyles.SetInputTransparent(window, false));
                Assert.True(NativeOverlayWindowStyles.TryGetInputTransparentStyles(window, out isLayered, out isTransparent));
                Assert.Equal(wasLayered, isLayered);
                Assert.False(isTransparent);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    // The meter's ping graph is a popup in a window of its own: it goes on top with the meter, or the game covers it.
    [Fact]
    public void NativeWindowStyles_PutAPopupsOwnWindowIntoTheTopmostBand()
    {
        AvaloniaTestHost.Run(() =>
        {
            var anchor = new Border { Width = 120, Height = 80 };
            var popup = new Popup { PlacementTarget = anchor, IsLightDismissEnabled = false, Child = new Border { Width = 60, Height = 40 } };
            var window = new Window { Content = new Panel { Children = { anchor, popup } }, ShowActivated = false, ShowInTaskbar = false };

            try
            {
                window.Show();
                popup.IsOpen = true;
                Dispatcher.UIThread.RunJobs();

                var host = TopLevel.GetTopLevel(popup.Child);
                Assert.NotNull(host);
                Assert.NotSame(window, host);
                Assert.True(NativeOverlayWindowStyles.SetTopmostBand(host, true));
                Assert.True(NativeOverlayWindowStyles.SetTopmostBand(host, false));
            }
            finally
            {
                popup.IsOpen = false;
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    [Fact]
    public void NativeWindowStyles_ReapplyInputTransparencyAfterWindowRestore()
    {
        AvaloniaTestHost.Run(() =>
        {
            var window = new Window
            {
                Width = 120,
                Height = 80,
                ShowActivated = false,
                ShowInTaskbar = false
            };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.True(NativeOverlayWindowStyles.SetInputTransparent(window, true));

                window.WindowState = WindowState.Minimized;
                Dispatcher.UIThread.RunJobs();
                window.WindowState = WindowState.Normal;
                Dispatcher.UIThread.RunJobs();

                Assert.True(NativeOverlayWindowStyles.SetInputTransparent(window, true));
                Assert.True(NativeOverlayWindowStyles.TryGetInputTransparentStyles(window, out var isLayered, out var isTransparent));
                Assert.True(isLayered);
                Assert.True(isTransparent);
                Assert.True(NativeOverlayWindowStyles.SetInputTransparent(window, false));
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
}
