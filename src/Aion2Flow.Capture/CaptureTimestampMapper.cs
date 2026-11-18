namespace Cloris.Aion2Flow.Capture;

internal sealed class CaptureTimestampMapper
{
    private readonly TimeProvider _timeProvider;
    private readonly long _originTimestamp;
    private readonly long _originUnixMilliseconds;

    internal CaptureTimestampMapper(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        _originTimestamp = timeProvider.GetTimestamp();
        _originUnixMilliseconds = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
    }

    public long ToTimelineUnixMilliseconds(long captureTimestamp)
    {
        var elapsedMilliseconds = _timeProvider.GetElapsedTime(_originTimestamp, captureTimestamp).Ticks /
            TimeSpan.TicksPerMillisecond;
        return checked(_originUnixMilliseconds + elapsedMilliseconds);
    }

    // Now on this clock: what a packet captured at this moment is stamped with.
    public long NowUnixMilliseconds => ToTimelineUnixMilliseconds(_timeProvider.GetTimestamp());

    public long ToCurrentUtcUnixMilliseconds(long captureTimestamp)
    {
        var calibrationTimestamp = _timeProvider.GetTimestamp();
        var calibrationUnixMilliseconds = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var elapsedMilliseconds = _timeProvider.GetElapsedTime(captureTimestamp, calibrationTimestamp).Ticks /
            TimeSpan.TicksPerMillisecond;
        return checked(calibrationUnixMilliseconds - elapsedMilliseconds);
    }
}

// The capture clock as the live scene's clock: its now is the mapper's, which stamps the scene's entries, so the two stay
// together when Windows moves its clock. The rest is the inner clock's (a test's too); the mapper keeps the inner one, as
// ToCurrentUtcUnixMilliseconds needs the real UTC.
internal sealed class CaptureTimeProvider(CaptureTimestampMapper mapper, TimeProvider inner) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(mapper.NowUnixMilliseconds);

    public override long GetTimestamp() => inner.GetTimestamp();

    public override long TimestampFrequency => inner.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        inner.CreateTimer(callback, state, dueTime, period);
}
