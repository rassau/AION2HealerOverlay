using System.IO.Compression;
using System.Reflection;
using System.Text;
using Cloris.Aion2Flow.Capture.Streams;

namespace Cloris.Aion2Flow.Capture.Diagnostics;

// Settings > Debugging: everything the game server sends, reassembled as in stream.log, gzipped into one
// .aion2healdebug file that PacketLogReplayService replays. While off it costs the capture one null check per chunk.
public static class PacketRecording
{
    public const string Extension = ".aion2healdebug";
    private const string FileName = "packet-recording" + Extension;
    private static readonly Lock SyncRoot = new();
    private static volatile StreamWriter? _writer;
    private static string? _path;

    public static bool IsRecording => _writer is not null;

    // Recording, or recorded and not saved yet.
    public static bool HasRecording => _path is not null;

    // Starts over in the given folder (the log folder); the recording stays there until it is saved.
    public static void Start(string directory)
    {
        lock (SyncRoot)
        {
            StopCore();
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, FileName);
            var writer = new StreamWriter(
                new GZipStream(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read), CompressionLevel.Fastest),
                new UTF8Encoding(false),
                64 * 1024);
            writer.WriteLine($"# Aion2Heal {Assembly.GetEntryAssembly()?.GetName().Version} packet recording (stream.log lines), started {DateTimeOffset.Now:O}");
            _path = path;
            _writer = writer;
        }
    }

    public static void Stop()
    {
        lock (SyncRoot)
            StopCore();
    }

    // Ends the recording and copies it to the destination; once that worked, the file in the log folder goes.
    public static async Task SaveToAsync(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        string path;
        lock (SyncRoot)
        {
            StopCore();
            path = _path ?? throw new InvalidOperationException("Nothing has been recorded.");
        }

        await using (var source = File.OpenRead(path))
            await source.CopyToAsync(destination);

        lock (SyncRoot)
        {
            if (_path == path && _writer is null)
            {
                File.Delete(path);
                _path = null;
            }
        }
    }

    internal static void AppendReassembled(in TcpConnection connection, uint sequenceNumber, long timestampMilliseconds, long connectionOrdinal, ReadOnlySpan<byte> payload)
    {
        if (_writer is null)
            return;

        Write(RawPacketDump.FormatReassembled("inbound", in connection, sequenceNumber, timestampMilliseconds, connectionOrdinal, payload));
    }

    internal static void AppendTransportClose(in TcpConnection connection, long timestampMilliseconds, long connectionOrdinal)
    {
        if (_writer is null || connectionOrdinal <= 0)
            return;

        Write(RawPacketDump.FormatTransportClose(in connection, timestampMilliseconds, connectionOrdinal));
    }

    private static void Write(string line)
    {
        lock (SyncRoot)
        {
            try
            {
                _writer?.WriteLine(line);
            }
            catch (IOException)
            {
                // A full disk ends the recording, not the capture.
                StopCore();
            }
        }
    }

    private static void StopCore()
    {
        var writer = _writer;
        _writer = null;
        try
        {
            writer?.Dispose();
        }
        catch (IOException)
        {
        }
    }
}
