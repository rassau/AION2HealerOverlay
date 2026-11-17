using Cloris.Aion2Flow.Collections;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using CommunityToolkit.Mvvm.Input;

namespace Cloris.Aion2Flow.ViewModels;

// One boss fight (pull): the current one under the boss bar, or an earlier one in the list below the table. Its rows
// are the main table's, counted over the fight.
public sealed partial class BossFightViewModel(UiFrameBatchService frameBatchService, CombatantColumnLayoutViewModel columns, int key, bool isExpanded, Action<bool>? expandedChanged = null) : FrameBatchedObservableObject(frameBatchService)
{
    // The fight's index in its scene.
    public int Key { get; } = key;

    public KeyedObservableCollection<int, CombatantRowViewModel> Rows { get; } = new(x => x.Id);

    // The Sum row under the rows (RefreshSum): no player, so no bar, no class and no details.
    public CombatantRowViewModel Sum { get; } = new(frameBatchService, columns, 0, null, 0, 0, 0, 0, 0, 0);

    // From two rows on: one row is its own sum.
    public bool HasSum
    {
        get;
        private set
        {
            if (SetFrameProperty(ref field, value))
                QueueFramePropertyChanged(nameof(IsSumVisible));
        }
    }

    // The meter's "Add boss sum row" (SettingsFlyoutViewModel.ShowBossSumRow): the row is worked out anyway, so it shows at
    // once when that's turned on.
    public bool ShowSum
    {
        get;
        set
        {
            if (SetFrameProperty(ref field, value))
                QueueFramePropertyChanged(nameof(IsSumVisible));
        }
    }

    public bool IsSumVisible => ShowSum && HasSum && IsExpanded;

    // What the main view model last filled a fight of the list from (MainViewModel.RefreshBossFights).
    internal MainViewModel.BossFightFill? FilledFrom { get; set; }

    public bool IsVisible
    {
        get;
        set => SetFrameProperty(ref field, value);
    }

    public bool IsExpanded
    {
        get;
        set
        {
            if (SetFrameProperty(ref field, value))
            {
                QueueFramePropertyChanged(nameof(IsDpsCheckVisible));
                QueueFramePropertyChanged(nameof(IsSumVisible));
            }
        }
    } = isExpanded;

    // "Boss #1" or "Boss #1 failed attempt 2".
    public string Title
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = string.Empty;

    // "32% HP left · staggered 2x": HP left for a fight the boss survived, then how often it was staggered.
    public string DetailText
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = string.Empty;

    // The fight's first to last blow.
    public TimeSpan Duration
    {
        get;
        set => SetFrameProperty(ref field, value);
    }

    // The group's damage on the boss per second.
    public double DamagePerSecond
    {
        get;
        set => SetFrameProperty(ref field, value);
    }

    public bool HasDpsCheck
    {
        get;
        set
        {
            if (SetFrameProperty(ref field, value))
                QueueFramePropertyChanged(nameof(IsDpsCheckVisible));
        }
    }

    // The section under the bar keeps its DPS check when folded; a folded fight in the list stays one line.
    public bool IsDpsCheckVisible => HasDpsCheck && (Key < 0 || IsExpanded);

    // "416.7k/s required", or that the time limit has passed.
    public string DpsCheckText
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = string.Empty;

    // "(316.6k/s missing)" or "(+1.4k/s)".
    public string DpsGapText
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = string.Empty;

    // The bottom bar's short form beside the group's DPS: "(−317k)", "(+1.4k)", "(−∞)" once the time is up.
    public string DpsGapShortText
    {
        get;
        set => SetFrameProperty(ref field, value);
    } = string.Empty;

    public bool IsBehind
    {
        get;
        set => SetFrameProperty(ref field, value);
    }

    // The rows added up, once they're filled (MainViewModel.FillBossFight); the shares become the group's. The recovery
    // time is the group's as the bottom bar works it out (MainViewModel.RefreshFooterTotals): the UHS over the damage
    // taken of the rows with UHS, as a stranger's max HP never comes.
    internal void RefreshSum()
    {
        HasSum = Rows.Count >= 2;
        double dps = 0, damage = 0, hps = 0, healing = 0, taken = 0, unhealed = 0, takenWithUhs = 0, aggro = 0, stagger = 0, boss = 0;
        foreach (var row in Rows)
        {
            dps += row.DamagePerSecond;
            damage += row.Damage;
            hps += row.HealingPerSecond;
            healing += row.Healing;
            taken += row.DamageTaken;
            unhealed += row.UnhealedHealthSeconds;
            if (row.UnhealedHealthSeconds > 0)
                takenWithUhs += row.DamageTaken;
            aggro += row.AggroShare;
            stagger += row.StaggerShare;
            boss += row.BossShareRatio;
        }

        Sum.DamagePerSecond = dps;
        Sum.Damage = damage;
        Sum.HealingPerSecond = hps;
        Sum.Healing = healing;
        Sum.DamageTaken = taken;
        Sum.UnhealedHealthSeconds = unhealed;
        Sum.RecoveryTime = FightGraph.ShownSeconds(FightTimeline.RecoveryTime(unhealed, takenWithUhs));
        Sum.AggroShare = aggro;
        Sum.StaggerShare = stagger;
        Sum.UpdateBossShare(boss);
    }

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
        expandedChanged?.Invoke(IsExpanded);
    }
}
