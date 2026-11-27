using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cloris.Aion2Flow.Services.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Diagnostics.Etw;

namespace Cloris.Aion2Flow.Services;

// The game's frames per second, counted the way the Xbox Game Bar and PresentMon count them: Windows' DXGI provider reports
// every frame a program hands over to be shown (Present), and a trace session of the overlay's own takes in the game's.
// Nothing in the game is touched or read. A session needs administrator rights (or the Performance Log Users group), and
// the overlay runs as administrator; refused once, it stays off. One session per run, so all of it is static (the event
// callback has no instance).
internal static unsafe class GameFrameRate
{
    private const string SessionName = "Aion2Heal frame rate";
    private static readonly Guid DxgiProvider = new("ca11c036-0102-4a2d-a6ad-f03cfed5d3c9");
    // DXGI's "Events" keyword (Objects and JournalEntries stay off); Present's start (event 42) is a frame handed over.
    private const ulong EventsKeyword = 0x2;
    private const ushort PresentStart = 42;
    private const uint EnableProvider = 1;
    private const ushort Event32BitHeader = 0x20;
    private const uint PresentTest = 0x1;
    // ponytail: EVENT_TRACE_USE_MS_FLUSH_TIMER, missing from the SDK's evntrace.h (Windows' own ntwmi.h has it): the flush
    // timer in milliseconds. Without it ETW hands the frames over once a second, 1-2 s late even when flushed; a Windows
    // that ignored it would read 50 s, and frames coming later than DeliveryMilliseconds count short.
    private const uint MillisecondFlushTimer = 0x10;
    private const uint FlushMilliseconds = 50;
    // With that the game's frames came within 140 ms (Windows 11, the game at 95 fps): a sample takes the time up to this
    // long ago, all of whose frames are in.
    public const int DeliveryMilliseconds = 250;
    // How far back the 1 % low looks.
    public const int LowMilliseconds = 2_000;
    // The properties with room after them for the two names ETW writes back (1,024 characters each at most).
    private static readonly int PropertiesSize = sizeof(EVENT_TRACE_PROPERTIES) + 2 * 2_048;
    // The game's last presents (Stopwatch ticks): two seconds' worth at 2,000 fps, as far back as the 1 % low looks.
    private static readonly long[] s_presents = new long[4_096];
    private static readonly Lock s_gate = new();
    private static long s_written;
    private static Func<uint, bool> s_isGame = static _ => false;
    private static ulong s_session;
    private static PROCESSTRACE_HANDLE s_trace;
    private static bool s_refused;

    public static bool IsRunning => s_session != 0;

    // Counting from now on (true), or it can't be: the session was refused (no administrator rights, say).
    public static bool Start(Func<uint, bool> isGame)
    {
        if (IsRunning)
            return true;
        if (s_refused)
            return false;

        s_isGame = isGame;
        // A game started anew: its first frames don't go by the last one's (minutes ago).
        lock (s_gate)
            s_written = 0;
        // One left behind by a crash, found by its name.
        StopSession(0);
        var buffer = stackalloc byte[PropertiesSize];
        var properties = Properties(buffer, logFileName: false);
        properties->LogFileMode = PInvoke.EVENT_TRACE_REAL_TIME_MODE | MillisecondFlushTimer;
        properties->FlushTimer = FlushMilliseconds;
        var error = PInvoke.StartTrace(out var session, SessionName, ref *properties);
        if (error == WIN32_ERROR.ERROR_SUCCESS)
        {
            error = PInvoke.EnableTraceEx2(session, DxgiProvider, EnableProvider, (byte)PInvoke.TRACE_LEVEL_INFORMATION, EventsKeyword, 0, 0, null);
            if (error == WIN32_ERROR.ERROR_SUCCESS && Open() is { } trace)
            {
                (s_session, s_trace) = (session, trace);
                new Thread(static () =>
                {
                    var trace = s_trace;
                    PInvoke.ProcessTrace(&trace, 1, null, null);
                }) { IsBackground = true, Name = "Game frame rate" }.Start();
                return true;
            }

            StopSession(session);
        }

        s_refused = true;
        AppLog.Write(AppLogLevel.Warning, $"Game frame rate: no trace session ({error}).");
        return false;
    }

    // The session stopped (ProcessTrace returns and its thread ends).
    public static void Stop()
    {
        if (!IsRunning)
            return;

        StopSession(s_session);
        PInvoke.CloseTrace(s_trace);
        s_session = 0;
    }

    public static double FramesPerSecond(long start, long end, double previous)
    {
        lock (s_gate)
            return FramesPerSecond(s_presents.AsSpan(0, (int)Math.Min(s_written, s_presents.Length)), start, end, previous, Stopwatch.Frequency);
    }

