using Cloris.Aion2Flow.SceneRuntime.Twins;
using Cloris.Aion2Flow.Services;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class SkillSpecializationCodesTests
{
    // Ids as they came in a recorded fight: casts (last digit 0) with up to three picks; parts with two or three picks;
    // a part with one digit before the last (…0038, …0031) is not read as one.
    [Theory]
    [InlineData(17070340, 17070000, new[] { 3, 4 })]
    [InlineData(12241450, 12240000, new[] { 1, 4, 5 })]
    [InlineData(17410030, 17410000, new[] { 3 })]
    [InlineData(17070000, 17070000, new int[0])]
    [InlineData(14012353, 14010000, new[] { 2, 3, 5 })]
    [InlineData(14010343, 14010000, new[] { 3, 4 })]
    public void Ids_Read_As_Skill_And_Picks(int code, int skillId, int[] picks)
    {
        Assert.True(SkillSpecializationCodes.TryDecode(code, out var decodedSkill, out var decodedPicks));
        Assert.Equal(skillId, decodedSkill);
        Assert.Equal(picks, decodedPicks);
    }

    [Theory]
    [InlineData(12240038)]
    [InlineData(17410031)]
    [InlineData(11340028)]
    [InlineData(17070300)]
    [InlineData(17072130)]
    [InlineData(17070670)]
    [InlineData(0)]
    public void Other_Ids_Are_No_Picks(int code) => Assert.False(SkillSpecializationCodes.TryDecode(code, out _, out _));

    // Per skill the picks on the most hits, the others after; skills without specializations (passives) are skipped.
    [Fact]
    public void Collect_Keeps_The_Picks_Seen_Most()
    {
        int[] codes = [12240350, 12240350, 12240038, 12241450, 17070340, 11800010, 14012353, 14012351, 14010343];

        var seen = SkillSpecializationCodes.Collect(codes, static skillId => skillId != 11800000);

        Assert.Equal(
            [(12240000, "3,5", 2, 1), (14010000, "2,3,5", 2, 1), (17070000, "3,4", 1, 0)],
            seen.Select(static s => (s.SkillId, string.Join(",", s.Specializations), s.Hits, s.AlsoSeen.Length)));
        Assert.Equal([1, 4, 5], seen[0].AlsoSeen[0]);
    }

    // The bundled table: every active skill and stigma of the eight classes, five specializations each.
    [Fact]
    public void Table_Has_Every_Skill_With_Specializations()
    {
        Assert.Equal(200, SkillSpecializations.Count);
        var chain = SkillSpecializations.Get(17070000)!;
        Assert.False(chain.IsStigma);
        Assert.Equal(new SkillSpecializations.Specialization(12, "+10% PvE Damage Tolerance reduction and +5% PvP Damage Tolerance reduction"), chain.Specializations[3]);
        var light = SkillSpecializations.Get(17410000)!;
        Assert.True(light.IsStigma);
        Assert.Equal([5, 10, 15, 20, null], light.Specializations.Select(static s => s.SkillLevel));
        Assert.False(SkillSpecializations.Has(11800000));
    }
}
