using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.Audio;

namespace Cloris.Aion2Flow.Services.Audio;

// Plays a sound with Windows' own PlaySound, in the background, on the default output device; a new one stops the last.
internal static unsafe class AlertSoundPlayer
{
    // Windows reads the sound from this memory while it plays: pinned, and kept until the next one.
    private static byte[]? _playing;

    // Tests keep the speakers quiet.
    internal static bool Muted { get; set; }

    public static void Play(AudioClip clip, int volumePercent)
    {
        if (Muted)
            return;

        var wav = clip.ToWav(volumePercent, pinned: true);
        Stop();
        fixed (byte* sound = wav)
            PInvoke.PlaySound((char*)sound, default, SND_FLAGS.SND_MEMORY | SND_FLAGS.SND_ASYNC | SND_FLAGS.SND_NODEFAULT);
        _playing = wav;
    }

    public static void Stop()
    {
        if (!Muted)
            PInvoke.PlaySound(default(PCWSTR), default, 0);
    }
}
