using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Twins;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

// The skill levels the game sent, as aura opens (0x2A38: caster, then level) and spawn passives (StateCodes.SkillLevel)
// put them into the journal.
public sealed class SeenSkillLevelsTests
{
    private static readonly DateTimeOffset Started = new(2026, 11, 18, 20, 0, 0, TimeSpan.Zero);
    private const int Me = 100;
    private const int Other = 200;
    private const uint Passive = 177_300_001;
    private const uint Regeneration = 170_903_501;

    // A passive (a group-17 self-buff) at zone-in: 1, then the base level, then the effective one.
    [Fact]
    public void A_Passive_Counts_At_The_Last_Level_It_Sent()
    {
        var (scene, sink) = CreateScene();
        Open(sink, 0, target: Me, caster: Me, group: 17, Passive, level: 1);
        Open(sink, 1, target: Me, caster: Me, group: 17, Passive, level: 10);
        Open(sink, 2, target: Me, caster: Me, group: 17, Passive, level: 16);

        Assert.Equal(new Dictionary<int, int> { [17_730_000] = 16 }, Read(scene, long.MaxValue)[Me]);
    }

    // A buff on someone else is the caster's level; an aura of another group carries none.
    [Fact]
    public void A_Buff_Counts_For_Its_Caster_Not_Its_Target()
    {
        var (scene, sink) = CreateScene();
        Open(sink, 0, target: Other, caster: Me, group: 19, Regeneration, level: 13);
        Open(sink, 1, target: Other, caster: Me, group: 3, 177_100_001, level: 9);

        var levels = Read(scene, long.MaxValue);

        Assert.Equal([Me], levels.Keys);
        Assert.Equal(new Dictionary<int, int> { [17_090_000] = 13 }, levels[Me]);
    }

    [Theory]
    [InlineData(177_300_001, 17_730_000)]
    [InlineData(1_742_001_011, 17_420_000)]
    [InlineData(17_090_350, 17_090_000)]
    public void Effect_Ids_Count_Under_Their_Base_Skill(long code, int skill) => Assert.Equal(skill, SeenSkillLevels.SkillOf(code));

    // 0 and 1 are effects whose numbers don't go by level, or no level: never one, and they don't undo a real one.
    [Fact]
    public void Levels_0_And_1_Never_Count()
    {
        var (scene, sink) = CreateScene();
        Open(sink, 0, target: Me, caster: Me, group: 17, Passive, level: 16);
        Open(sink, 1, target: Me, caster: Me, group: 17, Passive, level: 1);
        Open(sink, 2, target: Me, caster: Me, group: 17, Passive, level: 0);
        Open(sink, 3, target: Other, caster: Me, group: 19, Regeneration, level: 1);

        Assert.Equal(new Dictionary<int, int> { [17_730_000] = 16 }, Read(scene, long.MaxValue)[Me]);
    }

    [Fact]
    public void Levels_Sent_After_The_Time_Are_Left_Out()
    {
        var (scene, sink) = CreateScene();
        Open(sink, 5, target: Me, caster: Me, group: 17, Passive, level: 10);
        Open(sink, 15, target: Me, caster: Me, group: 17, Passive, level: 12);
        sink.AppendSkillLevel(Source(16), Me, Passive, 14);

        Assert.Equal(10, Read(scene, 10_000)[Me][17_730_000]);
    }

    [Fact]
    public void An_Empty_Stretch_Has_No_Levels() =>
        Assert.Empty(SeenSkillLevels.Read(default, long.MaxValue, TestContext.Current.CancellationToken));

    // A spawn's passive counts like an open, and of the two the later one wins.
    [Fact]
    public void A_Spawn_Passive_Counts_Like_An_Open()
    {
        var (scene, sink) = CreateScene();
        sink.AppendSkillLevel(Source(1), Me, Passive, 16);
        Open(sink, 2, target: Me, caster: Me, group: 17, Passive, level: 13);
        Open(sink, 3, target: Other, caster: Other, group: 17, Passive, level: 13);
        sink.AppendSkillLevel(Source(4), Other, Passive, 16);
        sink.AppendSkillLevel(Source(5), Other, 177_100_001, 1);

        var levels = Read(scene, long.MaxValue);

        Assert.Equal(new Dictionary<int, int> { [17_730_000] = 13 }, levels[Me]);
        Assert.Equal(new Dictionary<int, int> { [17_730_000] = 16 }, levels[Other]);
    }

