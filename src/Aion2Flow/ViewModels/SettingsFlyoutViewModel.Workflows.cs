using System.Collections.ObjectModel;
using Cloris.Aion2Flow.Services.Settings;

namespace Cloris.Aion2Flow.ViewModels;

// Workflows (Settings → Workflows): when something happens in game (WorkflowTriggerTracker), a workflow that's on applies
// its scene.
public sealed partial class SettingsFlyoutViewModel
{
    public ObservableCollection<WorkflowViewModel> Workflows { get; } = [];

    // The triggers in their order, for the workflows' boxes; their names follow the language.
    public IReadOnlyList<WorkflowTriggerOption> WorkflowTriggerOptions { get; } =
        [.. Enum.GetValues<WorkflowTrigger>().Select(static trigger => new WorkflowTriggerOption(trigger))];

    public bool HasWorkflows => Workflows.Count > 0;

    // A new workflow: when a boss fight starts, the first scene. It needs a scene to apply.
    public WorkflowViewModel? AddWorkflow()
    {
        if (Scenes.Count == 0)
            return null;

        var added = new WorkflowViewModel(this, WorkflowTrigger.BossFightStarted, Scenes[0], enabled: true);
        Workflows.Add(added);
        OnPropertyChanged(nameof(HasWorkflows));
        SaveWorkflows();
        return added;
    }

    // A workflow's trash button: the first click asks ("Delete?"), the second deletes it.
    public void DeleteWorkflowOrAsk(WorkflowViewModel workflow)
    {
        if (!workflow.IsDeleteArmed)
        {
            foreach (var other in Workflows)
                other.IsDeleteArmed = false;
            workflow.IsDeleteArmed = true;
            return;
        }

        Workflows.Remove(workflow);
        OnPropertyChanged(nameof(HasWorkflows));
        SaveWorkflows();
    }

    // Each workflow that's on and has the trigger applies its scene, in the order they're listed.
    public void RunWorkflows(WorkflowTrigger trigger)
    {
        if (TutorialRunning)
            return;

        foreach (var workflow in Workflows.ToArray())
        {
            if (workflow.Enabled && workflow.Trigger == trigger && workflow.Scene is { } scene && Scenes.Contains(scene))
                ApplyScene(scene);
        }
    }

    internal void SaveWorkflows() => PersistSettings();

    private void LoadWorkflows(AppSettings persisted)
    {
        foreach (var workflow in persisted.Workflows)
        {
            if (Enum.IsDefined(workflow.Trigger))
                Workflows.Add(new WorkflowViewModel(this, workflow.Trigger, FindScene(workflow.SceneId), workflow.Enabled));
        }
    }

    private void SaveWorkflows(AppSettings settings) => settings.Workflows = [.. Workflows.Select(static workflow => workflow.ToModel())];

    private void RefreshWorkflowTriggerLabels()
    {
        foreach (var option in WorkflowTriggerOptions)
        {
            option.Label = Localization[$"Workflows_Trigger_{option.Trigger}"];
            option.Tip = option.Trigger == WorkflowTrigger.EnteredDungeon ? Localization["Workflows_DungeonTip"] : null;
        }
    }

    // A deleted scene leaves its workflows without one: they do nothing until another is chosen.
    private void ForgetSceneInWorkflows(SceneViewModel scene)
    {
        foreach (var workflow in Workflows)
        {
            if (workflow.Scene == scene)
                workflow.Scene = null;
        }
    }
}
