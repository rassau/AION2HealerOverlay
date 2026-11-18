using System.Globalization;
using Cloris.Aion2Flow.Resources.Generated;
using Cloris.Aion2Flow.SceneRuntime.Twins;
using Cloris.Aion2Flow.Services;

namespace Cloris.Aion2Flow.ViewModels;

// One line of a digital twin's cards: a stat, item, skill, board or title, its value, and below it what it's made of. A
// skill's line has its icon, and hovering it shows what the skill does (Tip).
public sealed record DigitalTwinRowViewModel(string Label, string Value, string? Detail = null, string? IconAssetName = null, SkillTip? Tip = null)
{
    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    // A skill's level the game sent in a fight, or a stigma's range from one (the website's level where it's inside):
    // in italics.
    public bool IsSeen => Tip?.Seen == true;
}

// What hovering a skill shows (see SkillTipTemplate): its name, what it does with the numbers at the caster's level when
// that's known (see GroupFrameViewModel.DescribeAura), the caster's specializations where the skill's code tells them, and
// which levels the numbers are for. Seen: the level is one the game sent in a fight, or a stigma's range from one (see
// SeenSkillLevels).
public sealed record SkillTip(string Name, string? Effect, string? EffectNote, string? Specializations, bool Seen = false)
{
    // skillName: the name of the skill the code belongs to, when the code is one of its effects (Earth's Blessing).
    // levelNote: what the note says the numbers' level is, where they're at a level.
    internal static SkillTip For(int skillCode, string name, string? skillName, LocalizationService localization, int? level = null, string? caster = null,
        string? levelNote = null)
    {
        var (effect, note, specializations) = GroupFrameViewModel.DescribeAura(skillCode, localization, level, caster, name, skillName, levelNote);
        return new SkillTip(name, effect, note, specializations);
    }

    // At a level the game sent in a fight (at: its end): the note names the fight, and the website's level (its text, as the
    // twin's cards show it) where there's a twin.
    internal static SkillTip ForSeen(int skillCode, string name, LocalizationService localization, int level, string? caster, DateTimeOffset at, string? website = null)
    {
        var culture = CultureInfo.CurrentCulture;
        var note = string.Format(culture, localization["Twin_SeenLevelTip"], level, caster, at.ToLocalTime()) +
            (website is null ? "" : Environment.NewLine + string.Format(culture, localization["Twin_WebsiteLevel"], website));
        return For(skillCode, name, name, localization, level, caster, note) with { Seen = true };
    }

    // For a stigma whose level a fight only narrowed down (see SkillSpecializations.StigmaLevelRange): the numbers at
    // level (the website's, where it's inside the range) or from level 1 to max, then the fight and the range, then the
    // website's level where given (it's outside the range).
    internal static SkillTip ForRange(int skillCode, string name, LocalizationService localization, SeenLevelRange range, int? level, string? caster, DateTimeOffset at,
        string? website = null)
    {
        var culture = CultureInfo.CurrentCulture;
        var tip = For(skillCode, name, name, localization, level, caster);
        var note = string.Format(culture, localization["Twin_SeenRangeTip"], range, at.ToLocalTime()) +
            (website is null ? "" : Environment.NewLine + string.Format(culture, localization["Twin_WebsiteLevel"], website));
        return tip with { EffectNote = tip.EffectNote is null ? note : tip.EffectNote + Environment.NewLine + note, Seen = true };
    }
}

