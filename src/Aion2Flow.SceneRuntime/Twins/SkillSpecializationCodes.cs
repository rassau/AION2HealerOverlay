using System.Globalization;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Playback;

namespace Cloris.Aion2Flow.SceneRuntime.Twins;

// A skill as a player used it: the base skill and the specializations they picked for it, with how many hits showed
// that, and other picks seen for it in the same stretch (switched in between, or a part that reads like one).
public sealed record SeenSpecialization(int SkillId, int[] Specializations, int Hits, int[][] AlsoSeen);

// The specializations a player picked, read from the skill ids of their hits. An id is the base id (…0000) plus 10 × the
// picked numbers written as one number, smallest first, plus a last digit numbering the skill's parts (0 for the cast
// itself): 17070340 is Chain of Torment (17070000) with specializations 3 and 4, 12241450 Judgment with 1, 4 and 5,
// 14012353 Deadshot with 2, 3 and 5 at charge level 3. Some skills give a part an id with a single digit before the last
// (…0038) whatever was picked, so of the parts only ids with two or three such digits count.
public static class SkillSpecializationCodes
{
    public static bool TryDecode(int skillCode, out int skillId, out int[] specializations)
    {
        skillId = skillCode / 10_000 * 10_000;
        specializations = [];
        if (skillCode <= 0)
            return false;

        var part = skillCode % 10;
        var picked = skillCode % 10_000 / 10;
        if (picked == 0)
            return part == 0;

        var digits = picked.ToString(CultureInfo.InvariantCulture);
        if (part != 0 && digits.Length < 2)
            return false;

        for (var i = 0; i < digits.Length; i++)
        {
            if (digits[i] is < '1' or > '5' || i > 0 && digits[i] <= digits[i - 1])
                return false;
        }

        specializations = [.. digits.Select(static digit => digit - '0')];
        return true;
    }

    // The player's picks over a stretch of a scene (all of it, or one boss fight), from every journal entry they're the
    // source of: buffs like Light of Protection show up there too, not only hits and heals.
    public static IReadOnlyList<SeenSpecialization> Read(SceneJournalSegment segment, int sourceId, long fromMilliseconds, long toMilliseconds,
        Func<int, bool> hasSpecializations, CancellationToken cancellationToken = default)
    {
        var codes = new List<int>();
        ReadCodes(segment, fromMilliseconds, toMilliseconds, (source, code) =>
        {
            if (source == sourceId)
                codes.Add(code);
        }, cancellationToken);
        return Collect(codes, hasSpecializations);
    }

    // Every source's picks over a stretch, by source, in one pass over the journal (a boss fight's players for the twin
    // library).
    public static Dictionary<int, IReadOnlyList<SeenSpecialization>> ReadBySource(SceneJournalSegment segment, long fromMilliseconds, long toMilliseconds,
        Func<int, bool> hasSpecializations, CancellationToken cancellationToken = default)
    {
        var codes = new Dictionary<int, List<int>>();
        ReadCodes(segment, fromMilliseconds, toMilliseconds, (source, code) =>
        {
            if (!hasSpecializations(code / 10_000 * 10_000))
                return;

            if (!codes.TryGetValue(source, out var ofSource))
                codes[source] = ofSource = [];
            ofSource.Add(code);
        }, cancellationToken);
        return codes.ToDictionary(static source => source.Key, source => Collect(source.Value, hasSpecializations));
    }

    // Each journal entry of the combat domain in the stretch: its source and skill code.
    private static void ReadCodes(SceneJournalSegment segment, long fromMilliseconds, long toMilliseconds, Action<int, int> keep, CancellationToken cancellationToken)
    {
        if (segment.IsEmpty)
            return;

        var cursor = segment.CreateCursor();
        while (cursor.NextObservationOrdinal < segment.CurrentEndObservationOrdinalExclusive)
        {
            var read = segment.ReadEntries(cursor, ScenePlaybackTimeline.DefaultReadBatchSize, entries =>
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = entries[i];
                    if (entry.Domain != ObservedEventDomain.Combat)
                        continue;

                    var at = ScenePlaybackTimeline.ResolveOffsetMilliseconds(entry);
                    if (at >= fromMilliseconds && at <= toMilliseconds)
                        keep(entry.SourceEntityId, entry.Combat.SkillCode);
                }
            });
            if (read.Count == 0)
                break;

            cursor = read.Cursor;
        }
    }

    // Per skill with specializations (the rest, passives above all, use the same digits for other things), the picks seen
    // on the most hits, then the others. Picks come before none: a skill's parts can carry its bare id (a stigma at level
    // 18 sent 8 hits without its 3 after 2 with it), so none is shown only where nothing else was seen.
    public static IReadOnlyList<SeenSpecialization> Collect(IEnumerable<int> skillCodes, Func<int, bool> hasSpecializations)
    {
        var seen = new Dictionary<int, Dictionary<int, (int[] Specializations, int Hits)>>();
        foreach (var code in skillCodes)
        {
            if (!TryDecode(code, out var skillId, out var specializations) || !hasSpecializations(skillId))
                continue;

            if (!seen.TryGetValue(skillId, out var picks))
                seen[skillId] = picks = [];
            var key = code % 10_000 / 10;
            picks[key] = (specializations, picks.GetValueOrDefault(key).Hits + 1);
        }

        return seen
            .OrderBy(static skill => skill.Key)
            .Select(static skill =>
            {
                var picks = skill.Value.Values.OrderBy(static pick => pick.Specializations.Length == 0).ThenByDescending(static pick => pick.Hits)
                    .ThenBy(static pick => pick.Specializations.Length).ToArray();
                return new SeenSpecialization(skill.Key, picks[0].Specializations, picks[0].Hits, [.. picks.Skip(1).Select(static pick => pick.Specializations)]);
            })
            .ToArray();
    }
}
