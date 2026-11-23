using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.Services.Logging;
using Cloris.Aion2Flow.Services.Settings;

namespace Cloris.Aion2Flow.Services;

// One of the user's pulls, kept as a personal best of a boss or because they stored it: their values over it (per second
// on average, as the meter's boss fight rows have them), the same second by second for the fight graphs, and the history
// entry it came from, a .aion2heal file next to the index.
public sealed class BestRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ServerId { get; set; }
    public string Character { get; set; } = string.Empty;
    public CharacterClass? Class { get; set; }
    public int NpcCode { get; set; }
    public string Boss { get; set; } = string.Empty;
    public uint MapId { get; set; }
    // When the pull started (its first blow) and how long it took.
    public DateTimeOffset At { get; set; }
    public long Milliseconds { get; set; }
    // The pull in its history entry: the encounter and its place among the encounter's boss fights.
    public Guid EncounterId { get; set; }
    public int FightIndex { get; set; }
    // Stored by the user ("Store run"): it stays, whatever beats it.
    public bool Custom { get; set; }
    // A kill the overlay saw from the start; only those can be bests (the ones kept before this was saved all were). A
    // training dummy pull that ran its full length.
    public bool Complete { get; set; } = true;
    // A training dummy pull: ranked by damage alone, against pulls of the same length only (its Milliseconds).
    public bool Dummy { get; set; }
    public Dictionary<string, double> Values { get; set; } = [];
    public Dictionary<string, double[]> Curves { get; set; } = [];
}

// A metric a pull beat the best in: its value, and the best's before (none for the first pull of a boss).
public readonly record struct BestImprovement(string Key, double Value, double? Previous);

// What a kill (or a full-length training dummy pull) set: the boss and the metrics it's the best in now.
public sealed record BestRecord(string Boss, IReadOnlyList<BestImprovement> Improvements, bool Dummy = false)
{
    // The boss's first complete kill: every complete kill has damage. A metric added later has no earlier value either,
    // but the boss's earlier kills do.
    public bool IsFirst => Improvements.Any(static improvement => improvement.Key == nameof(FightMetric.Damage) && improvement.Previous is null);
}

// The fight graphs' switches for UHS, Taken and Rec.: the whole group's value (UHS*, Taken*, Rec.*) or the user's own.
public enum GroupValueMode
{
    // The group's for a cleric, the user's own for every other class.
    Auto,
    Group,
    Own,
}

// The metrics a pull is kept for: the fight graphs' metrics, healing twice (all of it, and only the HP it restored) to
// follow the meter's overhealing setting, and missing HP and the recovery time also the whole group's. The damage taken
// is always the user's own: how much the group takes isn't up to them.
public static class BestRunKeys
{
    public const string EffectiveHealing = "EffectiveHealing";
    // Everyone's missing HP in the pull added up (the graphs' "Party" line): a cleric's runs go by it.
    public const string GroupUnhealedHealth = "GroupUnhealedHealth";
    // The group's recovery time, their UHS ÷ their damage taken (the "Party" line): a cleric's runs go by it too.
    public const string GroupRecoveryTime = "GroupRecoveryTime";

    // A training dummy is there to hit.
    private static readonly string[] DummyKeys = [nameof(FightMetric.Damage)];

    // The user's choice: unhealed health (and the recovery time from it) is a healer's key figure, so Auto takes a cleric's
    // the group's, everyone else's their own; the switches can say otherwise.
    public static bool UsesGroup(GroupValueMode mode, CharacterClass? characterClass) => mode switch
    {
        GroupValueMode.Group => true,
        GroupValueMode.Own => false,
        _ => characterClass == CharacterClass.Cleric,
    };

