using System.Drawing;

namespace Cloris.Aion2Flow.Services.Settings;

// How a scene leaves a frame: shown, folded to its title line (its eye), or turned off (its cross).
public enum SceneFrameState
{
    Open,
    Folded,
    Closed
}

// A frame in a scene: its state, and where it was (the corner the frame's own position setting keeps). RightCorner: the
// group frame's top right corner (its auras were on the left), not its top left one.
public sealed class SceneFrame
{
    public SceneFrameState State { get; set; }

    public Point? Position { get; set; }

    public bool? RightCorner { get; set; }

    // The frame's "Clickable without focus" and "Title line always clickable"; null: the scene leaves the setting as it is
    // (scenes saved before they were kept have neither).
    public bool? ClickableWithoutFocus { get; set; }

    public bool? TitleClickable { get; set; }
}

// A saved layout of the frames (Settings → Scenes): each frame open, folded or closed, where it was, and where it takes
// clicks. Applied from the settings, with its own shortcut or by a workflow.
public sealed class FrameScene
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // By frame key (SettingsFlyoutViewModel.Frames).
    public Dictionary<string, SceneFrame> Frames
    {
        get;
        set => field = value?.Where(static pair => pair.Value is not null).ToDictionary(static pair => pair.Key, static pair => pair.Value) ?? [];
    } = [];

    public uint? HotkeyModifiers { get; set; }

    public uint? HotkeyVirtualKey { get; set; }

    public FrameScene Clone() => new()
    {
        Id = Id,
        Name = Name,
        Frames = Frames.ToDictionary(static pair => pair.Key, static pair => new SceneFrame
        {
            State = pair.Value.State,
            Position = pair.Value.Position,
            RightCorner = pair.Value.RightCorner,
            ClickableWithoutFocus = pair.Value.ClickableWithoutFocus,
            TitleClickable = pair.Value.TitleClickable
        }),
        HotkeyModifiers = HotkeyModifiers,
        HotkeyVirtualKey = HotkeyVirtualKey
    };
}
