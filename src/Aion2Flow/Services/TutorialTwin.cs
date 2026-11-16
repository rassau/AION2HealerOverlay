using Cloris.Aion2Flow.SceneRuntime.Twins;

namespace Cloris.Aion2Flow.Services;

// The tutorial's made-up cleric "HappyCat": built like a website lookup (DigitalTwinBuilder adds up the Totals), never looked
// up. Server 1001 is the Korean Siel: outside DigitalTwinService.RegionOf, so a "Look up" of her can't reach the website.
internal static class TutorialTwin
{
    public const string Name = "HappyCat";
    public const int ServerId = 1001;

    public static DigitalTwin Create(DateTimeOffset at) =>
        DigitalTwinBuilder.Build(ServerId, at, Info, Equipment, Items, Boards);

    private const string Info = """
        {"profile":{"characterName":"HappyCat","className":"Cleric","characterLevel":45,"combatPower":158400,"serverName":"Siel"},
         "stat":{"statList":[
           {"name":"Might","value":28,"statSecondList":["Attack increase +2.8%"]},
           {"name":"Dexterity","value":12,"statSecondList":["Evasion increase +1.2%","Block increase +1.2%","Critical Hit Resist increase +1.2%"]},
           {"name":"Intelligence","value":0},
           {"name":"Constitution","value":30,"statSecondList":["HP increase +3%"]},
           {"name":"Precision","value":10,"statSecondList":["Accuracy increase +1%","Critical Hit increase +1%"]},
           {"name":"Willpower","value":16,"statSecondList":["Status Effect Resist +1.6%"]},
           {"name":"Justice [Nezekan]","value":50,"statSecondList":["Defense increase +5%","Perfect Chance +5%"]},
           {"name":"Freedom [Vaizel]","value":30,"statSecondList":["Accuracy increase +3%","Evasion increase +3%"]},
           {"name":"Illusion [Kaisinel]","value":35,"statSecondList":["Cooldown -3.5%","Endurance Penetration +3.5%"]},
           {"name":"Life [Yustiel]","value":40,"statSecondList":["HP increase +4%","Regeneration +4%"]},
           {"name":"Time [Siel]","value":60,"statSecondList":["Combat Speed +6%","Double Resist +6%"]},
           {"name":"Destruction [Zikel]","value":45,"statSecondList":["Attack increase +4.5%","Perfect Resist +4.5%"]},
           {"name":"Death [Triniel]","value":40,"statSecondList":["Critical Hit increase +4%","Regeneration Penetration +4%"]},
           {"name":"Wisdom [Lumiel]","value":45,"statSecondList":["MP Cost -4.5%","Double Chance +4.5%"]},
           {"name":"Destiny [Marchutan]","value":40,"statSecondList":["MP increase +4%","Endurance +4%"]},
           {"name":"Space [Israphel]","value":30,"statSecondList":["Move Speed +3%","Block increase +3%"]},
           {"type":"ItemLevel","value":2860}]},
         "title":{"titleList":[
           {"equipCategory":"Attack","name":"Purring Healer","equipStatList":[{"desc":"Critical Damage Boost +3%"},{"desc":"Accuracy Bonus +60"}]},
           {"equipCategory":"Defense","name":"Nine Lives","equipStatList":[{"desc":"Defense Bonus +300"},{"desc":"Damage Tolerance +3%"}]},
           {"equipCategory":"Etc","name":"Tutorial Graduate","equipStatList":[{"desc":"Cooldown Reduction +3%"},{"desc":"HP +900"}]}]},
         "daevanion":{"boardList":[
           {"id":71,"name":"Nezekan","open":1,"openNodeCount":56,"totalNodeCount":88},
           {"id":72,"name":"Zikel","open":1,"openNodeCount":52,"totalNodeCount":88},
           {"id":73,"name":"Vaizel","open":1,"openNodeCount":48,"totalNodeCount":88},
           {"id":74,"name":"Triniel","open":1,"openNodeCount":40,"totalNodeCount":116},
           {"id":75,"name":"Ariel","open":0,"openNodeCount":0,"totalNodeCount":152},
           {"id":76,"name":"Azphel","open":0,"openNodeCount":0,"totalNodeCount":152}]}}
        """;