    // What a character's runs are ranked by, missing HP and the recovery time as the UHS and Rec. switches say.
    public static string[] RankedFor(CharacterClass? characterClass, GroupValueMode uhs = GroupValueMode.Auto, GroupValueMode rec = GroupValueMode.Auto) =>
    [
        nameof(FightMetric.Damage), nameof(FightMetric.Healing), EffectiveHealing, nameof(FightMetric.DamageTaken),
        UsesGroup(uhs, characterClass) ? GroupUnhealedHealth : nameof(FightMetric.UnhealedHealth),
        UsesGroup(rec, characterClass) ? GroupRecoveryTime : nameof(FightMetric.RecoveryTime),
        nameof(FightMetric.Aggro), nameof(FightMetric.Stagger),
    ];

    public static string[] RankedFor(BestRun run, GroupValueMode uhs = GroupValueMode.Auto, GroupValueMode rec = GroupValueMode.Auto) =>
        run.Dummy ? DummyKeys : RankedFor(run.Class, uhs, rec);

    public static string Of(FightMetric metric, bool includeOverhealing, CharacterClass? characterClass, GroupValueMode uhs = GroupValueMode.Auto,
        GroupValueMode rec = GroupValueMode.Auto) => metric switch
    {
        FightMetric.Healing when !includeOverhealing => EffectiveHealing,
        FightMetric.UnhealedHealth when UsesGroup(uhs, characterClass) => GroupUnhealedHealth,
        FightMetric.RecoveryTime when UsesGroup(rec, characterClass) => GroupRecoveryTime,
        _ => metric.ToString(),
    };

    // The user's choice: less damage taken, less missing HP and a shorter recovery time are better; the boss's attention is
    // good for tanks (Templars, and Gladiators, who tank too) and bad for everyone else; more of the rest is better.
    public static bool HigherIsBetter(string key, CharacterClass? characterClass) => key switch
    {
        nameof(FightMetric.DamageTaken) or nameof(FightMetric.UnhealedHealth) or GroupUnhealedHealth or nameof(FightMetric.RecoveryTime) or GroupRecoveryTime => false,
        nameof(FightMetric.Aggro) => characterClass is CharacterClass.Templar or CharacterClass.Gladiator,
        _ => true,
    };

    public static bool Beats(double value, double best, string key, CharacterClass? characterClass) =>
        HigherIsBetter(key, characterClass) ? value > best : value < best;
}

// The personal bests per character and boss, and the pulls the user stored. A best stays while it's the best in at least
// one metric; a stored one until the user removes it. An index file holds them; each pull's history entry lies beside it.
public sealed class BestRunStore
{
    private const string IndexFileName = "index.json";
    private readonly string _directory;
    // The fight graphs' UHS and Rec. switches (Auto without settings).
    private readonly SettingsService? _settings;
    private readonly Lock _gate = new();
    private readonly List<BestRun> _runs;

    public BestRunStore(string directory, SettingsService? settings = null)
    {
        _directory = directory;
        _settings = settings;
        _runs = Load();
    }

    // A new best, or a run stored or removed; from any thread.
    public event Action? Changed;

    public IReadOnlyList<BestRun> Runs
    {
        get
        {
            lock (_gate)
                return [.. _runs];
        }
    }

    public string FileOf(BestRun run) => Path.Combine(_directory, run.Id.ToString("N") + SceneArchiveFile.Extension);

    // The character's best pull of a boss by one metric; of a training dummy, the best pull of that length.
    public BestRun? BestOf(int serverId, string character, int npcCode, string key, long dummyMilliseconds = 0)
    {
        lock (_gate)
            return Best(Of(serverId, character, npcCode, dummyMilliseconds), key);
    }

    // The metrics a run is its character's best of the boss in.
    public IReadOnlySet<string> BestKeysOf(BestRun run)
    {
        lock (_gate)
        {
            var runs = Of(run).ToArray();
            return RankedFor(run).Where(key => ReferenceEquals(Best(runs, key), run)).ToHashSet();
        }
    }

    // What a run is ranked by: missing HP and the recovery time as the switches say now.
    private string[] RankedFor(BestRun run) =>
        BestRunKeys.RankedFor(run, _settings?.Current.FightGraphsUhs ?? GroupValueMode.Auto, _settings?.Current.FightGraphsRec ?? GroupValueMode.Auto);

