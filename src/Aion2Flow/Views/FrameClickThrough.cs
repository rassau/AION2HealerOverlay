using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// How a frame's window takes the mouse while the game has focus (OverlayWindowInputState), the same for the meter, the
// frames and the playback. It's told whether the overlay has focus (SetAppFocused: by the frame's controller,
// FocusClickThrough or the meter) and follows the frame's "Clickable without focus" and "Title line always clickable" as
// they change (its hand, the settings, a scene). While only the title line takes clicks, the window lets the mouse through
// only while the cursor is over the body below it (a folded frame, FrameChrome's "minimized", has none; the folded meter
// keeps its bottom bar): the cursor coming over the body (a move, or the frame growing, unfolding or moving under it) lets
// the mouse through, and as the window then hears nothing of the mouse, the cursor is watched for leaving the body.
internal sealed class FrameClickThrough
{
    private static readonly ConditionalWeakTable<Window, FrameClickThrough> ByWindow = new();
    private readonly Window _window;
    private readonly SettingsFlyoutViewModel _settings;
    private readonly string _frame;
    private readonly Control? _root;
    private readonly Control? _titleLine;
    private readonly DispatcherTimer _cursorWatch = new() { Interval = TimeSpan.FromMilliseconds(33) };
    // Until the window says otherwise the overlay has focus: nothing lets the mouse through.
    private bool _appFocused = true;
    private bool _checkQueued;

    private FrameClickThrough(Window window, SettingsFlyoutViewModel settings, string frame, Control? root, Control? titleLine)
    {
        _window = window;
        _settings = settings;
        _frame = frame;
        _root = root;
        _titleLine = titleLine;
        _cursorWatch.Tick += (_, _) => WatchCursor();
        settings.PropertyChanged += OnSettingsChanged;
        if (root is not null)
        {
            root.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, handledEventsToo: true);
            window.SizeChanged += OnGeometryChanged;
            window.PositionChanged += OnGeometryChanged;
            window.ScalingChanged += OnGeometryChanged;
        }

        window.Closed += (_, _) =>
        {
            _cursorWatch.Stop();
            settings.PropertyChanged -= OnSettingsChanged;
        };
    }

    public OverlayWindowInputState State { get; private set; }

    // Whether the cursor is being watched (the timer runs); for the tests.
    internal bool WatchesCursor => _cursorWatch.IsEnabled;

    // root and titleLine: the frame and its title line; without them the mouse goes through all of the window (the
    // playback, with Windows' own title bar).
    public static FrameClickThrough Attach(Window window, SettingsFlyoutViewModel settings, string frame, Control? root = null, Control? titleLine = null)
    {
        var clickThrough = new FrameClickThrough(window, settings, frame, root, titleLine);
        ByWindow.AddOrUpdate(window, clickThrough);
        return clickThrough;
    }

    // The window's, once attached.
    public static FrameClickThrough? Of(Window window) => ByWindow.TryGetValue(window, out var clickThrough) ? clickThrough : null;

    // appFocused: the overlay has focus (this window or another of its windows is in front).
    public void SetAppFocused(bool appFocused)
    {
        _appFocused = appFocused;
        Apply();
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsFlyoutViewModel.ClickableWithoutFocus) or nameof(SettingsFlyoutViewModel.TitleClickable))
            Apply();
    }

    private void Apply()
    {
        var clickThrough = !_appFocused && !_settings.IsClickableWithoutFocus(_frame);
        var titleClickable = _titleLine is not null && _settings.IsTitleClickable(_frame);
        // Once only the title line takes clicks, the cursor moves the window between armed and active.
        var overBody = clickThrough && titleClickable && (State is OverlayWindowInputState.ClickThroughArmed or OverlayWindowInputState.ClickThroughActive
            ? State == OverlayWindowInputState.ClickThroughActive
            : IsCursorOverBody());
        Enter(OverlayWindowInputStateLogic.Enter(clickThrough, titleClickable, overBody));
    }

    // A state whose window style couldn't be set isn't taken: the next change tries again.
    private void Enter(OverlayWindowInputState state)
    {
        if (!NativeOverlayWindowStyles.SetInputTransparent(_window, state.RequiresInputTransparency()))
            return;

        State = state;
        _cursorWatch.IsEnabled = state.ShouldPollCursor();
    }

    // The cursor came over the body, from outside or from the title line (no PointerEntered then): the mouse goes through.
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (State == OverlayWindowInputState.ClickThroughArmed && IsOverBody(e.GetPosition(_root)))
            Enter(OverlayWindowInputState.ClickThroughActive);
    }

    // The frame grew, shrank, folded, unfolded or moved under the cursor: looked at once it's laid out.
    private void OnGeometryChanged(object? sender, EventArgs e)
    {
        if (_checkQueued || State != OverlayWindowInputState.ClickThroughArmed)
            return;

        _checkQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _checkQueued = false;
            if (State == OverlayWindowInputState.ClickThroughArmed && IsCursorOverBody())
                Enter(OverlayWindowInputState.ClickThroughActive);
        }, DispatcherPriority.Loaded);
    }

    // The window lets the mouse through and hears nothing of it: the cursor leaving the body (for the title line too) makes
    // it take clicks again.
    internal void WatchCursor()
    {
        if (State == OverlayWindowInputState.ClickThroughActive && !IsCursorOverBody())
            Enter(OverlayWindowInputState.ClickThroughArmed);
    }

    // No cursor (or no window) counts as elsewhere: rather a title line that takes clicks than one that can't.
    private bool IsCursorOverBody() =>
        _window.PlatformImpl is not null && NativeOverlayWindowStyles.TryGetCursorPosition(out var cursor) && IsOverBody(cursor);

    // A point on the screen (in pixels) over the body as the frame is laid out: the screen's DPI, the UI scale and the
    // frame's own size go into where that is.
    internal bool IsOverBody(PixelPoint point) =>
        _root is not null && _window.TranslatePoint(_window.PointToClient(point), _root) is { } inFrame && IsOverBody(inFrame);

    private bool IsOverBody(Point inFrame) =>
        _root is not null && _titleLine is not null && !_root.Classes.Contains("minimized")
        && _titleLine.TranslatePoint(new Point(0, _titleLine.Bounds.Height), _root) is { } titleBottom
        && OverlayWindowInputStateLogic.IsOverBody(_root.Bounds.Size, titleBottom.Y, inFrame);
}
