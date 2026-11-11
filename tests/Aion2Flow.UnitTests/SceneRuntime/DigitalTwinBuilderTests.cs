using Cloris.Aion2Flow.SceneRuntime.Twins;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

// The official character website's JSON as it comes (numbers as text, lists sometimes a single object), made up but laid
// out like the real answers.
public sealed class DigitalTwinBuilderTests
{
    internal const string Info = """
        {"profile":{"characterName":"Healy","className":"Cleric","characterLevel":45,"combatPower":123456,"serverName":"Siel"},
         "stat":{"statList":[
           {"type":"AGI","name":"Precision","value":56,"statSecondList":["Accuracy increase +5.6%","Critical Hit increase +5.6%"]},
           {"type":"DEX","name":"Dexterity","value":0,"statSecondList":null},
           {"type":"ItemLevel","name":"Item Level","value":2650}]},
         "title":{"titleList":[{"equipCategory":"Attack","name":"Golden Glow","equipStatList":[{"desc":"Critical Hit +20"}],"statList":[{"desc":"Accuracy +10"}]}]},
         "daevanion":{"boardList":[{"id":71,"name":"Nezekan","open":1,"openNodeCount":62,"totalNodeCount":88},{"id":72,"name":"Zikel","open":0,"openNodeCount":0,"totalNodeCount":88}]}}
        """;

    internal const string Equipment = """
        {"equipment":{"equipmentList":[
           {"id":110730051,"name":"Warhammer","grade":"Unique","enchantLevel":15,"exceedLevel":2,"slotPos":1,"slotPosName":"MainHand"},
           {"id":40000001,"name":"Chalice","grade":"Epic","enchantLevel":4,"exceedLevel":0,"slotPos":41,"slotPosName":"Arcana1"}]},
         "skill":{"skillList":[
           {"id":17010000,"name":"Earth's Retribution","skillLevel":12,"category":"Active","acquired":1,"equip":1},
           {"id":17120000,"name":"Radiant Recovery","skillLevel":10,"category":"Active","acquired":1,"equip":0},
           {"id":17990000,"name":"Not Learned","skillLevel":0,"category":"Active","acquired":0,"equip":0}]},
         "petwing":{"pet":{"name":"Swarm","level":3},"wing":{"name":"Dark Veil Wings","enchantLevel":0}}}
        """;

    internal const string Weapon = """
        {"name":"Warhammer",
         "mainStats":[{"id":"WeaponFixingDamage","name":"Attack","minValue":"309","value":"398","extra":"146"},{"id":"Critical","name":"Critical Hit","value":"50","extra":"0"}],
         "subStats":[{"id":"WeaponFixingDamage","name":"Attack","value":"48"},{"id":"AmplifyAllDamage","name":"Damage Boost","value":"6.1%"}],
         "subSkills":{"id":17240000,"level":1,"name":"Defiance"},
         "magicStoneStat":[{"id":"HPMax","name":"HP","value":"+50"}],
         "godStoneStat":{"name":"Theostone: Orissan's Blood","desc":"Has a 5% chance to deal Poison damage"}}
        """;

    internal const string Arcana = """
        {"name":"Chalice","mainStats":{"id":"Space","name":"Space [Israphel]","value":"20","extra":"4"},"subStats":null,
         "subSkills":[{"id":17120000,"level":2,"name":"Radiant Recovery"}],"magicStoneStat":null,"godStoneStat":null}
        """;

    internal const string Board = """
        {"nodeList":[],"openStatEffectList":[{"desc":"Critical Hit +30"},{"desc":"Cooldown Reduction +3%"}],"openSkillEffectList":[{"desc":"Earth's Retribution +1"}]}
        """;

    private static readonly DateTimeOffset Fetched = new(2026, 11, 11, 20, 0, 0, TimeSpan.Zero);

    internal static DigitalTwin Build() =>
        DigitalTwinBuilder.Build(1301, Fetched, Info, Equipment, new Dictionary<int, string> { [1] = Weapon, [41] = Arcana }, new Dictionary<int, string> { [71] = Board });