    // A run comes in and stays while it's the best in what it's ranked by now or in what Auto ranks it by: a switch tried for
    // a while costs none of the class's own bests, new ones included (Auto keeps just its own, as before the switches).
    private string[] KeptFor(BestRun run) => [.. RankedFor(run).Union(BestRunKeys.RankedFor(run))];

    public bool IsStored(Guid encounterId, int fightIndex)
    {
        lock (_gate)
            return _runs.Any(run => run.Custom && run.EncounterId == encounterId && run.FightIndex == fightIndex);
    }

    // A finished pull: the metrics it beats the character's bests of the boss in. If any, it's kept with its history
    // entry (made only then), and pulls that are no longer the best in anything go, unless the user stored them.
    public Task<IReadOnlyList<BestImprovement>> ConsiderAsync(BestRun candidate, Func<ArchivedEncounterRecord> historyEntry, CancellationToken cancellationToken = default) =>
        AddAsync(candidate, historyEntry, cancellationToken);

    // "Store run": the user keeps a pull whether it beats anything or not; one kept already (a best) becomes theirs.
    public async Task<IReadOnlyList<BestImprovement>> StoreAsync(BestRun run, Func<ArchivedEncounterRecord> historyEntry, CancellationToken cancellationToken = default)
    {
        BestRun? kept;
        lock (_gate)
        {
            kept = _runs.FirstOrDefault(existing => existing.EncounterId == run.EncounterId && existing.FightIndex == run.FightIndex);
            if (kept is not null)
            {
                kept.Custom = true;
                Save();
            }
        }

        if (kept is not null)
        {
            Changed?.Invoke();
            return [];
        }

        run.Custom = true;
        return await AddAsync(run, historyEntry, cancellationToken).ConfigureAwait(false);
    }

    // The trash: a stored run goes, unless it's a best, which stays as one.
    public void Remove(BestRun run)
    {
        bool gone, saved;
        lock (_gate)
        {
            run.Custom = false;
            var runs = Of(run).ToArray();
            gone = !KeptFor(run).Any(key => ReferenceEquals(Best(runs, key), run));
            if (gone)
                _runs.Remove(run);
            saved = Save();
        }

        // The index on disk lists the run until a save works.
        if (gone && saved)
            DeleteFile(run);
        Changed?.Invoke();
    }

