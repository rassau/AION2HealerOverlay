using Cloris.Aion2Flow.Resources.Catalog;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.Tests.SceneRuntime.Combat;

public sealed class CombatLocalizationSceneTests
{
    [Fact]
    public void CombatResourceRegistry_DisplaySkillName_Tracks_Current_Language_Resources()
    {
        try
        {
            var observation = new CombatWireObservation
            {
                SkillCode = 2011101
            };

            CombatResourceRegistry.LoadSkillMap("ko-KR");
            var koName = ResourceCatalog.Load(ResourceLanguage.Korean).Skills[2011101].Name;

            Assert.Equal(koName, CombatResourceRegistry.DisplaySkillNameFor(observation.SkillCode));

            CombatResourceRegistry.LoadSkillMap("en-US");
            var enName = ResourceCatalog.Load(ResourceLanguage.English).Skills[2011101].Name;

            Assert.Equal(enName, CombatResourceRegistry.DisplaySkillNameFor(observation.SkillCode));
            Assert.NotEqual(koName, enName);
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap("en-US");
        }
    }

    [Theory]
    [InlineData(1227237, "Attack")]
    [InlineData(1227265, "Wraith Surge")]
    public void CombatResourceRegistry_DisplaySkillName_Resolves_Client_SkillDat_Entries(int skillCode, string expectedName)
    {
        try
        {
            CombatResourceRegistry.LoadSkillMap("en-US");

            Assert.Equal(expectedName, CombatResourceRegistry.DisplaySkillNameFor(skillCode));
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap("en-US");
        }
    }

    [Theory]
    [InlineData(1607415, "Attack")]
    [InlineData(1607400, "Attack")]
    public void CombatResourceRegistry_DisplaySkillName_Uses_Exact_Client_SkillDat_Entries(int skillCode, string expectedName)
    {
        try
        {
            CombatResourceRegistry.LoadSkillMap("en-US");

            Assert.Equal(expectedName, CombatResourceRegistry.DisplaySkillNameFor(skillCode));
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap("en-US");
        }
    }

    [Fact]
    public void CreateBattleSnapshot_Keeps_Combat_Totals_Stable_When_Language_Changes()
    {
        try
        {
            CombatResourceRegistry.LoadSkillMap("ko-KR");
            using var scene = new SceneTestHarness();
            const int sourceId = 2007;
            const int targetId = 55783;
            const int skillCode = 11800008;

            scene.AppendNickname(sourceId, "Perigee");
            var firstDamage = new CombatWireObservation
            {
                SkillCode = skillCode,
                Damage = 77669,
                HitCount = 1,
                AttemptCount = 1
            };
            scene.AppendCombatWireObservation(sourceId, targetId, in firstDamage);
            Thread.Sleep(5);
            var secondDamage = new CombatWireObservation
            {
                SkillCode = skillCode,
                Damage = 77669,
                HitCount = 1,
                AttemptCount = 1
            };
            scene.AppendCombatWireObservation(sourceId, targetId, in secondDamage);

            var koSnapshot = scene.CreateSnapshot();
            Assert.True(koSnapshot.Combatants.TryGetValue(sourceId, out var koCombatant));
            var koSkills = scene.CreateSkillBreakdown(koSnapshot, sourceId).Skills;
            Assert.True(koSkills.TryGetBySkillCode(skillCode, out var koSkill));
            var koSkillName = CombatResourceRegistry.DisplaySkillNameFor(koSkill.SkillCode);

            CombatResourceRegistry.LoadSkillMap("en-US");
            var enSnapshot = scene.CreateSnapshot();

            Assert.True(enSnapshot.Combatants.TryGetValue(sourceId, out var enCombatant));
            var enSkills = scene.CreateSkillBreakdown(enSnapshot, sourceId).Skills;
            Assert.True(enSkills.TryGetBySkillCode(skillCode, out var enSkill));

            Assert.Equal(koCombatant.DamageAmount, enCombatant.DamageAmount);
            Assert.Equal(koCombatant.HealingAmount, enCombatant.HealingAmount);
            Assert.Equal(koCombatant.DrainDamageAmount, enCombatant.DrainDamageAmount);
            Assert.Equal(koCombatant.DamageContribution, enCombatant.DamageContribution);

            Assert.Equal(koSkill, enSkill);

            Assert.Equal("살기 파열", koSkillName);
            var enSkillName = CombatResourceRegistry.DisplaySkillNameFor(enSkill.SkillCode);
            Assert.Equal("Murderous Burst", enSkillName);
            Assert.NotEqual(koSkillName, enSkillName);
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap("en-US");
        }
    }
}
