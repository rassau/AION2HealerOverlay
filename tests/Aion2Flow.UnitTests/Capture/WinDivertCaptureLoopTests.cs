using System.Reflection;
using System.Runtime.CompilerServices;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Logging;
using Cloris.Aion2Flow.WinDivert;

namespace Cloris.Aion2Flow.Tests.Capture;

// The log fact sets the process-wide AppLog writer, so no other test may run beside this collection.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppLogTestCollection
{
    public const string Name = "App log";
}

[Collection(AppLogTestCollection.Name)]
public sealed class WinDivertCaptureLoopTests
{
    [Fact]
    public async Task A_Receive_That_Keeps_Throwing_Ends_The_Loop_At_The_Breaker()
    {
        await using var ports = new ProcessPortDiscoveryService();
        var capture = new WinDivertCaptureService(ports);

        Assert.True(RunCaptureLoop(capture));
        Assert.True(capture.HasDriverError);
        Assert.StartsWith("Capture loop error after NullReferenceException", capture.LastStatusMessage);
    }

    // The first skip is logged at once, the 1,000th failure in a row ends the loop, and the 998 skips in between, not
    // reported yet by the 10 s rate limit, are logged when it ends.
    [Fact]
    public async Task The_Loop_Skips_Then_Breaks_And_Flushes_The_Count()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aion2flow-applog-{Guid.NewGuid():N}");
        try
        {
            var writer = new AppLogWriter(AppLogLevel.Error, directory);
            AppLog.Initialize(writer);
            try
            {
                await using var ports = new ProcessPortDiscoveryService();
                Assert.True(RunCaptureLoop(new WinDivertCaptureService(ports)));
            }
            finally
            {
                // The tests run without a writer.
                AppLog.Initialize(null!);
                writer.Dispose();
            }

            Assert.Collection(
                File.ReadAllLines(Path.Combine(directory, "app.log"))
                    .Where(line => line.Contains("] Capture ", StringComparison.Ordinal))
                    .Select(line => line[(line.IndexOf("] Capture ", StringComparison.Ordinal) + 2)..]),
                line => Assert.StartsWith("Capture skipped 1 packet(s), the last after NullReferenceException", line),
                line => Assert.StartsWith("Capture loop error after NullReferenceException", line),
                line => Assert.StartsWith("Capture skipped 998 packet(s), the last after NullReferenceException", line));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Runs the capture loop, a local function of StartAsync, without the WinDivert driver: a session without a handle
    // throws a NullReferenceException on every Receive, before WinDivert is called. True if the loop ended within 10 s.
    private static bool RunCaptureLoop(WinDivertCaptureService capture)
    {
        var type = typeof(WinDivertCaptureService);
        var ctsField = type.GetField("_cts", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var divertField = type.GetField("_divert", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var worker = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(method => method.Name.Contains("DivertCaptureWorker", StringComparison.Ordinal));
        using var cts = new CancellationTokenSource();
        ctsField.SetValue(capture, cts);
        divertField.SetValue(capture, RuntimeHelpers.GetUninitializedObject(typeof(WinDivertSession)));
        try
        {
            var run = Task.Run(() => worker.Invoke(capture, null));
            var ended = run.Wait(TimeSpan.FromSeconds(10));
            cts.Cancel();
            run.Wait();
            return ended;
        }
        finally
        {
            // A later StopAsync must not reach the session without a handle.
            divertField.SetValue(capture, null);
            ctsField.SetValue(capture, null);
        }
    }
}