    private const string Equipment = """
        {"equipment":{"equipmentList":[
           {"name":"Enraged Kromede Warhammer","grade":"Unique","enchantLevel":12,"slotPos":1,"slotPosName":"MainHand"},
           {"name":"Nuakum Guard","grade":"Unique","enchantLevel":12,"slotPos":2,"slotPosName":"SubHand"},
           {"name":"Nuakum Breastplate","grade":"Unique","enchantLevel":10,"slotPos":5,"slotPosName":"Torso"},
           {"name":"Nuakum Helm","grade":"Unique","enchantLevel":10,"slotPos":3,"slotPosName":"Helmet"},
           {"name":"Nuakum Gauntlets","grade":"Unique","enchantLevel":10,"slotPos":7,"slotPosName":"Gloves"},
           {"name":"Nuakum Greaves","grade":"Unique","enchantLevel":10,"slotPos":8,"slotPosName":"Boots"},
           {"name":"Enraged Kromede Necklace","grade":"Unique","enchantLevel":10,"slotPos":10,"slotPosName":"Necklace"},
           {"name":"Enraged Kromede Ring","grade":"Unique","enchantLevel":10,"slotPos":13,"slotPosName":"Ring1"},
           {"name":"Enraged Kromede Ring","grade":"Unique","enchantLevel":10,"slotPos":14,"slotPosName":"Ring2"},
           {"name":"Chalice of Vigor","grade":"Legend","enchantLevel":4,"slotPos":41,"slotPosName":"Arcana1"}]},
         "skill":{"skillList":[
           {"id":17010000,"name":"Earth's Retribution","skillLevel":14,"category":"Active","acquired":1,"equip":1},
           {"id":17040000,"name":"Judgment Thunder","skillLevel":13,"category":"Active","acquired":1,"equip":1},
           {"id":17080000,"name":"Debilitating Mark","skillLevel":12,"category":"Active","acquired":1,"equip":1},
           {"id":17150000,"name":"Divine Aura","skillLevel":15,"category":"Active","acquired":1,"equip":1},
           {"id":17070000,"name":"Chain of Torment","skillLevel":14,"category":"Active","acquired":1,"equip":1},
           {"id":17090000,"name":"Light of Regeneration","skillLevel":17,"category":"Active","acquired":1,"equip":1},
           {"id":17100000,"name":"Healing Light","skillLevel":20,"category":"Active","acquired":1,"equip":1},
           {"id":17120000,"name":"Radiant Recovery","skillLevel":20,"category":"Active","acquired":1,"equip":1},
           {"id":17240000,"name":"Defiance","skillLevel":12,"category":"Active","acquired":1,"equip":0},
           {"id":17710000,"name":"Warm Benediction","skillLevel":15,"category":"Passive","acquired":1},
           {"id":17740000,"name":"Healing Enhancement","skillLevel":18,"category":"Passive","acquired":1},
           {"id":17750000,"name":"Immortal Veil","skillLevel":16,"category":"Passive","acquired":1},
           {"id":17800000,"name":"Radiant Benediction","skillLevel":17,"category":"Passive","acquired":1},
           {"id":17160000,"name":"Benevolence","skillLevel":15,"category":"Dp","acquired":1,"equip":1},
           {"id":17290000,"name":"Absolution","skillLevel":18,"category":"Dp","acquired":1,"equip":1},
           {"id":17410000,"name":"Light of Protection","skillLevel":15,"category":"Dp","acquired":1,"equip":1},
           {"id":17420000,"name":"Yustiel's Power","skillLevel":12,"category":"Dp","acquired":1,"equip":1},
           {"id":17390000,"name":"Summon Resurrection","skillLevel":12,"category":"Dp","acquired":1,"equip":1}]},
         "petwing":{"pet":{"name":"Duduka Worker","level":3},"wing":{"name":"Kromede Wings","enchantLevel":0}}}
        """;

