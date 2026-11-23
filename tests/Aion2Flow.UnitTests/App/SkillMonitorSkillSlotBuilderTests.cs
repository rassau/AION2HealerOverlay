using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

public sealed class SkillMonitorSkillSlotBuilderTests
{
    [Fact]
    public void Build_MergesLocalBuffAndActiveCooldownByRowBaseSkillId()
    {
        var auras = new[]
        {
            new SkillMonitorAuraCandidate(7, 13_050_000, 10_000, 8_000),
            new SkillMonitorAuraCandidate(7, 13_050_000, 10_000, 9_000),
            new SkillMonitorAuraCandidate(8, 13_050_000, 10_000, 15_000),
        };
        var cooldowns = new[]
        {
            new PacketCooldownState(13_050_000, 13_050_040, 7, 12_000, 2_000, PacketCooldownTransition.Observed, null, 0x0238),
            new PacketCooldownState(13_130_000, 13_130_040, 7, 0, 2_000, PacketCooldownTransition.Ready, null, 0x4738),
        };

        var builder = new SkillMonitorSkillSlotBuilder();
        var slots = builder.Build(auras, 7, cooldowns, static _ => true, 3_000);

        Assert.Equal(1, slots.Length);
        var slot = slots[0];
        Assert.Equal(13_050_000, slot.RowBaseSkillId);
        Assert.Equal(6_000, slot.BuffTimer!.Value.RemainingMilliseconds);
        Assert.Equal(10_000, slot.BuffTimer!.Value.ReferenceMilliseconds);
        Assert.Equal(11_000, slot.CooldownTimer!.Value.RemainingMilliseconds);
        Assert.Equal(12_000, slot.CooldownTimer!.Value.ReferenceMilliseconds);
    }

    [Fact]
    public void Build_KeepsIndefiniteLocalBuffAndExcludesOtherTargets()
    {
        var auras = new[]
        {
            new SkillMonitorAuraCandidate(7, 13_050_000, 10_000, 8_000),
            new SkillMonitorAuraCandidate(7, 13_050_000, ushort.MaxValue, null),
            new SkillMonitorAuraCandidate(8, 13_130_000, ushort.MaxValue, null),
        };

        var builder = new SkillMonitorSkillSlotBuilder();
        var slots = builder.Build(auras, 7, [], static _ => true, 3_000);

        Assert.Equal(1, slots.Length);
        var slot = slots[0];
        Assert.Equal(13_050_000, slot.RowBaseSkillId);
        Assert.True(slot.BuffTimer!.Value.IsIndefinite);
        Assert.Null(slot.CooldownTimer);
    }

    [Fact]
    public void Build_PreservesCooldownCycleDurationAcross4738Updates()
    {
        var cooldowns = new[]
        {
            new PacketCooldownState(
                13_050_000,
                13_050_040,
                0,
                3_000,
                7_000,
                PacketCooldownTransition.ResetOrReduced,
                null,
                0x4738,
                10_000),
        };

        var builder = new SkillMonitorSkillSlotBuilder();
        var slots = builder.Build([], 0, cooldowns, static _ => true, 7_000);
        Assert.Equal(1, slots.Length);
        var slot = slots[0];

        Assert.Equal(3_000, slot.CooldownTimer!.Value.RemainingMilliseconds);
        Assert.Equal(10_000, slot.CooldownTimer!.Value.ReferenceMilliseconds);
        Assert.Equal(0.3d, slot.CooldownTimer!.Value.ProgressValue, 3);
    }

    [Fact]
    public void Build_ExcludesReadyChargeWithoutActiveCooldown()
    {
        var cooldowns = new[]
        {
            new PacketCooldownState(
                13_050_000,
                13_050_040,
                0,
                0,
                7_000,
                PacketCooldownTransition.Ready,
                3,
                0x2238),
        };

        var builder = new SkillMonitorSkillSlotBuilder();
        var slots = builder.Build([], 0, cooldowns, static _ => true, 7_000);

        Assert.Equal(0, slots.Length);
    }

    [Fact]
    public void Build_PreservesReadyChargeCountWhenBuffIsActive()
    {
        var auras = new[]
        {
            new SkillMonitorAuraCandidate(7, 13_050_000, 10_000, 15_000)
        };
        var cooldowns = new[]
        {
            new PacketCooldownState(
                13_050_000,
                13_050_040,
                0,
                0,
                7_000,
                PacketCooldownTransition.Ready,
                3,
                0x2238),
        };

        var builder = new SkillMonitorSkillSlotBuilder();
        var slots = builder.Build(auras, 7, cooldowns, static _ => true, 7_000);
        Assert.Equal(1, slots.Length);
        var slot = slots[0];

        Assert.NotNull(slot.BuffTimer);
        Assert.Null(slot.CooldownTimer);
        Assert.Equal(3, slot.AvailableCount);
    }