// A digital twin as its cards show it (the details' Digital twin tab, the library): who, combat power, item level and when,
// then the sums of its stats, base stats, gear, skills (at the level the website gives: "21 (7 from gear, 4 from
// Daevanion)", in groups by kind, see SkillGroup), Daevanion boards and titles.
// seen: the levels the game sent for the player in a fight (see SeenSkillLevels). Each skill it has is at that level, in
// italics: the website's text where they're equal, else the number with "Website: 21 (…)" under the name; its hover text
// names the fight. The website lists one of the player's three skill setups, the fight shows the one they played: a
// skill seen was in use, so the website's "not equipped" says it is the website's ("Website: not equipped"). A stigma
// the fight only gives a range for (see SeenSkillLevels.Ranges) is in italics too: the website's text where it's inside
// the range, "Fight: 15–19" under the name, else the range with "Website: 12" under it.
// ponytail: a seen skill the website doesn't list (learned after the snapshot) isn't shown; add it to its group if that
// comes up.
public sealed record DigitalTwinSheet(string Header, string Summary, DigitalTwinRowViewModel[] Stats, DigitalTwinRowViewModel[] PrimaryStats,
    DigitalTwinRowViewModel[] Gear, DigitalTwinRowGroup[] Skills, DigitalTwinRowViewModel[] Boards, DigitalTwinRowViewModel[] Titles)
{
    public static DigitalTwinSheet From(DigitalTwin twin, LocalizationService localization, SeenSkillLevels? seen = null)
    {
        var culture = CultureInfo.CurrentCulture;
        string Stat(TwinStatValue stat) =>
            stat.Name + " " + (stat.Min is { } min ? $"{Number(min)}–{Number(stat.Value)}" : Number(stat.Value, stat.Percent)) +
            (stat.Extra != 0 ? $" +{Number(stat.Extra)}" : "");

        DigitalTwinRowViewModel Item(TwinItem item)
        {
            var parts = item.MainStats.Concat(item.SubStats).Select(Stat)
                .Concat(item.Stones.Select(Stat))
                .Concat(item.SkillBonuses.Select(skill => $"{skill.Name} +{skill.Levels}"))
                .Concat(item.Theostones);
            var level = item.Enchant > 0 ? $"+{item.Enchant}" : "";
            if (item.Exceed > 0)
                level += " " + string.Format(culture, localization["Twin_Exceed"], item.Exceed);
            return new DigitalTwinRowViewModel($"{item.Slot}: {item.Name}", level.Trim(), string.Join(" · ", parts));
        }

        return new DigitalTwinSheet(
            string.Format(culture, localization["Twin_Header"], twin.Name, twin.ClassName, twin.Level, twin.ServerName),
            string.Format(culture, localization["Twin_Summary"], twin.CombatPower, twin.ItemLevel, twin.FetchedAt.ToLocalTime()) +
                string.Concat(new[] { twin.Pet, twin.Wings }.OfType<string>().Select(static part => " · " + part)) +
                (twin.Skills.Any(skill => SeenLevel(seen, skill.Id) is not null || SeenRange(seen, skill.Id) is not null)
                    ? " · " + string.Format(culture, localization["Twin_SeenSummary"], seen!.At.ToLocalTime())
                    : ""),
            [.. twin.Totals.Select(static total => new DigitalTwinRowViewModel(total.Name,
                Number(total.Value, total.Percent) + (total.Min is { } min && min != total.Value ? $" (min {Number(min)})" : ""),
                string.Join(" · ", total.Sources.Select(source => $"{source.Source} {Number(source.Value, total.Percent)}"))))],
            [.. twin.PrimaryStats.Select(static stat => new DigitalTwinRowViewModel(stat.Name, Number(stat.Value), string.Join(" · ", stat.Effects)))],
            [.. twin.Gear.Select(Item)],
            [.. twin.Skills.GroupBy(skill => SkillGroup(skill.Category, localization)).OrderBy(static group => group.Key.Order)
                .Select(group => new DigitalTwinRowGroup(group.Key.Title, [.. group.Select(skill => SkillRow(skill, twin.Name, seen, localization))]))],
            [.. twin.Boards.Select(static board => new DigitalTwinRowViewModel(board.Name, $"{board.OpenNodes}/{board.Nodes}", string.Join(" · ", board.StatEffects.Concat(board.SkillEffects))))],
            [.. twin.Titles.Select(static title => new DigitalTwinRowViewModel($"{title.Category}: {title.Name}", "", string.Join(" · ", title.Effects)))]);
    }

    // A skill's line (see From): at the level the fight sent, by the range it gives, or as the website gives it. caster:
    // whose skill it is, for the hover text.
    internal static DigitalTwinRowViewModel SkillRow(TwinSkill skill, string caster, SeenSkillLevels? seen, LocalizationService localization)
    {
        var culture = CultureInfo.CurrentCulture;
        var level = LevelText(skill, localization);
        // Under the name only that an active or a stigma isn't equipped: the group's title gives the kind, and the website
        // marks no passive equipped (they can't be).
        var detail = !skill.Equipped && skill.Category is "Active" or "Dp" ? localization["Twin_NotEquipped"] : null;
        var icon = SkillIconCatalog.ResolveAssetName(skill.Id);
        string Website(string text) => string.Format(culture, localization["Twin_WebsiteLevel"], text);
        if (SeenRange(seen, skill.Id) is { } range)
        {
            var inside = range.Contains(skill.Level);
            var rangeTip = SkillTip.ForRange(skill.Id, skill.Name, localization, range, inside ? skill.Level : null, caster, seen!.At, inside ? null : level);
            return inside
                ? new DigitalTwinRowViewModel(skill.Name, level, string.Format(culture, localization["Twin_SeenRange"], range) + (detail is null ? "" : " · " + Website(detail)), icon,
                    rangeTip)
                : new DigitalTwinRowViewModel(skill.Name, range.ToString(), Website(level) + (detail is null ? "" : " · " + detail), icon, rangeTip);
        }

        if (SeenLevel(seen, skill.Id) is not { } seenLevel)
            return new DigitalTwinRowViewModel(skill.Name, level, detail, icon, SkillTip.For(skill.Id, skill.Name, skill.Name, localization, skill.Level > 0 ? skill.Level : null, caster));

        var tip = SkillTip.ForSeen(skill.Id, skill.Name, localization, seenLevel, caster, seen!.At, level);
        return seenLevel == skill.Level
            ? new DigitalTwinRowViewModel(skill.Name, level, detail is null ? null : Website(detail), icon, tip)
            : new DigitalTwinRowViewModel(skill.Name, seenLevel.ToString(culture), Website(level) + (detail is null ? "" : " · " + detail), icon, tip);
    }

    // "2,152", "9.2%".
    internal static string Number(double value, bool percent = false) => value.ToString("#,0.##", CultureInfo.CurrentCulture) + (percent ? "%" : "");

    // A skill's level as the website gives it, with what gear and Daevanion add: "21 (7 from gear, 4 from Daevanion)".
    internal static string LevelText(TwinSkill skill, LocalizationService localization)
    {
        var culture = CultureInfo.CurrentCulture;
        var parts = new List<string>(2);
        if (skill.GearLevels > 0)
            parts.Add(string.Format(culture, localization["Twin_FromGear"], skill.GearLevels));
        if (skill.DaevanionLevels > 0)
            parts.Add(string.Format(culture, localization["Twin_FromDaevanion"], skill.DaevanionLevels));
        return skill.Level.ToString(culture) + (parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "");
    }

    // A skill's level the game sent in a fight (by its base id), when there's one: 0 and 1 never count (see SeenSkillLevels).
    internal static int? SeenLevel(SeenSkillLevels? seen, int skillId) =>
        seen is not null && seen.Levels.TryGetValue(skillId, out var level) && level >= 2 ? level : null;

    // A stigma's range from a fight (see SeenSkillLevels.Ranges), where it sent no level for it: a level wins.
    internal static SeenLevelRange? SeenRange(SeenSkillLevels? seen, int skillId) =>
        seen is not null && SeenLevel(seen, skillId) is null && seen.Ranges.TryGetValue(skillId, out var range) ? range : null;

    // A skill's group by the website's category, with the group's title: actives, passives, stigmas ("Dp"), then any other
    // kind, in this order.
    internal static (int Order, string Title) SkillGroup(string? category, LocalizationService localization) => category switch
    {
        "Active" => (0, localization["Twin_ActiveSkills"]),
        "Passive" => (1, localization["Twin_PassiveSkills"]),
        "Dp" => (2, localization["Twin_Stigmas"]),
        _ => (3, localization["Twin_OtherSkills"]),
    };
}

// Lines of one kind under their title: a twin's actives, passives or stigmas.
public sealed record DigitalTwinRowGroup(string Title, DigitalTwinRowViewModel[] Rows);
