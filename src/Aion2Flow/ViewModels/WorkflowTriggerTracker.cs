using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.Services.Settings;

namespace Cloris.Aion2Flow.ViewModels;

// The live frames as the workflows' triggers (Settings → Workflows), whatever the meter shows meanwhile: a boss fight
// starting while none was on, the last one that was on ending, and arriving on a dungeon's map from another map.
internal sealed class WorkflowTriggerTracker
{
    private uint _mapId;
    private bool _fightOn;

    // Which maps are dungeons. The game data has no map type, but the ids come in ranges: 600000-699999 are the dungeons
    // (Krao Cave to Vakron Sky Island with their hard modes, Abyssal Forge: Ludra, the 61xxxx temples and dens) and
    // 910000-919999 the tiered instances ("Nightmare Altar (Easy)" to "(Hell)"). Towns and fields (1000-1111), the Legion
    // airships (901-905), the Shugo Festival's maps (500001-502001, 510001-510040), arenas and battlefields are not.
    public static bool IsDungeon(uint mapId) => mapId is >= 600_000 and <= 699_999 or >= 910_000 and <= 919_999;

    // A map not known yet (0) changes nothing; a teleport within the same map (a wipe, a portal) fires nothing either. A pull
    // still on as the map is left ends before the arrival, so that the dungeon's scene is the one that stays.
    public void Observe(in SceneReadModelFrame frame, List<WorkflowTrigger> fired)
    {
        var fightOn = false;
        foreach (var fight in frame.BossFights)
            fightOn |= fight is { Outcome: BossFightOutcome.Ongoing, IsTrainingDummy: false };
        if (_fightOn && !fightOn)
            fired.Add(WorkflowTrigger.BossFightEnded);

        var mapId = frame.Snapshot.MapId;
        if (mapId != 0 && mapId != _mapId)
        {
            _mapId = mapId;
            if (IsDungeon(mapId))
                fired.Add(WorkflowTrigger.EnteredDungeon);
        }

        if (fightOn && !_fightOn)
            fired.Add(WorkflowTrigger.BossFightStarted);
        _fightOn = fightOn;
    }
}
