using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Cloris.Aion2Flow.Capture.Streams;

using static Cloris.Aion2Flow.Tests.Capture.TransportTestData;

namespace Cloris.Aion2Flow.Tests.Capture;

// The boundary scan of a candidate stream remembers what it learned between appends. These tests hold it to the
// full scan from offset 0 that it replaced, after every append and every consumption, on many kinds of streams.
public sealed class LengthPrefixedBoundaryScanTests
{
    private const long CaptureMilliseconds = 1_800_000_000_000;

    public enum StreamKind
    {
        RandomBytes,
        TlsRecords,
        HttpText,
        DenseSmallIntegers,
        GameFrames,
        GameFramesInEnvelopes,
        FakeHeadersAndTicks,
        RecordedGame,
        RecordedGameInEnvelopes
    }

    [Theory]
    [InlineData(StreamKind.RandomBytes)]
    [InlineData(StreamKind.TlsRecords)]
    [InlineData(StreamKind.HttpText)]
    [InlineData(StreamKind.DenseSmallIntegers)]
    [InlineData(StreamKind.GameFrames)]
    [InlineData(StreamKind.GameFramesInEnvelopes)]
    [InlineData(StreamKind.FakeHeadersAndTicks)]
    [InlineData(StreamKind.RecordedGame)]
    [InlineData(StreamKind.RecordedGameInEnvelopes)]
    public void IncrementalScanMatchesTheFullScanAfterEveryAppendAndConsumption(StreamKind kind)
    {
        var random = new Random(17 + (int)kind);
        var limit = kind == StreamKind.DenseSmallIntegers ? 8 * 1024 : 16 * 1024;
        var steps = 0;
        for (var run = 0; run < 32; run++)
        {
            var stream = CreateStream(kind, random, limit);
            var position = random.Next(stream.Length / 2);
            var scan = new LengthPrefixedBoundaryScan();
            var claims = new Dictionary<int, (PacketLengthPrefixedProbe Result, long StableBelow)>();
            var bufferStart = position;
            var bufferEnd = position;
            var consumed = 0L;
            var tinyAppends = 0;
            while (bufferEnd < stream.Length)
            {
                // Runs of 1-3 byte appends land on the exact lengths where a probe result changes.
                if (tinyAppends == 0 && random.Next(12) == 0)
                {
                    tinyAppends = random.Next(20, 80);
                }

                var length = tinyAppends > 0 ? random.Next(1, 4) : NextSegmentLength(random);
                tinyAppends = Math.Max(0, tinyAppends - 1);
                bufferEnd = Math.Min(stream.Length, bufferEnd + length);
                var payload = stream.AsSpan(bufferStart, bufferEnd - bufferStart);
                Assert.Equal(
                    FullScan(payload, PacketTransportCodec.MaximumEnvelopeBodyLength),
                    PacketTransportCodec.ProbeLengthPrefixedStreamBoundary(payload, scan, consumed));
                CheckStableClaims(stream, bufferStart, bufferEnd, claims);
                steps++;

                var action = random.Next(100);
                if (action < 6)
                {
                    var count = random.Next(bufferEnd - bufferStart + 1);
                    bufferStart += count;
                    consumed += count;
                }
                else if (action == 6)
                {
                    // A faulted deframer clears its buffer without consuming (and takes no more bytes after that).
                    bufferStart = bufferEnd;
                    Assert.Equal(
                        PacketLengthPrefixedBoundaryProbe.None,
                        PacketTransportCodec.ProbeLengthPrefixedStreamBoundary([], scan, consumed));
                }
            }
        }

        Assert.True(steps > 40, $"only {steps} steps");
    }

