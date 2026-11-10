using System.Buffers.Binary;

namespace Cloris.Aion2Flow.Services.Audio;

// Ogg Opus files (Discord's soundboard sounds, for one). Windows has an Opus decoder but no reader for the Ogg container
// around it, so the sound moves, packet by packet, into a WebM container, which Windows reads. One stream, as Discord and
// most tools write them.
internal static class OggOpus
{
    private const int Rate = 48_000; // Opus counts its time in 48 kHz samples, whatever the source's rate

    // Whether the file starts with an Ogg page holding an Opus head.
    public static bool Is(string path)
    {
        Span<byte> start = stackalloc byte[64];
        using var file = File.OpenRead(path);
        start = start[..file.ReadAtLeast(start, start.Length, throwOnEndOfStream: false)];
        return start.StartsWith("OggS"u8) && start.IndexOf("OpusHead"u8) > 0;
    }

    // The same sound as WebM, at most its first `longest`.
    public static byte[] ToWebm(ReadOnlySpan<byte> ogg, TimeSpan longest)
    {
        var packets = Packets(ogg);
        if (packets.Count < 2 || packets[0].Length < 19 || !packets[0].AsSpan().StartsWith("OpusHead"u8))
            throw new InvalidDataException("Not an Ogg Opus file.");

        var head = packets[0];
        var track = Element(0xAE, // TrackEntry
            Unsigned(0xD7, 1), // TrackNumber
            Unsigned(0x73C5, 1), // TrackUID
            Unsigned(0x83, 2), // TrackType: audio
            Element(0x86, "A_OPUS"u8.ToArray()), // CodecID
            Element(0x63A2, head), // CodecPrivate: the Opus head
            Unsigned(0x56AA, BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(10)) * 1_000_000_000UL / Rate), // CodecDelay: the pre-skip, in ns
            Unsigned(0x56BB, 80_000_000), // SeekPreRoll: 80 ms
            Element(0xE1, Float(0xB5, Rate), Unsigned(0x9F, head[9]))); // Audio: SamplingFrequency, Channels
        var segment = new List<byte[]> { Element(0x1549A966, Unsigned(0x2AD7B1, 1_000_000)), Element(0x1654AE6B, track) }; // Info (times in ms), Tracks

        // A block's time is a 16-bit number of ms after its cluster's: a new cluster every 30 s.
        var blocks = new List<byte[]>();
        long cluster = 0, samples = 0, limit = (long)(longest.TotalSeconds * Rate);
        foreach (var packet in packets.Skip(2)) // after the head and the tags
        {
            if (samples >= limit)
                break;
            if (packet.Length == 0)
                continue;
            var ms = samples / (Rate / 1_000);
            if (ms - cluster > 30_000)
            {
                segment.Add(Cluster(cluster, blocks));
                blocks.Clear();
                cluster = ms;
            }

            var block = new byte[4 + packet.Length];
            block[0] = 0x81; // track 1
            BinaryPrimitives.WriteInt16BigEndian(block.AsSpan(1), (short)(ms - cluster));
            block[3] = 0x80; // a keyframe
            packet.CopyTo(block, 4);
            blocks.Add(Element(0xA3, block)); // SimpleBlock
            samples += Samples(packet);
        }

        if (blocks.Count == 0)
            throw new InvalidDataException("The file has no sound.");
        segment.Add(Cluster(cluster, blocks));
        var webm = Element(0x1A45DFA3, // EBML header: a WebM file
            Unsigned(0x4286, 1), Unsigned(0x42F7, 1), Unsigned(0x42F2, 4), Unsigned(0x42F3, 8), Element(0x4282, "webm"u8.ToArray()), Unsigned(0x4287, 4), Unsigned(0x4285, 2));
        return [.. webm, .. Element(0x18538067, [.. segment])]; // Segment
    }

    // An Ogg file's packets in order. A page holds segments of up to 255 bytes; a shorter one ends a packet, which may go
    // on over the next page. A cut-off file gives what it has.
    private static List<byte[]> Packets(ReadOnlySpan<byte> ogg)
    {
        var packets = new List<byte[]>();
        var packet = new List<byte>();
        var at = 0;
        while (at + 27 <= ogg.Length && ogg[at..].StartsWith("OggS"u8))
        {
            var sizes = ogg.Slice(at + 27, Math.Min(ogg[at + 26], ogg.Length - at - 27));
            at += 27 + sizes.Length;
            foreach (var size in sizes)
            {
                if (at + size > ogg.Length)
                    return packets;
                packet.AddRange(ogg.Slice(at, size));
                at += size;
                if (size < 255)
                {
                    packets.Add([.. packet]);
                    packet.Clear();
                }
            }
        }

        return packets;
    }

    // A packet's length in 48 kHz samples: its first byte gives the frames' length and how many, with code 3 its second
    // the count (RFC 6716, 3.1).
    private static int Samples(byte[] packet)
    {
        var config = packet[0] >> 3;
        var frame = config < 12 ? SilkFrames[config & 3] : config < 16 ? 480 << (config & 1) : 120 << (config & 3);
        return frame * ((packet[0] & 3) switch { 0 => 1, 3 => packet.Length > 1 ? packet[1] & 63 : 0, _ => 2 });
    }

    private static readonly int[] SilkFrames = [480, 960, 1_920, 2_880]; // 10, 20, 40 and 60 ms

    // An EBML element: its id, its content's size (always in 8 bytes here) and the content.
    private static byte[] Element(uint id, params byte[][] content)
    {
        var idBytes = id > 0xFF_FFFF ? 4 : id > 0xFFFF ? 3 : id > 0xFF ? 2 : 1;
        var element = new byte[idBytes + 8 + content.Sum(part => part.Length)];
        for (var i = 0; i < idBytes; i++)
            element[i] = (byte)(id >> 8 * (idBytes - 1 - i));
        // The leading 0x01 says the size takes 8 bytes.
        BinaryPrimitives.WriteUInt64BigEndian(element.AsSpan(idBytes), (uint)(element.Length - idBytes - 8) | (1UL << 56));
        var at = idBytes + 8;
        foreach (var part in content)
        {
            part.CopyTo(element, at);
            at += part.Length;
        }

        return element;
    }

    private static byte[] Unsigned(uint id, ulong value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return Element(id, bytes);
    }

    private static byte[] Float(uint id, double value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(bytes, value);
        return Element(id, bytes);
    }

    private static byte[] Cluster(long ms, List<byte[]> blocks) => Element(0x1F43B675, [Unsigned(0xE7, (ulong)ms), .. blocks]); // Cluster: Timestamp, SimpleBlocks
}