    private async Task<IReadOnlyList<BestImprovement>> AddAsync(BestRun run, Func<ArchivedEncounterRecord> historyEntry, CancellationToken cancellationToken)
    {
        List<BestImprovement> improvements = [];
        var kept = run.Custom;
        if (run.Complete)
        {
            lock (_gate)
            {
                // Kept for a best by Auto too, while a switch says otherwise; told (the banner) only what it's ranked by now.
                var runs = Of(run).ToArray();
                var ranked = RankedFor(run);
                foreach (var key in KeptFor(run))
                {
                    if (!run.Values.TryGetValue(key, out var value))
                        continue;

                    var best = Best(runs, key);
                    if (best is null || BestRunKeys.Beats(value, best.Values[key], key, run.Class))
                    {
                        kept = true;
                        if (ranked.Contains(key))
                            improvements.Add(new BestImprovement(key, value, best?.Values[key]));
                    }
                }
            }
        }

        if (!kept)
            return improvements;

        // Made before the file: one that fails to be made or written leaves none behind.
        var entry = historyEntry();
        Directory.CreateDirectory(_directory);
        try
        {
            await using var stream = File.Create(FileOf(run));
            await SceneArchiveFile.WriteAsync(stream, [entry], cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            DeleteFile(run);
            throw;
        }

        List<BestRun> dropped;
        bool saved;
        lock (_gate)
        {
            // One entry per pull, as StoreAsync keeps it: a store and the pull's best can cross (a pull can be stored while
            // it's still on, and a best takes a moment to write). The complete copy stays, and the user's mark with it.
            var twin = _runs.Find(other => other.EncounterId == run.EncounterId && other.FightIndex == run.FightIndex);
            if (twin is not null && (twin.Complete || !run.Complete))
            {
                twin.Custom |= run.Custom;
                dropped = [run];
            }
            else
            {
                if (twin is not null)
                {
                    run.Custom |= twin.Custom;
                    _runs.Remove(twin);
                }

                _runs.Add(run);
                var runs = Of(run).ToArray();
                var bests = KeptFor(run).Select(key => Best(runs, key)).OfType<BestRun>().ToHashSet();
                dropped = [.. runs.Where(other => !bests.Contains(other) && !other.Custom)];
                _runs.RemoveAll(dropped.Contains);
                if (twin is not null)
                    dropped.Add(twin);
            }

            saved = Save();
        }

        // The index on disk lists the runs it had until a save works; the new run is in none.
        foreach (var gone in dropped)
        {
            if (saved || ReferenceEquals(gone, run))
                DeleteFile(gone);
        }

        Changed?.Invoke();
        return improvements;
    }

    private void DeleteFile(BestRun run)
    {
        try
        {
            File.Delete(FileOf(run));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write(AppLogLevel.Warning, $"Removing the run '{FileOf(run)}' failed: {ex.Message}");
        }
    }

    // A boss's runs, or the training dummies' of one length (dummyMilliseconds): every kind takes the same damage (the two in
    // Altgard hit for hit), so they share their bests, whatever their code (none for one whose spawn the overlay missed).
    private IEnumerable<BestRun> Of(int serverId, string character, int npcCode, long dummyMilliseconds) =>
        _runs.Where(run => run.ServerId == serverId &&
                           (dummyMilliseconds > 0 ? run.Dummy && run.Milliseconds == dummyMilliseconds : !run.Dummy && run.NpcCode == npcCode) &&
                           string.Equals(run.Character, character, StringComparison.Ordinal));

    private IEnumerable<BestRun> Of(BestRun run) => Of(run.ServerId, run.Character, run.NpcCode, run.Dummy ? run.Milliseconds : 0);

    // Of the complete kills; the earlier of two equal pulls stays the best.
    private static BestRun? Best(IEnumerable<BestRun> runs, string key)
    {
        BestRun? best = null;
        foreach (var run in runs)
        {
            if (run.Complete && run.Values.TryGetValue(key, out var value) && (best is null || BestRunKeys.Beats(value, best.Values[key], key, run.Class)))
                best = run;
        }

        return best;
    }

    private List<BestRun> Load()
    {
        var path = Path.Combine(_directory, IndexFileName);
        try
        {
            if (!File.Exists(path))
                return [];

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, BestRunJsonContext.Default.ListBestRun) ?? [];
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Reading the best runs from '{path}' failed: {ex}");
            return [];
        }
    }

    // Written aside and moved over, so a crash halfway leaves the last good index; false when the last good index stayed.
    private bool Save()
    {
        var path = Path.Combine(_directory, IndexFileName);
        try
        {
            Directory.CreateDirectory(_directory);
            var temp = path + ".tmp";
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, _runs, BestRunJsonContext.Default.ListBestRun);
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Saving the best runs to '{path}' failed: {ex}");
            return false;
        }
    }
}

// Watches the live boss fights for kills: each complete one is worked out for the user, second by second, and handed to
// the store; what it beat is kept for the fight graphs' banner.
public sealed class BestRunRecorder(BestRunStore store, SceneLiveReadModel scene, GameResourceService resources)
{
    // The boss lost at least this share of its HP while the overlay watched: a pull it came into halfway doesn't count.
    private const double CompleteShare = 0.9;
    private readonly HashSet<(Guid Encounter, int Fight)> _seen = [];
    private readonly ConcurrentDictionary<(Guid Encounter, int Fight), BestRecord> _records = [];

