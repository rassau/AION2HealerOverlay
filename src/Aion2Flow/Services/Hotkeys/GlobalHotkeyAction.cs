namespace Cloris.Aion2Flow.Services.Hotkeys;

public enum GlobalHotkeyAction
{
    BattleReset = 0,
    CycleOverlayInteraction = 1,
    // Every frame folded to its title and eye, or all back again (like each frame's eye button).
    MinimizeAllFrames = 2,
    // The frames as they are now kept as a scene, under a name asked for then (Settings → Scenes).
    SaveScene = 3
    // A scene's own shortcut applies it: GlobalHotkeyService.SceneAction(its id), past the named actions.
}
