using System.Globalization;
using System.Text.Json;
using Cloris.Aion2Flow.SceneRuntime.Twins;

namespace Cloris.Aion2Flow.Services;

// The twin library: every digital twin looked up, kept as a file of its own in "twin-library" next to the settings, so
// the snapshots outlive the session: listed in the library window, and each player's newest is there again at the next
// start (see DigitalTwinService.Preload).
// ponytail: the window reads every file; keep an index beside them if the library grows into the thousands.
public sealed class DigitalTwinLibrary(string folder)
{
    private const string Extension = ".twin.gz";

    public sealed record Entry(string Path, DigitalTwin Twin);

    public string Folder { get; } = folder;

    // A snapshot was added or deleted; raised on the thread that did it.
    public event Action? Changed;

    public void Add(DigitalTwin twin)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            DigitalTwinFile.Write(System.IO.Path.Combine(Folder, FileName(twin)), twin);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not kept (disk full, OneDrive holding the folder): the twin still shows for the session.
            return;
        }

        Changed?.Invoke();
    }

    public void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        Changed?.Invoke();
    }

    // Every snapshot, the newest first; a file that can't be read is left out.
    public List<Entry> Load() => Read(Files());

    // Each player's newest snapshot, picked by file name (time, server, name) before reading only those.
    public List<DigitalTwin> LoadNewestPerPlayer() =>
        [.. Read(Files().GroupBy(static path => System.IO.Path.GetFileName(path)[16..], StringComparer.OrdinalIgnoreCase)
            .Select(static player => player.Max(StringComparer.Ordinal)!))
            .Select(static entry => entry.Twin)];

    // "20261111-213302 2304 Healer.twin.gz" (the time in UTC), characters a file name can't hold as "_".
    internal static string FileName(DigitalTwin twin)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var name = string.Concat(twin.Name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c));
        return string.Create(CultureInfo.InvariantCulture, $"{twin.FetchedAt.UtcDateTime:yyyyMMdd-HHmmss} {twin.ServerId} {name}{Extension}");
    }

    private IEnumerable<string> Files() =>
        Directory.Exists(Folder)
            ? Directory.EnumerateFiles(Folder, "*" + Extension).Where(static path => System.IO.Path.GetFileName(path).Length > 16 + Extension.Length)
            : [];

    private static List<Entry> Read(IEnumerable<string> paths)
    {
        var entries = new List<Entry>();
        foreach (var path in paths)
        {
            try
            {
                if (DigitalTwinFile.Read(path) is { } twin)
                    entries.Add(new Entry(path, twin));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
            }
        }

        entries.Sort(static (left, right) => right.Twin.FetchedAt.CompareTo(left.Twin.FetchedAt));
        return entries;
    }
}
