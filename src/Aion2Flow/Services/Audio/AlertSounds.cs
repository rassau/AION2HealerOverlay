using Cloris.Aion2Flow.Services.Logging;

namespace Cloris.Aion2Flow.Services.Audio;

// The boss alert's sounds: the built-in chime (named null) and the user's own, made from or imported with a file, kept as
// WAV files in a folder beside the settings. At most five, the newest first: a sixth one takes the oldest's place, and
// its file goes with it.
public sealed class AlertSounds(string folder)
{
    public const int MaxSounds = 5;
    // An alert is at most this long; of a file, the editor reads at most MaxSource to cut it from.
    public static readonly TimeSpan MaxLength = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MaxSource = TimeSpan.FromMinutes(10);
    private static readonly AudioClip ChimeClip = AudioClip.Chime();
    private long? _lastAlertAt;
    private (string Name, AudioClip Clip)? _loaded;

    public string PathOf(string name) => Path.Combine(folder, name + ".wav");

    // The sound by its name, null for the chime; the chime too when its file is gone or can't be read.
    public AudioClip Load(string? name)
    {
        if (name is null)
            return ChimeClip;
        if (_loaded is { } loaded && loaded.Name == name)
            return loaded.Clip;

        try
        {
            var clip = AudioClip.ReadWav(File.ReadAllBytes(PathOf(name)));
            _loaded = (name, clip);
            return clip;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            AppLog.Write(AppLogLevel.Warning, $"Alert sound '{name}' could not be read, playing the chime instead: {ex.Message}");
            return ChimeClip;
        }
    }

    public void Play(string? name, int volumePercent) => AlertSoundPlayer.Play(Load(name), volumePercent);

    // The boss just turned to you: whether to sound the alert, which waits out the quiet time after the last one (the
    // setting, 0-10 s: the boss's target can flip back and forth).
    public bool TryAlert(long nowMilliseconds, long quietMilliseconds)
    {
        if (_lastAlertAt is { } last && nowMilliseconds - last < quietMilliseconds)
            return false;

        _lastAlertAt = nowMilliseconds;
        return true;
    }

    // Saves the clip first in the list, under the name made fit for a file and free ("Gong 2" if there's a "Gong");
    // past five the oldest goes. Returns the name it got.
    public string Add(AudioClip clip, string name, List<string> sounds)
    {
        var clean = string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim();
        if (clean.Length == 0)
            clean = "Sound";
        var free = clean;
        for (var number = 2; sounds.Contains(free, StringComparer.OrdinalIgnoreCase) || File.Exists(PathOf(free)); number++)
            free = $"{clean} {number}";

        Directory.CreateDirectory(folder);
        File.WriteAllBytes(PathOf(free), clip.ToWav());
        _loaded = null;
        sounds.Insert(0, free);
        while (sounds.Count > MaxSounds)
        {
            try
            {
                File.Delete(PathOf(sounds[^1]));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppLog.Write(AppLogLevel.Warning, $"Old alert sound '{sounds[^1]}' could not be deleted: {ex.Message}");
            }

            sounds.RemoveAt(sounds.Count - 1);
        }

        return free;
    }
}
