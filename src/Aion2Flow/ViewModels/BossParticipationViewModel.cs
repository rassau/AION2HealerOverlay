using System.Globalization;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cloris.Aion2Flow.ViewModels;

public enum BossParticipationState
{
    None,
    Short,
    OnCourse,
    Reached
}

// The group frame's loot bag: the player's share of a boss against the share its loot needs (Settings → Group frame, and
// General). Green once the share is reached; yellow while it isn't yet, but the player's share of the damage dealt so far
// would reach it at the kill (what they end with if everyone keeps their pace); red while it wouldn't. It judges the
// boss of the newest pulls (training dummies aside): of the pulls that overlap in time, a boss and its adds, the one with
// the most HP. While that one is on, as it goes; once killed, the final result until the next pull or map; after a wipe,
// nothing.
public sealed partial class BossParticipationViewModel(SettingsFlyoutViewModel settings) : ObservableObject
{
    private IReadOnlyList<SceneBossFight> _fights = [], _liveFights = [];
    private int _me, _liveMe;
    private bool _demo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    [NotifyPropertyChangedFor(nameof(IsShort))]
    [NotifyPropertyChangedFor(nameof(IsOnCourse))]
    [NotifyPropertyChangedFor(nameof(IsReached))]
    public partial BossParticipationState State { get; private set; }

    // How far the player is, on hover.
    [ObservableProperty]
    public partial string? Tip { get; private set; }

    public bool IsShown => State != BossParticipationState.None;

    public bool IsShort => State == BossParticipationState.Short;

    public bool IsOnCourse => State == BossParticipationState.OnCourse;

    public bool IsReached => State == BossParticipationState.Reached;

    // Each live frame: the scene's pulls, and who the player is (0 while unknown). Kept while the tutorial shows its fight.
    public void Update(IReadOnlyList<SceneBossFight> fights, int me)
    {
        _liveFights = fights;
        _liveMe = me;
        if (!_demo)
            Show(fights, me);
    }

    // The tutorial's fight instead of the live ones, until EndDemo (then the live ones again, as they are by then).
    public void ShowDemo(IReadOnlyList<SceneBossFight> fights, int me)
    {
        _demo = true;
        Show(fights, me);
    }

    public void EndDemo()
    {
        _demo = false;
        Show(_liveFights, _liveMe);
    }

    private void Show(IReadOnlyList<SceneBossFight> fights, int me)
    {
        _fights = fights;
        _me = me;
        Refresh();
    }

    // Also when the warning or the share it needs changes.
    public void Refresh()
    {
        var needed = settings.BossParticipationPercent / 100;
        var (state, share, expected, final) = settings.BossParticipationWarning ? Assess(_fights, _me, needed) : default;
        State = state;
        Tip = state == BossParticipationState.None
            ? null
            : string.Format(CultureInfo.CurrentCulture, settings.Localization[state switch
            {
                BossParticipationState.Reached => "BossParticipation_Reached",
                BossParticipationState.OnCourse => "BossParticipation_OnCourse",
                _ => final ? "BossParticipation_Missed" : "BossParticipation_Short"
            }], Percent(share), Percent(needed), Percent(expected));
    }

    // share: the player's damage on the boss over its HP (over the damage dealt, where that's more: a boss that healed);
    // expected: their share of the damage dealt on it so far. Final: a kill's result. A boss whose HP isn't known can't
    // be reached, only be on course, and leaves no result.
    internal static (BossParticipationState State, double Share, double Expected, bool Final) Assess(IReadOnlyList<SceneBossFight> fights, int me, double needed)
    {
        if (me <= 0 || Boss(fights) is not { } boss)
            return default;

        var (share, expected) = Shares(boss, me);
        var hpKnown = boss.BossMaxHp > 0;
        return boss.Outcome switch
        {
            BossFightOutcome.Ongoing => (hpKnown && share >= needed ? BossParticipationState.Reached
                : expected >= needed ? BossParticipationState.OnCourse : BossParticipationState.Short, share, expected, false),
            BossFightOutcome.Killed when hpKnown => (share >= needed ? BossParticipationState.Reached : BossParticipationState.Short, share, expected, true),
            _ => default
        };
    }

    // The newest pulls that overlap in time (the newest one, and back from it each one still on or ending after the group's
    // start), and of them the one with the most HP: the boss rather than an add.
    private static SceneBossFight? Boss(IReadOnlyList<SceneBossFight> fights)
    {
        SceneBossFight? boss = null;
        var groupStart = long.MaxValue;
        for (var i = fights.Count - 1; i >= 0; i--)
        {
            var fight = fights[i];
            if (fight.IsTrainingDummy)
                continue;
            if (boss is not null && fight.Outcome != BossFightOutcome.Ongoing && fight.Snapshot.EncounterEndTime < groupStart)
                break;

            groupStart = Math.Min(groupStart, fight.Snapshot.EncounterStartTime);
            if (boss is null || fight.BossMaxHp > boss.BossMaxHp)
                boss = fight;
        }

        return boss;
    }

    private static (double Share, double Expected) Shares(SceneBossFight fight, int me)
    {
        long mine = 0, all = 0;
        foreach (var (id, metrics) in fight.Snapshot.Combatants)
        {
            all += metrics.DamageAmount;
            if (id == me)
                mine = metrics.DamageAmount;
        }

        var hp = Math.Max(fight.BossMaxHp, all);
        return (hp > 0 ? (double)mine / hp : 0, all > 0 ? (double)mine / all : 0);
    }

    // Cut off, not rounded: 3.46 % reads 3.4 %, never the 3.5 % it falls short of.
    internal static string Percent(double fraction) => (Math.Floor(fraction * 1000 + 1e-6) / 10).ToString("0.0", CultureInfo.CurrentCulture) + " %";
}
