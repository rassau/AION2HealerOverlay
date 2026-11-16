using System.Collections.ObjectModel;
using Cloris.Aion2Flow.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cloris.Aion2Flow.ViewModels;

// A workflow in the settings (Settings → Workflows): its trigger, the scene it applies, on or off.
public sealed partial class WorkflowViewModel : ObservableObject
{
    private static readonly WorkflowTrigger[] AllTriggers = Enum.GetValues<WorkflowTrigger>();
    private readonly SettingsFlyoutViewModel _settings;
    private bool _loading = true;

    internal WorkflowViewModel(SettingsFlyoutViewModel settings, WorkflowTrigger trigger, SceneViewModel? scene, bool enabled)
    {
        _settings = settings;
        Trigger = Array.IndexOf(AllTriggers, trigger) >= 0 ? trigger : AllTriggers[0];
        TriggerIndex = Array.IndexOf(AllTriggers, Trigger);
        Scene = scene;
        Enabled = enabled;
        _loading = false;
    }

    // The triggers in the order of WorkflowTrigger, named in the language shown (SettingsFlyoutViewModel.WorkflowTriggerOptions).
    public IReadOnlyList<WorkflowTriggerOption> Triggers => _settings.WorkflowTriggerOptions;

    public ObservableCollection<SceneViewModel> Scenes => _settings.Scenes;

    public WorkflowTrigger Trigger { get; private set; }

    // The trigger's box; a box that has nothing chosen for a moment (-1) leaves the trigger as it was.
    [ObservableProperty]
    public partial int TriggerIndex { get; set; }

    // None once its scene was deleted: then it does nothing until another one is chosen.
    [ObservableProperty]
    public partial SceneViewModel? Scene { get; set; }

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    // Its trash button asked once ("Delete?"); the next click deletes it.
    [ObservableProperty]
    public partial bool IsDeleteArmed { get; set; }

    partial void OnTriggerIndexChanged(int value)
    {
        if (value < 0 || value >= AllTriggers.Length)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => TriggerIndex = Array.IndexOf(AllTriggers, Trigger));
            return;
        }

        Trigger = AllTriggers[value];
        Save();
    }

    partial void OnSceneChanged(SceneViewModel? value) => Save();

    partial void OnEnabledChanged(bool value) => Save();

    internal SceneWorkflow ToModel() => new() { Trigger = Trigger, SceneId = Scene?.Id ?? 0, Enabled = Enabled };

    private void Save()
    {
        if (!_loading)
            _settings.SaveWorkflows();
    }
}

// A trigger in a workflow's box, named in the language shown.
public sealed partial class WorkflowTriggerOption(WorkflowTrigger trigger) : ObservableObject
{
    public WorkflowTrigger Trigger { get; } = trigger;

    [ObservableProperty]
    public partial string Label { get; set; } = string.Empty;

    // What counts as a dungeon, for its trigger.
    [ObservableProperty]
    public partial string? Tip { get; set; }
}
