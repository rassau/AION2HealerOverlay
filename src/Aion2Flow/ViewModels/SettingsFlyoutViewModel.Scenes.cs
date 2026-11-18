using System.Collections.ObjectModel;
using System.Drawing;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cloris.Aion2Flow.ViewModels;

// A frame's window while it's open (FrameChrome, the meter): where it is, as the corner its position setting keeps, and
// moving it there.
public sealed record FramePlacement(Func<Point> Corner, Action<Point> MoveTo);

// Scenes (Settings → Scenes): saved layouts of the frames, each frame open, folded or closed, where it was and where it
// takes clicks. Applied from the settings, with a scene's own shortcut or by a workflow; the "save scene" shortcut keeps the
// frames as they are under a name asked for then.
public sealed partial class SettingsFlyoutViewModel
{
    private readonly Dictionary<string, FramePlacement> _framePlacements = [];
    private int _nextSceneId = 1;

    public ObservableCollection<SceneViewModel> Scenes { get; } = [];

    // Whether a frame's corner is on a screen (the meter sets it); a corner off every screen isn't kept or applied.
    public Func<Point, bool>? IsOnScreen { get; set; }

    // The twin library opened or closed by a scene; the meter opens it (it has what the library needs).
    public event Action<bool>? TwinLibraryWanted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveSceneHotkeyDisplay))]
    [NotifyPropertyChangedFor(nameof(HasSaveSceneHotkey))]
    public partial HotkeyDefinition? SaveSceneHotkey { get; private set; }

    public string SaveSceneHotkeyDisplay => CapturingHotkeyAction == GlobalHotkeyAction.SaveScene
        ? Localization["Settings_Hotkey_PressKeys"]
        : SaveSceneHotkey?.Display ?? Localization["Settings_Hotkey_None"];

    public bool HasSaveSceneHotkey => SaveSceneHotkey is not null;

    public bool HasScenes => Scenes.Count > 0;

    public static string FrameLabelKey(string frame) => frame switch
    {
        MeterKey => "Settings_TabMeter",
        GroupFrameKey => "Settings_GroupFrame",
        SkillMonitorKey => "Settings_SkillMonitor",
        FightGraphsKey => "Settings_FightGraphs",
        HiddenCubesKey => "HiddenCubes_Title",
        TwinLibraryKey => "TwinLibrary_Title",
        _ => throw new ArgumentOutOfRangeException(nameof(frame), frame, "Not a frame.")
    };

    public void AttachFramePlacement(string frame, FramePlacement placement) => _framePlacements[frame] = placement;

    public void DetachFramePlacement(string frame, FramePlacement placement)
    {
        if (_framePlacements.TryGetValue(frame, out var attached) && ReferenceEquals(attached, placement))
            _framePlacements.Remove(frame);
    }

    // The frames as they are now: each one's state, where it is (an open window's own corner, else its saved one) and where
    // it takes clicks.
    public FrameScene CaptureFrames()
    {
        var saved = _settingsService.Current;
        var scene = new FrameScene();
        foreach (var frame in Frames)
        {
            var position = _framePlacements.TryGetValue(frame, out var placement) ? placement.Corner() : StoredPosition(saved, frame);
            scene.Frames[frame] = new SceneFrame
            {
                State = !IsFrameOn(frame) ? SceneFrameState.Closed : IsMinimized(frame) ? SceneFrameState.Folded : SceneFrameState.Open,
                Position = position is { } corner && OnScreen(corner) ? corner : null,
                RightCorner = frame == GroupFrameKey ? GroupFrameAuraSide == GroupFrameSide.Left : null,
                ClickableWithoutFocus = IsClickableWithoutFocus(frame),
                TitleClickable = IsTitleClickable(frame)
            };
        }

        return scene;
    }

    // A new scene with the frames as they are (or as captured before a name was asked for). A name that's taken saves that
    // scene anew, keeping its shortcut; an empty one adds nothing, and so does the tour (its frames aren't the user's).
    public SceneViewModel? AddScene(string? name, FrameScene? captured = null)
    {
        name = name?.Trim();
        if (TutorialRunning || string.IsNullOrEmpty(name))
            return null;

        captured ??= CaptureFrames();
        if (Scenes.FirstOrDefault(scene => string.Equals(scene.Name, name, StringComparison.OrdinalIgnoreCase)) is { } existing)
        {
            existing.Replace(captured);
            SaveScenes();
            return existing;
        }

        if (_nextSceneId > GlobalHotkeyService.MaxSceneId)
            return null;

        captured.Id = _nextSceneId++;
        captured.Name = name;
        var added = new SceneViewModel(this, captured, hotkey: null);
        Scenes.Add(added);
        OnPropertyChanged(nameof(HasScenes));
        SaveScenes();
        return added;
    }

    // An empty name, or one another scene has, leaves it as it was.
    public void RenameScene(SceneViewModel scene, string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || Scenes.Any(other => other != scene && string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase)))
            return;

        scene.Name = name;
        SaveScenes();
    }

    // A scene's trash button: the first click asks ("Delete?"), the second deletes it.
    public void DeleteSceneOrAsk(SceneViewModel scene)
    {
        if (scene.IsDeleteArmed)
        {
            DeleteScene(scene);
            return;
        }

        DisarmScenes();
        scene.IsDeleteArmed = true;
    }

    // A scene's Overwrite button: the first click asks ("Overwrite?"), the second keeps the frames as they are now in it, as
    // adding it would; its name, its place in the list, its shortcut and its workflows stay. Not during the tour.
    public void OverwriteSceneOrAsk(SceneViewModel scene)
    {
        if (TutorialRunning)
            return;

        if (!scene.IsOverwriteArmed)
        {
            DisarmScenes();
            scene.IsOverwriteArmed = true;
            return;
        }

        scene.IsOverwriteArmed = false;
        scene.Replace(CaptureFrames());
        SaveScenes();
    }

    // One question at a time: a scene's trash or Overwrite asking takes back any other.
    private void DisarmScenes()
    {
        foreach (var other in Scenes)
            (other.IsDeleteArmed, other.IsOverwriteArmed) = (false, false);
    }

    public void DeleteScene(SceneViewModel scene)
    {
        if (!Scenes.Contains(scene))
            return;

        if (CapturingHotkeyAction == scene.HotkeyAction)
            CapturingHotkeyAction = null;
        if (scene.Hotkey is not null)
            _globalHotkeyService.TrySetHotkey(scene.HotkeyAction, null);
        Scenes.Remove(scene);
        ForgetSceneInWorkflows(scene);
        OnPropertyChanged(nameof(HasScenes));
        RefreshHotkeyRegistrationError();
        SaveScenes();
    }

    public bool ApplyScene(int id)
    {
        if (FindScene(id) is not { } scene)
            return false;

        ApplyScene(scene);
        return true;
    }

    // Each frame as the scene has it: its place first (a frame the scene turns on opens there), folded or not (a frame the
    // scene closes keeps its own), where it takes clicks (unless the scene leaves that as it is), on or off, and an open
    // window moved to its place. A place off every screen stays unused, and so does the group frame's when its auras
    // changed sides since (its corner is another one then).
    public void ApplyScene(SceneViewModel scene) => ApplyFrames(scene.ToModel());

    // The frames as CaptureFrames had them (a scene's, or those the tutorial puts back: a hand switched during the tour
    // goes back too, as build 73's pin did).
    public void ApplyFrames(FrameScene model)
    {
        var auraSideRight = GroupFrameAuraSide == GroupFrameSide.Left;
        var places = model.Frames
            .Where(pair => pair.Value.Position is { } corner && OnScreen(corner) && (pair.Value.RightCorner is not { } right || right == auraSideRight))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value.Position!.Value);
        _settingsService.Update(settings =>
        {
            foreach (var (frame, position) in places)
                StorePosition(settings, frame, position);
        });
        MinimizedFrames = [.. Frames.Where(frame => model.Frames.TryGetValue(frame, out var saved)
            ? saved.State == SceneFrameState.Folded || saved.State == SceneFrameState.Closed && IsMinimized(frame)
            : IsMinimized(frame))];
        var (clickable, title) = (ClickableWithoutFocus, TitleClickable);
        foreach (var (frame, saved) in model.Frames)
        {
            if (saved.ClickableWithoutFocus is { } clickableOn)
                clickable = WithFrame(clickable, frame, clickableOn);
            if (saved.TitleClickable is { } titleOn)
                title = WithFrame(title, frame, titleOn);
        }

        (ClickableWithoutFocus, TitleClickable) = (clickable, title);
        foreach (var (frame, saved) in model.Frames)
            SetFrameOn(frame, saved.State != SceneFrameState.Closed);
        foreach (var (frame, position) in places)
        {
            if (_framePlacements.TryGetValue(frame, out var placement))
                placement.MoveTo(position);
        }
    }

    private bool OnScreen(Point corner) => IsOnScreen?.Invoke(corner) != false;

    internal void SaveScenes() => PersistSettings();

    private SceneViewModel? FindScene(int id) => Scenes.FirstOrDefault(scene => scene.Id == id);

    private bool IsFrameOn(string frame) => frame switch
    {
        MeterKey => true,
        GroupFrameKey => GroupFrameEnabled,
        SkillMonitorKey => SkillMonitorEnabled,
        FightGraphsKey => FightGraphsEnabled,
        HiddenCubesKey => HiddenCubesEnabled,
        TwinLibraryKey => _framePlacements.ContainsKey(TwinLibraryKey),
        _ => false
    };

    private void SetFrameOn(string frame, bool on)
    {
        if (IsFrameOn(frame) == on)
            return;

        switch (frame)
        {
            case GroupFrameKey:
                GroupFrameEnabled = on;
                break;
            case SkillMonitorKey:
                SkillMonitorEnabled = on;
                break;
            case FightGraphsKey:
                FightGraphsEnabled = on;
                break;
            case HiddenCubesKey:
                HiddenCubesEnabled = on;
                break;
            case TwinLibraryKey:
                TwinLibraryWanted?.Invoke(on);
                break;
        }
    }

    private static Point? StoredPosition(AppSettings settings, string frame) => frame switch
    {
        MeterKey => settings.MainWindowPosition,
        GroupFrameKey => settings.GroupFramePosition,
        SkillMonitorKey => settings.SkillMonitorPosition,
        FightGraphsKey => settings.FightGraphsPosition,
        HiddenCubesKey => settings.HiddenCubesPosition,
        _ => null
    };

    private static void StorePosition(AppSettings settings, string frame, Point position)
    {
        switch (frame)
        {
            case MeterKey:
                settings.MainWindowPosition = position;
                break;
            case GroupFrameKey:
                settings.GroupFramePosition = position;
                break;
            case SkillMonitorKey:
                settings.SkillMonitorPosition = position;
                break;
            case FightGraphsKey:
                settings.FightGraphsPosition = position;
                break;
            case HiddenCubesKey:
                settings.HiddenCubesPosition = position;
                break;
        }
    }

    // From the settings: each scene with its shortcut, unless another action (or an earlier scene) has those keys.
    private void LoadScenes(AppSettings persisted)
    {
        _nextSceneId = Math.Max(persisted.NextSceneId, persisted.Scenes.Count == 0 ? 1 : persisted.Scenes.Max(static scene => scene.Id) + 1);
        SaveSceneHotkey = CreateHotkey(persisted.SaveSceneHotkeyModifiers, persisted.SaveSceneHotkeyVirtualKey);
        HotkeyDefinition?[] taken = [BattleResetHotkey, OverlayInteractionHotkey, MinimizeAllHotkey];
        if (SaveSceneHotkey is not null && taken.Contains(SaveSceneHotkey))
            SaveSceneHotkey = null;
        var used = new HashSet<HotkeyDefinition>(taken.Append(SaveSceneHotkey).OfType<HotkeyDefinition>());
        var ids = new HashSet<int>();
        foreach (var scene in persisted.Scenes)
        {
            if (scene.Id is <= 0 or > GlobalHotkeyService.MaxSceneId || !ids.Add(scene.Id) || string.IsNullOrWhiteSpace(scene.Name))
                continue;

            var hotkey = CreateHotkey(scene.HotkeyModifiers, scene.HotkeyVirtualKey);
            if (hotkey is not null && !used.Add(hotkey))
                hotkey = null;
            Scenes.Add(new SceneViewModel(this, scene, hotkey));
        }
    }

    private void SaveScenes(AppSettings settings)
    {
        settings.Scenes = [.. Scenes.Select(static scene => scene.ToModel())];
        settings.NextSceneId = _nextSceneId;
        settings.SaveSceneHotkeyModifiers = SaveSceneHotkey is null ? null : (uint)SaveSceneHotkey.Modifiers;
        settings.SaveSceneHotkeyVirtualKey = SaveSceneHotkey?.VirtualKey;
    }

    // The named actions and every scene's.
    private GlobalHotkeyAction[] AllHotkeyActions() => [.. Enum.GetValues<GlobalHotkeyAction>(), .. Scenes.Select(static scene => scene.HotkeyAction)];

    partial void OnCapturingHotkeyActionChanged(GlobalHotkeyAction? value)
    {
        OnPropertyChanged(nameof(SaveSceneHotkeyDisplay));
        foreach (var scene in Scenes)
            scene.Refresh();
    }
}
