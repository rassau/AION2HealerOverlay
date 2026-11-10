using Cloris.Aion2Flow.Collections;
using CommunityToolkit.Mvvm.Input;

namespace Cloris.Aion2Flow.ViewModels;

// One boss fight (pull): the current one under the boss bar, or an earlier one in the list below the table. Its rows
// are the main table's, counted over the fight.
public sealed partial class BossFightViewModel(UiFrameBatchService frameBatchService, int key, bool isExpanded, Action<bool>? expandedChanged = null) : FrameBatchedObservableObject(frameBatchService)
{
    // The fight's index in its scene.
    public int Key { get; } = key;

    public KeyedObservableCollection<int, CombatantRowViewModel> Rows { get; } = new(x => x.Id);

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
                QueueFramePropertyChanged(nameof(IsDpsCheckVisible));
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

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
        expandedChanged?.Invoke(IsExpanded);
    }
}