    public BestRunStore Store => store;

    public BestRecord? RecordOf(Guid encounterId, int fightIndex) => _records.GetValueOrDefault((encounterId, fightIndex));

    // The kills being worked out (for tests).
    internal Task Pending { get; private set; } = Task.CompletedTask;

    // A kill the overlay watched (nearly) from the start: the boss lost at least 90 % of its HP meanwhile. A training
    // dummy pull that ran its full length.
    public static bool IsComplete(SceneBossFight fight)
    {
        if (fight.IsTrainingDummy)
            return fight.Outcome == BossFightOutcome.TimeUp;

        if (fight.Outcome != BossFightOutcome.Killed || fight.BossMaxHp <= 0)
            return false;

        var damage = 0d;
        foreach (ref readonly var combatant in fight.Snapshot.Combatants.AsSpan())
            damage += combatant.Metrics.DamageAmount;
        return damage >= CompleteShare * fight.BossMaxHp;
    }

    // The players in a pull, as the fight graphs count them (its "Party").
    public static int[] PlayersOf(SceneBossFight fight) =>
    [
        .. fight.Snapshot.Combatants.AsSpan().ToArray()
            .Where(static combatant => combatant.Metrics.IsVisiblePlayerCombatant && combatant.Metrics.CharacterClass is not null)
            .Select(static combatant => combatant.Id)
            .Order(),
    ];

    // The user's seconds of a pull, as the fight graphs have them, healing also without overhealing, and the whole group's
    // missing HP (everyone's from the journal) and recovery time (eventsOf gives everyone's events, the user's too); the
    // values are their totals per second of the pull, a recovery time the whole pull's.
    public static void WorkOut(BestRun run, int me, IReadOnlyCollection<int> players, SceneBossFight fight, Func<int, IReadOnlyList<CombatMetricDetailEvent>> eventsOf,
        SceneJournalSegment journal)
    {
        var (from, to) = (fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime);
        int[] everyone = players.Contains(me) ? [.. players] : [.. players, me];
        var timeline = FightTimeline.Build(from, to, fight.BossId, everyone, eventsOf, journal);
        var group = new double[timeline.Seconds];
        foreach (var player in everyone)
        {
            var missing = timeline.Values[player][(int)FightMetric.UnhealedHealth];
            for (var second = 0; second < group.Length; second++)
                group[second] += missing[second];
        }

        run.Curves[BestRunKeys.GroupUnhealedHealth] = group;
        run.Curves[BestRunKeys.GroupRecoveryTime] = FightTimeline.RecoveryTime(timeline, everyone);
        var effective = new double[timeline.Seconds];
        foreach (var e in eventsOf(me))
        {
            if (e.SourceId == me && e.Metric == CombatMetricKind.Healing && e.EffectiveHealing > 0 && e.ObservedAtMilliseconds >= from && e.ObservedAtMilliseconds <= to)
                effective[(int)Math.Min(effective.Length - 1, (e.ObservedAtMilliseconds - from) / 1000)] += e.EffectiveHealing;
        }

        var values = timeline.Values[me];
        foreach (var metric in Enum.GetValues<FightMetric>())
            run.Curves[metric.ToString()] = values[(int)metric];
        run.Curves[BestRunKeys.EffectiveHealing] = effective;
        var seconds = Math.Max(1, run.Milliseconds) / 1000d;
        foreach (var (key, curve) in run.Curves)
            run.Values[key] = curve.Sum() / seconds;
        // Not an average: the pull's UHS ÷ its damage taken, its curve's last second. Without one it's none, never a best.
        foreach (var key in (string[])[nameof(FightMetric.RecoveryTime), BestRunKeys.GroupRecoveryTime])
        {
            if (double.IsNaN(run.Curves[key][^1]))
                run.Values.Remove(key);
            else
                run.Values[key] = run.Curves[key][^1];
        }
    }

