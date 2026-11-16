namespace Cloris.Aion2Flow.Services.Settings;

// What sets a workflow off (Settings → Workflows).
public enum WorkflowTrigger
{
    // A boss pull begins while none was on (a training dummy's doesn't count).
    BossFightStarted,
    // The last pull that was on is over: a kill, a reset or wipe, the boss left alone, or the map left.
    BossFightEnded,
    // Arriving on a dungeon's map from another map (WorkflowTriggerTracker.IsDungeon).
    EnteredDungeon
}

// A workflow (Settings → Workflows): when its trigger happens, its scene is applied. One that's off is kept but does
// nothing.
public sealed class SceneWorkflow
{
    public WorkflowTrigger Trigger { get; set; }

    // The scene's id (FrameScene.Id); 0, or a deleted scene's: none.
    public int SceneId { get; set; }

    public bool Enabled { get; set; } = true;

    public SceneWorkflow Clone() => new() { Trigger = Trigger, SceneId = SceneId, Enabled = Enabled };
}
