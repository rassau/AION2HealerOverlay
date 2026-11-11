using System.Text.Json;

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
    // included (still to be confirmed in game); an active skill's numbers are the picked ones.
    public static IEnumerable<(int Number, string Effect)> EffectsOf(int skillId, int[] picks)
    {
        if (Get(skillId) is not { } skill)
            return [];

        var numbers = skill.IsStigma && picks.Length == 1 ? Enumerable.Range(1, picks[0]) : picks;
        return numbers.Where(n => n >= 1 && n <= skill.Specializations.Length).Select(n => (n, skill.Specializations[n - 1].Effect));
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
