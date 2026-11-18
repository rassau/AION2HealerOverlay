using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class FightTimelineTests
{
    private static readonly DateTimeOffset Started = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    private const int Me = 100;
    private const int Tank = 200;
    private const int Boss = 900;
    private const int Add = 901;

    // A 10 s fight. I hit the boss at 0.5 s and 1.2 s (that one wears down 5 stagger points) and an add at 0.6 s, and heal
    // the tank for 800 (600 of it real) at 2.5 s. The boss hits the tank for 400 at 2.1 s; the tank is down to 6000 of 10000
    // HP from 3 s, dies at 4.5 s and is back at full at 6 s. The boss frame names the tank from 1 s, me from 5 s. My ping
    // is 40 ms at 1.5 s and 60.5 ms at 3 s.
    [Fact]
    public void Each_Second_Holds_What_Happened_In_It()
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(30)));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendPing(Source(1.5, 0x0336), 40);
        sink.AppendPing(Source(3, 0x0336), 60.5);
        sink.AppendNpcHp(Source(0, 0x1B92), Tank, 10_000, 10_000);
        sink.AppendBossTarget(Source(1, 0x2092), Tank);
        sink.AppendNpcHp(Source(3, 0x1B92), Tank, 6_000, 10_000);
        sink.AppendNpcHp(Source(4.5, 0x1B92), Tank, 0, 10_000);
        sink.AppendBossTarget(Source(5, 0x2092), Me);
        sink.AppendNpcHp(Source(6, 0x1B92), Tank, 10_000, 10_000);
        var heal = Event(Me, Tank, CombatMetricKind.Healing, 800, 2.5, skill: 13, effective: 600);
        var events = new Dictionary<int, IReadOnlyList<CombatMetricDetailEvent>>
        {
            [Me] =
            [
                Event(Me, Boss, CombatMetricKind.Damage, 1_000, 0.5, skill: 11),
                Event(Me, Add, CombatMetricKind.Damage, 500, 0.6, skill: 11),
                Event(Me, Boss, CombatMetricKind.Damage, 300, 1.2, skill: 12) with { Stagger = 5 },
                heal,
            ],
            [Tank] = [Event(Boss, Tank, CombatMetricKind.Damage, 400, 2.1, skill: 99), heal],
        };

        var timeline = FightTimeline.Build(0, 10_000, Boss, [Me, Tank], id => events[id], scene.Owner.CreateLiveTimelineSegment(), sourcesOf: Me,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(10, timeline.Seconds);
        Assert.Equal([1_000d, 300, 0, 0, 0, 0, 0, 0, 0, 0], Series(Me, FightMetric.Damage));
        Assert.Equal([0d, 5, 0, 0, 0, 0, 0, 0, 0, 0], Series(Me, FightMetric.Stagger));
        Assert.Equal([0d, 0, 800, 0, 0, 0, 0, 0, 0, 0], Series(Me, FightMetric.Healing));
        Assert.All(Series(Tank, FightMetric.Healing), static value => Assert.Equal(0, value));
        Assert.Equal([0d, 0, 400, 0, 0, 0, 0, 0, 0, 0], Series(Tank, FightMetric.DamageTaken));
        Assert.Equal([0d, 0, 0, 4_000, 2_000, 0, 0, 0, 0, 0], Series(Tank, FightMetric.UnhealedHealth));
        // The UHS so far ÷ the 400 taken: none while no HP was missing yet; I missed none at all.
        Assert.Equal([double.NaN, double.NaN, double.NaN, 10, 15, 15, 15, 15, 15, 15], Series(Tank, FightMetric.RecoveryTime));
        Assert.All(Series(Me, FightMetric.RecoveryTime), static value => Assert.True(double.IsNaN(value)));
        Assert.Equal([0d, 1, 1, 1, 1, 0, 0, 0, 0, 0], Series(Tank, FightMetric.Aggro));
        Assert.Equal([0d, 0, 0, 0, 0, 1, 1, 1, 1, 1], Series(Me, FightMetric.Aggro));
        Assert.Equal([(Tank, 4.5)], timeline.Deaths);
        Assert.Equal([(Skill(11), 1_000d)], timeline.Sources[(int)FightMetric.Damage][0]);
        Assert.Equal([(Skill(13), 800d)], timeline.Sources[(int)FightMetric.Healing][2]);
        Assert.Empty(timeline.Sources[(int)FightMetric.Damage][2]);
        // None yet in the first second; the one at 3 s counts for the second that ends then.
        Assert.Equal([double.NaN, 40, 60.5, 60.5, 60.5, 60.5, 60.5, 60.5, 60.5, 60.5], timeline.Ping);

        // A ping over 30 s old says nothing any more.
        var longer = FightTimeline.Build(0, 40_000, Boss, [Me], id => events[id], scene.Owner.CreateLiveTimelineSegment(),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal((60.5, double.NaN), (longer.Ping[32], longer.Ping[33]));

        var effective = FightTimeline.Build(0, 10_000, Boss, [Me, Tank], id => events[id], default, includeOverhealing: false,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(600, effective.Values[Me][(int)FightMetric.Healing][2]);
        Assert.Empty(effective.Sources);

        double[] Series(int player, FightMetric metric) => timeline.Values[player][(int)metric];
    }

    // The recovery time up to each second: the tank misses 2000 of its HP from 1 s to 3 s (4000 HP·s) for the 2000 the boss
    // dealt it at 1 s. I take 1000 but my HP never comes (a stranger's): none for me, and the group's leaves me out.
    [Fact]
    public void Recovery_Time_Is_Unhealed_Health_So_Far_Over_Damage_Taken_So_Far()
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(30)));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNpcHp(Source(0, 0x1B92), Tank, 10_000, 10_000);
        sink.AppendNpcHp(Source(1, 0x1B92), Tank, 8_000, 10_000);
        sink.AppendNpcHp(Source(3, 0x1B92), Tank, 10_000, 10_000);
        var events = new Dictionary<int, IReadOnlyList<CombatMetricDetailEvent>>
        {
            [Me] = [Event(Boss, Me, CombatMetricKind.Damage, 1_000, 0.5, skill: 99)],
            [Tank] = [Event(Boss, Tank, CombatMetricKind.Damage, 2_000, 1, skill: 99)],
        };

        var timeline = FightTimeline.Build(0, 4_000, Boss, [Me, Tank], id => events[id], scene.Owner.CreateLiveTimelineSegment(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([double.NaN, 1, 2, 2], timeline.Values[Tank][(int)FightMetric.RecoveryTime]);
        Assert.All(timeline.Values[Me][(int)FightMetric.RecoveryTime], static value => Assert.True(double.IsNaN(value)));
        // With my damage taken, second 1 would be 2000 ÷ 3000.
        Assert.Equal([double.NaN, 1, 2, 2], FightTimeline.RecoveryTime(timeline, [Me, Tank]));
        Assert.Equal(2, FightTimeline.RecoveryTime(4_000, 2_000));
        // 20000 HP missing for 20 s before a first hit of 300: 1333 s, kept at the 999 s it's shown as.
        Assert.Equal(999, FightTimeline.RecoveryTime(400_000, 300));
        Assert.True(double.IsNaN(FightTimeline.RecoveryTime(0, 1_000)));
        Assert.True(double.IsNaN(FightTimeline.RecoveryTime(300, 0)));
    }

    // A pull still going is worked out anew each second. A build that takes up the last one (its journal checkpoint: where
    // the journal stood before the first entry after its end) gives what reading the scene from its start gives: HP, the
    // boss's aimed casts and the ping from before the pull carry over; what came after the last end (a ping, a death, the
    // boss frame's target), which the last build read only up to its end, is read in full; entries that come late with an
    // earlier time are read alike. Another start, an earlier end or other players read from the start again; a history
    // entry's journal gets no checkpoint.
    [Fact]
    public void A_Build_Taken_Up_From_The_Last_Ones_Checkpoint_Is_The_Same()
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(60)));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendPing(Source(1, 0x0336), 45);
        sink.AppendNpcHp(Source(2, 0x1B92), Tank, 8_000, 10_000);
        // Between a 9.5 s and a 10 s start, read before the 10 s checkpoint: a build from 9.5 s can't take that one up.
        sink.AppendNpcHp(Source(9.8, 0x1B92), Tank, 4_000, 10_000);
        sink.AppendNpcHp(Source(3, 0x1B92), Me, 5_000, 9_000);
        sink.RegisterCompactControl0238(Source(6, 0x0238), Boss, 0, 1_801_500, 2, 2, Me);
        sink.AppendNpcHp(Source(11, 0x1B92), Tank, 6_000, 10_000);
        sink.AppendPing(Source(12.5, 0x0336), 50);
        // After the first build's end, there already when it is made.
        sink.AppendPing(Source(14.5, 0x0336), 52);
        sink.AppendNpcHp(Source(15.2, 0x1B92), Me, 0, 9_000);
        sink.AppendBossTarget(Source(15.5, 0x2092), Me);
        sink.AppendNpcHp(Source(12, 0x1B92), Tank, 7_000, 10_000);
        var first = Build(10_000, 14_000, [Me, Tank], null);
        Assert.NotNull(first.Checkpoint);

        sink.AppendNpcHp(Source(9, 0x1B92), Me, 9_000, 9_000);
        sink.AppendPing(Source(15, 0x0336), 70);
        sink.AppendBossTarget(Source(16, 0x2092), Tank);
        sink.AppendNpcHp(Source(17, 0x1B92), Tank, 0, 10_000);
        sink.AppendNpcHp(Source(19, 0x1B92), Tank, 10_000, 10_000);
        AssertSame(Build(10_000, 20_000, [Me, Tank], null), Build(10_000, 20_000, [Me, Tank], first));
        AssertSame(Build(10_000, 14_000, [Me, Tank], null), Build(10_000, 14_000, [Me, Tank], first));
        AssertSame(Build(10_000, 13_000, [Me, Tank], null), Build(10_000, 13_000, [Me, Tank], first));
        AssertSame(Build(9_500, 20_000, [Me, Tank], null), Build(9_500, 20_000, [Me, Tank], first));
        AssertSame(Build(10_000, 20_000, [Me], null), Build(10_000, 20_000, [Me], first));
        // A chain of builds, each taking up the one before, as the fight graphs make them.
        var chained = first;
        foreach (var to in (long[])[15_000, 15_000, 16_700, 20_000])
        {
            chained = Build(10_000, to, [Me, Tank], chained);
            AssertSame(Build(10_000, to, [Me, Tank], null), chained);
        }

        // It is taken up: what it says shows.
        typeof(FightTimeline).GetProperty(nameof(FightTimeline.Checkpoint))!.SetValue(first, first.Checkpoint! with { Ping = 99, PingAt = 10_000, PingSecond = 0 });
        Assert.Equal(99, Build(10_000, 20_000, [Me, Tank], first).Ping[0]);
        Assert.Null(FightTimeline.Build(10_000, 20_000, Boss, [Me], _ => [], new SceneJournalSegment(scene.Journal, 0, scene.Journal.NextObservationOrdinal),
            cancellationToken: TestContext.Current.CancellationToken).Checkpoint);

        FightTimeline Build(long from, long to, int[] players, FightTimeline? previous) =>
            FightTimeline.Build(from, to, Boss, players, static _ => new FightEvents(static _ => []), scene.Owner.CreateLiveTimelineSegment().CreateBoundedSnapshot(),
                0, true, TestContext.Current.CancellationToken, previous);
    }

    private static void AssertSame(FightTimeline expected, FightTimeline actual)
    {
        Assert.Equal(expected.Values.Keys, actual.Values.Keys);
        foreach (var (player, values) in expected.Values)
        {
            for (var metric = 0; metric < values.Length; metric++)
                Assert.Equal(values[metric], actual.Values[player][metric]);
        }

        Assert.Equal(expected.Deaths, actual.Deaths);
        Assert.Equal(expected.Ping, actual.Ping);
    }

    private static CombatMetricDetailEvent Event(int source, int target, CombatMetricKind metric, long amount, double atSeconds, int skill, long effective = 0) =>
        new(new CombatDetailFact(new CombatWireObservation { SkillCode = skill }, source, target, (long)(atSeconds * 1000), 0, 0, Skill(skill), default),
            new CombatContribution(metric, default, amount, default))
        {
            EffectiveHealing = effective
        };

    private static CombatEventKey Skill(int code) => new(code, default, default);

    private static PacketObservationSource Source(double atSeconds, ushort opcode)
        => new(Started.AddSeconds(atSeconds).ToUnixTimeMilliseconds(), 0, opcode, 0, 0, default);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TestTimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
