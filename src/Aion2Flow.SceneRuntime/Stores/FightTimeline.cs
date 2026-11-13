using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Playback;
using Cloris.Aion2Flow.SceneRuntime.Projection;

namespace Cloris.Aion2Flow.SceneRuntime.Stores;

// The meter's columns of a boss fight, one value per second of it (the fight graphs).
public enum FightMetric
{
    // On the boss, like the fight's damage column.
    Damage,
    // On players; with overhealing left out, only the HP it restored.
    Healing,
    DamageTaken,
    // HP missing while alive, times the part of the second it was missing (the UHS column).
    UnhealedHealth,
    // The part of the second the boss was after them, 0 to 1.
    Aggro,
    // Stagger gauge points worn down.
    Stagger,
}

// One boss fight second by second: what each player dealt, healed, took, ... in each second from its first blow (the
// value at second 99 is what happened between 99 and 100 s), who died when, and for one player (the user) which skills
// it came from.
public sealed class FightTimeline
{
    public static readonly int MetricCount = Enum.GetValues<FightMetric>().Length;

    // An older ping says nothing about this moment any more (no capture, a loading screen).
    private const long PingStaleMilliseconds = 30_000;

    private FightTimeline(int seconds)
    {
        Seconds = seconds;
        Ping = new double[seconds];
        Array.Fill(Ping, double.NaN);
    }

    // Whole seconds from the first blow on; the last one may be cut short by the fight's end.
    public int Seconds { get; }

    // Each player's amount per metric and second: Values[player][(int)metric][second].
    public Dictionary<int, double[][]> Values { get; } = [];

    // Who died when, in seconds since the first blow.
    public List<(int PlayerId, double AtSeconds)> Deaths { get; } = [];

    // The user's ping (ms) in each second: the last one measured by its end, NaN without a recent one.
    public double[] Ping { get; }

    // The tracked player's amounts per skill (as the details key them), biggest first: Sources[(int)metric][second]
    // (empty without one).
    public (CombatEventKey Skill, double Amount)[][][] Sources { get; private set; } = [];

    // The fight from its first to its last blow (scene milliseconds). eventsOf gives a player's metric events in it, as the
    // details have them (source credited to its owner, heals on players only); the journal gives HP (deaths, UHS) and the
    // boss frame's target (aggro), with the boss's aimed casts standing in while the frame names no one.
    public static FightTimeline Build(
        long fromMilliseconds,
        long toMilliseconds,
        int bossId,
        IReadOnlyCollection<int> players,
        Func<int, IReadOnlyList<CombatMetricDetailEvent>> eventsOf,
        SceneJournalSegment journal,
        int sourcesOf = 0,
        bool includeOverhealing = true,
        CancellationToken cancellationToken = default)
    {
        var seconds = (int)Math.Max(1, (toMilliseconds - fromMilliseconds + 999) / 1000);
        var timeline = new FightTimeline(seconds);
        foreach (var player in players)
        {
            var values = new double[MetricCount][];
            for (var metric = 0; metric < values.Length; metric++)
                values[metric] = new double[seconds];
            timeline.Values[player] = values;
        }

        var sources = sourcesOf > 0 && players.Contains(sourcesOf) ? new Dictionary<CombatEventKey, double>?[MetricCount, seconds] : null;
        foreach (var player in players)
        {
            var values = timeline.Values[player];
            foreach (var e in eventsOf(player))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var at = e.ObservedAtMilliseconds;
                if (at < fromMilliseconds || at > toMilliseconds)
                    continue;

                var second = (int)Math.Min(seconds - 1, (at - fromMilliseconds) / 1000);
                if (e.SourceId == player)
                {
                    if (e.Metric == CombatMetricKind.Damage && e.TargetId == bossId)
                        Add(FightMetric.Damage, e.Amount);
                    else if (e.Metric == CombatMetricKind.Healing)
                        Add(FightMetric.Healing, includeOverhealing ? e.Amount : e.EffectiveHealing);
                    if (e.Stagger > 0)
                        Add(FightMetric.Stagger, e.Stagger);
                }

                if (e.TargetId == player && e.Metric == CombatMetricKind.Damage)
                    Add(FightMetric.DamageTaken, e.Amount);

                void Add(FightMetric metric, double amount)
                {
                    if (amount <= 0)
                        return;

                    values[(int)metric][second] += amount;
                    if (player == sourcesOf && sources is not null)
                    {
                        var bySkill = sources[(int)metric, second] ??= [];
                        bySkill[e.EventKey] = bySkill.GetValueOrDefault(e.EventKey) + amount;
                    }
                }
            }
        }

        if (!journal.IsEmpty)
            ReadJournal(timeline, journal, fromMilliseconds, toMilliseconds, bossId, cancellationToken);

