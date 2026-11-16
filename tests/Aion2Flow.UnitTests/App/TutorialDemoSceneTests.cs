using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Playback;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using static Cloris.Aion2Flow.Services.TutorialDemoScene;

namespace Cloris.Aion2Flow.Tests.App;

public sealed class TutorialDemoSceneTests
{
    private static readonly DateTimeOffset Started = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly (int Id, string Name, CharacterClass Class)[] Players =
    [
        (HappyCatId, "HappyCat", CharacterClass.Cleric),
        (TankyBearId, "TankyBear", CharacterClass.Templar),
        (FlameFoxId, "FlameFox", CharacterClass.Sorcerer),
        (AngryRaccoonId, "AngryRaccoon", CharacterClass.Gladiator),
    ];

    // The demo as a history entry: one pull of Vakron, killed, with the four made-up players and HappyCat as the user.
    // Everyone dealt damage; HappyCat healed (partly overhealing), kept a HoT up and shielded; the tank had the aggro,
    // Vakron was staggered once, and HappyCat's damage passes a 3.5 % loot share of the damage dealt by half time and
    // of his HP only later. It plays back to the kill, and every run builds the same fight.
    [Fact]
    public void The_Demo_Fight_Is_A_History_Entry_Of_A_Kill()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var demo = Build(Started);
        var payload = demo.Record.ScenePayload;

        Assert.Equal(("tutorial", Started, Started + Length), (demo.Record.Trigger, demo.Record.ArchivedAt, demo.EndedAt));
        Assert.Null(demo.Record.Name);
        Assert.Equal(MapId, payload.Snapshot.MapId);
        var fight = Assert.Single(payload.BossFights.AsSpan().ToArray());
        Assert.Equal((VakronId, BossFightOutcome.Killed, 0L, VakronMaxHp, 1), (fight.BossId, fight.Outcome, fight.BossHp, fight.BossMaxHp, fight.StaggerCount));
        Assert.InRange(fight.Snapshot.EncounterTime, 90_000, 110_000);
        // He falls to the last blow (none lands on him dead), a moment before the scene ends.
        Assert.Equal(fight.Snapshot.EncounterEndTime, payload.Snapshot.EncounterEndTime);
        Assert.InRange((long)Length.TotalMilliseconds - fight.Snapshot.EncounterEndTime, 0, 2_000);

        Assert.Equal(Players.Select(static player => player.Id).Order(),
            payload.Snapshot.Combatants.AsSpan().ToArray().Where(static entry => entry.Metrics.IsVisiblePlayerCombatant).Select(static entry => entry.Id).Order());
        foreach (var (id, name, characterClass) in Players)
        {
            Assert.True(payload.IdentityScope.TryGetPcMetadata(id, out var pc));
            Assert.Equal((name, (CharacterClass?)characterClass, id == HappyCatId, (int?)ServerId), (pc.Nickname, pc.CharacterClass, pc.IsLocalPlayer, pc.OriginServerId));
            Assert.True(pc.IsLocalPlayer || pc.GroupRelation == PlayerGroupRelation.PartyMember, name);
            Assert.True(payload.Snapshot.Combatants[id].DamageAmount > 0, name);
        }

        var cat = payload.Snapshot.Combatants[HappyCatId];
        Assert.InRange(cat.EffectiveHealingAmount, 1, cat.HealingAmount - 1);
        Assert.True(cat.PeriodicHealingAmount > 0);
        Assert.True(cat.ShieldAmount > 0);
        foreach (var snapshot in (SceneCombatSnapshot[])[payload.Snapshot, fight.Snapshot])
            Assert.Equal(TankyBearId, Players.MaxBy(player => snapshot.Combatants[player.Id].AggroShare).Id);

        var (from, to) = (fight.Snapshot.EncounterStartTime, fight.Snapshot.EncounterEndTime);
        var half = from + (to - from) / 2;
        var mine = DamageOnVakron(payload, HappyCatId, from, half);
        var dealt = Players.Sum(player => DamageOnVakron(payload, player.Id, from, half));
        Assert.True(mine >= 0.035 * dealt && mine < 0.035 * VakronMaxHp, $"{mine} of {dealt} dealt by half time");
        Assert.True(fight.Snapshot.Combatants[HappyCatId].DamageAmount >= 0.05 * VakronMaxHp);

