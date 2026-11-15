using System.IO.Compression;
using System.Reflection;
using System.Text;
using Cloris.Aion2Flow.Capture.Diagnostics;
using Cloris.Aion2Flow.Capture.Streams;

namespace Cloris.Aion2Flow.Tests.Capture;

public sealed class PacketRecordingTests
{
    [Fact]
    public async Task Records_Only_While_On_And_Saves_A_Gzipped_Stream_Log_The_Replay_Reads()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Aion2Flow.Tests", Guid.NewGuid().ToString("N"));
        var connection = new TcpConnection(0x0100007F, 0x0200007F, 7777, 50000);
        var at = new DateTimeOffset(2026, 11, 5, 23, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        PacketRecording.AppendReassembled(in connection, 1, at, 1, [0x01]);
        Assert.False(PacketRecording.HasRecording);

        PacketRecording.Start(directory);
        Assert.True(PacketRecording.IsRecording);
        PacketRecording.AppendReassembled(in connection, 100, at, 1, [0xAB, 0xCD]);
        PacketRecording.AppendTransportClose(in connection, at + 1_000, 1);

        using var saved = new MemoryStream();
        await PacketRecording.SaveToAsync(saved);
        Assert.False(PacketRecording.IsRecording);
        Assert.False(PacketRecording.HasRecording);
        Assert.Empty(Directory.EnumerateFiles(directory));

        saved.Position = 0;
        using var reader = new StreamReader(new GZipStream(saved, CompressionMode.Decompress));
        var lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("# Aion2Heal", lines[0]);
        Assert.EndsWith("|seq=100|attempt=1|len=2|data=ABCD", lines[1]);
        Assert.Contains("event=transport-close", lines[2]);

        // Only the header is no packet line.
        var replay = PacketLogReplayService.Replay(new StringReader(string.Join('\n', lines)), "recording");
        Assert.Equal(3, replay.TotalLines);
        Assert.Equal(1, replay.SkippedEventCounts.GetValueOrDefault("<invalid>"));

        // The saved file replays straight from disk.
        var file = Path.Combine(directory, "saved" + PacketRecording.Extension);
        await File.WriteAllBytesAsync(file, saved.ToArray(), TestContext.Current.CancellationToken);
        Assert.Equal(3, PacketLogReplayService.Replay(file).TotalLines);
    }

    // A recording reaches the disk while it runs, not only when it stops: a crash or a kill (nothing ends the file then)
    // loses about the last second.
    [Fact]
    public async Task A_Running_Recording_Is_On_Disk_Within_A_Second_Or_So()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Aion2Flow.Tests", Guid.NewGuid().ToString("N"));
        var connection = new TcpConnection(0x0100007F, 0x0200007F, 7777, 50000);
        var at = new DateTimeOffset(2026, 11, 5, 23, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        PacketRecording.Start(directory);
        try
        {
            PacketRecording.AppendReassembled(in connection, 100, at, 1, [0xAB, 0xCD]);
            var file = Path.Combine(directory, "packet-recording" + PacketRecording.Extension);
            var onDisk = "";
            for (var waited = 0; waited < 5_000 && !onDisk.Contains("data=ABCD"); waited += 100)
            {
                await Task.Delay(100, TestContext.Current.CancellationToken);
                onDisk = ReadWithoutItsEnd(file);
            }

            Assert.StartsWith("# Aion2Heal", onDisk);
            Assert.Contains("|seq=100|attempt=1|len=2|data=ABCD", onDisk);
        }
        finally
        {
            // Saved, not only stopped: a stopped recording is still one (HasRecording), which a test after this one may not expect.
            await PacketRecording.SaveToAsync(Stream.Null);
            Directory.Delete(directory, recursive: true);
        }
    }

    // The flush runs on a timer thread, where an exception ends the overlay: a write an anti-virus filter denies ends the
    // recording as a full disk does, also as closing the file is denied the same way.
    [Fact]
    public async Task A_Write_Denied_On_The_Timer_Ends_The_Recording_Not_The_Overlay()
    {
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
        var directory = Path.Combine(Path.GetTempPath(), "Aion2Flow.Tests", Guid.NewGuid().ToString("N"));
        var connection = new TcpConnection(0x0100007F, 0x0200007F, 7777, 50000);
        var writer = typeof(PacketRecording).GetField("_writer", Hidden)!;
        PacketRecording.Start(directory);
        try
        {
            using (((Lock)typeof(PacketRecording).GetField("SyncRoot", Hidden)!.GetValue(null)!).EnterScope())
            {
                ((StreamWriter)writer.GetValue(null)!).Dispose();
                writer.SetValue(null, new StreamWriter(new GZipStream(new DeniedStream(), CompressionLevel.Fastest), new UTF8Encoding(false), 64 * 1024));
            }

            PacketRecording.AppendReassembled(in connection, 100, 0, 1, [0xAB, 0xCD]);
            typeof(PacketRecording).GetMethod("Flush", Hidden)!.Invoke(null, null);

            Assert.False(PacketRecording.IsRecording);
        }
        finally
        {
            writer.SetValue(null, null);
            await PacketRecording.SaveToAsync(Stream.Null);
            Directory.Delete(directory, recursive: true);
        }
    }

    // On the packet dispatch thread a denied write ends the recording too, and throws nothing: the dispatcher hands the
    // packet on to the meter right after.
    [Fact]
    public void A_Write_Denied_On_The_Capture_Thread_Ends_The_Recording_Not_The_Packet()
    {
        var writer = typeof(PacketRecording).GetField("_writer", BindingFlags.NonPublic | BindingFlags.Static)!;
        var connection = new TcpConnection(0x0100007F, 0x0200007F, 7777, 50000);
        writer.SetValue(null, new DeniedWriter());
        try
        {
            PacketRecording.AppendReassembled(in connection, 100, 0, 1, [0xAB, 0xCD]);

            Assert.False(PacketRecording.IsRecording);
        }
        finally
        {
            writer.SetValue(null, null);
        }
    }

    // As a file stream: writes go to its buffer, and writing that to the file is denied, on every try.
    private sealed class DeniedStream : MemoryStream
    {
        public override void Flush() => throw new UnauthorizedAccessException();
    }

    // As a writer whose full buffer goes to a file that is denied.
    private sealed class DeniedWriter() : StreamWriter(Stream.Null)
    {
        public override void WriteLine(string? value) => throw new UnauthorizedAccessException();
    }

    // What a reader finds while the recording still runs, as after a kill.
    private static string ReadWithoutItsEnd(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(new GZipStream(file, CompressionMode.Decompress));
        try
        {
            return reader.ReadToEnd();
        }
        catch (InvalidDataException)
        {
            return "";
        }
    }
}
