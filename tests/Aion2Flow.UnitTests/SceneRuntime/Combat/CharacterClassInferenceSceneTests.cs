using Cloris.Aion2Flow.Resources.Catalog;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.Tests.SceneRuntime.Combat;

public sealed class CharacterClassInferenceSceneTests
{
    [Fact]
    public void Does_Not_Infer_CharacterClass_From_Periodic_Self_Proc()
    {
        CombatResourceRegistry.LoadSkillMap("en-US");
        using var scene = new SceneTestHarness();
        const int playerId = 101;
        const int targetId = 9001;

        scene.AppendNickname(playerId, "Player");
        var periodicObservation = new CombatWireObservation
        {
            SkillCode = 18160030,
            Damage = 120,
            PeriodicRelation = PeriodicEffectRelation.Self
        };
        scene.AppendCombatWireObservation(playerId, targetId, in periodicObservation);

        Thread.Sleep(5);

        var unknownDamage = new CombatWireObservation
        {
            SkillCode = 99999999,
            Damage = 1350,
            HitCount = 1,
            AttemptCount = 1
        };
        scene.AppendCombatWireObservation(playerId, targetId, in unknownDamage);

        _ = scene.CreateSnapshot();

        Assert.True(scene.Owner.Entities.TryGet(playerId, out var player));
        Assert.Null(player.CharacterClass);
    }

    [Fact]
    public void Prefers_Offensive_Class_Evidence_Over_Sprint_Mantra_Proc()
    {
        CombatResourceRegistry.LoadSkillMap("en-US");
        using var scene = new SceneTestHarness();
        const int playerId = 102;
        const int targetId = 9002;

        scene.AppendNickname(playerId, "Ranger");
        var periodicObservation = new CombatWireObservation
        {
            SkillCode = 18160030,
            Damage = 90,
            PeriodicRelation = PeriodicEffectRelation.Self
        };
        scene.AppendCombatWireObservation(playerId, targetId, in periodicObservation);

        Thread.Sleep(5);

        var rangerDamage = new CombatWireObservation
        {
            SkillCode = 14342350,
            Damage = 2450,
            HitCount = 1,
            AttemptCount = 1
        };
        scene.AppendCombatWireObservation(playerId, targetId, in rangerDamage);

        _ = scene.CreateSnapshot();

        Assert.True(scene.Owner.Entities.TryGet(playerId, out var player));
        Assert.Equal(CharacterClass.Ranger, player.CharacterClass);
    }

    [Fact]
    public void DamageContribution_Includes_PreInference_Damage_When_Class_Is_Inferred_Later()
    {
        CombatResourceRegistry.LoadSkillMap("en-US");
        using var scene = new SceneTestHarness();
        const int playerId = 103;
        const int targetId = 9003;

        scene.AppendNickname(playerId, "Late Ranger");
        var unknownDamage = new CombatWireObservation
        {
            SkillCode = 99999999,
            Damage = 1000,
            HitCount = 1,
            AttemptCount = 1
        };
        scene.AppendCombatWireObservation(playerId, targetId, in unknownDamage);

        Thread.Sleep(5);

        var rangerDamage = new CombatWireObservation
        {
            SkillCode = 14342350,
            Damage = 500,
            HitCount = 1,
            AttemptCount = 1
        };
        scene.AppendCombatWireObservation(playerId, targetId, in rangerDamage);

        var snapshot = scene.CreateSnapshot();

        Assert.True(snapshot.Combatants.TryGetValue(playerId, out var combatant));
        Assert.Equal(CharacterClass.Ranger, combatant.CharacterClass);
        Assert.Equal(1500, combatant.DamageAmount);
        Assert.Equal(1d, combatant.DamageContribution, 10);
    }