    [Fact]
    public void Build_PutsTargetDebuffBesideBuffAndCooldownInOneSlot()
    {
        var auras = new[] { new SkillMonitorAuraCandidate(7, 17_400_000, 10_000, 9_000) };
        var debuffs = new[]
        {
            new SkillMonitorAuraCandidate(500, 17_400_000, 20_000, 15_000),
            new SkillMonitorAuraCandidate(500, 17_080_000, 10_000, null),
        };
        var cooldowns = new[]
        {
            new PacketCooldownState(17_400_000, 17_400_010, 7, 12_000, 2_000, PacketCooldownTransition.Observed, null, 0x0238),
        };

        var slots = new SkillMonitorSkillSlotBuilder().Build(auras, 7, cooldowns, static _ => true, 3_000, debuffs).ToArray();

        Assert.Equal([17_080_000, 17_400_000], slots.Select(static slot => slot.RowBaseSkillId));
        Assert.True(slots[0].DebuffTimer!.Value.IsIndefinite);
        Assert.Null(slots[0].BuffTimer);
        Assert.Equal((6_000L, 12_000L, 11_000L),
            (slots[1].BuffTimer!.Value.RemainingMilliseconds, slots[1].DebuffTimer!.Value.RemainingMilliseconds, slots[1].CooldownTimer!.Value.RemainingMilliseconds));
    }

    [Fact]
    public void Build_KeepsTheLongestDebuffPerSkill()
    {
        var debuffs = new[]
        {
            new SkillMonitorAuraCandidate(500, 17_070_000, 10_000, 8_000),
            new SkillMonitorAuraCandidate(500, 17_070_000, 10_000, 12_000),
        };
        var buff = new[] { new SkillMonitorAuraCandidate(7, 17_070_000, 10_000, 9_000) };
        var builder = new SkillMonitorSkillSlotBuilder();

        Assert.Equal(9_000, Assert.Single(builder.Build([], 7, [], static _ => true, 3_000, debuffs).ToArray()).DebuffTimer!.Value.RemainingMilliseconds);
        // Without debuffs (as before them), none.
        Assert.Null(Assert.Single(builder.Build(buff, 7, [], static _ => true, 3_000).ToArray()).DebuffTimer);
    }

    // The buffs on your target (from anyone, see SkillMonitorViewModel) show in their skill's buff timer only where there's
    // none on you: yours keeps it, even with less time left; beside the skill's debuff.
    [Fact]
    public void Build_KeepsYourBuffOverOneOnYourTarget()
    {
        var yours = new[] { new SkillMonitorAuraCandidate(7, 17_410_000, 30_000, 9_000) };
        var onTarget = new[]
        {
            new SkillMonitorAuraCandidate(300, 17_410_000, 60_000, 15_000),
            new SkillMonitorAuraCandidate(300, 17_420_000, 20_000, 5_000),
        };
        var debuffs = new[] { new SkillMonitorAuraCandidate(300, 17_420_000, 10_000, 8_000) };
        var builder = new SkillMonitorSkillSlotBuilder();

        Assert.Equal([(17_410_000, 6_000L, (long?)null), (17_420_000, 2_000L, 5_000L)],
            builder.Build(yours, 7, [], static _ => true, 3_000, debuffs, onTarget).ToArray()
                .Select(static slot => (slot.RowBaseSkillId, slot.BuffTimer!.Value.RemainingMilliseconds, slot.DebuffTimer?.RemainingMilliseconds)));
        // None on you: your target's.
        Assert.Equal(12_000, builder.Build([], 7, [], static _ => true, 3_000, targetBuffs: onTarget).ToArray()[0].BuffTimer!.Value.RemainingMilliseconds);
    }

    [Fact]
    public void Build_ExcludesCooldownRejectedBySelection()
    {
        var cooldowns = new[]
        {
            new PacketCooldownState(
                13_050_000,
                13_050_040,
                0,
                5_000,
                7_000,
                PacketCooldownTransition.Observed,
                null,
                0x0238),
        };

        var builder = new SkillMonitorSkillSlotBuilder();
        var slots = builder.Build([], 0, cooldowns, static _ => false, 7_000);

        Assert.Equal(0, slots.Length);
    }
}
