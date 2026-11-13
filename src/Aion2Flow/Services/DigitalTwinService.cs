using System.Text.Json;
using System.Text.RegularExpressions;
using Cloris.Aion2Flow.SceneRuntime.Twins;

namespace Cloris.Aion2Flow.Services;

public enum DigitalTwinState
{
    None,
    Queued,
    Loading,
    Ready,
    NotFound,
    Unsupported,
    Failed
}

// Twin: the one fetched closest to the asked moment (the last before it, else the first after it), also while a newer
// lookup is running or after one failed.
public readonly record struct DigitalTwinLookup(DigitalTwinState State, DigitalTwin? Twin, string? Error);

// A lookup waiting or running, as the twin library lists it: the class once known (from the profile, or the player's last
// twin), Progress the share of its requests answered (0 while it waits).
public readonly record struct DigitalTwinFetch(int ServerId, string Name, string? ClassName, double Progress);

// Digital twins of players from the game's official character website, through the JSON its character search uses (no
// login): the search by name and server gives the website's id for the character, then come the profile, the gear with
// each item's stats, and the open Daevanion boards. That's about 34 requests per player, made one after another 0.7 s
// apart; each player is looked up once per session unless "Look up again" asks for it. Only the global servers (NA, EU,
// Asia, Latin America); the server id's hundreds digit gives the region (1301 = EU Siel, 2205 = NA West, …).
// ponytail: twins live for the session (and in exported history entries); keep them on disk if repeat lookups become a problem.
public sealed partial class DigitalTwinService : IDisposable
{
    private const string SearchUrl = "https://api-search.plaync.com/aion2global/search/v2/character";
    private const string SiteUrl = "https://aion2.plaync.com/api/character/";
    // Until the gear list gives the exact count: every snapshot so far took 34.
    private const int EstimatedRequests = 34;

    private readonly HttpClient _http;
    private readonly TimeSpan _requestGap;
    private readonly Lock _lock = new();
    private readonly Dictionary<(int ServerId, string Name), Entry> _entries = [];
    private readonly Queue<(int ServerId, string Name)> _queue = new();
    private readonly SemaphoreSlim _queued = new(0);
    private readonly CancellationTokenSource _stop = new();
    private Task? _worker;
    private readonly DigitalTwinLibrary? _library;
    private int _version;
    private long _asked;