    // Each slot's stats (equipment/item); "extra" is the enchantment's part, "minValue" the weapon's low end.
    private static readonly Dictionary<int, string> Items = new()
    {
        [1] = """{"mainStats":[{"name":"Attack","minValue":"309","value":"398","extra":"110"},{"name":"Accuracy","value":"100"},{"name":"Critical Hit","value":"50"},{"name":"Block","value":"50"}],"subStats":[{"name":"Combat Speed","value":"11.5%"},{"name":"Attack","value":"45"},{"name":"Weapon Damage Boost","value":"8.5%"}],"magicStoneStat":[{"name":"HP","value":"+120"},{"name":"Attack","value":"+5"}]}""",
        [2] = """{"mainStats":[{"name":"Attack","value":"177","extra":"110"},{"name":"Accuracy","value":"50"},{"name":"Critical Hit","value":"50"}],"subStats":[{"name":"Combat Speed","value":"9%"},{"name":"Attack","value":"35"}],"subSkills":[{"id":17150000,"name":"Divine Aura","level":1}]}""",
        [5] = """{"mainStats":[{"name":"Defense","value":"637","extra":"250"},{"name":"HP","value":"344","extra":"200"}],"subStats":[{"name":"Defense","value":"240"},{"name":"HP","value":"200"}]}""",
        [3] = """{"mainStats":[{"name":"Defense","value":"477","extra":"250"},{"name":"HP","value":"215","extra":"200"}],"subStats":[{"name":"Attack","value":"18"}],"subSkills":[{"id":17710000,"name":"Warm Benediction","level":1}]}""",
        [7] = """{"mainStats":[{"name":"Defense","value":"424","extra":"250"},{"name":"HP","value":"172","extra":"200"}],"subStats":[{"name":"Attack","value":"20"}],"subSkills":[{"id":17740000,"name":"Healing Enhancement","level":1}]}""",
        [8] = """{"mainStats":[{"name":"Defense","value":"424","extra":"250"},{"name":"HP","value":"172","extra":"200"}],"subStats":[{"name":"Natural HP Regen","value":"35"}]}""",
        [10] = """{"mainStats":[{"name":"Attack","value":"133","extra":"50"},{"name":"Defense","value":"318","extra":"130"}],"subStats":[{"name":"Accuracy","value":"50"},{"name":"MP","value":"100"}],"subSkills":[{"id":17740000,"name":"Healing Enhancement","level":1}]}""",
        [13] = """{"mainStats":[{"name":"Attack","value":"88","extra":"50"},{"name":"Defense","value":"212","extra":"130"}],"subSkills":[{"id":17090000,"name":"Light of Regeneration","level":1},{"id":17100000,"name":"Healing Light","level":1}]}""",
        [14] = """{"mainStats":[{"name":"Attack","value":"88","extra":"50"},{"name":"Defense","value":"212","extra":"130"}],"subStats":[{"name":"Critical Hit","value":"20"}]}""",
        [41] = """{"mainStats":[{"name":"Time [Siel]","value":"16","extra":"4"}],"subSkills":[{"id":17100000,"name":"Healing Light","level":2},{"id":17750000,"name":"Immortal Veil","level":1}]}""",
    };

    // The open boards' nodes (daevanion/detail): stats, and skill levels by the skill's name.
    private static readonly Dictionary<int, string> Boards = new()
    {
        [71] = """{"openStatEffectList":[{"desc":"HP +700"},{"desc":"MP +150"},{"desc":"Critical Hit Resist +25"}],"openSkillEffectList":[{"desc":"Radiant Recovery +1"},{"desc":"Divine Aura +1"}]}""",
        [72] = """{"openStatEffectList":[{"desc":"HP +500"},{"desc":"Damage Tolerance +3%"},{"desc":"MP +300"}],"openSkillEffectList":[{"desc":"Healing Light +1"},{"desc":"Radiant Benediction +1"}]}""",
        [73] = """{"openStatEffectList":[{"desc":"HP +600"},{"desc":"Critical Hit Resist +30"}],"openSkillEffectList":[{"desc":"Healing Enhancement +1"},{"desc":"Radiant Recovery +1"}]}""",
        [74] = """{"openStatEffectList":[{"desc":"HP +800"},{"desc":"Multi-hit Resist +3%"}],"openSkillEffectList":[{"desc":"Healing Light +1"},{"desc":"Light of Regeneration +1"}]}""",
    };
}