    // Frames per second over (start, end] (ticks): the frames handed over in it, over the time they took from the last one
    // before it. With none in it, the frame still shown has taken all the time since the last: at most one over that, and
    // no more than the last rate (a slow but steady game doesn't look faster between its frames). The presents in any
    // order (they come from each processor's buffer in turn).
    internal static double FramesPerSecond(ReadOnlySpan<long> presents, long start, long end, double previous, long frequency)
    {
        var (frames, latest, before) = (0, long.MinValue, long.MinValue);
        foreach (var at in presents)
        {
            if (at > end)
                continue;
            if (at > start)
                (frames, latest) = (frames + 1, Math.Max(latest, at));
            else
                before = Math.Max(before, at);
        }

        if (frames > 0)
            return frames * (double)frequency / (latest - (before == long.MinValue ? start : before));
        return before == long.MinValue ? 0 : Math.Min(previous, frequency / (double)(end - before));
    }

    public static double OnePercentLow(long start, long end)
    {
        lock (s_gate)
            return OnePercentLow(s_presents.AsSpan(0, (int)Math.Min(s_written, s_presents.Length)), start, end, Stopwatch.Frequency);
    }

    // The 1 % low over (start, end]: the frame rate 99 % of its frames reached, one over the frame time 99 % of them took
    // no longer than (nearest rank). A frame's time runs from the one before it (the first one's from the last before the
    // window), and the frame still shown counts with its time so far, so a stall pulls it down at once.
    internal static double OnePercentLow(ReadOnlySpan<long> presents, long start, long end, long frequency)
    {
        // ponytail: one reused list instead of LINQ, as in PingHistory.Percentile99.
        var times = t_times ??= [];
        times.Clear();
        var before = long.MinValue;
        foreach (var at in presents)
        {
            if (at > start && at <= end)
                times.Add(at);
            else if (at <= start)
                before = Math.Max(before, at);
        }

        if (times.Count == 0 && before == long.MinValue)
            return 0;
        if (before != long.MinValue)
            times.Add(before);
        times.Add(end);
        times.Sort();
        for (var i = 0; i < times.Count - 1; i++)
            times[i] = times[i + 1] - times[i];
        times.RemoveAt(times.Count - 1);
        times.Sort();
        return frequency / (double)Math.Max(1, times[(int)Math.Ceiling(0.99 * times.Count) - 1]);
    }

    [ThreadStatic]
    private static List<long>? t_times;

    private static PROCESSTRACE_HANDLE? Open()
    {
        var logfile = new EVENT_TRACE_LOGFILEW();
        // Time stamps as the session takes them: QueryPerformanceCounter's, Stopwatch's clock.
        logfile.ProcessTraceMode = PInvoke.PROCESS_TRACE_MODE_REAL_TIME | PInvoke.PROCESS_TRACE_MODE_EVENT_RECORD | PInvoke.PROCESS_TRACE_MODE_RAW_TIMESTAMP;
        logfile.Anonymous2.EventRecordCallback = &OnEvent;
        fixed (char* name = SessionName)
        {
            logfile.LoggerName = name;
            var trace = PInvoke.OpenTrace(&logfile);
            return (ulong)trace == ulong.MaxValue ? null : trace;
        }
    }

    // On the session's thread. Present's data: the swap chain (a pointer, 4 bytes in a 32-bit program), its flags and its
    // sync interval; a test present (DXGI_PRESENT_TEST: could a frame be shown?) shows nothing.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnEvent(EVENT_RECORD* record)
    {
        ref readonly var header = ref record->EventHeader;
        var flagsAt = (header.Flags & Event32BitHeader) != 0 ? 4 : 8;
        if (header.EventDescriptor.Id != PresentStart || header.ProviderId != DxgiProvider || record->UserDataLength < flagsAt + 4
            || (*(uint*)((byte*)record->UserData + flagsAt) & PresentTest) != 0 || !s_isGame(header.ProcessId))
            return;

        lock (s_gate)
            s_presents[s_written++ % s_presents.Length] = header.TimeStamp;
    }

    // The session stopped; with none, the one of that name.
    private static void StopSession(ulong session)
    {
        var buffer = stackalloc byte[PropertiesSize];
        PInvoke.ControlTrace(session, session == 0 ? SessionName : null, ref *Properties(buffer, logFileName: true), EVENT_TRACE_CONTROL.EVENT_TRACE_CONTROL_STOP);
    }

    private static EVENT_TRACE_PROPERTIES* Properties(byte* buffer, bool logFileName)
    {
        new Span<byte>(buffer, PropertiesSize).Clear();
        var properties = (EVENT_TRACE_PROPERTIES*)buffer;
        properties->Wnode.BufferSize = (uint)PropertiesSize;
        properties->Wnode.Flags = PInvoke.WNODE_FLAG_TRACED_GUID;
        properties->Wnode.ClientContext = 1;
        properties->LoggerNameOffset = (uint)sizeof(EVENT_TRACE_PROPERTIES);
        if (logFileName)
            properties->LogFileNameOffset = (uint)(sizeof(EVENT_TRACE_PROPERTIES) + 2_048);
        return properties;
    }
}