    // Each live frame (UI thread): a kill (or a dummy pull's full length) not seen yet is worked out in the background.
    public void Observe(SceneReadModelFrame frame)
    {
        var fights = frame.BossFights;
        for (var i = 0; i < fights.Count; i++)
        {
            if (fights[i].Outcome is not (BossFightOutcome.Killed or BossFightOutcome.TimeUp) || !_seen.Add((frame.Snapshot.EncounterId, i)))
                continue;

            if (Candidate(frame, i) is var (candidate, me))
                Pending = Task.WhenAll(Pending, RecordAsync(candidate, me, fights[i], frame.Snapshot));
        }
    }

    // The user's complete kill of a boss the game data knows (or full-length pull on a dummy), by a known character.
    private (BestRun Run, int Me)? Candidate(SceneReadModelFrame frame, int index)
    {
        var fight = frame.BossFights[index];
        var registry = frame.MetadataRegistry;
        var me = registry?.LocalPlayerEntityId ?? 0;
        if (me <= 0 || registry is null || !fight.Snapshot.Combatants.TryGetValue(me, out var mine) ||
            !registry.TryGetPcMetadata(me, out var pc) || pc.OriginServerId is not > 0 || !pc.HasNickname ||
            !(registry.TryGetNpcCode(fight.BossId, out var npcCode) || fight.IsTrainingDummy) || !IsComplete(fight))
        {
            return null;
        }

        return (new BestRun
        {
            ServerId = pc.OriginServerId.Value,
            Character = pc.Nickname,
            Class = mine.CharacterClass ?? pc.CharacterClass,
            NpcCode = npcCode,
            Boss = npcCode > 0 ? resources.ResolveNpcName(npcCode) : string.Empty,
            MapId = frame.Snapshot.MapId,
            At = scene.SessionStarted.AddMilliseconds(fight.Snapshot.EncounterStartTime).ToLocalTime(),
            Milliseconds = fight.Snapshot.EncounterTime,
            EncounterId = frame.Snapshot.EncounterId,
            FightIndex = index,
            Dummy = fight.IsTrainingDummy,
        }, me);
    }

    private async Task RecordAsync(BestRun candidate, int me, SceneBossFight fight, SceneCombatSnapshot snapshot)
    {
        try
        {
            var (from, to) = (fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime);
            // The journal here, on the UI thread, as the fight graphs take theirs: taking it applies the newest entries under
            // the owner's lock alone, which on the pool raced the UI reading the registry. The capture thread's lock-free
            // reads race it on either thread, as they race the fight graphs'.
            var journal = scene.Owner.CreateLiveTimelineSegment().CreateBoundedSnapshot();
            var players = PlayersOf(fight);
            await Task.Run(() => WorkOut(candidate, me, players, fight, scene.CreateMetricEventsWithin(snapshot, players.Contains(me) ? players : [.. players, me], from, to),
                journal)).ConfigureAwait(false);
            // The scene moved on meanwhile (a map change, the next pull in boss mode): what was read is the next scene's.
            if (scene.Owner.EncounterId != candidate.EncounterId)
            {
                AppLog.Write(AppLogLevel.Info, $"A best run of '{candidate.Boss}' was left out: the scene changed while it was worked out.");
                return;
            }
            var improvements = await store.ConsiderAsync(candidate, () =>
            {
                var payload = scene.CreateArchivePayload();
                return new ArchivedEncounterRecord { ArchivedAt = payload.SceneStarted.ToLocalTime(), Trigger = "best", IsAutomatic = true, ScenePayload = payload };
            }).ConfigureAwait(false);
            if (improvements.Count > 0)
                _records[(candidate.EncounterId, candidate.FightIndex)] = new BestRecord(candidate.Boss, improvements, candidate.Dummy);
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Recording a best run of '{candidate.Boss}' failed: {ex}");
        }
    }
}

[JsonSourceGenerationOptions(UseStringEnumConverter = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(List<BestRun>))]
internal sealed partial class BestRunJsonContext : JsonSerializerContext;