    [Fact]
    public void CharacterClassEvidence_Rebuilds_When_SkillMap_Arrives_After_Combat()
    {
        CombatResourceRegistry.SetGameResources(ResourceCatalog.Load(ResourceLanguage.English));
        using var scene = new SceneTestHarness();
        const int playerId = 10408;
        const int targetId = 9004;
        const int lateSkillCode = 997_060_233;

        var firstDamage = new CombatWireObservation
        {
            SkillCode = lateSkillCode,
            Damage = 281_041,
            HitCount = 1,
            AttemptCount = 1
        };
        scene.AppendCombatWireObservation(playerId, targetId, in firstDamage, 1_000);
        var secondDamage = new CombatWireObservation
        {
            SkillCode = lateSkillCode,
            Damage = 532_761,
            HitCount = 1,
            AttemptCount = 1
        };
        scene.AppendCombatWireObservation(playerId, targetId, in secondDamage, 2_000);

        var snapshotBeforeResources = scene.CreateSnapshot();

        Assert.True(snapshotBeforeResources.Combatants.TryGetValue(playerId, out var beforeResources));
        Assert.Null(beforeResources.CharacterClass);

        CombatResourceTestFixture.SetResources(
        [
            new SkillDisplayEntry(lateSkillCode, "Thunderbolt MAX", SkillCategory.Cleric, SkillSourceType.PcSkill)
        ], new Dictionary<int, NpcDisplayEntry>());

        var snapshotAfterResources = scene.CreateSnapshot();

        Assert.True(snapshotAfterResources.Combatants.TryGetValue(playerId, out var afterResources));
        Assert.Equal(CharacterClass.Cleric, afterResources.CharacterClass);
        Assert.Equal(813_802, afterResources.DamageAmount);
    }

    [Fact]
    public void Ignores_Derived_Regeneration_ClassEvidence_And_Uses_Direct_Damage_Evidence()
    {
        CombatResourceTestFixture.SetResources(
        [
            new SkillDisplayEntry(13352450, "Heart Gore", SkillCategory.Assassin, SkillSourceType.PcSkill),
            new SkillDisplayEntry(16190040, "Enhance: Spirit's Benediction", SkillCategory.Elementalist, SkillSourceType.PcSkill)
        ], new Dictionary<int, NpcDisplayEntry>());

        using var scene = new SceneTestHarness();
        const int assassinId = 9942;
        const int playerId = 10190;
        const int targetId = 12225;

        scene.AppendNickname(playerId, "Elementalist");
        var damageWithRegeneration = new CombatWireObservation
        {
            SkillCode = 13352450,
            Damage = 12000,
            HitCount = 1,
            AttemptCount = 1,
            RegenerationAmount = 781
        };
        scene.AppendCombatWireObservation(assassinId, playerId, in damageWithRegeneration, 1_000);

        var elementalistDamage = new CombatWireObservation
        {
            SkillCode = 16190040,
            Damage = 1,
            HitCount = 1,
            AttemptCount = 1
        };
        scene.AppendCombatWireObservation(playerId, targetId, in elementalistDamage, 1_040);

        var snapshot = scene.CreateSnapshot();

        Assert.True(snapshot.Combatants.TryGetValue(playerId, out var combatant));
        Assert.Equal(CharacterClass.Elementalist, combatant.CharacterClass);
    }

    // A source's class evidence follows who the source is at each snapshot, on one adapter: none while it is someone's summon
    // (its hits are the owner's, who gets no class from them), its own again once the link is cleared, none as an NPC.
    [Fact]
    public void ClassEvidence_Follows_The_Sources_Identity_At_Each_Snapshot()
    {
        CombatResourceTestFixture.SetResources(
        [
            new SkillDisplayEntry(13352450, "Heart Gore", SkillCategory.Assassin, SkillSourceType.PcSkill)
        ], new Dictionary<int, NpcDisplayEntry>());

        var entities = new EntityStore();
        var combat = new CombatStore();
        var mechanics = new MechanicStore();
        var resources = new ResourceStore();
        var adapter = new SceneCombatSnapshotAdapter(entities, new EntityVitalStore(), combat, mechanics, resources, new SceneBoundaryStore());
        const int ownerId = 501;
        const int sourceId = 502;
        const int targetId = 9001;
        combat.ApplyResolvedCombat(mechanics, resources, sourceId, targetId, new CombatWireObservation { SkillCode = 13352450, Damage = 100, HitCount = 1, AttemptCount = 1 }, 1_000);
        Assert.Equal(CharacterClass.Assassin, adapter.CreateSnapshot().Combatants[sourceId].CharacterClass);

        entities.ApplySummon(ownerId, sourceId);
        combat.ApplyResolvedCombat(mechanics, resources, sourceId, targetId, new CombatWireObservation { SkillCode = 13352450, Damage = 100, HitCount = 1, AttemptCount = 1 }, 2_000);
        var linked = adapter.CreateSnapshot();
        Assert.False(linked.Combatants.ContainsKey(sourceId));
        Assert.Equal(200, linked.Combatants[ownerId].DamageAmount);
        Assert.Null(linked.Combatants[ownerId].CharacterClass);

        entities.ClearSummon(sourceId);
        Assert.Equal(CharacterClass.Assassin, adapter.CreateSnapshot().Combatants[sourceId].CharacterClass);

        entities.ApplyNpcCode(sourceId, 2310108);
        Assert.Null(adapter.CreateSnapshot().Combatants[sourceId].CharacterClass);
    }

