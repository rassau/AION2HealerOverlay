using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class SceneLocalPlayerFrameTests
{
    private static readonly DateTimeOffset Started = new(2026, 9, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CopyLocalPlayerAurasTo_RefreshesIdentityAndKeepsOnlyActiveLocalTargetsIncludingPartyBuffs()
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(2)));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var auras = new List<AuraInstanceState> { default };
        OpenAura(sink, 100, 200, 1, durationMilliseconds: 5_000);
        OpenAura(sink, 100, 100, 2, durationMilliseconds: 5_000);
        OpenAura(sink, 200, 200, 3, durationMilliseconds: 5_000);
        OpenAura(sink, 100, 200, 4, durationMilliseconds: 500);

        var unknown = scene.CopyLocalPlayerAurasTo(auras);
        Assert.Equal(0, unknown.EntityId);
        Assert.Empty(auras);

        sink.AppendNickname(Source(), 100, "Local", isLocalPlayer: true);
        var frame = scene.CopyLocalPlayerAurasTo(auras);

        Assert.Equal(scene.SessionId, frame.SessionId);
        Assert.Equal(100, frame.EntityId);
        Assert.Equal(2_000, frame.ObservedAtMilliseconds);
        Assert.Equal(scene.Journal.NextObservationOrdinal, frame.EndObservationOrdinalExclusive);
        Assert.Equal([1, 2], auras.Select(static aura => aura.InstanceSequenceId));
        Assert.Equal([200, 100], auras.Select(static aura => aura.OriginEntityId));
        Assert.All(auras, aura => Assert.Equal(frame.EntityId, aura.TargetEntityId));
    }

    // Everyone's on the target (the skill monitor's settings pick whose show), the expired one and those elsewhere left out.
    [Fact]
    public void CopyLocalPlayerAurasTo_AlsoCopiesEveryActiveAuraOnTheTargetAtOneTime()
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(2)));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(), 100, "Local", isLocalPlayer: true);
        OpenAura(sink, 100, 100, 1);
        OpenAura(sink, 300, 100, 2, durationMilliseconds: 5_000);
        OpenAura(sink, 300, 100, 3, durationMilliseconds: 500);
        OpenAura(sink, 400, 100, 4);
        // Another player's on the target.
        OpenAura(sink, 300, 200, 5);
        var (auras, onTarget) = (new List<AuraInstanceState>(), new List<AuraInstanceState> { default });

        var frame = scene.CopyLocalPlayerAurasTo(auras, 300, onTarget);

        Assert.Equal(2_000, frame.ObservedAtMilliseconds);
        Assert.Equal(1, Assert.Single(auras).InstanceSequenceId);
        Assert.Equal([(2, 100), (5, 200)], onTarget.Select(static aura => (aura.InstanceSequenceId, aura.OpenerEntityId)).Order());

        scene.CopyLocalPlayerAurasTo(auras, 0, onTarget);
        Assert.Empty(onTarget);
    }

    // An ally is a player of your faction, one whose faction (or yours) isn't known, or one in your party whatever faction
    // they read as, and their summons and yours; an NPC, another player of the other faction, their summon and no target at
    // all are not. Of these only the NPC is one (a player's summon is none).
    [Fact]
    public void CopyLocalPlayerAurasTo_SaysWhetherTheTargetIsAnAlly()
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(2)));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(), 100, "Abcd", Faction.Light, isLocalPlayer: true);
        sink.AppendNickname(Source(), 200, "Efgh", Faction.Light);
        sink.AppendNickname(Source(), 300, "Ijkl", Faction.Dark);
        sink.AppendNickname(Source(), 400, "Mnop");
        sink.AppendNickname(Source(), 600, "Qrst", Faction.Dark);
        sink.AppendPlayerGroupMember(Source(), 600, PlayerGroupMembership.Party(1));
        sink.AppendNpcCode(Source(), 500, 2_100_002);
        sink.AppendSummon(Source(), 300, 700);
        sink.AppendSummon(Source(), 100, 800);
        sink.AppendSummon(Source(), 200, 900);
        var (auras, onTarget) = (new List<AuraInstanceState>(), new List<AuraInstanceState>());
        int[] targets = [200, 300, 400, 600, 500, 700, 800, 900, 0];

        Assert.Equal([true, false, true, true, false, false, true, true, false], targets.Select(target => scene.CopyLocalPlayerAurasTo(auras, target, onTarget).TargetIsAlly));
        Assert.Equal([false, false, false, false, true, false, false, false, false], targets.Select(target => scene.CopyLocalPlayerAurasTo(auras, target, onTarget).TargetIsNpc));
    }

    // Who opened an aura on the target from the other side: a player of the other faction outside your party, or their
    // summon; not a party member who reads as the other faction, one of no known faction, an NPC or you. Auras on you don't
    // count: the skill monitor's settings are about your target.
    [Fact]
    public void CopyLocalPlayerAurasTo_SaysWhoOpenedAurasOnTheTargetFromTheOtherSide()
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(2)));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(), 100, "Abcd", Faction.Light, isLocalPlayer: true);
        sink.AppendNickname(Source(), 300, "Ijkl", Faction.Dark);
        sink.AppendNickname(Source(), 400, "Mnop");
        sink.AppendNickname(Source(), 600, "Qrst", Faction.Dark);
        sink.AppendNickname(Source(), 800, "Uvwx", Faction.Dark);
        sink.AppendPlayerGroupMember(Source(), 600, PlayerGroupMembership.Party(1));
        sink.AppendNpcCode(Source(), 500, 2_100_002);
        sink.AppendSummon(Source(), 300, 700);
        foreach (var (opener, sequence) in new[] { (300, 1), (400, 2), (600, 3), (100, 4), (500, 5), (700, 6) })
            OpenAura(sink, 500, opener, sequence);
        OpenAura(sink, 100, 800, 7);
        var (auras, onTarget, enemies) = (new List<AuraInstanceState>(), new List<AuraInstanceState>(), new HashSet<int> { 1 });

        scene.CopyLocalPlayerAurasTo(auras, 500, onTarget, enemies);

        Assert.Equal([300, 700], enemies.Order());
    }

    // A player who fought you or your group on this map is on the other side whatever their faction (in battlefields the
    // other side carries yours): one who hit your party member, and one you hit. Not one who fought only someone else, nor
    // one whose summon hit you (the player must, themselves), nor one who healed you; a meter reset keeps it, a new map forgets.
    [Fact]
    public void CopyLocalPlayerAurasTo_A_Player_Who_Fought_You_Or_Your_Group_Is_On_The_Other_Side()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(2)));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var heal = new CombatWireObservation { SkillCode = 17_160_037, Damage = 1_275, DetailResourceEffectRef = ResourceEffectRef.FromRaw(1_716_003_711) };
        sink.SetCurrentMap(Source(), 200_003);
        sink.AppendNickname(Source(), 100, "Abcd", Faction.Light, isLocalPlayer: true);
        foreach (var (id, name) in new[] { (200, "Efgh"), (300, "Ijkl"), (400, "Mnop"), (500, "Qrst"), (600, "Uvwx"), (700, "Yzab") })
            sink.AppendNickname(Source(), id, name, Faction.Light);
        sink.AppendPlayerGroupMember(Source(), 200, PlayerGroupMembership.Party(1));
        sink.AppendSummon(Source(), 700, 800);
        sink.AppendCombatWireObservation(Source(), 300, 200, Hit());
        sink.AppendCombatWireObservation(Source(), 100, 400, Hit());
        sink.AppendCombatWireObservation(Source(), 500, 600, Hit());
        sink.AppendCombatWireObservation(Source(), 800, 100, Hit());
        sink.AppendCombatWireObservation(Source(), 600, 100, in heal);
        foreach (var (opener, sequence) in new[] { (300, 1), (400, 2), (500, 3), (600, 4), (700, 5), (800, 6) })
            OpenAura(sink, 900, opener, sequence);
        var (auras, onTarget, enemies) = (new List<AuraInstanceState>(), new List<AuraInstanceState>(), new HashSet<int>());
        int[] targets = [300, 400, 500, 600, 700, 800, 200];

        Assert.Equal([false, false, true, true, true, true, true], targets.Select(target => scene.CopyLocalPlayerAurasTo(auras, target, onTarget).TargetIsAlly));
        scene.CopyLocalPlayerAurasTo(auras, 900, onTarget, enemies);
        Assert.Equal([300, 400], enemies.Order());

        scene.Reset(Started.AddSeconds(1));
        Assert.False(scene.CopyLocalPlayerAurasTo(auras, 300, onTarget).TargetIsAlly);

        sink.SetCurrentMap(Source(), 200_004);
        sink.AppendNickname(Source(), 300, "Ijkl", Faction.Light);
        Assert.True(scene.CopyLocalPlayerAurasTo(auras, 300, onTarget).TargetIsAlly);

        static CombatWireObservation Hit() =>
            new() { SkillCode = 12_220_040, Damage = 1_000, HitCount = 1, AttemptCount = 1, DetailResourceEffectRef = ResourceEffectRef.FromRaw(1_222_004_011) };
    }

    [Fact]
    public void CopyLocalPlayerAurasTo_PreservesJournalBoundaryAndStartsFreshAfterReset()
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(10)));
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(), 100, "Local", isLocalPlayer: true);
        OpenAura(sink, 100, 200, 1);
        var auras = new List<AuraInstanceState>();
        var first = scene.CopyLocalPlayerAurasTo(auras);

        sink.RegisterCooldown4738(Source(), 12_780_000, 5_000);
        var oldEnd = scene.Journal.NextObservationOrdinal;
        scene.Reset(Started.AddSeconds(5));
        sink.AppendNickname(Source(), 200, "NextLocal", isLocalPlayer: true);
        OpenAura(sink, 200, 100, 2);
        var next = scene.CopyLocalPlayerAurasTo(auras);

        Assert.NotEqual(first.SessionId, next.SessionId);
        Assert.Equal(oldEnd, next.StartObservationOrdinal);
        Assert.Equal(5_000, next.ObservedAtMilliseconds);
        Assert.Equal(200, next.EntityId);
        Assert.Equal(2, Assert.Single(auras).InstanceSequenceId);
        var cursor = new JournalCursor(first.StartObservationOrdinal);
        var result = scene.Journal.ReadEntries(cursor, first.EndObservationOrdinalExclusive, 100, entries =>
        {
            for (var i = 0; i < entries.Count; i++)
            {
                Assert.Equal(first.SessionId, entries[i].SceneSessionId);
                Assert.True(entries[i].Domain != ObservedEventDomain.State || entries[i].State.StateCode != StateCodes.Cooldown4738);
            }
        });
        Assert.Equal(first.EndObservationOrdinalExclusive, result.Cursor.NextObservationOrdinal);
    }

    [Theory]
    [InlineData(SceneKind.Standard)]
    [InlineData(SceneKind.Boss)]
    public async Task CopyLocalPlayerAurasTo_KeepsAurasAndJournalOnOneRevisionDuringConcurrentCombatAndReset(SceneKind kind)
    {
        var scene = new SceneLiveReadModel(Started, new FixedTimeProvider(Started.AddSeconds(1)));
        scene.ChangeKind(kind, Started, archiveCurrent: false);
        var sink = SceneSinkFactory.CreateForLive(scene)();
        sink.AppendNickname(Source(), 100, "Local", isLocalPlayer: true);
        sink.AppendNpcCode(Source(), 300, 2_100_002);
        sink.AppendNpcKind(Source(), 300, NpcKind.Boss);
        var combat = new CombatWireObservation { SkillCode = 11_000_010, Damage = 10, HitCount = 1, AttemptCount = 1 };
        sink.AppendCombatWireObservation(Source(), 100, 300, in combat);
        using var rendezvous = new Barrier(2);
        using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellationSource.CancelAfter(TimeSpan.FromSeconds(30));
        var cancellationToken = cancellationSource.Token;
        var writer = Task.Run(() =>
        {
            for (var batch = 0; batch < 40; batch++)
            {
                rendezvous.SignalAndWait(cancellationToken);
                for (var i = 0; i < 128; i++)
                {
                    var sequenceId = batch * 128 + i + 1;
                    OpenAura(sink, 100, 200, sequenceId);
                    OpenAura(sink, 200 + i, 200, sequenceId);
                    sink.AppendCombatWireObservation(Source(), 100, 300, in combat);
                }
                scene.Reset(Started);
                sink.AppendCombatWireObservation(Source(), 100, 300, in combat);
            }
        }, cancellationToken);
        var reader = Task.Run(() =>
        {
            var auras = new List<AuraInstanceState>();
            for (var batch = 0; batch < 40; batch++)
            {
                rendezvous.SignalAndWait(cancellationToken);
                for (var i = 0; i < 128; i++)
                {
                    var frame = scene.CopyLocalPlayerAurasTo(auras);
                    Assert.Equal(100, frame.EntityId);
                    Assert.InRange(frame.ObservedAtMilliseconds, 0, 1_000);
                    foreach (var aura in auras)
                    {
                        Assert.Equal(frame.EntityId, aura.TargetEntityId);
                        Assert.InRange(aura.LastObservationOrdinal, frame.StartObservationOrdinal, frame.EndObservationOrdinalExclusive - 1);
                        // A reset after the frame lets go of its scene's entries; the ones still there belong to it.
                        scene.Journal.TryReadEntry(aura.LastObservationOrdinal, entry => Assert.Equal(frame.SessionId, entry.SceneSessionId));
                    }
                }
            }
        }, cancellationToken);

        try
        {
            await await Task.WhenAny(writer, reader);
            await Task.WhenAll(writer, reader);
        }
        finally
        {
            await cancellationSource.CancelAsync();
        }
    }

    private static void OpenAura(IRuntimeObservationSink sink, int targetId, int originId, int sequenceId, ushort durationMilliseconds = ushort.MaxValue)
        => sink.RegisterObservation2A38(Source(), targetId, 1, 19, sequenceId, 0, durationMilliseconds, 0, 0, 0, originId, 1, ResourceEffectRef.FromRaw(12_780_001), 0, 0, 0);

    private static PacketObservationSource Source() => new(Started.AddSeconds(1).ToUnixTimeMilliseconds(), 0, 0, 0, 0, default);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TestTimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
