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

    // A skill's parts can carry its bare id (no picks) more often than its picks (a stigma at level 18 sent 8 bare hits
    // after 2 with its 3): the picks still come first, none only where nothing else was seen.
    [Fact]
    public void Picks_Come_Before_None()
    {
        int[] codes = [14380030, 14380030, .. Enumerable.Repeat(14380000, 8), 17010000, 17010000];

        var seen = SkillSpecializationCodes.Collect(codes, static _ => true);

        Assert.Equal([(14380000, "3", 2, 1), (17010000, "", 2, 0)], seen.Select(static s => (s.SkillId, string.Join(",", s.Specializations), s.Hits, s.AlsoSeen.Length)));
    }

    // A stigma's number is how far its specializations are unlocked, and they unlock at levels 5, 10, 15 and 20, the
    // fifth past the fourth: its level is a range, open at the top from 4 on.
    [Theory]
    [InlineData(1, 5, 9, "5–9")]
    [InlineData(2, 10, 14, "10–14")]
    [InlineData(3, 15, 19, "15–19")]
    [InlineData(4, 20, null, "20+")]
    [InlineData(5, 21, null, "21+")]
    public void A_Stigma_s_Number_Gives_Its_Level_Range(int unlocked, int min, int? max, string text)
    {
        var range = SkillSpecializations.StigmaLevelRange(17410000, [unlocked]);

        Assert.Equal(new SeenLevelRange(min, max), range);
        Assert.Equal(text, range!.Value.ToString());
        Assert.Equal((true, max is null), (range.Value.Contains(min), range.Value.Contains(100)));
        Assert.False(range.Value.Contains(min - 1));
        Assert.True(range.Value.Contains(max ?? min));
        if (max is { } top)
            Assert.False(range.Value.Contains(top + 1));
    }

    // None (the bare id high stigmas' parts send too), a number past the five, more numbers than one, an active skill's
    // picks, a skill without specializations: no range.
    [Theory]
    [InlineData(17410000, new int[0])]
    [InlineData(17410000, new[] { 6 })]
    [InlineData(17410000, new[] { 1, 3 })]
    [InlineData(17070000, new[] { 3 })]
    [InlineData(11800000, new[] { 3 })]
    public void Other_Picks_Give_No_Range(int skillId, int[] picks) => Assert.Null(SkillSpecializations.StigmaLevelRange(skillId, picks));

    // A player's stigmas' ranges by skill, from the picks on the most hits; active skills and no picks have none.
    [Fact]
    public void A_Player_s_Stigma_Ranges_Come_From_Their_Picks() =>
        Assert.Equal(new Dictionary<int, SeenLevelRange> { [17410000] = new(10, 14) },
            SkillSpecializations.StigmaLevelRanges([new(17410000, [2], 5, [[3]]), new(17070000, [3, 4], 9, []), new(17160000, [], 3, [])]));

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