        var end = new ScenePlaybackSession(new ArchivedScenePlaybackSource(payload)).Seek((long)Length.TotalMilliseconds, TestContext.Current.CancellationToken);
        Assert.Equal(0, end.EntityVitals.Single(static vital => vital.EntityId == VakronId).CurrentHp);
        Assert.Equal(Players.Select(player => Amounts(payload.Snapshot, player.Id)), Players.Select(player => Amounts(end.Snapshot, player.Id)));

        Assert.Equal(Numbers(payload), Numbers(Build(Started.AddDays(3)).Record.ScenePayload));
    }

    // The scene itself, read at its end the way the group frame reads it: HappyCat first, then the party in list order,
    // each with other HP; HappyCat's Light of Regeneration and shields on two members each, Vakron's Gaze on AngryRaccoon,
    // FlameFox's death in the fight. And as the skill monitor reads it: HappyCat's Debilitating Mark still on Vakron.
    [Fact]
    public void The_Demo_Scene_Ends_With_The_Party_Hurt_Buffed_And_Shielded()
    {
        CombatResourceRegistry.EnsureCombatResources();
        var demo = Build(Started);
        var (members, auras, shields) = (new List<GroupFrameMember>(), new List<GroupFrameAura>(), new List<GroupFrameShield>());

        Assert.Equal(demo.EndedAt.ToUnixTimeMilliseconds(), demo.Scene.CopyGroupFrameTo(members, auras, shields));

        Assert.Equal(Players.Select(static player => (player.Id, player.Name, player.Id == HappyCatId, ServerId)),
            members.Select(static member => (member.EntityId, member.Nickname, member.IsLocalPlayer, member.ServerId)));
        var health = members.Select(static member => (double)member.CurrentHp!.Value / member.MaxHp!.Value).ToArray();
        Assert.Equal(4, health.Distinct().Count());
        Assert.True(health[0] > 0.95 && health[3] < 0.4, string.Join(" ", health));
        Assert.Equal([TankyBearId, AngryRaccoonId],
            auras.Where(static aura => aura is { SkillId: 17_090_350, Kind: GroupFrameAuraKind.Mine, CasterEntityId: HappyCatId }).Select(static aura => aura.TargetEntityId).Order());
        Assert.Equal([HappyCatId, TankyBearId], shields.Where(static shield => shield.Remaining > 0).Select(static shield => shield.TargetEntityId).Order());
        Assert.Contains(auras, static aura => aura is { TargetEntityId: AngryRaccoonId, Kind: GroupFrameAuraKind.Debuff });
        Assert.Equal([0, 0, 1, 0], members.Select(static member => member.Deaths));

        var onVakron = new List<AuraInstanceState>();
        demo.Scene.CopyLocalPlayerAurasTo([], VakronId, onVakron);
        Assert.Contains(onVakron, static aura => aura is { OpenerEntityId: HappyCatId, OpenerEffectRef.RawId: 17_080_240u });
    }

    private static long DamageOnVakron(SceneArchivePayload payload, int playerId, long from, long to) =>
        payload.CreateMetricEventsWithin(playerId, from, to)
            .Where(e => e.SourceId == playerId && e.TargetId == VakronId && e.Contribution.Metric == CombatMetricKind.Damage)
            .Sum(static e => e.Amount);

    private static (long Damage, long Healing, long Real, long Shield, long Taken) Amounts(SceneCombatSnapshot snapshot, int playerId)
    {
        var metrics = snapshot.Combatants[playerId];
        return (metrics.DamageAmount, metrics.HealingAmount, metrics.EffectiveHealingAmount, metrics.ShieldAmount, metrics.DamageTakenAmount);
    }

    private static string[] Numbers(SceneArchivePayload payload) =>
        [.. payload.Snapshot.Combatants.AsSpan().ToArray().Select(static entry => $"{entry.Id} {entry.Metrics}")];
}
