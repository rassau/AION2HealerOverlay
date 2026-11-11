using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.Services;

public sealed class SkillEffectsTests
{
    // Every class skill of the eight classes, found by any of its ids; numbers that change with the level read
    // "level 1–max".
    [Fact]
    public void Every_Skill_Has_Its_Text_With_Level_Ranges()
    {
        Assert.Equal(280, SkillEffects.Count);
        var light = SkillEffects.Get(17410031)!;
        Assert.Equal(25, light.MaxLevel);
        Assert.StartsWith("Increases PvE Damage Boost and PvE Damage Tolerance by 10.5–22.5% and PvP Damage Boost and PvP Damage Tolerance by 5.25–11.25%", light.Text);
        Assert.Contains("deals 325–4,015 (+192.1% Attack) Earth damage", SkillEffects.Get(17350000)!.Text);
        Assert.Contains("by 5.4–21%", SkillEffects.Get(18780001)!.Text);
        Assert.Null(SkillEffects.Get(2212001));
        Assert.Null(SkillEffects.Get(0));
    }

    // At one skill level (the caster's, from their digital twin) the numbers of that level; none past the known levels or
    // for a skill whose numbers don't change.
    [Fact]
    public void Numbers_At_One_Skill_Level()
    {
        var light = SkillEffects.Get(17410031)!;
        Assert.StartsWith("Increases PvE Damage Boost and PvE Damage Tolerance by 20.5% and PvP Damage Boost and PvP Damage Tolerance by 10.25%", light.At(21));
        Assert.StartsWith("Increases PvE Damage Boost and PvE Damage Tolerance by 10.5% and", light.At(1));
        Assert.Null(light.At(0));
        Assert.Null(light.At(26));
        Assert.Contains("Restores 1,051 (+100% Attack)-1,156 HP", SkillEffects.Get(17100000)!.At(21));
        Assert.Null(SkillEffects.Get(17390000)!.At(5));
    }

    // A stigma's number covers the ones below it; an active skill's are the picked ones.
    [Fact]
    public void Specialization_Effects_Follow_The_Picks()
    {
        Assert.Equal([1, 2, 3], SkillSpecializations.EffectsOf(17410000, [3]).Select(static e => e.Number));
        Assert.Equal([3, 4], SkillSpecializations.EffectsOf(17070000, [3, 4]).Select(static e => e.Number));
        Assert.Equal("+10% Max HP", SkillSpecializations.EffectsOf(17410000, [3]).Last().Effect);
        Assert.Empty(SkillSpecializations.EffectsOf(11780000, [1]));
    }

    // Hovering a buff in the group frame: the text, which levels its numbers span, and the caster's specializations
    // where the id names them; a boss's debuff only has its name.
    [Fact]
    public void Group_Frame_Tooltip_Says_What_The_Aura_Does()
    {
        using var localization = new LocalizationService(new LanguageService());

        var (effect, note, specializations) = GroupFrameViewModel.DescribeAura(17410030, localization);
        Assert.StartsWith("Increases PvE Damage Boost", effect);
        Assert.Contains("25", note);
        Assert.Equal(3, specializations!.Split(Environment.NewLine).Length);
        Assert.Contains("+10% Max HP", specializations);

        Assert.Equal(2, GroupFrameViewModel.DescribeAura(17070340, localization).Specializations!.Split(Environment.NewLine).Length);
        // A part of the buff with a single digit before the last doesn't tell the picks for sure.
        Assert.Null(GroupFrameViewModel.DescribeAura(17410031, localization).Specializations);
        Assert.NotNull(GroupFrameViewModel.DescribeAura(17410031, localization).Effect);
        Assert.Equal((null, null, null), GroupFrameViewModel.DescribeAura(2212001, localization));

        // With the caster's level from their digital twin: the numbers at that level, and whose level it is; a level
        // past the known ones keeps the ranges.
        var (atLevel, levelNote, _) = GroupFrameViewModel.DescribeAura(17410030, localization, 21, "Healy");
        Assert.Contains("by 20.5% and", atLevel);
        Assert.Contains("21", levelNote);
        Assert.Contains("Healy", levelNote);
        Assert.Contains("by 10.5–22.5%", GroupFrameViewModel.DescribeAura(17410030, localization, 30, "Healy").Effect);

        // An effect named apart from its skill (Earth's Blessing, from the stigma Earth Punishment): whose effect it is and
        // the caster's specializations naming it (the stigma's 3: specializations 1 to 3 unlocked, as the game shows),
        // not the skill's damage.
        var (blessing, blessingNote, blessingSpecializations) = GroupFrameViewModel.DescribeAura(17400037, localization, auraName: "Earth's Blessing", skillName: "Earth Punishment");
        Assert.Contains("Earth Punishment", blessing);
        Assert.DoesNotContain("damage", blessing);
        Assert.Null(blessingNote);
        Assert.Equal(2, blessingSpecializations!.Split(Environment.NewLine).Length);
        Assert.Contains("+10% Double Chance to caster", blessingSpecializations);
        Assert.Contains("+10% Attack and Defense to caster", blessingSpecializations);
    }
}
