using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cloris.Aion2Flow.Services;

// What each class skill does, as the game client's tooltip words it (English only): Assets/skill-effects.json, read from
// metabot.gg's skill pages (update with tools/update-skill-specializations.py, outside the repository). The numbers that
// change with the skill level come as {0}, {1}, … with their values for each level; Text has them as "level 1–max" ranges
// ("by 10.5–22.5%"), At(level) at one level. Skills whose wording changes between levels only have the ranges. Keyed by
// base skill id; any id of a skill (with specializations or parts in its last digits) finds it.
public static partial class SkillEffects
{
    public sealed record Effect(int MaxLevel, string Text, string? Template = null, string[][]? Levels = null)
    {
        // The tooltip at a skill level; null where the numbers per level aren't known or the level is past 1 to MaxLevel.
        public string? At(int level) =>
            Template is not null && Levels is not null && level >= 1 && level <= Levels.Length ? Fill(Template, Levels[level - 1]) : null;
    }

    private static readonly Lazy<Dictionary<int, Effect>> s_effects = new(Load);

    public static Effect? Get(int skillCode) => skillCode > 0 ? s_effects.Value.GetValueOrDefault(skillCode / 10_000 * 10_000) : null;

    public static int Count => s_effects.Value.Count;

    private static Dictionary<int, Effect> Load()
    {
        using var stream = typeof(SkillEffects).Assembly.GetManifestResourceStream("skill-effects.json")
            ?? throw new InvalidOperationException("skill-effects.json is missing from the program.");
        using var document = JsonDocument.Parse(stream);
        var effects = new Dictionary<int, Effect>();
        foreach (var skill in document.RootElement.GetProperty("skills").EnumerateObject())
        {
            var max = skill.Value.GetProperty("max").GetInt32();
            var text = skill.Value.GetProperty("text").GetString() ?? "";
            Effect effect;
            if (skill.Value.TryGetProperty("levels", out var levels))
            {
                var numbers = levels.EnumerateArray().Select(static level => (level.GetString() ?? "").Split('|')).ToArray();
                var range = numbers[0].Zip(numbers[^1], static (low, high) => low == high ? low : $"{low}–{high}").ToArray();
                effect = new Effect(max, Fill(text, range), text, numbers);
            }
            else
            {
                effect = new Effect(max, text);
            }

            effects[int.Parse(skill.Name, CultureInfo.InvariantCulture)] = effect;
        }

        return effects;
    }

    private static string Fill(string template, string[] values) =>
        Placeholder().Replace(template, match => values[int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture)]);

    [GeneratedRegex(@"\{(\d+)\}")]
    private static partial Regex Placeholder();
}
