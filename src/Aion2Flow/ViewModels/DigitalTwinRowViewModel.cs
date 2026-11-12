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
}

// What hovering a skill shows (see SkillTipTemplate): its name, what it does with the numbers at the caster's level when
// that's known (see GroupFrameViewModel.DescribeAura), the caster's specializations where the skill's code tells them, and
// which levels the numbers are for.
public sealed record SkillTip(string Name, string? Effect, string? EffectNote, string? Specializations)
{
    // skillName: the name of the skill the code belongs to, when the code is one of its effects (Earth's Blessing).
    internal static SkillTip For(int skillCode, string name, string? skillName, LocalizationService localization, int? level = null, string? caster = null)
    {
        var (effect, note, specializations) = GroupFrameViewModel.DescribeAura(skillCode, localization, level, caster, name, skillName);
        return new SkillTip(name, effect, note, specializations);
    }
}

// A digital twin as its cards show it (the details' Digital twin tab, the library): who, combat power, item level and when,
// then the sums of its stats, base stats, gear, skills (at the level the website gives: "21 (7 from gear, 4 from
// Daevanion)"), Daevanion boards and titles.
public sealed record DigitalTwinSheet(string Header, string Summary, DigitalTwinRowViewModel[] Stats, DigitalTwinRowViewModel[] PrimaryStats,
    DigitalTwinRowViewModel[] Gear, DigitalTwinRowViewModel[] Skills, DigitalTwinRowViewModel[] Boards, DigitalTwinRowViewModel[] Titles)
{
    public static DigitalTwinSheet From(DigitalTwin twin, LocalizationService localization)
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

        DigitalTwinRowViewModel Skill(TwinSkill skill)
        {
            var parts = new List<string>(2);
            if (skill.GearLevels > 0)
                parts.Add(string.Format(culture, localization["Twin_FromGear"], skill.GearLevels));
            if (skill.DaevanionLevels > 0)
                parts.Add(string.Format(culture, localization["Twin_FromDaevanion"], skill.DaevanionLevels));
            var level = skill.Level.ToString(culture) + (parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "");
            return new DigitalTwinRowViewModel(skill.Name, level, skill.Equipped ? skill.Category : $"{skill.Category} · {localization["Twin_NotEquipped"]}",
                SkillIconCatalog.ResolveAssetName(skill.Id), SkillTip.For(skill.Id, skill.Name, skill.Name, localization, skill.Level > 0 ? skill.Level : null, twin.Name));
        }

        return new DigitalTwinSheet(
            string.Format(culture, localization["Twin_Header"], twin.Name, twin.ClassName, twin.Level, twin.ServerName),
            string.Format(culture, localization["Twin_Summary"], twin.CombatPower, twin.ItemLevel, twin.FetchedAt.ToLocalTime()) +
                string.Concat(new[] { twin.Pet, twin.Wings }.OfType<string>().Select(static part => " · " + part)),
            [.. twin.Totals.Select(static total => new DigitalTwinRowViewModel(total.Name,
                Number(total.Value, total.Percent) + (total.Min is { } min && min != total.Value ? $" (min {Number(min)})" : ""),
                string.Join(" · ", total.Sources.Select(source => $"{source.Source} {Number(source.Value, total.Percent)}"))))],
            [.. twin.PrimaryStats.Select(static stat => new DigitalTwinRowViewModel(stat.Name, Number(stat.Value), string.Join(" · ", stat.Effects)))],
            [.. twin.Gear.Select(Item)],
            [.. twin.Skills.Select(Skill)],
            [.. twin.Boards.Select(static board => new DigitalTwinRowViewModel(board.Name, $"{board.OpenNodes}/{board.Nodes}", string.Join(" · ", board.StatEffects.Concat(board.SkillEffects))))],
            [.. twin.Titles.Select(static title => new DigitalTwinRowViewModel($"{title.Category}: {title.Name}", "", string.Join(" · ", title.Effects)))]);
    }

    // "2,152", "9.2%".
    internal static string Number(double value, bool percent = false) => value.ToString("#,0.##", CultureInfo.CurrentCulture) + (percent ? "%" : "");
}
