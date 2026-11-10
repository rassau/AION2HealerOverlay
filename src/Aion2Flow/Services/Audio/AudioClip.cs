using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Cloris.Aion2Flow.Services.Audio;

// A mono 16-bit sound: the boss alert's chime, a clip cut from the user's file, or one of theirs read back from its WAV.
public sealed class AudioClip(short[] samples, int sampleRate)
{
    private const int WavHeaderBytes = 44;

    public short[] Samples { get; } = samples;

    public int SampleRate { get; } = sampleRate;

    // Two bell tones going up a fifth (A5, then E6 a little later), each dying away: short and clear over the game.
    public static AudioClip Chime()
    {
        const int rate = 44_100;
        var samples = new short[(int)(rate * 0.75)];
        var mix = new double[samples.Length];
        Tone(0, 880);
        Tone(0.15, 1318.5);
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (short)Math.Clamp(mix[i] * short.MaxValue, short.MinValue, short.MaxValue);
        return new AudioClip(samples, rate);

        void Tone(double start, double frequency)
        {
            var first = (int)(start * rate);
            for (var i = first; i < mix.Length; i++)
            {
                var t = (i - first) / (double)rate;
                var envelope = Math.Min(1, t / 0.004) * Math.Exp(-t / 0.15);
                mix[i] += 0.32 * envelope * (Math.Sin(2 * Math.PI * frequency * t) + 0.25 * Math.Sin(4 * Math.PI * frequency * t));
            }
        }
    }

    // The samples from start up to end, faded in and out over 5 ms so the cut doesn't click.
    public AudioClip Cut(int start, int end)
    {
        var samples = Samples[start..end];
        var fade = Math.Min(SampleRate / 200, samples.Length / 4);
        for (var i = 0; i < fade; i++)
        {
            var gain = i / (double)fade;
            samples[i] = (short)(samples[i] * gain);
            samples[^(i + 1)] = (short)(samples[^(i + 1)] * gain);
        }

        return new AudioClip(samples, SampleRate);
    }

    // As a WAV file, at the volume: 100 as it is, up to 200 louder for a quiet sound, capped at full scale (a loud sound
    // turned up distorts there). Pinned for Windows to read while it plays.
    public byte[] ToWav(int volumePercent = 100, bool pinned = false)
    {
        var bytes = GC.AllocateUninitializedArray<byte>(WavHeaderBytes + Samples.Length * 2, pinned);
        var wav = bytes.AsSpan();
        "RIFF"u8.CopyTo(wav);
        BinaryPrimitives.WriteInt32LittleEndian(wav[4..], bytes.Length - 8);
        "WAVEfmt "u8.CopyTo(wav[8..]);
        BinaryPrimitives.WriteInt32LittleEndian(wav[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(wav[20..], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(wav[22..], 1); // mono
        BinaryPrimitives.WriteInt32LittleEndian(wav[24..], SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wav[28..], SampleRate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(wav[32..], 2);
        BinaryPrimitives.WriteInt16LittleEndian(wav[34..], 16);
        "data"u8.CopyTo(wav[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(wav[40..], Samples.Length * 2);
        var data = MemoryMarshal.Cast<byte, short>(wav[WavHeaderBytes..]);
        var gain = volumePercent / 100d;
        for (var i = 0; i < Samples.Length; i++)
            data[i] = (short)Math.Clamp(Samples[i] * gain, short.MinValue, short.MaxValue);
        return bytes;
    }

    // A WAV file as ToWav writes it: 16-bit mono. Anything else goes through AudioFileDecoder.
    public static AudioClip ReadWav(byte[] bytes)
    {
        var wav = bytes.AsSpan();
        if (wav.Length < 12 || !wav[..4].SequenceEqual("RIFF"u8) || !wav[8..12].SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Not a WAV file.");

        int channels = 0, rate = 0, bits = 0;
        for (var at = 12; at + 8 <= wav.Length;)
        {
            var size = BinaryPrimitives.ReadInt32LittleEndian(wav[(at + 4)..]);
            var body = wav.Slice(at + 8, Math.Clamp(size, 0, wav.Length - at - 8));
            if (wav.Slice(at, 4).SequenceEqual("fmt "u8) && body.Length >= 16)
            {
                channels = BinaryPrimitives.ReadInt16LittleEndian(body[2..]);
                rate = BinaryPrimitives.ReadInt32LittleEndian(body[4..]);
                bits = BinaryPrimitives.ReadInt16LittleEndian(body[14..]);
            }
            else if (wav.Slice(at, 4).SequenceEqual("data"u8))
            {
                if (channels != 1 || bits != 16 || rate <= 0)
                    throw new InvalidDataException("Not a 16-bit mono WAV file.");
                return new AudioClip(MemoryMarshal.Cast<byte, short>(body[..(body.Length & ~1)]).ToArray(), rate);
            }

            at += 8 + Math.Max(0, size) + (size & 1);
        }

        throw new InvalidDataException("The WAV file has no sound.");
    }
}
