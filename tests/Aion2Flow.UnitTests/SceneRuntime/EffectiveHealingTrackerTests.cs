using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class EffectiveHealingTrackerTests
{
    private const int Healer = 1;
    private const int Enemy = 900;

    private readonly CombatStore _combat = new();
    private readonly EffectiveHealingTracker _tracker = new();
    private long _ordinal;

    [Fact]
    public void Resolve_CreditsOnlyHealingThatRestoredMissingHp()
    {
        // 2: the heal restores missing HP in full.
        Vital(2, 1_000, 0);
        Heal(2, 300, 10);
        Vital(2, 1_300, 20);

        // 3: HP caps at max, so 200 of the 500 is overhealing.
        Vital(3, 1_000, 0);
        var cappedHeal = Heal(3, 500, 10);
        Vital(3, 1_300, 20);

        // 4: the target is already full, so no HP update ever follows the heal.
        Vital(4, 1_000, 0);
        Heal(4, 400, 10);

        // 5: a hit lands between the heal and the next update; the damage taken is added back.
        Vital(5, 1_000, 0);
        Heal(5, 400, 10);
        Hit(5, 300, 15);
        Vital(5, 1_100, 20);

        // 6: a target that never loses HP never gets an update, so its heals restored nothing.
        Heal(6, 250, 10);

        // 8: the first update seen follows the heal; with no earlier HP to compare, the heal counts in full.
        Heal(8, 250, 10);
        Vital(8, 1_250, 20);

        // 7: the heal materializes after its HP update but precedes it in journal order.
        Vital(7, 1_000, 0);
        var healOrdinal = ++_ordinal;
        _tracker.ObserveVital(7, 1_200, ++_ordinal, 20);
        Apply(Healer, 7, CombatMetricKind.Healing, 300, 10, healOrdinal);

        _tracker.Resolve(_combat);

        Assert.Equal(300, EffectiveHealing(2));
        Assert.Equal(300, EffectiveHealing(3));
        Assert.Equal(300, _combat.GetEvent(cappedHeal).EffectiveHealing);
        Assert.Equal(0, EffectiveHealing(4));
        Assert.Equal(400, EffectiveHealing(5));
        Assert.Equal(0, EffectiveHealing(6));
        Assert.Equal(200, EffectiveHealing(7));
        Assert.Equal(250, EffectiveHealing(8));

        // Once the update window has passed, unanswered heals are settled at zero.
        Vital(99, 1, 5_000);
        _tracker.Resolve(_combat);

        Assert.Equal(0, EffectiveHealing(4));
        Assert.Equal(0, EffectiveHealing(6));
    }

    private void Vital(int target, long hp, long observedAt) => _tracker.ObserveVital(target, hp, ++_ordinal, observedAt);

    private int Heal(int target, long amount, long observedAt) => Apply(Healer, target, CombatMetricKind.Healing, amount, observedAt, ++_ordinal);

    private void Hit(int target, long amount, long observedAt) => Apply(Enemy, target, CombatMetricKind.Damage, amount, observedAt, ++_ordinal);

    private int Apply(int source, int target, CombatMetricKind metric, long amount, long observedAt, long ordinal)
    {
        var contribution = new CombatContribution(metric, CombatDeliveryKind.Direct, amount, default);
        var eventIndex = _combat.ApplyCombat(source, target, default, in contribution, observedAt, ordinal);
        _tracker.ObserveCombat(target, in contribution, ordinal, observedAt, eventIndex);
        return eventIndex;
    }

    private long EffectiveHealing(int target) =>
        _combat.TryGetPair(Healer, target, out var pair) && pair is not null ? pair.TotalEffectiveHealing : -1;
}