    [Theory]
    [InlineData(StreamKind.RandomBytes)]
    [InlineData(StreamKind.TlsRecords)]
    [InlineData(StreamKind.HttpText)]
    [InlineData(StreamKind.DenseSmallIntegers)]
    [InlineData(StreamKind.GameFrames)]
    [InlineData(StreamKind.GameFramesInEnvelopes)]
    [InlineData(StreamKind.FakeHeadersAndTicks)]
    [InlineData(StreamKind.RecordedGame)]
    [InlineData(StreamKind.RecordedGameInEnvelopes)]
    public void ClassifierDecidesAsWithAFreshScanOnEveryAppend(StreamKind kind)
    {
        var random = new Random(101 + (int)kind);
        var limit = kind == StreamKind.DenseSmallIntegers ? 40 * 1024 : 192 * 1024;
        for (var run = 0; run < 12; run++)
        {
            var stream = CreateStream(kind, random, limit);
            var position = run % 3 == 0 ? 0 : random.Next(stream.Length / 2);
            using var incremental = new TcpWorldStreamClassifier(allowMidstreamRecovery: true);
            using var fresh = new TcpWorldStreamClassifier(allowMidstreamRecovery: true);
            while (position < stream.Length)
            {
                var length = Math.Min(stream.Length - position, NextSegmentLength(random));
                var segment = stream.AsSpan(position, length);
                var captureMilliseconds = LatestTickTime(stream.AsSpan(0, position + length));
                BoundaryScan(fresh) = null;
                var expected = fresh.Append(segment, captureMilliseconds);
                Assert.Equal(expected, incremental.Append(segment, captureMilliseconds));
                Assert.Equal(fresh.ReplayStartByteOffset, incremental.ReplayStartByteOffset);
                Assert.Equal(fresh.HasProtocolEvidence, incremental.HasProtocolEvidence);
                position += length;
                if (expected != TcpWorldStreamClassification.Pending)
                {
                    break;
                }
            }
        }
    }

    [Fact]
    public void OffsetWhoseProbeIsInvalidUntilATickArrivesIsProbedAgain()
    {
        // A 10-byte "envelope" ending inside a gameplay frame, then a tick. With one byte of the tick the probe at
        // offset 0 is Invalid (its next header is frame bytes); with the whole tick it is Ambiguous, and the stream
        // starts at the envelope body, not at the tick.
        var timestamp = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(timestamp, CaptureMilliseconds);
        var stream = Concat(
            [0x0a, 0x00, 0x00, 0x00],
            BuildFrame(0x15, 0x36, Enumerable.Repeat((byte)0xee, 20).ToArray()),
            BuildFrame(0x00, 0x36, timestamp));
        Assert.Equal(
            PacketLengthPrefixedProbeKind.Invalid,
            PacketTransportCodec.ProbeLengthPrefixedStream(stream.AsSpan(0, 28), PacketTransportCodec.MaximumEnvelopeBodyLength).Kind);

        using var classifier = new TcpWorldStreamClassifier(allowMidstreamRecovery: true);
        Assert.Equal(TcpWorldStreamClassification.Pending, classifier.Append(stream.AsSpan(0, 28), CaptureMilliseconds));
        Assert.Equal(TcpWorldStreamClassification.Confirmed, classifier.Append(stream.AsSpan(28), CaptureMilliseconds));
        Assert.Equal(4, classifier.ReplayStartByteOffset);
    }

    [Fact]
    public void CompactionKeepsOffsetZeroWhileItWaitsForItsBody()
    {
        // Offset 0 starts a 3000-byte envelope. Inside it, every 9 bytes, a 1-byte envelope runs into an out-of-range
        // header, so most remembered offsets are forgotten and the list is compacted while offset 0 still waits.
        var stream = new byte[3136];
        BinaryPrimitives.WriteInt32LittleEndian(stream, 3000);
        for (var offset = 4; offset + 9 <= 3004; offset += 9)
        {
            stream[offset] = 0x01;
            stream[offset + 4] = 0x10;
            stream.AsSpan(offset + 5, 4).Fill(0xff);
        }

        BinaryPrimitives.WriteInt32LittleEndian(stream.AsSpan(3004), 16);
        CompareByteByByte(stream, 1);
    }

    [Fact]
    public void BodyIncompletePromiseEndsWhereTheBodyEnds()
    {
        // One envelope whose body is one complete gameplay frame: NeedMore until one byte short, Complete at the
        // body's end. A promise that ends one byte later would still report Pending there.
        var stream = BuildLengthPrefixedEnvelope(BuildFrame(0x15, 0x36, new byte[20]));
        CompareByteByByte(stream, stream.Length - 3);
        Assert.Equal(
            PacketLengthPrefixedBoundaryProbeKind.Complete,
            FullScan(stream, PacketTransportCodec.MaximumEnvelopeBodyLength).Kind);
    }