        if (sources is not null)
        {
            timeline.Sources = new (CombatEventKey, double)[MetricCount][][];
            for (var metric = 0; metric < MetricCount; metric++)
            {
                var perSecond = timeline.Sources[metric] = new (CombatEventKey, double)[seconds][];
                for (var second = 0; second < seconds; second++)
                {
                    perSecond[second] = sources[metric, second] is { } bySkill
                        ? [.. bySkill.Select(static skill => (skill.Key, skill.Value)).OrderByDescending(static skill => skill.Item2)]
                        : [];
                }
            }
        }

        return timeline;
    }

    // HP: each player's missing HP over time (UHS, as EntityVitalStore counts it) and their deaths (HP from above 0 to 0,
    // as the boss fights count them). The boss frame's target: their aggro, from when the frame named them until it named
    // someone else; while it names no one, the boss's aimed casts (0x0238 flag 2) say whom it is after. And the user's
    // ping, the last one measured by the end of each second.
    private static void ReadJournal(FightTimeline timeline, SceneJournalSegment journal, long from, long to, int bossId, CancellationToken cancellationToken)
    {
        var hp = new Dictionary<int, (long Hp, long MaxHp, long Missing, long Since)>();
        foreach (var player in timeline.Values.Keys)
            hp[player] = (long.MaxValue, 0, 0, from);
        var (target, targetSince, byCast) = (0, from, false);
        var (pingSecond, ping, pingAt) = (0, double.NaN, 0L);
        var cursor = journal.CreateCursor();
        while (cursor.NextObservationOrdinal < journal.CurrentEndObservationOrdinalExclusive)
        {
            var read = journal.ReadEntries(cursor, ScenePlaybackTimeline.DefaultReadBatchSize, entries =>
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = entries[i];
                    var at = ScenePlaybackTimeline.ResolveOffsetMilliseconds(entry);
                    if (entry.Domain == ObservedEventDomain.EntityVital && hp.TryGetValue(entry.EntityVital.EntityId, out var state))
                    {
                        var vital = entry.EntityVital;
                        var player = vital.EntityId;
                        var since = Math.Max(at, state.Since);
                        Spread(timeline.Values[player][(int)FightMetric.UnhealedHealth], state.Since, since, state.Missing);
                        var current = Math.Max(0, vital.CurrentHp);
                        if (current == 0 && state.Hp is > 0 and < long.MaxValue && at >= from && at <= to)
                            timeline.Deaths.Add((player, (at - from) / 1000d));
                        var maxHp = vital.MaxHp is > 0 ? vital.MaxHp.Value : state.MaxHp;
                        hp[player] = (current, maxHp, current == 0 || maxHp == 0 ? 0 : Math.Max(0, maxHp - current), since);
                    }
                    else if (entry.Domain == ObservedEventDomain.State && entry.State.StateCode == StateCodes.BossTarget)
                    {
                        CreditTarget(at);
                        (target, byCast) = (entry.State.EntityId, false);
                    }
                    else if (entry.Domain == ObservedEventDomain.State && entry.State.StateCode == StateCodes.Ping && at <= to)
                    {
                        FillPing(at);
                        (ping, pingAt) = (entry.State.Value0 / 1000d, at);
                    }
                    else if (entry.Domain == ObservedEventDomain.Combat && entry.Raw.Opcode == 0x0238 && entry.SourceEntityId == bossId &&
                             entry.Combat.Flag == 2 && (target == 0 || byCast) && timeline.Values.ContainsKey(entry.Combat.ChainId))
                    {
                        CreditTarget(at);
                        (target, byCast) = (entry.Combat.ChainId, true);
                    }
                }
            });
            if (read.Count == 0)
                break;

            cursor = read.Cursor;
        }

        foreach (var (player, state) in hp)
            Spread(timeline.Values[player][(int)FightMetric.UnhealedHealth], state.Since, to, state.Missing);
        CreditTarget(to);
        FillPing(long.MaxValue);

        // The seconds that end before the next ping (at) get the one before it, while it's recent.
        void FillPing(long at)
        {
            for (; pingSecond < timeline.Seconds && from + (pingSecond + 1) * 1000L < at; pingSecond++)
            {
                var end = from + (pingSecond + 1) * 1000L;
                if (!double.IsNaN(ping) && end - pingAt <= PingStaleMilliseconds)
                    timeline.Ping[pingSecond] = ping;
            }
        }

        void CreditTarget(long until)
        {
            if (timeline.Values.TryGetValue(target, out var values))
                Spread(values[(int)FightMetric.Aggro], targetSince, until, 1);
            targetSince = Math.Max(targetSince, until);
        }

        // perSecond × the seconds of [start, end) that fall in each second of the fight.
        void Spread(double[] series, long start, long end, double perSecond)
        {
            start = Math.Max(start, from);
            end = Math.Min(end, to);
            if (perSecond == 0 || end <= start)
                return;

            for (var second = (start - from) / 1000; second < series.Length && from + second * 1000 < end; second++)
            {
                var overlap = Math.Min(end, from + (second + 1) * 1000) - Math.Max(start, from + second * 1000);
                if (overlap > 0)
                    series[second] += perSecond * overlap / 1000d;
            }
        }
    }
}
