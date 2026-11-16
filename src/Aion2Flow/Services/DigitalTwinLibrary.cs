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
    // The tutorial's made-up twin (TutorialTwin) under a file name no real snapshot has (Files needs a longer one): listed
    // first while the tutorial runs, never written; Load may run on a pool thread.
    private const string TemporaryFile = "tutorial" + Extension;
    private volatile Entry? _temporary;

    public sealed record Entry(string Path, DigitalTwin Twin)
    {
        // The file's last write and length when it was read (see Load).
        internal (DateTime Written, long Length) Stamp { get; init; }
    }

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

    // The tutorial's twin, in the list without a file, until RemoveTemporary.
    public void AddTemporary(DigitalTwin twin)
    {
        _temporary = new Entry(System.IO.Path.Combine(Folder, TemporaryFile), twin);
        Changed?.Invoke();
    }

    public DigitalTwin? RemoveTemporary()
    {
        var twin = _temporary?.Twin;
        _temporary = null;
        if (twin is not null)
            Changed?.Invoke();
        return twin;
    }

    public void Delete(string path)
    {
        if (_temporary?.Path == path)
        {
            RemoveTemporary();
            return;
        }

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
            File.WriteAllLines(System.IO.Path.Combine(Folder, FavouritesFile),
                fileNames.Where(static name => !name.Equals(TemporaryFile, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase));
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
                notes.Where(static note => !note.Key.Equals(TemporaryFile, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(static note => note.Key, StringComparer.OrdinalIgnoreCase).Select(static note => $"{note.Key}\t{note.Value}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not kept: the notes still show for the session.
        }
    }

    // Every snapshot, the newest first; a file that can't be read is left out. An entry of known (the list loaded before)
    // whose file hasn't been written since is taken as it is instead of being read again.
    public List<Entry> Load(IEnumerable<Entry>? known = null)
    {
        var entries = Read(Files(), known);
        if (_temporary is { } temporary)
            entries.Insert(0, temporary);
        return entries;
    }

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

    private static List<Entry> Read(IEnumerable<string> paths, IEnumerable<Entry>? known = null)
    {
        var entries = new List<Entry>();
        var read = known?.ToDictionary(static entry => entry.Path) ?? [];
        foreach (var path in paths)
        {
            try
            {
                // Stamped before it's read: a write in between is caught at the next load.
                var file = new FileInfo(path);
                var stamp = (file.LastWriteTimeUtc, file.Length);
                if (read.TryGetValue(path, out var same) && same.Stamp == stamp)
                    entries.Add(same);
                else if (DigitalTwinFile.Read(path) is { } twin)
                    entries.Add(new Entry(path, twin) { Stamp = stamp });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
            }
        }

        entries.Sort(static (left, right) => right.Twin.FetchedAt.CompareTo(left.Twin.FetchedAt));
        return entries;
    }
}
