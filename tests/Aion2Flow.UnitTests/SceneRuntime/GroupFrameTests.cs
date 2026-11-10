using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class GroupFrameTests
{
    private static readonly DateTimeOffset Started = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
    private const int Me = 100;
    private const int Tank = 200;
    private const int Dps = 300;
    private const int Unlisted = 400;
    private const int Boss = 500;
    private const int MySummon = 600;

    [Fact]
    public void Group_Frame_Follows_The_Party_List_Prefers_Direct_HP_And_Sorts_Auras_By_Who_Opened_Them()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var clock = new SettableTimeProvider { Now = Started.AddSeconds(10) };
        var scene = new SceneLiveReadModel(Started, clock);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var members = new List<GroupFrameMember>();
        var auras = new List<GroupFrameAura>();

        sink.AppendNickname(Source(0, 0x048D), Me, "Healer", isLocalPlayer: true);
        sink.AppendNickname(Source(0, 0x048D), Tank, "Tank");
        sink.AppendNickname(Source(0, 0x048D), Dps, "Dps");
        sink.AppendSummon(Source(0, 0x2836), Me, MySummon);
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Me, PlayerGroupMembership.Party(0));
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Tank, PlayerGroupMembership.Party(2));
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Dps, PlayerGroupMembership.Party(1));
        // Party status for someone the list missed.
        sink.AppendNpcHp(Source(2, 0x1B92), Unlisted, 7_000, 8_000);

        // Party status that still shows the HP from before the heal doesn't undo it; a later one counts again.
        sink.AppendNpcHp(Source(3, 0x008D), Tank, 5_000, 10_000);
        sink.AppendNpcHp(Source(3.5, 0x1B92), Tank, 4_000, 10_000);
        sink.AppendNpcHp(Source(4, 0x008D), Me, 9_000, 9_500);

        OpenAura(sink, 4, target: Tank, origin: Me, sequence: 1, effect: 999_000_001, durationMilliseconds: 30_000);
        OpenAura(sink, 4, target: Dps, origin: Boss, sequence: 2, effect: 999_000_002, durationMilliseconds: 8_000);
        OpenAura(sink, 4, target: Dps, origin: Tank, sequence: 3, effect: 999_000_003, durationMilliseconds: 8_000);
        OpenAura(sink, 4, target: Tank, origin: MySummon, sequence: 4, effect: 999_000_004, durationMilliseconds: 8_000);
        // Nobody known put this one there.
        OpenAura(sink, 4, target: Tank, origin: 0, sequence: 5, effect: 999_000_005, durationMilliseconds: 8_000);
        // An NPC's debuff (Vakron's Gaze) names its target as the origin; on you it's still a debuff.
        OpenAura(sink, 4, target: Me, origin: Me, sequence: 6, effect: 1_900_700, durationMilliseconds: 20_500);
        // A refresh overwrites the store's origin with an id that is no entity.
        Renew(sink, 5, target: Tank, sequence: 1);
        Renew(sink, 5, target: Dps, sequence: 2);

        var now = scene.CopyGroupFrameTo(members, auras);

        Assert.Equal(Started.AddSeconds(10).ToUnixTimeMilliseconds(), now);
        Assert.Equal([Me, Dps, Tank, Unlisted], members.Select(member => member.EntityId));
        Assert.Equal(["Healer", "Dps", "Tank", ""], members.Select(member => member.Nickname));
        Assert.True(members[0].IsLocalPlayer);
        Assert.Equal(9_000, members[0].CurrentHp);
        Assert.Equal(9_500, members[0].MaxHp);
        Assert.Equal(5_000, members[2].CurrentHp);
        Assert.Equal(10_000, members[2].MaxHp);
        Assert.Null(members[1].CurrentHp);
        // Your summon's buff is yours; the tank's buff on the DPS is someone else's.
        Assert.Equal(
            [
                (Tank, 999_000_001, GroupFrameAuraKind.Mine),
                (Tank, 999_000_004, GroupFrameAuraKind.Mine),
                (Dps, 999_000_002, GroupFrameAuraKind.Debuff),
                (Dps, 999_000_003, GroupFrameAuraKind.Other)
            ],
            auras.Where(aura => aura.TargetEntityId != Me).Select(aura => (aura.TargetEntityId, aura.SkillId, aura.Kind)).OrderBy(aura => aura.TargetEntityId == Tank ? 0 : 1).ThenBy(aura => aura.SkillId));
        Assert.Equal(Started.AddSeconds(5 + 30).ToUnixTimeMilliseconds(), auras.Single(aura => aura.SkillId == 999_000_001).ExpiresAtMilliseconds);
        Assert.Equal(GroupFrameAuraKind.Debuff, Assert.Single(auras, aura => aura.TargetEntityId == Me).Kind);

        // A meter reset leaves the frame alone.
        scene.Reset(Started.AddSeconds(20));
        sink.AppendNpcHp(Source(22, 0x1B92), Tank, 9_500, 10_000);
        clock.Now = Started.AddSeconds(25);
        scene.CopyGroupFrameTo(members, auras);
        Assert.Equal([Me, Dps, Tank, Unlisted], members.Select(member => member.EntityId));
        Assert.Equal(9_500L, members[2].CurrentHp);
        Assert.Contains(auras, aura => aura is { TargetEntityId: Tank, Kind: GroupFrameAuraKind.Mine });

        // A new list without the tank: the tank and the member it missed, silent since, are out.
        sink.AppendPlayerGroupMember(Source(40, 0x0092, sequence: 2), Me, PlayerGroupMembership.Party(0));
        sink.AppendPlayerGroupMember(Source(40, 0x0092, sequence: 2), Dps, PlayerGroupMembership.Party(1));
        clock.Now = Started.AddSeconds(41);
        scene.CopyGroupFrameTo(members, auras);
        Assert.Equal([Me, Dps], members.Select(member => member.EntityId));
    }

    [Fact]
    public void Shields_Count_While_The_Aura_Of_Their_Skill_Is_On_And_Follow_What_Is_Left()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var clock = new SettableTimeProvider { Now = Started.AddSeconds(4) };
        var scene = new SceneLiveReadModel(Started, clock);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var members = new List<GroupFrameMember>();
        var auras = new List<GroupFrameAura>();
        sink.AppendNickname(Source(0, 0x048D), Me, "Healer", isLocalPlayer: true);
        sink.AppendNickname(Source(0, 0x048D), Tank, "Tank");
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Me, PlayerGroupMembership.Party(0));
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Tank, PlayerGroupMembership.Party(1));
        sink.AppendNpcHp(Source(1, 0x008D), Tank, 5_000, 10_000);

        // Yustiel's Power: a shield pool the game data knows.
        var grant = new CombatWireObservation
        {
            SkillCode = 17_420_010,
            Damage = 3_119,
            ChainId = 79,
            BodyResourceEffectRef = ResourceEffectRef.FromRaw(1_742_001_011),
            PeriodicRelation = PeriodicEffectRelation.Target,
            PeriodicMode = 9,
            PeriodicTailSkillCodeRaw = 17_420_010,
            PeriodicTailLength = 4
        };
        var shieldSkillId = CombatSemanticEvidenceResolver.Evaluate(in grant).Resource.Slot!.Value.SkillId;

        // Counts right away, but goes when no aura of its skill follows.
        sink.AppendCombatWireObservation(Source(3, 0x0438), Me, Tank, in grant);
        Assert.Equal(3_119, ShieldOf(Tank));
        clock.Now = Started.AddSeconds(10);
        Assert.Equal(0, ShieldOf(Tank));

        sink.AppendCombatWireObservation(Source(11, 0x0438), Me, Tank, in grant);
        OpenAura(sink, 11, target: Tank, origin: Me, sequence: 7, effect: (uint)shieldSkillId, durationMilliseconds: 14_500);
        clock.Now = Started.AddSeconds(15);
        Assert.Equal(3_119, ShieldOf(Tank));
        Assert.Equal(0, ShieldOf(Me));

        // A hit takes part of it, then it breaks.
        sink.AppendCombatWireObservation(Source(16, 0x0438), Boss, Tank, grant with { PeriodicMode = 11, Damage = 1_000, PeriodicTailPrefixValue = 2_119 });
        clock.Now = Started.AddSeconds(17);
        Assert.Equal(1_000, ShieldOf(Tank));
        sink.AppendCombatWireObservation(Source(18, 0x0438), Boss, Tank, grant with { PeriodicMode = 10, Damage = 0 });
        clock.Now = Started.AddSeconds(19);
        Assert.Equal(0, ShieldOf(Tank));

        long ShieldOf(int entityId)
        {
            scene.CopyGroupFrameTo(members, auras);
            return members.Single(member => member.EntityId == entityId).Shield;
        }
    }

    [Fact]
    public void A_Shield_Lasts_As_Long_As_Its_Own_Aura_And_Direct_Hits_Wear_It_Down()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var clock = new SettableTimeProvider { Now = Started.AddSeconds(4) };
        var scene = new SceneLiveReadModel(Started, clock);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var members = new List<GroupFrameMember>();
        var auras = new List<GroupFrameAura>();
        sink.AppendNickname(Source(0, 0x048D), Me, "Healer", isLocalPlayer: true);
        sink.AppendNickname(Source(0, 0x048D), Tank, "Tank");
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Me, PlayerGroupMembership.Party(0));
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Tank, PlayerGroupMembership.Party(1));
        var grant = new CombatWireObservation
        {
            SkillCode = 17_420_010,
            Damage = 3_119,
            ChainId = 79,
            BodyResourceEffectRef = ResourceEffectRef.FromRaw(1_742_001_011),
            PeriodicRelation = PeriodicEffectRelation.Target,
            PeriodicMode = 9,
            PeriodicTailSkillCodeRaw = 17_420_010,
            PeriodicTailLength = 4
        };
        var shieldSkillId = (uint)CombatSemanticEvidenceResolver.Evaluate(in grant).Resource.Slot!.Value.SkillId;

        // Like Yustiel's Power: the shield's own aura (sequence = the pool's chain), and a longer one of the same skill.
        OpenAura(sink, 3, target: Tank, origin: Me, sequence: 79, effect: shieldSkillId, durationMilliseconds: 10_000);
        OpenAura(sink, 3, target: Tank, origin: Me, sequence: 80, effect: shieldSkillId, durationMilliseconds: 14_500);
        sink.AppendCombatWireObservation(Source(3, 0x0438), Me, Tank, in grant);

        // A direct hit wears it down; damage over time goes past it.
        sink.AppendCombatWireObservation(Source(3.5, 0x0438), Boss, Tank, new CombatWireObservation { SkillCode = 1_802_435, Damage = 1_000 });
        sink.AppendCombatWireObservation(Source(3.6, 0x0538), Boss, Tank, new CombatWireObservation { SkillCode = 1_802_435, Damage = 500, ChainId = 5, PeriodicRelation = PeriodicEffectRelation.Target, PeriodicMode = 10, PeriodicTailSkillCodeRaw = 1_802_435 });
        Assert.Equal(2_119, ShieldOf(Tank));
        var shields = new List<GroupFrameShield>();
        scene.CopyGroupFrameTo(members, auras, shields);
        Assert.Equal([new GroupFrameShield(Tank, 2_119, Started.AddSeconds(13).ToUnixTimeMilliseconds())], shields);

        // It runs out with its own aura, though the longer one is still on.
        clock.Now = Started.AddSeconds(13.5);
        Assert.Equal(0, ShieldOf(Tank));

        // The next one breaks: its own aura ends at once (2C38 result 7).
        OpenAura(sink, 14, target: Tank, origin: Me, sequence: 81, effect: shieldSkillId, durationMilliseconds: 10_000);
        sink.AppendCombatWireObservation(Source(14, 0x0438), Me, Tank, grant with { ChainId = 81 });
        clock.Now = Started.AddSeconds(14.5);
        Assert.Equal(3_119, ShieldOf(Tank));
        sink.RegisterObservation2C38(Source(15, 0x2C38), Tank, [new AuraResultRecord(0, 81, 7, 0, 0, 0)]);
        clock.Now = Started.AddSeconds(15.5);
        Assert.Equal(0, ShieldOf(Tank));

        long ShieldOf(int entityId)
        {
            scene.CopyGroupFrameTo(members, auras);
            return members.Single(member => member.EntityId == entityId).Shield;
        }
    }

    [Fact]
    public void A_Boss_Is_After_Whom_It_Last_Aimed_At_Until_It_Is_Down()
    {
        const int Mob = 600;
        var clock = new SettableTimeProvider { Now = Started.AddSeconds(1) };
        var scene = new SceneLiveReadModel(Started, clock);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var members = new List<GroupFrameMember>();
        var auras = new List<GroupFrameAura>();
        sink.AppendNickname(Source(0, 0x048D), Me, "Healer", isLocalPlayer: true);
        sink.AppendNickname(Source(0, 0x048D), Tank, "Tank");
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Me, PlayerGroupMembership.Party(0));
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Tank, PlayerGroupMembership.Party(1));
        sink.AppendNpcKind(Source(1, 0x0140), Boss, NpcKind.Boss);

        // Aimed casts (0x0238 flag 2) name their target in ChainId; not a plain mob's, nor the boss's follow-ups (flag 0).
        Aim(2, Boss, Tank);
        Aim(2.5, Mob, Me);
        Aim(2.7, Boss, Me, flag: 0);
        Assert.Equal([Tank], Targets());
        Aim(5, Boss, Me);
        Assert.Equal([Me], Targets());

        // It holds while the boss is quiet for a while, and goes once the boss is down.
        clock.Now = Started.AddSeconds(24);
        Assert.Equal([Me], Targets());
        clock.Now = Started.AddSeconds(26);
        Assert.Empty(Targets());
        Aim(27, Boss, Tank);
        sink.AppendNpcHp(Source(28, 0x008D), Boss, 0, 1_000_000);
        clock.Now = Started.AddSeconds(29);
        Assert.Empty(Targets());

        void Aim(double atSeconds, int caster, int target, int flag = 2)
        {
            sink.AppendCombatWireObservation(Source(atSeconds, 0x0238), caster, target, new CombatWireObservation { SkillCode = 1_802_435, ChainId = target, Flag = flag });
            clock.Now = Started.AddSeconds(atSeconds + 0.5);
        }

        int[] Targets()
        {
            scene.CopyGroupFrameTo(members, auras);
            return [.. members.Where(member => member.IsBossTarget).Select(member => member.EntityId)];
        }
    }

    [Fact]
    public void The_Boss_Frame_Names_The_Target_First_And_Aims_Fill_In_When_It_Is_Empty()
    {
        const int OtherBoss = 601;
        var clock = new SettableTimeProvider { Now = Started.AddSeconds(1) };
        var scene = new SceneLiveReadModel(Started, clock);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var members = new List<GroupFrameMember>();
        var auras = new List<GroupFrameAura>();
        sink.AppendNickname(Source(0, 0x048D), Me, "Healer", isLocalPlayer: true);
        sink.AppendNickname(Source(0, 0x048D), Tank, "Tank");
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Me, PlayerGroupMembership.Party(0));
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Tank, PlayerGroupMembership.Party(1));
        sink.AppendNpcKind(Source(1, 0x0140), Boss, NpcKind.Boss);
        sink.AppendNpcKind(Source(1, 0x0140), OtherBoss, NpcKind.Boss);

        // The boss frame (0x2092) names whom the boss turned to before it attacks them, and stands over the older aim
        // and a random-target cast at someone else.
        Aim(2, Boss, Tank);
        Turn(3, Me);
        Assert.Equal([Me], Targets());
        Aim(4, Boss, Tank);
        Assert.Equal([Me], Targets());

        // No one: the boss left the fight and its aims go with it; another boss still at it shows by its next aim.
        Turn(5, 0);
        Assert.Empty(Targets());
        Aim(6, OtherBoss, Tank);
        Assert.Equal([Tank], Targets());

        void Turn(double atSeconds, int target)
        {
            sink.AppendBossTarget(Source(atSeconds, 0x2092), target);
            clock.Now = Started.AddSeconds(atSeconds + 0.5);
        }

        void Aim(double atSeconds, int caster, int target)
        {
            sink.AppendCombatWireObservation(Source(atSeconds, 0x0238), caster, target, new CombatWireObservation { SkillCode = 1_802_435, ChainId = target, Flag = 2 });
            clock.Now = Started.AddSeconds(atSeconds + 0.5);
        }

        int[] Targets()
        {
            scene.CopyGroupFrameTo(members, auras);
            return [.. members.Where(member => member.IsBossTarget).Select(member => member.EntityId)];
        }
    }

    private static void OpenAura(IRuntimeObservationSink sink, double atSeconds, int target, int origin, int sequence, uint effect, ushort durationMilliseconds)
        => sink.RegisterObservation2A38(Source(atSeconds, 0x2A38), target, 1, 19, sequence, 0, durationMilliseconds, 0, 0, 0, origin, 1, ResourceEffectRef.FromRaw(effect), 0, 0, 0);

    private static void Renew(IRuntimeObservationSink sink, double atSeconds, int target, int sequence)
        => sink.RegisterObservation2B38(Source(atSeconds, 0x2B38), target, 161, 19, sequence, ResourceEffectRef.FromRaw(888_000_001), 0, 0, 0, 0);

    // A death is HP falling to 0 while a boss fight is on. The count starts over at the next pull once every fight is
    // over or has been quiet for a minute, so a boss-kind add pulled mid-fight doesn't wipe it; it shows until then.
    [Fact]
    public void Deaths_Count_Per_Boss_Fight()
    {
        const int Add = 601;
        const int NextBoss = 602;
        var clock = new SettableTimeProvider { Now = Started.AddSeconds(1) };
        var scene = new SceneLiveReadModel(Started, clock);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var members = new List<GroupFrameMember>();
        var auras = new List<GroupFrameAura>();
        sink.AppendNickname(Source(0, 0x048D), Me, "Healer", isLocalPlayer: true);
        sink.AppendNickname(Source(0, 0x048D), Tank, "Tank");
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Me, PlayerGroupMembership.Party(0));
        sink.AppendPlayerGroupMember(Source(1, 0x0092, sequence: 1), Tank, PlayerGroupMembership.Party(1));
        foreach (var boss in (int[])[Boss, Add, NextBoss])
        {
            sink.AppendNpcKind(Source(1, 0x0140), boss, NpcKind.Boss);
            sink.AppendNpcHp(Source(1, 0x008D), boss, 10_000_000, 10_000_000);
        }

        Hp(2, Tank, 10_000);
        Hp(2, Me, 8_000);
        Hp(3, Tank, 0); // on the way there: no fight on
        Hp(5, Tank, 10_000);
        Hit(10, Tank, Boss);
        Hp(20, Tank, 0);
        Hp(30, Tank, 10_000);
        Hp(40, Tank, 0);
        Hp(50, Tank, 10_000);
        Hit(60, Me, Add);
        Hp(70, Me, 0);
        Hp(75, Me, 8_000);
        Assert.Equal([(Me, 1), (Tank, 2)], Deaths(76));

        sink.AppendNpcHp(Source(80, 0x008D), Boss, 0, 10_000_000);
        Hp(130, Tank, 10_000); // party status keeps the tank listed
        Hp(140, Me, 0); // after the kill, with the add quiet for over a minute
        Assert.Equal([(Me, 1), (Tank, 2)], Deaths(141));

        Hit(200, Tank, NextBoss);
        Hp(210, Tank, 0);
        Assert.Equal([(Tank, 1)], Deaths(211));

        // Party status, as the game sends HP for members.
        void Hp(double atSeconds, int entityId, long hp) => sink.AppendNpcHp(Source(atSeconds, 0x1B92), entityId, hp, 10_000);

        void Hit(double atSeconds, int sourceId, int targetId) =>
            sink.AppendCombatWireObservation(Source(atSeconds, 0x0438), sourceId, targetId, new CombatWireObservation { SkillCode = 11_000_010, Damage = 1_000, HitCount = 1, AttemptCount = 1 });

        (int, int)[] Deaths(double atSeconds)
        {
            clock.Now = Started.AddSeconds(atSeconds);
            scene.CopyGroupFrameTo(members, auras);
            return [.. members.Where(member => member.Deaths > 0).Select(member => (member.EntityId, member.Deaths)).OrderBy(death => death.EntityId)];
        }
    }

    private static PacketObservationSource Source(double atSeconds, ushort opcode, long sequence = 0)
        => new(Started.AddSeconds(atSeconds).ToUnixTimeMilliseconds(), 0, opcode, 0, sequence, default);

    private sealed class SettableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