    [Fact]
    public void DirectTickNeedMoreHoldsNoPromise()
    {
        // A 23-byte envelope ends 1 byte before the tick that crosses it does: NeedMore until the tick completes,
        // then Ambiguous at the tick (17), before the next header could be complete (31).
        var timestamp = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(timestamp, CaptureMilliseconds);
        var stream = Concat(
            [0x17, 0x00, 0x00, 0x00],
            BuildFrame(0x15, 0x36, new byte[10]),
            BuildFrame(0x00, 0x36, timestamp));
        CompareByteByByte(stream, 25);
        Assert.Equal(
            new PacketLengthPrefixedBoundaryProbe(PacketLengthPrefixedBoundaryProbeKind.Ambiguous, 0, 0, 17),
            FullScan(stream, PacketTransportCodec.MaximumEnvelopeBodyLength));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_boundaryScan")]
    private static extern ref LengthPrefixedBoundaryScan? BoundaryScan(TcpWorldStreamClassifier classifier);

    // Capture time as the server's latest tick says, so recorded ticks can confirm.
    private static long LatestTickTime(ReadOnlySpan<byte> received)
    {
        var tick = received.LastIndexOf([(byte)0x0e, (byte)0x00, (byte)0x36]);
        if (tick >= 0 && tick + 11 <= received.Length)
        {
            var serverMilliseconds = BinaryPrimitives.ReadInt64LittleEndian(received[(tick + 3)..]);
            if (serverMilliseconds is > 1_700_000_000_000 and < 1_900_000_000_000)
            {
                return serverMilliseconds;
            }
        }

        return CaptureMilliseconds;
    }

    // Each offset's probe says until which payload length its result holds; check it against later, longer payloads.
    private static void CheckStableClaims(
        byte[] stream,
        int bufferStart,
        int bufferEnd,
        Dictionary<int, (PacketLengthPrefixedProbe Result, long StableBelow)> claims)
    {
        for (var start = bufferStart; start + PacketTransportCodec.LengthPrefixedHeaderLength <= bufferEnd; start++)
        {
            var bodyLength = BinaryPrimitives.ReadInt32LittleEndian(stream.AsSpan(start));
            if (bodyLength <= 0 || bodyLength > PacketTransportCodec.MaximumEnvelopeBodyLength)
            {
                continue;
            }

            var result = PacketTransportCodec.ProbeLengthPrefixedStream(
                stream.AsSpan(start, bufferEnd - start),
                PacketTransportCodec.MaximumEnvelopeBodyLength,
                out var stableBelowLength);
            if (claims.TryGetValue(start, out var claim) && bufferEnd < claim.StableBelow)
            {
                Assert.Equal(claim.Result, result);
                continue;
            }

            claims[start] = (result, stableBelowLength == int.MaxValue ? long.MaxValue : (long)start + stableBelowLength);

            // A promise one byte too long breaks only at the last length it covers: check it there now, whatever the
            // append sizes.
            var lastCovered = stableBelowLength == int.MaxValue ? stream.Length - start : stableBelowLength - 1;
            if (stableBelowLength > 0 && lastCovered > bufferEnd - start && start + lastCovered <= stream.Length)
            {
                Assert.Equal(
                    result,
                    PacketTransportCodec.ProbeLengthPrefixedStream(
                        stream.AsSpan(start, lastCovered),
                        PacketTransportCodec.MaximumEnvelopeBodyLength));
            }
        }
    }

    // One-byte appends from firstLength to the whole stream; after each, the scan must equal the full scan.
    private static void CompareByteByByte(byte[] stream, int firstLength)
    {
        var scan = new LengthPrefixedBoundaryScan();
        for (var length = firstLength; length <= stream.Length; length++)
        {
            Assert.Equal(
                FullScan(stream.AsSpan(0, length), PacketTransportCodec.MaximumEnvelopeBodyLength),
                PacketTransportCodec.ProbeLengthPrefixedStreamBoundary(stream.AsSpan(0, length), scan, 0));
        }
    }

    // The 8561f2c loop over the current probe: every offset from 0, on every call.
    private static PacketLengthPrefixedBoundaryProbe FullScan(
        ReadOnlySpan<byte> payload,
        int maximumEnvelopeLength)
    {
        var pending = PacketLengthPrefixedBoundaryProbe.None;
        var pendingNextHeaderOffset = -1;
        for (var candidateOffset = 0;
             candidateOffset + PacketTransportCodec.LengthPrefixedHeaderLength <= payload.Length;
             candidateOffset++)
        {
            var candidate = payload[candidateOffset..];
            var bodyLength = BinaryPrimitives.ReadInt32LittleEndian(candidate);
            if (bodyLength <= 0 || bodyLength > maximumEnvelopeLength)
            {
                continue;
            }

            var probe = PacketTransportCodec.ProbeLengthPrefixedStream(candidate, maximumEnvelopeLength);
            if (probe.Kind == PacketLengthPrefixedProbeKind.Complete)
            {
                if (candidateOffset == pendingNextHeaderOffset)
                {
                    continue;
                }

                return new PacketLengthPrefixedBoundaryProbe(
                    PacketLengthPrefixedBoundaryProbeKind.Complete,
                    candidateOffset,
                    probe.CanonicalPrefixLength,
                    -1);
            }

            if (probe.Kind == PacketLengthPrefixedProbeKind.Ambiguous &&
                pending.Kind == PacketLengthPrefixedBoundaryProbeKind.None)
            {
                pending = new PacketLengthPrefixedBoundaryProbe(
                    PacketLengthPrefixedBoundaryProbeKind.Ambiguous,
                    candidateOffset,
                    0,
                    checked(candidateOffset + probe.DirectRecoveryTickOffset));
                continue;
            }

            if (probe.Kind == PacketLengthPrefixedProbeKind.NeedMore &&
                pending.Kind == PacketLengthPrefixedBoundaryProbeKind.None &&
                TryGetPotentialNextEnvelopeOffset(
                    candidate,
                    bodyLength,
                    maximumEnvelopeLength,
                    out var nextHeaderOffset))
            {
                pending = new PacketLengthPrefixedBoundaryProbe(
                    PacketLengthPrefixedBoundaryProbeKind.Pending,
                    candidateOffset,
                    0,
                    -1);
                pendingNextHeaderOffset = checked(candidateOffset + nextHeaderOffset);
            }
        }

        return pending;
    }

    private static bool TryGetPotentialNextEnvelopeOffset(
        ReadOnlySpan<byte> payload,
        int bodyLength,
        int maximumEnvelopeLength,
        out int nextHeaderOffset)
    {
        nextHeaderOffset = checked(PacketTransportCodec.LengthPrefixedHeaderLength + bodyLength);
        if (payload.Length < nextHeaderOffset)
        {
            return false;
        }

        if (payload.Length - nextHeaderOffset < PacketTransportCodec.LengthPrefixedHeaderLength)
        {
            return true;
        }

        var nextBodyLength = BinaryPrimitives.ReadInt32LittleEndian(payload[nextHeaderOffset..]);
        return nextBodyLength > 0 && nextBodyLength <= maximumEnvelopeLength;
    }

    private static int NextSegmentLength(Random random) =>
        random.Next(10) switch
        {
            0 => random.Next(1, 4),
            < 6 => 1460,
            _ => random.Next(1, 3000)
        };

    private static byte[] CreateStream(StreamKind kind, Random random, int limit)
    {
        var stream = new List<byte>(limit + 64 * 1024);
        switch (kind)
        {
            case StreamKind.RandomBytes:
                stream.AddRange(RandomBytes(random, limit));
                break;
            case StreamKind.TlsRecords:
                while (stream.Count < limit)
                {
                    var length = random.Next(4) == 0 ? random.Next(1, 300) : random.Next(1, 16_385);
                    stream.AddRange([0x17, 0x03, 0x03, (byte)(length >> 8), (byte)length]);
                    stream.AddRange(RandomBytes(random, length));
                }

                break;
            case StreamKind.HttpText:
                while (stream.Count < limit)
                {
                    stream.AddRange("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nTransfer-Encoding: chunked\r\n\r\n"u8);
                    for (var chunk = random.Next(1, 20); chunk > 0; chunk--)
                    {
                        var text = new StringBuilder();
                        while (text.Length < random.Next(1, 4000))
                        {
                            text.Append(CultureInfo.InvariantCulture, $"{{\"id\":{random.Next(100_000)},\"name\":\"item{random.Next(1000)}\"}},");
                        }

                        stream.AddRange(Encoding.ASCII.GetBytes($"{text.Length:x}\r\n{text}\r\n"));
                    }
                }

                break;
            case StreamKind.DenseSmallIntegers:
                while (stream.Count < limit)
                {
                    var value = random.Next(1, 4096);
                    stream.AddRange([(byte)value, (byte)(value >> 8), 0, 0]);
                }

                break;
            case StreamKind.GameFrames:
                stream.AddRange(GameFrames(random, limit));
                break;
            case StreamKind.GameFramesInEnvelopes:
                stream.AddRange(InEnvelopes(random, GameFrames(random, limit)));
                break;
            case StreamKind.FakeHeadersAndTicks:
                var bytes = RandomBytes(random, limit);
                for (var offset = random.Next(64); offset + 11 <= bytes.Length; offset += random.Next(1, 160))
                {
                    switch (random.Next(4))
                    {
                        case 0:
                            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), random.Next(1, 600));
                            break;
                        case 1:
                            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), random.Next(1, 0x100000));
                            break;
                        case 2:
                            PacketTransportCodec.Pattern.CopyTo(bytes.AsSpan(offset));
                            break;
                        default:
                            Tick(random).CopyTo(bytes.AsSpan(offset));
                            break;
                    }
                }

                stream.AddRange(bytes);
                break;
            case StreamKind.RecordedGame:
                stream.AddRange(RecordedGame(random, limit));
                break;
            case StreamKind.RecordedGameInEnvelopes:
                stream.AddRange(InEnvelopes(random, RecordedGame(random, limit)));
                break;
        }

        return [.. stream];
    }

    private static byte[] RandomBytes(Random random, int length)
    {
        var bytes = new byte[length];
        random.NextBytes(bytes);
        return bytes;
    }

    private static byte[] Tick(Random random)
    {
        var timestamp = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(timestamp, CaptureMilliseconds + random.Next(-1000, 1000));
        return BuildFrame(0x00, 0x36, timestamp);
    }

    private static byte[] GameFrames(Random random, int limit)
    {
        (byte, byte)[] gameplay = [(0x15, 0x36), (0x21, 0x36), (0x02, 0x38), (0x00, 0x8d), (0x0d, 0x92), (0x29, 0x33)];
        var frames = new List<byte>(limit);
        while (frames.Count < limit)
        {
            frames.AddRange(random.Next(6) switch
            {
                0 => Tick(random),
                1 => PacketTransportCodec.Pattern.ToArray(),
                2 => BuildCompressedFrame(Concat(
                    BuildFrame(0x15, 0x36, RandomBytes(random, random.Next(40))),
                    BuildFrame((byte)random.Next(256), (byte)random.Next(256), RandomBytes(random, random.Next(40))))),
                3 => BuildFrame((byte)random.Next(256), (byte)random.Next(256), RandomBytes(random, random.Next(200))),
                _ => BuildFrame(
                    gameplay[random.Next(gameplay.Length)].Item1,
                    gameplay[random.Next(gameplay.Length)].Item2,
                    RandomBytes(random, random.Next(random.Next(4) == 0 ? 3000 : 120)))
            });
        }

        return [.. frames];
    }

    private static byte[] InEnvelopes(Random random, byte[] canonical)
    {
        var enveloped = new List<byte>(canonical.Length + canonical.Length / 16);
        for (var offset = 0; offset < canonical.Length;)
        {
            var length = Math.Min(canonical.Length - offset, random.Next(4) == 0 ? random.Next(1, 24) : random.Next(1, 2000));
            enveloped.AddRange(BuildLengthPrefixedEnvelope(canonical.AsSpan(offset, length)));
            offset += length;
        }

        return [.. enveloped];
    }

    // The inbound payloads of the busiest connection in a recorded session, in file order.
    private static byte[] RecordedGame(Random random, int limit)
    {
        string[] fixtures = ["aion2flow.stream.20260703041828.log", "aion2flow.stream.20260728234353.log"];
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs", fixtures[random.Next(fixtures.Length)]);
        var byConnection = new Dictionary<string, List<byte>>();
        foreach (var line in File.ReadLines(path))
        {
            var fields = line.Split('|');
            var data = Array.Find(fields, static field => field.StartsWith("data=", StringComparison.Ordinal));
            if (fields.Length < 3 || fields[1] != "dir=inbound" || data is null)
            {
                continue;
            }

            if (!byConnection.TryGetValue(fields[2], out var bytes))
            {
                byConnection[fields[2]] = bytes = [];
            }

            bytes.AddRange(Convert.FromHexString(data.AsSpan(5)));
        }

        var busiest = byConnection.Values.MaxBy(static bytes => bytes.Count)!;
        var start = random.Next(Math.Max(1, busiest.Count - limit));
        return [.. busiest.GetRange(start, Math.Min(limit, busiest.Count - start))];
    }
}