    [Fact]
    public void Profile_Gear_Skills_Boards_And_Titles()
    {
        var twin = Build();

        Assert.Equal(("Healy", 1301, "Siel", "Cleric", 45, 123456, 2650, Fetched), (twin.Name, twin.ServerId, twin.ServerName, twin.ClassName, twin.Level, twin.CombatPower, twin.ItemLevel, twin.FetchedAt));
        Assert.Equal(("Swarm (level 3)", "Dark Veil Wings +0"), (twin.Pet, twin.Wings));
        Assert.Equal([("Precision", 56d, 2), ("Dexterity", 0d, 0)], twin.PrimaryStats.Select(static s => (s.Name, s.Value, s.Effects.Length)));

        var weapon = twin.Gear[0];
        Assert.Equal((1, "MainHand", "Warhammer", 15, 2), (weapon.SlotPos, weapon.Slot, weapon.Name, weapon.Enchant, weapon.Exceed));
        Assert.Equal(new TwinStatValue("Attack", 398, false, 146, 309), weapon.MainStats[0]);
        Assert.Equal([("Attack", 48d, false), ("Damage Boost", 6.1, true)], weapon.SubStats.Select(static s => (s.Name, s.Value, s.Percent)));
        Assert.Equal(new TwinStatValue("HP", 50, false), Assert.Single(weapon.Stones));
        Assert.Equal(new TwinSkillBonus(17240000, "Defiance", 1), Assert.Single(weapon.SkillBonuses));
        Assert.Equal(["Theostone: Orissan's Blood"], weapon.Theostones);
        Assert.Equal(new TwinStatValue("Space [Israphel]", 20, false, 4), Assert.Single(twin.Gear[1].MainStats));

        // Not learned skills are left out; levels from gear by skill id, from Daevanion by name. A skill's level (gear and
        // Daevanion included) is found by any of its ids.
        Assert.Equal(
            [new TwinSkill(17010000, "Earth's Retribution", 12, "Active", true, 0, 1), new TwinSkill(17120000, "Radiant Recovery", 10, "Active", false, 2, 0)],
            twin.Skills);
        Assert.Equal((10, 12, null), (twin.SkillLevel(17120031), twin.SkillLevel(17010000), twin.SkillLevel(17990000)));
        Assert.Equal([("Nezekan", 62, 88, 2, 1), ("Zikel", 0, 88, 0, 0)], twin.Boards.Select(static b => (b.Name, b.OpenNodes, b.Nodes, b.StatEffects.Length, b.SkillEffects.Length)));
        Assert.Equal(("Attack", "Golden Glow"), (twin.Titles[0].Category, twin.Titles[0].Name));
        Assert.Equal(["Critical Hit +20", "Accuracy +10"], twin.Titles[0].Effects);
    }

    // Each stat summed from gear (with the enchantment), stones, Daevanion, titles and what base stats give, flat and %
    // apart, with the parts; the main ones first.
    [Fact]
    public void Totals_Add_Up_Every_Source()
    {
        var totals = Build().Totals.ToDictionary(static t => (t.Name, t.Percent));

        var attack = totals[("Attack", false)];
        Assert.Equal((592d, 503d), (attack.Value, attack.Min!.Value));
        Assert.Equal([new TwinStatSource(DigitalTwinBuilder.GearSource, 592)], attack.Sources);
        Assert.Equal(
            [new TwinStatSource(DigitalTwinBuilder.TitleSource, 20), new TwinStatSource(DigitalTwinBuilder.GearSource, 50), new TwinStatSource(DigitalTwinBuilder.DaevanionSource, 30)],
            totals[("Critical Hit", false)].Sources);
        Assert.Equal(100, totals[("Critical Hit", false)].Value);
        Assert.Equal([new TwinStatSource("Precision", 5.6)], totals[("Critical Hit", true)].Sources);
        Assert.Equal((10d, 5.6), (totals[("Accuracy", false)].Value, totals[("Accuracy", true)].Value));
        Assert.Equal(6.1, totals[("Damage Boost", true)].Value);
        Assert.Equal([new TwinStatSource(DigitalTwinBuilder.StoneSource, 50)], totals[("HP", false)].Sources);
        Assert.Equal(3, totals[("Cooldown Reduction", true)].Value);
        Assert.Equal(24, totals[("Space [Israphel]", false)].Value);
        Assert.Null(totals[("Accuracy", false)].Min);
        Assert.Equal(
            [("Attack", false), ("Accuracy", false), ("Accuracy", true), ("Critical Hit", false), ("Critical Hit", true)],
            Build().Totals.Take(5).Select(static t => (t.Name, t.Percent)));
    }

    [Theory]
    [InlineData("Attack Bonus +24", "Attack Bonus", 24, false)]
    [InlineData("Cooldown Reduction +3%", "Cooldown Reduction", 3, true)]
    [InlineData("Accuracy increase +5.6%", "Accuracy", 5.6, true)]
    [InlineData("HP +2,000", "HP", 2000, false)]
    [InlineData("Earth's Retribution +1", "Earth's Retribution", 1, false)]
    [InlineData("Move Speed -10%", "Move Speed", -10, true)]
    public void Effects_Read_As_Name_Value_And_Unit(string text, string name, double value, bool percent)
    {
        Assert.True(DigitalTwinBuilder.TryParseEffect(text, out var parsed));
        Assert.Equal(new TwinStatValue(name, value, percent), parsed);
    }

    [Fact]
    public void Text_Without_A_Value_Is_No_Effect()
    {
        Assert.False(DigitalTwinBuilder.TryParseEffect("Space [Israphel]", out _));
        Assert.False(DigitalTwinBuilder.TryParseEffect("", out _));
    }
}
