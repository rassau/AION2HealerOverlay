using System.Text.Json;
using Cloris.Aion2Flow.SceneRuntime.Twins;

namespace Cloris.Aion2Flow.Services;

// What each skill's five specializations do, as the game client words them (English only), with the skill level each
// needs (none given for the stigmas' fifth): Assets/skill-specializations.json, read from metabot.gg's skill pages for
// the 4 Nov 2026 patch and checked against bloxodes.com. Keyed by the skill id the overlay sees.
public static class SkillSpecializations
{
    public readonly record struct Specialization(int? SkillLevel, string Effect);

    public sealed record Skill(bool IsStigma, Specialization[] Specializations);

    private static readonly Lazy<Dictionary<int, Skill>> s_skills = new(Load);

    public static bool Has(int skillId) => s_skills.Value.ContainsKey(skillId);

    public static Skill? Get(int skillId) => s_skills.Value.GetValueOrDefault(skillId);

    public static int Count => s_skills.Value.Count;

    // What the picks do, by number. A stigma's single number is how far its specializations are unlocked, the ones below
    // included; an active skill's numbers are the picked ones.
    public static IEnumerable<(int Number, string Effect)> EffectsOf(int skillId, int[] picks)
    {
        if (Get(skillId) is not { } skill)
            return [];

        var numbers = skill.IsStigma && picks.Length == 1 ? Enumerable.Range(1, picks[0]) : picks;
        return numbers.Where(n => n >= 1 && n <= skill.Specializations.Length).Select(n => (n, skill.Specializations[n - 1].Effect));
    }

    // The skill levels a stigma's number allows: its specializations unlock at set levels (5, 10, 15 and 20 for every
    // stigma), so 3 is level 15 to 19 and 4 is 20 or more, as the fifth's level isn't known; a 5 (never seen) is past the
    // fourth's, 21 or more. No gear or Daevanion adds to a stigma's level, and wherever the game also sent the level the
    // number matched it (bar a few hits in crowds), but on battlefields every stigma reads 4 (callers skip them). Null for
    // an active skill, more numbers than one, or none: high stigmas' parts carry the bare id too.
    public static SeenLevelRange? StigmaLevelRange(int skillId, int[] picks)
    {
        if (Get(skillId) is not { IsStigma: true } skill || picks is not [var unlocked] || unlocked < 1 || unlocked > skill.Specializations.Length)
            return null;

        var levels = skill.Specializations.Select(static spec => spec.SkillLevel).ToArray();
        var min = levels[unlocked - 1] ?? levels[..(unlocked - 1)].Max() + 1;
        return min is { } lowest ? new SeenLevelRange(lowest, unlocked < levels.Length ? levels[unlocked] - 1 : null) : null;
    }

    // A player's stigmas' ranges by skill, from their picks (the ones on the most hits, see SkillSpecializationCodes.Collect).
    public static Dictionary<int, SeenLevelRange> StigmaLevelRanges(IEnumerable<SeenSpecialization> seen)
    {
        var ranges = new Dictionary<int, SeenLevelRange>();
        foreach (var skill in seen)
        {
            if (StigmaLevelRange(skill.SkillId, skill.Specializations) is { } range)
                ranges[skill.SkillId] = range;
        }

        return ranges;
    }

    private static Dictionary<int, Skill> Load()
    {
        using var stream = typeof(SkillSpecializations).Assembly.GetManifestResourceStream("skill-specializations.json")
            ?? throw new InvalidOperationException("skill-specializations.json is missing from the program.");
        using var document = JsonDocument.Parse(stream);
        var skills = new Dictionary<int, Skill>();
        foreach (var skill in document.RootElement.GetProperty("skills").EnumerateObject())
        {
            var specializations = skill.Value.GetProperty("specs").EnumerateArray()
                .Select(static spec => new Specialization(spec[0].ValueKind == JsonValueKind.Number ? spec[0].GetInt32() : null, spec[1].GetString() ?? ""))
                .ToArray();
            skills[int.Parse(skill.Name, System.Globalization.CultureInfo.InvariantCulture)] = new Skill(skill.Value.GetProperty("stigma").GetBoolean(), specializations);
        }

        return skills;
    }
}
