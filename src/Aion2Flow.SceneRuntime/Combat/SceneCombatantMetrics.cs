using Cloris.Aion2Flow.SceneRuntime.Model;

namespace Cloris.Aion2Flow.SceneRuntime.Combat;

public readonly record struct SceneCombatantMetrics
{
    internal SceneCombatantMetrics(CharacterClass? characterClass, bool isVisiblePlayerCombatant, double damagePerSecond, double healingPerSecond, long damageAmount, long healingAmount, long periodicHealingAmount, long drainDamageAmount, long drainHealingAmount, long regenerationHealingAmount, long shieldAmount, int shieldTimes, long shieldAbsorbedAmount, int shieldAbsorbedTimes, double damageContribution, double effectiveHealingPerSecond, long effectiveHealingAmount, long damageTakenAmount, long unhealedHealthSeconds, double aggroShare, double staggerShare)
    {
        CharacterClass = characterClass;
        IsVisiblePlayerCombatant = isVisiblePlayerCombatant;
        DamagePerSecond = damagePerSecond;
        HealingPerSecond = healingPerSecond;
        EffectiveHealingPerSecond = effectiveHealingPerSecond;
        DamageAmount = damageAmount;
        HealingAmount = healingAmount;
        EffectiveHealingAmount = effectiveHealingAmount;
        DamageTakenAmount = damageTakenAmount;
        UnhealedHealthSeconds = unhealedHealthSeconds;
        AggroShare = aggroShare;
        StaggerShare = staggerShare;
        PeriodicHealingAmount = periodicHealingAmount;
        DrainDamageAmount = drainDamageAmount;
        DrainHealingAmount = drainHealingAmount;
        RegenerationHealingAmount = regenerationHealingAmount;
        ShieldAmount = shieldAmount;
        ShieldTimes = shieldTimes;
        ShieldAbsorbedAmount = shieldAbsorbedAmount;
        ShieldAbsorbedTimes = shieldAbsorbedTimes;
        DamageContribution = damageContribution;
    }

    public CharacterClass? CharacterClass { get; init; }

    public bool IsVisiblePlayerCombatant { get; init; }

    public double DamagePerSecond { get; init; }

    public double HealingPerSecond { get; init; }

    public double EffectiveHealingPerSecond { get; init; }

    public long DamageAmount { get; init; }

    public long HealingAmount { get; init; }

    public long EffectiveHealingAmount { get; init; }

    public long DamageTakenAmount { get; init; }

    // HP missing below max, summed over every second of the encounter.
    public long UnhealedHealthSeconds { get; init; }

    // Share of the casts enemies aimed at players that went to this one.
    public double AggroShare { get; init; }

    // Share of the stagger gauge points that went to this one (bosses' gauges, credited by timing).
    public double StaggerShare { get; init; }

    public long PeriodicHealingAmount { get; init; }

    public long DrainDamageAmount { get; init; }

    public long DrainHealingAmount { get; init; }

    public long RegenerationHealingAmount { get; init; }

    public long ShieldAmount { get; init; }

    public int ShieldTimes { get; init; }

    public long ShieldAbsorbedAmount { get; init; }

    public int ShieldAbsorbedTimes { get; init; }

    public double DamageContribution { get; init; }
}

internal struct SceneCombatantMetricsAccumulator
{
    public CharacterClass? CharacterClass;
    public bool IsVisiblePlayerCombatant;
    public double DamagePerSecond;
    public double HealingPerSecond;
    public double EffectiveHealingPerSecond;
    public long DamageAmount;
    public long HealingAmount;
    public long EffectiveHealingAmount;
    public long DamageTakenAmount;
    public long UnhealedHealthSeconds;
    public double AggroShare;
    public double StaggerShare;
    public long PeriodicHealingAmount;
    public long DrainDamageAmount;
    public long DrainHealingAmount;
    public long RegenerationHealingAmount;
    public long ShieldAmount;
    public int ShieldTimes;
    public long ShieldAbsorbedAmount;
    public int ShieldAbsorbedTimes;
    public double DamageContribution;

    public void Reset()
    {
        this = default;
    }

    public void ApplyCombatTotals(long damageAmount, long healingAmount, long effectiveHealingAmount, long periodicHealingAmount, long drainDamageAmount, long drainHealingAmount, long regenerationHealingAmount, long shieldAmount, int shieldTimes, long shieldAbsorbedAmount, int shieldAbsorbedTimes)
    {
        DamageAmount += damageAmount;
        HealingAmount += healingAmount;
        EffectiveHealingAmount += effectiveHealingAmount;
        PeriodicHealingAmount += periodicHealingAmount;
        DrainDamageAmount += drainDamageAmount;
        DrainHealingAmount += drainHealingAmount;
        RegenerationHealingAmount += regenerationHealingAmount;
        ShieldAmount += shieldAmount;
        ShieldTimes += shieldTimes;
        ShieldAbsorbedAmount += shieldAbsorbedAmount;
        ShieldAbsorbedTimes += shieldAbsorbedTimes;
    }

    public readonly SceneCombatantMetrics ToSnapshot()
    {
        return new SceneCombatantMetrics(CharacterClass, IsVisiblePlayerCombatant, DamagePerSecond, HealingPerSecond, DamageAmount, HealingAmount, PeriodicHealingAmount, DrainDamageAmount, DrainHealingAmount, RegenerationHealingAmount, ShieldAmount, ShieldTimes, ShieldAbsorbedAmount, ShieldAbsorbedTimes, DamageContribution, EffectiveHealingPerSecond, EffectiveHealingAmount, DamageTakenAmount, UnhealedHealthSeconds, AggroShare, StaggerShare);
    }
}
