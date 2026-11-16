using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

public sealed class WorkflowTriggerTrackerTests
{
    // A dungeon's map fires once on arrival (not for a teleport within it, nor for a town, a Shugo Festival map or a Legion
    // airship); a boss fight fires when the first pull starts and when the last one that was on is over, also with two
    // bosses at once; a training dummy's pull fires nothing.
    [Fact]
    public void The_Live_Frames_Fire_The_Workflows_Triggers()
    {
        var triggers = new WorkflowTriggerTracker();
        var fired = new List<WorkflowTrigger>();
        void See(uint mapId, params SceneBossFight[] fights) =>
            triggers.Observe(new SceneReadModelFrame { Snapshot = Snapshot(mapId), BossFights = fights }, fired);

        See(0);
        See(1110);
        Assert.Empty(fired);
        See(600073);
        See(600073);
        Assert.Equal([WorkflowTrigger.EnteredDungeon], fired);

        fired.Clear();
        See(600073, Fight(1, BossFightOutcome.Ongoing));
        See(600073, Fight(1, BossFightOutcome.Ongoing), Fight(2, BossFightOutcome.Ongoing));
        See(600073, Fight(1, BossFightOutcome.Killed), Fight(2, BossFightOutcome.Ongoing));
        See(600073, Fight(1, BossFightOutcome.Killed), Fight(2, BossFightOutcome.Reset));
        Assert.Equal([WorkflowTrigger.BossFightStarted, WorkflowTrigger.BossFightEnded], fired);

        fired.Clear();
        See(1110, Fight(1, BossFightOutcome.Ongoing) with { IsTrainingDummy = true });
        See(510026);
        See(901);
        See(910033);
        Assert.Equal([WorkflowTrigger.EnteredDungeon], fired);
        Assert.False(WorkflowTriggerTracker.IsDungeon(200003));

        // Into another dungeon with a pull still on: it ends first, so the dungeon's scene is the one that stays.
        See(910033, Fight(1, BossFightOutcome.Ongoing));
        fired.Clear();
        See(600023);
        Assert.Equal([WorkflowTrigger.BossFightEnded, WorkflowTrigger.EnteredDungeon], fired);
    }

    private static SceneCombatSnapshot Snapshot(uint mapId) =>
        new(Guid.Empty, SceneKind.Standard, 0, 0, mapId, 0, 0, 0, 0, [], null, EncounterSummarySnapshot.Empty, [], []);

    private static SceneBossFight Fight(int number, BossFightOutcome outcome) =>
        new(BossId: 100 + number, BossNumber: number, Attempt: 1, outcome, BossHp: 1, BossMaxHp: 1, SceneCombatSnapshot.Empty);
}
