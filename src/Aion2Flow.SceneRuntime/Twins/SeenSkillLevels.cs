using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Playback;

namespace Cloris.Aion2Flow.SceneRuntime.Twins;

// A stigma's skill level as far as a fight tells it, from Min to Max; no Max: no end known ("20+").
public readonly record struct SeenLevelRange(int Min, int? Max)
{
    public bool Contains(int level) => level >= Min && (Max is null || level <= Max);

    // "15–19", "20+".
    public override string ToString() => Max is { } max ? $"{Min}–{max}" : $"{Min}+";
}

// The levels the game sent for a player's skills in a fight, by base skill (…0000): the skill setup they fought with,
// where the website lists one of their three. At: the end of that fight. Ranges: the levels of stigmas it sent none for,
// as far as their hits tell (see SkillSpecializations.StigmaLevelRange); a level in Levels wins over a range.
public sealed record SeenSkillLevels(DateTimeOffset At, IReadOnlyDictionary<int, int> Levels)
{
    public IReadOnlyDictionary<int, SeenLevelRange> Ranges { get; init; } = new Dictionary<int, SeenLevelRange>();

    // Battlefields, where every level reads 20.
    // ponytail: battlefields only, by their map or, while the map isn't known yet, by their 20s (see IsBattlefield); add
    // arena ids if they turn out to normalize levels too.
    public const uint BattlefieldMapId = 62;

    // A battlefield: map 62, or a map not known yet (the overlay started inside one, until the first map packet) where nine
    // in ten of ten or more levels read 20; elsewhere 0 to 10 % do. Not every unknown map: a boss fight there is real.
    public static bool IsBattlefield(uint mapId, Dictionary<int, Dictionary<int, int>> levels)
    {
        if (mapId != 0)
            return mapId == BattlefieldMapId;

        var all = levels.Values.Sum(static ofCaster => ofCaster.Count);
        var twenty = levels.Values.Sum(static ofCaster => ofCaster.Values.Count(static level => level == 20));
        return all >= 10 && twenty * 10 >= all * 9;
    }

    // An effect id is a skill code with a digit or two more (177300001, 1742001011 for 17730000, 17420010): cut back to
    // the skill's eight digits as BuffUptime.SkillCodeOf does, then to the base skill.
    public static int SkillOf(long code)
    {
        while (code >= 100_000_000)
            code /= 10;
        return (int)(code / 10_000 * 10_000);
    }

    // The last level per caster and base skill up to a time, from the aura opens (0x2A38, groups 17 and 19) and the
    // passives of spawns (StateCodes.SkillLevel). The level is the effective one, gear and Daevanion included, as on the
    // website. The last one wins: zone-in sends 1, then the base level, then the effective one, and switching setups
    // sends them again. 0 and 1 (effects whose numbers don't go by level, or no level) never count. Callers skip
    // battlefields (see IsBattlefield). This reads the raw journal, not AuraStore, which keeps group 19 only and would
    // drop the group-17 passives. An aura a summon cast names the summon, so it drops out (in the histories, 6 of 6
    // didn't match the owner's website level).
    // ponytail: skills that only hit or heal send no level; the user's own full skill list (0x0051 at each map change,
    // 0x0F38/0x1338 for one skill) has all of theirs, read it if the own twin should show every level.
    public static Dictionary<int, Dictionary<int, int>> Read(SceneJournalSegment segment, long toMilliseconds, CancellationToken cancellationToken = default)
    {
        var levels = new Dictionary<int, Dictionary<int, int>>();
        if (segment.IsEmpty)
            return levels;

        var cursor = segment.CreateCursor();
        while (cursor.NextObservationOrdinal < segment.CurrentEndObservationOrdinalExclusive)
        {
            var read = segment.ReadEntries(cursor, ScenePlaybackTimeline.DefaultReadBatchSize, entries =>
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = entries[i];
                    if (entry.Domain == ObservedEventDomain.Aura)
                    {
                        ref readonly var aura = ref entry.Aura;
                        if (aura.Kind == AuraObservationKind.Open && aura.GroupCode is 17 or 19 && ScenePlaybackTimeline.ResolveOffsetMilliseconds(entry) <= toMilliseconds)
                            Keep(aura.EchoSourceEntityId, aura.HeadCode, aura.StackCount);
                    }
                    else if (entry.Domain == ObservedEventDomain.State && entry.State.StateCode == StateCodes.SkillLevel &&
                        ScenePlaybackTimeline.ResolveOffsetMilliseconds(entry) <= toMilliseconds)
                    {
                        Keep(entry.State.EntityId, entry.State.Value0, entry.State.Value1);
                    }
                }
            });
            if (read.Count == 0)
                break;

            cursor = read.Cursor;
        }

        return levels;

        void Keep(int casterId, long code, long level)
        {
            if (casterId <= 0 || level < 2)
                return;

            if (!levels.TryGetValue(casterId, out var ofCaster))
                levels[casterId] = ofCaster = [];
            ofCaster[SkillOf(code)] = (int)level;
        }
    }
}
