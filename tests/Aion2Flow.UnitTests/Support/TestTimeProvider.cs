namespace Cloris.Aion2Flow.Tests.Support;

// A clock a test holds. Its timestamp (TimeProvider.GetTimestamp: the monotonic clock the scene's running time goes by)
// moves with its time, so a clock held still holds both still; TimeProvider's own is the system's, which runs on.
internal abstract class TestTimeProvider : TimeProvider
{
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => GetUtcNow().UtcTicks;
}
