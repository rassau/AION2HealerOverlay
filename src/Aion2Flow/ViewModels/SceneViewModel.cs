using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cloris.Aion2Flow.ViewModels;

// A scene in the settings (Settings → Scenes, and its shortcut's row under Shortcuts): its name, its shortcut, and each
// frame's state and where it takes clicks. Where the frames were stays as saved.
public sealed partial class SceneViewModel : ObservableObject
{
    private readonly SettingsFlyoutViewModel _settings;
    // Where each frame was (its Position and RightCorner), by key.
    private readonly Dictionary<string, SceneFrame> _places;

    internal SceneViewModel(SettingsFlyoutViewModel settings, FrameScene scene, HotkeyDefinition? hotkey)
    {
        _settings = settings;
        Id = scene.Id;
        Name = scene.Name;
        Hotkey = hotkey;
        _places = scene.Frames
            .Where(static pair => pair.Value.Position is not null)
            .ToDictionary(static pair => pair.Key, static pair => Place(pair.Value));
        Frames = [.. SettingsFlyoutViewModel.Frames.Select(frame => new SceneFrameViewModel(this, frame, scene.Frames.GetValueOrDefault(frame) ?? new SceneFrame()))];
    }

    public int Id { get; }

    public GlobalHotkeyAction HotkeyAction => GlobalHotkeyService.SceneAction(Id);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApplyHotkeyLabel))]
    public partial string Name { get; internal set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyDisplay))]
    [NotifyPropertyChangedFor(nameof(HasHotkey))]
    public partial HotkeyDefinition? Hotkey { get; internal set; }

    public string HotkeyDisplay => _settings.CapturingHotkeyAction == HotkeyAction
        ? _settings.Localization["Settings_Hotkey_PressKeys"]
        : Hotkey?.Display ?? _settings.Localization["Settings_Hotkey_None"];

    public bool HasHotkey => Hotkey is not null;

    // Its trash button asked once ("Delete?"); the next click deletes it.
    [ObservableProperty]
    public partial bool IsDeleteArmed { get; set; }

    // Its row under Shortcuts.
    public string ApplyHotkeyLabel => string.Format(_settings.Localization["Settings_Hotkey_ApplySceneFormat"], Name);

    public IReadOnlyList<SceneFrameViewModel> Frames { get; }

    // After a language switch, or while its shortcut is being pressed.
    internal void Refresh()
    {
        OnPropertyChanged(nameof(HotkeyDisplay));
        OnPropertyChanged(nameof(ApplyHotkeyLabel));
        foreach (var frame in Frames)
            frame.Refresh();
    }

    internal FrameScene ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Frames = Frames.ToDictionary(static frame => frame.Frame, frame => new SceneFrame
        {
            State = frame.State,
            Position = _places.GetValueOrDefault(frame.Frame)?.Position,
            RightCorner = _places.GetValueOrDefault(frame.Frame)?.RightCorner,
            ClickableWithoutFocus = frame.ClickableWithoutFocus,
            TitleClickable = frame.TitleClickable
        }),
        HotkeyModifiers = Hotkey is null ? null : (uint)Hotkey.Modifiers,
        HotkeyVirtualKey = Hotkey?.VirtualKey
    };

    // The frames as saved again (the same name saved once more): their states, places and clicks.
    internal void Replace(FrameScene captured)
    {
        _places.Clear();
        foreach (var (frame, saved) in captured.Frames)
        {
            if (saved.Position is not null)
                _places[frame] = Place(saved);
        }

        foreach (var frame in Frames)
            frame.Set(captured.Frames.GetValueOrDefault(frame.Frame) ?? new SceneFrame());
    }

    internal LocalizationService Localization => _settings.Localization;

    private static SceneFrame Place(SceneFrame saved) => new() { Position = saved.Position, RightCorner = saved.RightCorner };

    internal void StateChanged() => _settings.SaveScenes();
}

// A frame's row in a scene: its name and what the scene does with it. The meter can't be closed: its cross ends the
// overlay.
public sealed partial class SceneFrameViewModel : ObservableObject
{
    private readonly SceneViewModel _scene;
    private bool _settingState;

    internal SceneFrameViewModel(SceneViewModel scene, string frame, SceneFrame saved)
    {
        _scene = scene;
        Frame = frame;
        Set(saved);
    }

    public string Frame { get; }

    public bool CanClose => Frame != SettingsFlyoutViewModel.MeterKey;

    public string Label => _scene.Localization[SettingsFlyoutViewModel.FrameLabelKey(Frame)];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    [NotifyPropertyChangedFor(nameof(IsFolded))]
    [NotifyPropertyChangedFor(nameof(IsClosed))]
    public partial SceneFrameState State { get; private set; }

    // The three toggles: a click on the chosen one leaves it chosen (like the settings' tabs).
    public bool IsOpen { get => State == SceneFrameState.Open; set => Choose(SceneFrameState.Open, value); }

    public bool IsFolded { get => State == SceneFrameState.Folded; set => Choose(SceneFrameState.Folded, value); }

    public bool IsClosed { get => State == SceneFrameState.Closed; set => Choose(SceneFrameState.Closed, value && CanClose); }

    // Whether the scene makes the frame clickable without focus, and its title line always clickable: its two check boxes,
    // each with a third state (a dash) for null, where the scene leaves the setting as it is.
    [ObservableProperty]
    public partial bool? ClickableWithoutFocus { get; set; }

    [ObservableProperty]
    public partial bool? TitleClickable { get; set; }

    partial void OnStateChanged(SceneFrameState value) => Changed();

    partial void OnClickableWithoutFocusChanged(bool? value) => Changed();

    partial void OnTitleClickableChanged(bool? value) => Changed();

    // As saved (or saved anew): nothing to save again then.
    internal void Set(SceneFrame saved)
    {
        _settingState = true;
        try
        {
            State = CanClose || saved.State != SceneFrameState.Closed ? saved.State : SceneFrameState.Open;
            ClickableWithoutFocus = saved.ClickableWithoutFocus;
            TitleClickable = saved.TitleClickable;
        }
        finally
        {
            _settingState = false;
        }
    }

    private void Changed()
    {
        if (!_settingState)
            _scene.StateChanged();
    }

    internal void Refresh() => OnPropertyChanged(nameof(Label));

    private void Choose(SceneFrameState state, bool chosen, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (chosen)
            State = state;
        else if (State == state || property == nameof(IsClosed))
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(property));
    }
}
