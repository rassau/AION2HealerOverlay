using System.Numerics;
using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class BuffUptimeTests
{
    private static readonly DateTimeOffset Started = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
    private const int Me = 100;
    private const int Tank = 200;
    private const int Dps = 300;
    private const uint GroupBuff = 99_900_010;
    private const uint Hot = 99_900_011;

    // 20 s, 40 m range. My group buff G is on me and the tank throughout, on the DPS only the first 5 s, and the DPS walks
    // off to 100 m at 10 s. My HoT H is on the DPS from 8 s to 18 s, still running after they left. The tank dies at 15 s.
    // A member's moment counts for a buff's group uptime when they're in the caster's range or have it: G 25 of 30 s (the
    // DPS in range without it 5 s), H 10 of 33 s (the tank in range without it until they died, the DPS 8 s before it).
    [Fact]
    public void Group_Uptime_Counts_Members_In_Range_Or_With_The_Buff()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(30)));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(0, 0x048D), Me, "Healer", isLocalPlayer: true);
        sink.AppendNickname(Source(0, 0x048D), Tank, "Tank");
        sink.AppendNickname(Source(0, 0x048D), Dps, "Dps");
        for (var second = 0; second < 20; second += 5)
        {
            sink.AppendPosition(Source(second, 0x1C92), Me, Vector3.Zero);
            sink.AppendPosition(Source(second, 0x1C92), Tank, new Vector3(1_000, 0, 0));
            sink.AppendPosition(Source(second, 0x1C92), Dps, new Vector3(second < 10 ? 1_000 : 10_000, 0, 0));
        }

        OpenAura(sink, 0, target: Me, origin: Me, sequence: 1, GroupBuff, 20_000);
        OpenAura(sink, 0, target: Tank, origin: Me, sequence: 2, GroupBuff, 20_000);
        OpenAura(sink, 0, target: Dps, origin: Me, sequence: 3, GroupBuff, 5_000);
        OpenAura(sink, 8, target: Dps, origin: Me, sequence: 4, Hot, 10_000);
        sink.AppendNpcHp(Source(15, 0x1B92), Tank, 0, 10_000);

        var report = BuffUptime.Compute(scene.Owner.CreateLiveTimelineSegment(), 0, 20_000, [Me, Tank, Dps], rangeMeters: 40, TestContext.Current.CancellationToken);

        var group = report.OfCasters.Single(static buff => buff.Buff.SkillId == GroupBuff);
        Assert.Equal((Me, 1d), (group.Buff.CasterId, group.SelfUptime!.Value));
        Assert.Equal(25 / 30d, group.GroupUptime!.Value, 6);
        var hot = report.OfCasters.Single(static buff => buff.Buff.SkillId == Hot);
        Assert.Null(hot.SelfUptime);
        Assert.Equal(10 / 33d, hot.GroupUptime!.Value, 6);
        Assert.Equal(
            [(Me, GroupBuff, 1d), (Tank, GroupBuff, 1d), (Dps, GroupBuff, 0.25), (Dps, Hot, 0.5)],
            report.OnMembers.Select(static buff => (buff.MemberId, RawId: (uint)buff.Buff.SkillId, Math.Round(buff.Uptime, 6))).OrderBy(static buff => buff.RawId).ThenBy(static buff => buff.MemberId));
    }

    // A history from before positions were kept: how long a buff was on each member still counts, but not who could have
    // had it, so there is no group uptime (rather than 100 % from the buffed ones alone).
    [Fact]
    public void Without_Positions_There_Is_No_Group_Uptime()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(30)));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(0, 0x048D), Me, "Healer", isLocalPlayer: true);
        sink.AppendNickname(Source(0, 0x048D), Tank, "Tank");
        OpenAura(sink, 0, target: Me, origin: Me, sequence: 1, GroupBuff, 20_000);
        OpenAura(sink, 0, target: Tank, origin: Me, sequence: 2, GroupBuff, 10_000);

        var report = BuffUptime.Compute(scene.Owner.CreateLiveTimelineSegment(), 0, 20_000, [Me, Tank], rangeMeters: 40, TestContext.Current.CancellationToken);

        var buff = Assert.Single(report.OfCasters);
        Assert.Equal(1d, buff.SelfUptime);
        Assert.Null(buff.GroupUptime);
        Assert.Equal(0.5, report.OnMembers.Single(static on => on.MemberId == Tank).Uptime, 6);
    }

    // A HoT kept up by recasting it before it runs out: the game renews the same aura (0x2B38), naming an origin that is
    // no entity (161) and the effect by its own code (skill code × 10 + 1). It stays the caster's, one buff, on past the
    // renewal (11 Nov 2026: a Light of Regeneration kept up all fight showed 38 %).
    [Fact]
    public void A_Renewed_Buff_Stays_The_Casters_And_Goes_On()
    {
        CombatResourceRegistry.EnsureCombatResources();
        const uint regeneration = 17_090_350;
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(30)));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(0, 0x048D), Me, "Healer", isLocalPlayer: true);
        OpenAura(sink, 0, target: Me, origin: Me, sequence: 1, regeneration, 10_000);
        sink.RegisterObservation2B38(Source(8, 0x2B38), Me, 161, 19, 1, ResourceEffectRef.FromRaw(regeneration * 10 + 1), 0, 0, 0, 0);

        var report = BuffUptime.Compute(scene.Owner.CreateLiveTimelineSegment(), 0, 20_000, [Me], rangeMeters: 40, TestContext.Current.CancellationToken);

        var buff = Assert.Single(report.OfCasters);
        Assert.Equal((Me, (int)regeneration), (buff.Buff.CasterId, buff.Buff.SkillId));
        Assert.True(buff.SelfUptime > 0.5, $"uptime {buff.SelfUptime}");
    }

    private static void OpenAura(IRuntimeObservationSink sink, double atSeconds, int target, int origin, int sequence, uint effect, ushort durationMilliseconds)
        => sink.RegisterObservation2A38(Source(atSeconds, 0x2A38), target, 1, 19, sequence, 0, durationMilliseconds, 0, 0, 0, origin, 1, ResourceEffectRef.FromRaw(effect), 0, 0, 0);

    private static PacketObservationSource Source(double atSeconds, ushort opcode)
        => new(Started.AddSeconds(atSeconds).ToUnixTimeMilliseconds(), 0, opcode, 0, 0, default);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