    // Each twin looked up also goes into the library.
    public DigitalTwinService(DigitalTwinLibrary? library = null)
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }, TimeSpan.FromMilliseconds(700), library)
    {
    }

    internal DigitalTwinService(HttpClient http, TimeSpan requestGap, DigitalTwinLibrary? library = null)
    {
        _http = http;
        _library = library;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Aion2Heal");
        _requestGap = requestGap;
        // The library's newest snapshot of each player is there from the start (see Preload).
        if (library is not null)
            _ = Task.Run(() => Preload(library.LoadNewestPerPlayer()));
    }

    // A player's lookup changed state (server id, name); raised off the UI thread.
    public event Action<int, string>? Changed;

    // A running lookup got a request answered; raised off the UI thread.
    public event Action? Progressed;

    // Goes up with every Changed, for readers that check now and then instead of listening (the group frame).
    public int Version => Volatile.Read(ref _version);

    // Party members get looked up as they show up in the meter (Settings → Digital twins).
    public bool AutoLookup { get; set; }

    public static string? RegionOf(int serverId) => serverId is >= 1100 and < 1600 or >= 2100 and < 2600
        ? (serverId / 100 % 10) switch { 1 => "nae", 2 => "naw", 3 => "eu", 4 => "la", 5 => "as", _ => null }
        : null;

    public DigitalTwinLookup Get(int serverId, string name, DateTimeOffset? at = null)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(Key(serverId, name), out var entry))
                return new DigitalTwinLookup(DigitalTwinState.None, null, null);

            var twin = at is { } moment
                ? entry.Twins.LastOrDefault(t => t.FetchedAt <= moment) ?? entry.Twins.FirstOrDefault()
                : entry.Twins.LastOrDefault();
            return new DigitalTwinLookup(entry.State, twin, entry.Error);
        }
    }

    // The lookups waiting or running, in the order asked: the running one first.
    public List<DigitalTwinFetch> Fetches()
    {
        lock (_lock)
        {
            return [.. _entries.Values.Where(static entry => entry.State is DigitalTwinState.Queued or DigitalTwinState.Loading)
                .OrderBy(static entry => entry.Asked)
                .Select(static entry => new DigitalTwinFetch(entry.ServerId, entry.Name, entry.ClassName, Math.Min(1, (double)entry.Answered / entry.Expected)))];
        }
    }

    // Again: also when the player was looked up before (not while a lookup for them is still waiting or running).
    public void Request(int serverId, string name, bool again = false)
    {
        if (serverId <= 0 || string.IsNullOrWhiteSpace(name))
            return;

        var key = Key(serverId, name);
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry) && (entry.State is DigitalTwinState.Queued or DigitalTwinState.Loading || !again && !entry.FromLibrary))
                return;

            entry ??= _entries[key] = new Entry();
            entry.FromLibrary = false;
            entry.State = RegionOf(serverId) is null ? DigitalTwinState.Unsupported : DigitalTwinState.Queued;
            entry.Error = null;
            if (entry.State == DigitalTwinState.Queued)
            {
                (entry.ServerId, entry.Name, entry.Asked) = (serverId, name.Trim(), ++_asked);
                (entry.ClassName, entry.Answered, entry.Expected) = (entry.Twins.LastOrDefault()?.ClassName, 0, EstimatedRequests);
                _queue.Enqueue((serverId, name.Trim()));
                _queued.Release();
                _worker ??= Task.Run(() => RunAsync(_stop.Token));
            }
        }

        Interlocked.Increment(ref _version);
        Changed?.Invoke(serverId, name);
    }

    // Snapshots kept from earlier sessions: each player's shows right away (Ready), and still counts as not looked up this
    // session, so an automatic lookup fetches a fresh one once. A player looked up meanwhile keeps what's newer.
    internal void Preload(IEnumerable<DigitalTwin> twins)
    {
        var added = new List<DigitalTwin>();
        lock (_lock)
        {
            foreach (var twin in twins)
            {
                var key = Key(twin.ServerId, twin.Name);
                if (_entries.ContainsKey(key))
                    continue;

                var entry = _entries[key] = new Entry { State = DigitalTwinState.Ready, FromLibrary = true };
                entry.Twins.Add(twin);
                added.Add(twin);
            }
        }

        foreach (var twin in added)
        {
            Interlocked.Increment(ref _version);
            Changed?.Invoke(twin.ServerId, twin.Name);
        }
    }

    // A snapshot deleted from the library goes for the session too; a player left without any is unknown again.
    public void Remove(DigitalTwin twin)
    {
        var key = Key(twin.ServerId, twin.Name);
        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var entry) ||
                entry.Twins.RemoveAll(kept => kept.FetchedAt == twin.FetchedAt) == 0)
                return;

            if (entry.Twins.Count == 0 && entry.State is not (DigitalTwinState.Queued or DigitalTwinState.Loading))
                _entries.Remove(key);
        }

        Interlocked.Increment(ref _version);
        Changed?.Invoke(twin.ServerId, twin.Name);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _http.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _queued.WaitAsync(cancellationToken).ConfigureAwait(false);
            (int ServerId, string Name) next;
            Entry running;
            lock (_lock)
            {
                if (!_queue.TryDequeue(out next))
                    continue;
                running = _entries[Key(next.ServerId, next.Name)];
                running.State = DigitalTwinState.Loading;
            }

            Interlocked.Increment(ref _version);
            Changed?.Invoke(next.ServerId, next.Name);
            DigitalTwinState state;
            DigitalTwin? twin = null;
            string? error = null;
            try
            {
                twin = await LookUpAsync(running, next.ServerId, next.Name, cancellationToken).ConfigureAwait(false);
                state = twin is null ? DigitalTwinState.NotFound : DigitalTwinState.Ready;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Whatever the website answered, the next player still gets looked up.
                state = DigitalTwinState.Failed;
                error = ex is OperationCanceledException ? "no answer in time" : ex.Message;
            }

            lock (_lock)
            {
                var entry = _entries[Key(next.ServerId, next.Name)];
                entry.State = state;
                entry.Error = error;
                if (twin is not null)
                    entry.Twins.Add(twin);
            }

            if (twin is not null)
                _library?.Add(twin);

            Interlocked.Increment(ref _version);
            Changed?.Invoke(next.ServerId, next.Name);
        }
    }

    private async Task<DigitalTwin?> LookUpAsync(Entry entry, int serverId, string name, CancellationToken cancellationToken)
    {
        var region = RegionOf(serverId)!;
        var search = await GetAsync($"{SearchUrl}?keyword={Uri.EscapeDataString(name)}&region={region}&serverId={serverId}&localeInfo=en-US&size=40", cancellationToken).ConfigureAwait(false);
        Answered(entry);
        if (FindCharacterId(search, name, serverId) is not { } characterId)
            return null;

        var character = $"characterId={characterId}&serverId={serverId}&lang=en-US&region={region}";
        var info = await GetAsync($"{SiteUrl}info?{character}", cancellationToken).ConfigureAwait(false);
        Answered(entry, className: ClassOf(info));
        var equipment = await GetAsync($"{SiteUrl}equipment?{character}", cancellationToken).ConfigureAwait(false);
        var (gear, openBoards) = (GearSlots(equipment), OpenBoards(info));
        Answered(entry, expected: 3 + gear.Count + openBoards.Count);
        var items = new Dictionary<int, string>();
        foreach (var (id, enchant, slot) in gear)
        {
            try
            {
                items[slot] = await GetAsync($"{SiteUrl}equipment/item?id={id}&enchantLevel={enchant}&slotPos={slot}&{character}", cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                // The item still shows, without its stats.
            }

            Answered(entry);
        }

        var boards = new Dictionary<int, string>();
        foreach (var boardId in openBoards)
        {
            boards[boardId] = await GetAsync($"{SiteUrl}daevanion/detail?boardId={boardId}&{character}", cancellationToken).ConfigureAwait(false);
            Answered(entry);
        }

        return DigitalTwinBuilder.Build(serverId, DateTimeOffset.Now, info, equipment, items, boards);
    }

    // One more request of a running lookup answered; expected: the exact count once the gear list is in.
    private void Answered(Entry entry, int expected = 0, string? className = null)
    {
        lock (_lock)
        {
            entry.Answered++;
            if (expected > 0)
                entry.Expected = expected;
            entry.ClassName = className ?? entry.ClassName;
        }

        Progressed?.Invoke();
    }

    private async Task<string> GetAsync(string url, CancellationToken cancellationToken)
    {
        await Task.Delay(_requestGap, cancellationToken).ConfigureAwait(false);
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    // The website's id for the character with exactly this name on this server, escaped for a URL. Search results mark the
    // matched part of names ("<strong>Heal</strong>er"); the id comes escaped already.
    internal static string? FindCharacterId(string searchJson, string name, int serverId)
    {
        using var search = JsonDocument.Parse(searchJson);
        if (!search.RootElement.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var character in list.EnumerateArray())
        {
            if (character.ValueKind == JsonValueKind.Object &&
                character.TryGetProperty("name", out var found) && found.ValueKind == JsonValueKind.String &&
                string.Equals(Markup().Replace(found.GetString()!, "").Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                Int(character, "serverId") == serverId &&
                character.TryGetProperty("characterId", out var id) && id.ValueKind == JsonValueKind.String && id.GetString() is { Length: > 0 } characterId)
                return Uri.EscapeDataString(Uri.UnescapeDataString(characterId));
        }

        return null;
    }

    private static List<(int Id, int Enchant, int Slot)> GearSlots(string equipmentJson)
    {
        using var equipment = JsonDocument.Parse(equipmentJson);
        var slots = new List<(int, int, int)>();
        if (equipment.RootElement.TryGetProperty("equipment", out var gear) && gear.TryGetProperty("equipmentList", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
                slots.Add((Int(item, "id"), Int(item, "enchantLevel"), Int(item, "slotPos")));
        }

        return slots;
    }

    // The class on the profile ("Cleric").
    private static string? ClassOf(string infoJson)
    {
        using var info = JsonDocument.Parse(infoJson);
        return info.RootElement.TryGetProperty("profile", out var profile) && profile.ValueKind == JsonValueKind.Object &&
            profile.TryGetProperty("className", out var className) && className.ValueKind == JsonValueKind.String && className.GetString() is { Length: > 0 } text
            ? text
            : null;
    }

    private static List<int> OpenBoards(string infoJson)
    {
        using var info = JsonDocument.Parse(infoJson);
        var boards = new List<int>();
        if (info.RootElement.TryGetProperty("daevanion", out var daevanion) && daevanion.TryGetProperty("boardList", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var board in list.EnumerateArray())
            {
                if (Int(board, "open") > 0)
                    boards.Add(Int(board, "id"));
            }
        }

        return boards;
    }

    private static int Int(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;

    private static (int, string) Key(int serverId, string name) => (serverId, name.Trim().ToLowerInvariant());

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Markup();

    private sealed class Entry
    {
        public DigitalTwinState State { get; set; }
        public string? Error { get; set; }
        public List<DigitalTwin> Twins { get; } = [];
        // Only known from the library so far, not looked up this session.
        public bool FromLibrary { get; set; }
        // The last lookup asked for, as asked, and how far it got (see Fetches).
        public int ServerId { get; set; }
        public string Name { get; set; } = "";
        public long Asked { get; set; }
        public string? ClassName { get; set; }
        public int Answered { get; set; }
        public int Expected { get; set; } = EstimatedRequests;
    }
}
