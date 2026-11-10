using System.IO.Compression;
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
}
