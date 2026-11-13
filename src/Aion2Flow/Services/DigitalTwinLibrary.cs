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
    private const string FavouritesFile = "favourites.txt";
    private const string NotesFile = "notes.txt";

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

    // The snapshots marked as favourites, by file name, in "favourites.txt" beside them.
    public HashSet<string> LoadFavourites()
    {
        try
        {
            var path = System.IO.Path.Combine(Folder, FavouritesFile);
            if (File.Exists(path))
                return new HashSet<string>(File.ReadAllLines(path).Where(static line => line.Length > 0), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public void SaveFavourites(IEnumerable<string> fileNames)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllLines(System.IO.Path.Combine(Folder, FavouritesFile), fileNames.Order(StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not kept: the marks still show for the session.
        }
    }

    // The snapshots' notes, by file name, in "notes.txt" beside them: a line each, the file name, a tab and the note.
    public Dictionary<string, string> LoadNotes()
    {
        var notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var path = System.IO.Path.Combine(Folder, NotesFile);
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.Split('\t', 2) is [var file, { Length: > 0 } note])
                        notes[file] = note;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return notes;
    }

    // Notes are one line without tabs (see DigitalTwinLibraryViewModel.SetNote).
    public void SaveNotes(IReadOnlyDictionary<string, string> notes)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllLines(System.IO.Path.Combine(Folder, NotesFile),
                notes.OrderBy(static note => note.Key, StringComparer.OrdinalIgnoreCase).Select(static note => $"{note.Key}\t{note.Value}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not kept: the notes still show for the session.
        }
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