    // A battlefield: map 62, or a map not known yet where nine in ten of ten or more levels read 20 (here two skills a
    // caster, the 20s first, the others 16); never another map.
    [Theory]
    [InlineData(0u, 9, 0, false)]
    [InlineData(0u, 10, 0, true)]
    [InlineData(0u, 9, 1, true)]
    [InlineData(0u, 8, 2, false)]
    [InlineData(SeenSkillLevels.BattlefieldMapId, 0, 2, true)]
    [InlineData(1010u, 10, 0, false)]
    public void A_Battlefield_Is_Map_62_Or_A_Map_Not_Known_Yet_Where_The_Levels_Read_20(uint mapId, int twenties, int others, bool battlefield)
    {
        var levels = Enumerable.Range(0, twenties + others).GroupBy(static i => i / 2)
            .ToDictionary(static caster => caster.Key + 1, caster => caster.ToDictionary(static i => 17_000_000 + i % 2 * 10_000, i => i < twenties ? 20 : 16));

        Assert.Equal(battlefield, SeenSkillLevels.IsBattlefield(mapId, levels));
    }

    // Every player's picks in one read, for the twin library's stigma ranges: each source's own, within the stretch (a 3
    // before it and after it doesn't count), only of the skills the filter keeps (not Chain of Torment's, so a source
    // with nothing else isn't there).
    [Fact]
    public void Picks_Of_Every_Source_Come_In_One_Read()
    {
        var (scene, sink) = CreateScene();
        Hit(sink, 1, Me, 17_410_030);
        Hit(sink, 3, Me, 17_410_020);
        Hit(sink, 4, Me, 17_070_340);
        Hit(sink, 5, Other, 17_160_040);
        Hit(sink, 6, 300, 17_070_340);
        Hit(sink, 12, Other, 17_160_030);

        var picks = SkillSpecializationCodes.ReadBySource(scene.Owner.CreateLiveTimelineSegment(), 2_000, 10_000, static skill => skill != 17_070_000,
            TestContext.Current.CancellationToken);

        Assert.Equal([Me, Other], picks.Keys.Order());
        Assert.Equal([(17_410_000, "2", 0)], picks[Me].Select(static skill => (skill.SkillId, string.Join(",", skill.Specializations), skill.AlsoSeen.Length)));
        Assert.Equal([(17_160_000, "4", 0)], picks[Other].Select(static skill => (skill.SkillId, string.Join(",", skill.Specializations), skill.AlsoSeen.Length)));
    }

    private static void Hit(IRuntimeObservationSink sink, double atSeconds, int source, int skillCode)
    {
        var hit = new CombatWireObservation { SkillCode = skillCode, Damage = 100, HitCount = 1, AttemptCount = 1 };
        sink.AppendCombatWireObservation(Source(atSeconds), source, 900, in hit);
    }

    private static (SceneLiveReadModel Scene, IRuntimeObservationSink Sink) CreateScene()
    {
        var scene = new SceneLiveReadModel(Started);
        return (scene, SceneSinkFactory.CreateForLive(scene)());
    }

    private static Dictionary<int, Dictionary<int, int>> Read(SceneLiveReadModel scene, long toMilliseconds) =>
        SeenSkillLevels.Read(scene.Owner.CreateLiveTimelineSegment(), toMilliseconds, TestContext.Current.CancellationToken);

    private static void Open(IRuntimeObservationSink sink, double atSeconds, int target, int caster, int group, uint effect, int level) =>
        sink.RegisterObservation2A38(Source(atSeconds), target, 1, group, 1, effect, 0, 0, 0, 0, caster, level, default, 0, 0, 0);

    private static PacketObservationSource Source(double atSeconds) =>
        new(Started.AddSeconds(atSeconds).ToUnixTimeMilliseconds(), 0, 0x2A38, 0, 0, default);
}
