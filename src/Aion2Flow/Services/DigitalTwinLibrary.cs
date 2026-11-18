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
    private const string SeenLevelsFile = "seen-levels.txt";
    private readonly Lock _seenLevelsLock = new();
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

    // The skill levels the game sent for each player in the last boss fight the overlay saw them in (see
    // MainViewModel.RememberSeenLevels), in "seen-levels.txt" beside the snapshots: a line each, tab-separated, the
    // server id, the name as the game wrote it, the fight's end (UTC) and the levels by base skill ("17090000=16
    // 17730000=12"), stigmas' ranges among them ("17410000=15-19", "17160000=20+"). Keyed by server and name in lower case
    // (see PlayerKey); a line that can't be read is left out.
    public Dictionary<(int ServerId, string Name), SeenSkillLevels> LoadSeenLevels()
    {
        var seen = new Dictionary<(int ServerId, string Name), SeenSkillLevels>();
        try
        {
            lock (_seenLevelsLock)
            {
                foreach (var read in ReadSeenLevels())
                    seen[read.Key] = read.Levels;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return seen;
    }

    // A fight's levels and stigma ranges for its players (at: its end; a level wins over a range): a player's line goes
    // only for a fight that ended later, so a replayed old recording never undoes a newer one; a player the fight showed
    // nothing for keeps theirs. Under a lock, as two fights can end close together; written aside and moved over, as the
    // best runs are.
    // ponytail: never pruned, a short line per player ever met in a boss fight; keep the newest N players if it grows.
    public void RememberSeenLevels(IEnumerable<(int ServerId, string Name, IReadOnlyDictionary<int, int> Levels, IReadOnlyDictionary<int, SeenLevelRange> Ranges)> players,
        DateTimeOffset at)
    {
        lock (_seenLevelsLock)
        {
            try
            {
                // A file that can't be read isn't written over.
                var lines = new Dictionary<(int ServerId, string Name), (DateTimeOffset At, string Line)>();
                foreach (var read in ReadSeenLevels())
                    lines[read.Key] = (read.Levels.At, read.Line);
                var changed = false;
                foreach (var (serverId, name, levels, ranges) in players)
                {
                    var key = PlayerKey(serverId, name);
                    if (levels.Count == 0 && ranges.Count == 0 || lines.TryGetValue(key, out var kept) && kept.At >= at)
                        continue;

                    var pairs = string.Join(' ', levels.Keys.Union(ranges.Keys).Order().Select(skill => levels.TryGetValue(skill, out var level)
                        ? FormattableString.Invariant($"{skill}={level}")
                        : FormattableString.Invariant($"{skill}={ranges[skill].Min}") + (ranges[skill].Max is { } max ? FormattableString.Invariant($"-{max}") : "+")));
                    lines[key] = (at, string.Create(CultureInfo.InvariantCulture, $"{serverId}\t{name.Trim()}\t{at.ToUniversalTime():o}\t{pairs}"));
                    changed = true;
                }

                if (!changed)
                    return;

                Directory.CreateDirectory(Folder);
                var path = System.IO.Path.Combine(Folder, SeenLevelsFile);
                File.WriteAllLines(path + ".tmp", lines.Values.Select(static line => line.Line));
                File.Move(path + ".tmp", path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not kept: the library shows the players' older levels.
            }
        }
    }

    // A player as DigitalTwinService keys them: the server and the name in lower case.
    internal static (int ServerId, string Name) PlayerKey(int serverId, string name) => (serverId, name.Trim().ToLowerInvariant());

    // Each line read, the key and levels with the line as it was; IO errors go to the caller.
    private List<((int ServerId, string Name) Key, SeenSkillLevels Levels, string Line)> ReadSeenLevels()
    {
        var read = new List<((int, string), SeenSkillLevels, string)>();
        var path = System.IO.Path.Combine(Folder, SeenLevelsFile);
        if (!File.Exists(path))
            return read;

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Split('\t') is [var server, { Length: > 0 } name, var end, var pairs] &&
                int.TryParse(server, NumberStyles.Integer, CultureInfo.InvariantCulture, out var serverId) &&
                DateTimeOffset.TryParseExact(end, "o", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) &&
                Levels(pairs) is { } seen)
                read.Add((PlayerKey(serverId, name), new SeenSkillLevels(at, seen.Levels) { Ranges = seen.Ranges }, line));
        }

        return read;

        // "17090000=16 17410000=15-19 17160000=20+"; null for anything else.
        static (Dictionary<int, int> Levels, Dictionary<int, SeenLevelRange> Ranges)? Levels(string pairs)
        {
            var (levels, ranges) = (new Dictionary<int, int>(), new Dictionary<int, SeenLevelRange>());
            foreach (var pair in pairs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (pair.Split('=') is not [var skill, var level] || !int.TryParse(skill, NumberStyles.Integer, CultureInfo.InvariantCulture, out var skillId))
                    return null;

                if (int.TryParse(level, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    levels[skillId] = value;
                else if (RangeOf(level) is { } range)
                    ranges[skillId] = range;
                else
                    return null;
            }

            return (levels, ranges);
        }

        // "15-19" or "20+"; null for anything else.
        static SeenLevelRange? RangeOf(string text)
        {
            if (text.EndsWith('+'))
                return int.TryParse(text[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var open) ? new SeenLevelRange(open, null) : null;

            return text.Split('-') is [var low, var high] && int.TryParse(low, NumberStyles.None, CultureInfo.InvariantCulture, out var min) &&
                int.TryParse(high, NumberStyles.None, CultureInfo.InvariantCulture, out var max) && min <= max
                ? new SeenLevelRange(min, max)
                : null;
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
