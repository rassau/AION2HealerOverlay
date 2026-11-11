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

    // Two wooden knocks, "tok-tok", the second a little softer.
    public static AudioClip Knock()
    {
        var mix = new double[(int)(SynthRate * 0.45)];
        Add(mix, 0, KnockHit(1));
        Add(mix, 0.13, KnockHit(0.8));
        return Finish(mix);
    }

    // Two soft rising "bloops", the second a little higher.
    public static AudioClip Blip()
    {
        var mix = new double[(int)(SynthRate * 0.5)];
        Add(mix, 0, BlipHit(500, 1_000));
        Add(mix, 0.15, BlipHit(600, 1_200));
        return Finish(mix);
    }

    private const int SynthRate = 44_100;

    // A knock: a 2 ms click, a woody tone with an overtone and a little body underneath, all dying away fast.
    private static double[] KnockHit(double level)
    {
        var noise = new Random(7);
        var sound = new double[(int)(SynthRate * 0.25)];
        for (var i = 0; i < sound.Length; i++)
        {
            var t = i / (double)SynthRate;
            var value = 0.6 * Math.Exp(-t / 0.04) * Math.Sin(2 * Math.PI * 1_050 * t)
                + 0.25 * Math.Exp(-t / 0.012) * Math.Sin(2 * Math.PI * 2_750 * t)
                + 0.35 * Math.Exp(-t / 0.05) * Math.Sin(2 * Math.PI * 260 * t);
            if (t < 0.002)
                value += 0.3 * (noise.NextDouble() * 2 - 1) * (1 - t / 0.002);
            sound[i] = level * Math.Min(1, t / 0.001) * value;
        }

        return sound;
    }

    // A bloop: a tone sliding up from low to high in 80 ms, swelling in over 5 ms and dying away.
    private static double[] BlipHit(double low, double high)
    {
        var sound = new double[(int)(SynthRate * 0.3)];
        var phase = 0d;
        for (var i = 0; i < sound.Length; i++)
        {
            var t = i / (double)SynthRate;
            phase += 2 * Math.PI * low * Math.Pow(high / low, Math.Min(1, t / 0.08)) / SynthRate;
            sound[i] = Math.Min(1, t / 0.005) * Math.Exp(-t / 0.06) * Math.Sin(phase);
        }

        return sound;
    }

    private static void Add(double[] mix, double startSeconds, double[] sound)
    {
        var first = (int)(startSeconds * SynthRate);
        for (var i = 0; i < sound.Length && first + i < mix.Length; i++)
            mix[first + i] += sound[i];
    }

    // Peaking at 60 % of full scale like the chime, the last 30 ms faded out so it doesn't end in a click.
    private static AudioClip Finish(double[] mix)
    {
        var peak = mix.Max(static value => Math.Abs(value));
        var fade = SynthRate * 0.03;
        var samples = new short[mix.Length];
        for (var i = 0; i < mix.Length; i++)
            samples[i] = (short)Math.Clamp(mix[i] / peak * 0.6 * Math.Min(1, (mix.Length - i) / fade) * short.MaxValue, short.MinValue, short.MaxValue);
        return new AudioClip(samples, SynthRate);
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
