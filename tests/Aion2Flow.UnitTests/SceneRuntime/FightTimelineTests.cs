using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Combat;
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

    private static CombatMetricDetailEvent Event(int source, int target, CombatMetricKind metric, long amount, double atSeconds, int skill, long effective = 0) =>
        new(new CombatDetailFact(new CombatWireObservation { SkillCode = skill }, source, target, (long)(atSeconds * 1000), 0, 0, Skill(skill), default, default),
            new CombatContribution(metric, default, amount, default))
        {
            EffectiveHealing = effective
        };

    private static CombatEventKey Skill(int code) => new(code, default, default);

    private static PacketObservationSource Source(double atSeconds, ushort opcode)
        => new(Started.AddSeconds(atSeconds).ToUnixTimeMilliseconds(), 0, opcode, 0, 0, default);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