    // A source linked as a summon, then given NPC kind Unknown by a later kind state (the owner stays), and listed itself
    // through its own summon's hits: its earlier PC-skill evidence gives it no class. Only the resolve and summon tests in
    // ResolveCharacterClass decide this; the NPC check above them doesn't.
    [Fact]
    public void ClassEvidence_Of_A_Source_Resolved_Away_Stays_Off_When_It_Is_Listed()
    {
        CombatResourceTestFixture.SetResources(
        [
            new SkillDisplayEntry(13352450, "Heart Gore", SkillCategory.Assassin, SkillSourceType.PcSkill)
        ], new Dictionary<int, NpcDisplayEntry>());
        var entities = new EntityStore();
        var combat = new CombatStore();
        var mechanics = new MechanicStore();
        var resources = new ResourceStore();
        var adapter = new SceneCombatSnapshotAdapter(entities, new EntityVitalStore(), combat, mechanics, resources, new SceneBoundaryStore());
        combat.ApplyResolvedCombat(mechanics, resources, 502, 9001, new CombatWireObservation { SkillCode = 13352450, Damage = 100, HitCount = 1, AttemptCount = 1 }, 1_000);
        Assert.Equal(CharacterClass.Assassin, adapter.CreateSnapshot().Combatants[502].CharacterClass);

        entities.ApplySummon(501, 502);
        entities.ApplyNpcKind(502, NpcKind.Unknown);
        entities.ApplySummon(502, 503);
        combat.ApplyResolvedCombat(mechanics, resources, 503, 9001, new CombatWireObservation { SkillCode = 1000, Damage = 50, HitCount = 1, AttemptCount = 1 }, 2_000);
        var snapshot = adapter.CreateSnapshot();
        Assert.True(snapshot.Combatants.ContainsKey(502));
        Assert.Null(snapshot.Combatants[502].CharacterClass);
    }

    // A combat store cleared and refilled past its old count and revision under the same adapter: the evidence starts over
    // with the new journal and keeps nothing of the old one.
    [Fact]
    public void ClassEvidence_Starts_Over_When_The_Combat_Store_Is_Cleared()
    {
        CombatResourceTestFixture.SetResources(
        [
            new SkillDisplayEntry(13352450, "Heart Gore", SkillCategory.Assassin, SkillSourceType.PcSkill)
        ], new Dictionary<int, NpcDisplayEntry>());
        var entities = new EntityStore();
        var combat = new CombatStore();
        var mechanics = new MechanicStore();
        var resources = new ResourceStore();
        var adapter = new SceneCombatSnapshotAdapter(entities, new EntityVitalStore(), combat, mechanics, resources, new SceneBoundaryStore());
        combat.ApplyResolvedCombat(mechanics, resources, 502, 9001, new CombatWireObservation { SkillCode = 13352450, Damage = 100, HitCount = 1, AttemptCount = 1 }, 1_000);
        Assert.Equal(CharacterClass.Assassin, adapter.CreateSnapshot().Combatants[502].CharacterClass);

        combat.Clear();
        // An identity change in between, which no longer rescans the evidence by itself.
        entities.ApplyPlayerIdentity(601);
        for (var i = 0; i < 3; i++)
            combat.ApplyResolvedCombat(mechanics, resources, 502, 9001, new CombatWireObservation { SkillCode = 1000, Damage = 50, HitCount = 1, AttemptCount = 1 }, 2_000 + i);
        Assert.Null(adapter.CreateSnapshot().Combatants[502].CharacterClass);
    }
}
