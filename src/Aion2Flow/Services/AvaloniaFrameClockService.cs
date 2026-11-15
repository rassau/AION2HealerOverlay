using Avalonia.Controls;

namespace Cloris.Aion2Flow.Services;

public sealed class AvaloniaFrameClockService : IDisposable
{
    private readonly List<TopLevel> _topLevels = [];
    // One callback and one args for every frame: the clock runs on every display frame for the whole session.
    private readonly Action<TimeSpan> _onAnimationFrame;
    private readonly AvaloniaFrameEventArgs _args = new(TimeSpan.Zero);
    private bool _isRunning;
    private bool _isDisposed;
    private bool _frameRequested;
    private TopLevel? _activeTopLevel;

    public AvaloniaFrameClockService() => _onAnimationFrame = OnAnimationFrame;

    public event EventHandler<AvaloniaFrameEventArgs>? Frame;

    public event EventHandler<AvaloniaFrameEventArgs>? FrameCompleted;

    public void Attach(TopLevel topLevel)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!_topLevels.Contains(topLevel))
            _topLevels.Add(topLevel);
        _activeTopLevel = topLevel;
        if (_isRunning)
            RequestNextFrame();
        else
            StartLoopIfNeeded();
    }

    public void Detach(TopLevel topLevel)
    {
        _topLevels.Remove(topLevel);
        if (_activeTopLevel == topLevel)
            _activeTopLevel = null;
        if (_topLevels.Count == 0)
            _isRunning = false;
        else if (_isRunning)
            RequestNextFrame();
    }

    public void Dispose()
    {
        _isDisposed = true;
        _isRunning = false;
        _activeTopLevel = null;
        _topLevels.Clear();
    }

    private void StartLoopIfNeeded()
    {
        if (_isRunning || _topLevels.Count == 0)
            return;

        _isRunning = true;
        RequestNextFrame();
    }

    private void OnAnimationFrame(TimeSpan timestamp)
    {
        // First, so that a loop stopped (and started again later) can always ask for its next frame.
        _frameRequested = false;
        if (!_isRunning || _isDisposed)
            return;

        try
        {
            _args.Timestamp = timestamp;
            Frame?.Invoke(this, _args);
            FrameCompleted?.Invoke(this, _args);
        }
        finally
        {
            if (_isRunning && !_isDisposed && _topLevels.Count > 0)
                RequestNextFrame();
            else
                _isRunning = false;
        }
    }

    private void RequestNextFrame()
    {
        var topLevel = ResolveTopLevel();
        if (topLevel is null)
        {
            _isRunning = false;
            return;
        }

        // ponytail: one request waiting is enough, as Avalonia 12 queues RequestAnimationFrame callbacks for all windows
        // alike (MediaContext) and runs every one on the next pulse, a closed window's too; check again on an upgrade.
        if (_frameRequested)
            return;

        // Set once asked (it never calls back at once): a request that throws leaves the next Attach free to ask again.
        topLevel.RequestAnimationFrame(_onAnimationFrame);
        _frameRequested = true;
    }

    private TopLevel? ResolveTopLevel()
    {
        if (_activeTopLevel is not null && _topLevels.Contains(_activeTopLevel))
            return _activeTopLevel;

        _activeTopLevel = _topLevels.Count > 0 ? _topLevels[^1] : null;
        return _activeTopLevel;
    }
}

public sealed class AvaloniaFrameEventArgs(TimeSpan timestamp) : EventArgs
{
    // The clock hands every frame the same instance, set anew; no handler keeps it.
    public TimeSpan Timestamp { get; internal set; } = timestamp;
}
