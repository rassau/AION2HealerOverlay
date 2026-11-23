using System.Globalization;
using Cloris.Aion2Flow.Presentation;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

public sealed class SettingsFlyoutViewModelTests
{
    // A scene keeps each frame's state and place (an open window's own, else the saved one) and puts them back: on or
    // off, folded or not, the saved places, and an open window moved; it's saved with the settings and read back.
    [Fact]
    public void A_Scene_Keeps_The_Frames_And_Applies_Them_Again()
    {
        using var fixture = new SettingsViewModelFixture(settings =>
        {
            settings.GroupFrameEnabled = true;
            settings.FightGraphsEnabled = false;
            settings.SkillMonitorEnabled = true;
            settings.HiddenCubesEnabled = false;
            settings.GroupFramePosition = new System.Drawing.Point(100, 200);
            settings.MainWindowPosition = new System.Drawing.Point(1, 1);
        });
        var vm = fixture.ViewModel;
        var meterAt = new System.Drawing.Point(10, 20);
        var moves = new List<System.Drawing.Point>();
        vm.AttachFramePlacement(SettingsFlyoutViewModel.MeterKey, new FramePlacement(() => meterAt, moves.Add));
        vm.ToggleMinimized(SettingsFlyoutViewModel.SkillMonitorKey);

        var scene = Assert.IsType<SceneViewModel>(vm.AddScene("  Raid  "));

        Assert.Equal("Raid", scene.Name);
        var saved = scene.Frames.ToDictionary(static frame => frame.Frame, static frame => frame.State);
        Assert.Equal(SceneFrameState.Open, saved[SettingsFlyoutViewModel.MeterKey]);
        Assert.Equal(SceneFrameState.Open, saved[SettingsFlyoutViewModel.GroupFrameKey]);
        Assert.Equal(SceneFrameState.Folded, saved[SettingsFlyoutViewModel.SkillMonitorKey]);
        Assert.Equal(SceneFrameState.Closed, saved[SettingsFlyoutViewModel.FightGraphsKey]);
        Assert.Equal(SceneFrameState.Closed, saved[SettingsFlyoutViewModel.TwinLibraryKey]);

        vm.GroupFrameEnabled = false;
        vm.FightGraphsEnabled = true;
        vm.ToggleMinimized(SettingsFlyoutViewModel.SkillMonitorKey);
        meterAt = new System.Drawing.Point(500, 600);

        vm.ApplyScene(scene);

        Assert.True(vm.GroupFrameEnabled);
        Assert.False(vm.FightGraphsEnabled);
        Assert.True(vm.IsMinimized(SettingsFlyoutViewModel.SkillMonitorKey));
        Assert.False(vm.IsMinimized(SettingsFlyoutViewModel.MeterKey));
        Assert.Equal([new System.Drawing.Point(10, 20)], moves);
        var loaded = new SettingsService(fixture.SettingsPath).Current;
        Assert.Equal(new System.Drawing.Point(100, 200), loaded.GroupFramePosition);
        Assert.Equal(new System.Drawing.Point(10, 20), loaded.MainWindowPosition);
        var stored = Assert.Single(loaded.Scenes);
        Assert.Equal("Raid", stored.Name);
        Assert.Equal(SceneFrameState.Folded, stored.Frames[SettingsFlyoutViewModel.SkillMonitorKey].State);
        Assert.Equal(new System.Drawing.Point(10, 20), stored.Frames[SettingsFlyoutViewModel.MeterKey].Position);
    }

    // The same name saves that scene anew; a rename to a taken name does nothing; a scene's shortcut registers, moves to
    // another action like any shortcut and goes with the scene; a deleted scene's id isn't given to a new one.
    [Fact]
    public void Scenes_Keep_Their_Names_Shortcuts_And_Ids()
    {
        using var fixture = new SettingsViewModelFixture();
        var vm = fixture.ViewModel;
        var raid = vm.AddScene("Raid")!;
        var farm = vm.AddScene("Farm")!;
        vm.ToggleMinimized(SettingsFlyoutViewModel.MeterKey);

        Assert.Same(raid, vm.AddScene("RAID"));
        Assert.Equal(2, vm.Scenes.Count);
        Assert.Equal(SceneFrameState.Folded, raid.Frames.Single(static frame => frame.Frame == SettingsFlyoutViewModel.MeterKey).State);
        vm.RenameScene(farm, "raid");
        Assert.Equal("Farm", farm.Name);
        vm.RenameScene(farm, "Dungeon");
        Assert.Equal("Dungeon", farm.Name);

        var keys = new HotkeyDefinition(HotkeyModifiers.Control, 0x31);
        vm.BeginCaptureHotkey(raid.HotkeyAction);
        Assert.Equal(vm.Localization["Settings_Hotkey_PressKeys"], raid.HotkeyDisplay);
        Assert.True(vm.ApplyCapturedHotkey(raid.HotkeyAction, keys));
        Assert.Equal(keys, raid.Hotkey);
        Assert.True(fixture.HotkeyService.AttachWindow(0x1234));
        Assert.Contains(((nint)0x1234, GlobalHotkeyService.SceneHotkeyIdBase + raid.Id), fixture.HotkeyPlatform.ActiveRegistrations.Keys);

        vm.BeginCaptureHotkey(GlobalHotkeyAction.SaveScene);
        Assert.True(vm.ApplyCapturedHotkey(GlobalHotkeyAction.SaveScene, keys));
        Assert.Null(raid.Hotkey);
        Assert.Equal(keys, vm.SaveSceneHotkey);

        vm.BeginCaptureHotkey(farm.HotkeyAction);
        Assert.True(vm.ApplyCapturedHotkey(farm.HotkeyAction, new HotkeyDefinition(HotkeyModifiers.Alt, 0x32)));
        vm.DeleteSceneOrAsk(farm);
        Assert.Equal(2, vm.Scenes.Count);
        vm.DeleteSceneOrAsk(farm);
        Assert.Equal([raid], vm.Scenes);
        Assert.DoesNotContain(((nint)0x1234, GlobalHotkeyService.SceneHotkeyIdBase + farm.Id), fixture.HotkeyPlatform.ActiveRegistrations.Keys);

        var next = vm.AddScene("Next")!;
        Assert.True(next.Id > farm.Id);
        var loaded = new SettingsService(fixture.SettingsPath).Current;
        Assert.Equal(["Raid", "Next"], loaded.Scenes.Select(static scene => scene.Name));
        Assert.Equal(next.Id + 1, loaded.NextSceneId);
        Assert.Equal((uint)HotkeyModifiers.Control, loaded.SaveSceneHotkeyModifiers);
    }

    // A place off every screen (a window minimized to the taskbar, a monitor since gone) isn't kept; the group frame's is
    // applied only with the corner its setting keeps now (its top right one while its auras are on the left).
    [Fact]
    public void Scene_Places_Off_Screen_Or_For_The_Other_Corner_Stay_Unused()
    {
        using var fixture = new SettingsViewModelFixture(settings =>
        {
            settings.GroupFrameEnabled = true;
            settings.GroupFramePosition = new System.Drawing.Point(100, 100);
            settings.SkillMonitorPosition = new System.Drawing.Point(-32000, -32000);
        });
        var vm = fixture.ViewModel;
        vm.IsOnScreen = static corner => corner.X > -1_000;
        var scene = vm.AddScene("Layout")!;

        var stored = new SettingsService(fixture.SettingsPath).Current.Scenes.Single().Frames;
        Assert.Null(stored[SettingsFlyoutViewModel.SkillMonitorKey].Position);
        Assert.Equal(new System.Drawing.Point(100, 100), stored[SettingsFlyoutViewModel.GroupFrameKey].Position);
        Assert.False(stored[SettingsFlyoutViewModel.GroupFrameKey].RightCorner);

        var moves = new List<System.Drawing.Point>();
        vm.AttachFramePlacement(SettingsFlyoutViewModel.GroupFrameKey, new FramePlacement(static () => new System.Drawing.Point(300, 100), moves.Add));
        vm.GroupFrameAuraSide = GroupFrameSide.Left;
        vm.ApplyScene(scene);
        Assert.Empty(moves);
        vm.GroupFrameAuraSide = GroupFrameSide.Right;
        vm.ApplyScene(scene);
        Assert.Equal([new System.Drawing.Point(100, 100)], moves);
    }

    // A workflow needs a scene; it applies its scene when its trigger fires while it's on; off, or once its scene is
    // deleted, it does nothing; workflows are saved with the settings.
    [Fact]
    public void Workflows_Apply_Their_Scene_On_Their_Trigger()
    {
        using var fixture = new SettingsViewModelFixture(static settings => settings.GroupFrameEnabled = true);
        var vm = fixture.ViewModel;
        Assert.Null(vm.AddWorkflow());
        var raid = vm.AddScene("Raid")!;
        var workflow = vm.AddWorkflow()!;
        Assert.Equal(WorkflowTrigger.BossFightStarted, workflow.Trigger);
        Assert.Same(raid, workflow.Scene);

        vm.GroupFrameEnabled = false;
        vm.RunWorkflows(WorkflowTrigger.BossFightEnded);
        Assert.False(vm.GroupFrameEnabled);
        vm.RunWorkflows(WorkflowTrigger.BossFightStarted);
        Assert.True(vm.GroupFrameEnabled);

        vm.GroupFrameEnabled = false;
        workflow.Enabled = false;
        vm.RunWorkflows(WorkflowTrigger.BossFightStarted);
        Assert.False(vm.GroupFrameEnabled);

        workflow.Enabled = true;
        workflow.TriggerIndex = Array.IndexOf(Enum.GetValues<WorkflowTrigger>(), WorkflowTrigger.EnteredDungeon);
        var stored = Assert.Single(new SettingsService(fixture.SettingsPath).Current.Workflows);
        Assert.Equal(WorkflowTrigger.EnteredDungeon, stored.Trigger);
        Assert.Equal(raid.Id, stored.SceneId);
        Assert.True(stored.Enabled);

        vm.DeleteSceneOrAsk(raid);
        vm.DeleteSceneOrAsk(raid);
        Assert.Null(workflow.Scene);
        vm.RunWorkflows(WorkflowTrigger.EnteredDungeon);
        Assert.False(vm.GroupFrameEnabled);
        Assert.Equal(0, new SettingsService(fixture.SettingsPath).Current.Workflows.Single().SceneId);
    }

    // A scene's keys taken by a named action already, or by an earlier scene, are dropped as the settings are read.
    [Fact]
    public void Scene_Shortcuts_Taken_Elsewhere_Are_Dropped_On_Load()
    {
        using var fixture = new SettingsViewModelFixture(settings =>
        {
            settings.MinimizeAllHotkeyModifiers = (uint)HotkeyModifiers.Control;
            settings.MinimizeAllHotkeyVirtualKey = 0x4D;
            settings.Scenes =
            [
                new FrameScene { Id = 3, Name = "One", HotkeyModifiers = (uint)HotkeyModifiers.Control, HotkeyVirtualKey = 0x4D },
                new FrameScene { Id = 4, Name = "Two", HotkeyModifiers = (uint)HotkeyModifiers.Alt, HotkeyVirtualKey = 0x31 },
                new FrameScene { Id = 5, Name = "Three", HotkeyModifiers = (uint)HotkeyModifiers.Alt, HotkeyVirtualKey = 0x31 },
            ];
        });
        var vm = fixture.ViewModel;

        Assert.Equal(["One", "Two", "Three"], vm.Scenes.Select(static scene => scene.Name));
        Assert.Null(vm.Scenes[0].Hotkey);
        Assert.Equal(new HotkeyDefinition(HotkeyModifiers.Alt, 0x31), vm.Scenes[1].Hotkey);
        Assert.Null(vm.Scenes[2].Hotkey);
        Assert.Equal(6, vm.AddScene("Four")!.Id);
    }

    [Fact]
    public void SettingsService_PersistsBothGlobalHotkeys()
    {
        using var settingsFile = new TemporarySettingsFile();
        var settings = new SettingsService(settingsFile.Path);

        Assert.Null(settings.Current.OverlayInteractionHotkeyModifiers);
        Assert.Null(settings.Current.OverlayInteractionHotkeyVirtualKey);

        settings.Update(static value =>
        {
            value.BattleResetHotkeyModifiers = (uint)(HotkeyModifiers.Control | HotkeyModifiers.Shift);
            value.BattleResetHotkeyVirtualKey = 0x52;
            value.OverlayInteractionHotkeyModifiers = (uint)(HotkeyModifiers.Alt | HotkeyModifiers.Win);
            value.OverlayInteractionHotkeyVirtualKey = 0x49;
        });

        var loaded = new SettingsService(settingsFile.Path).Current;

        Assert.Equal((uint)(HotkeyModifiers.Control | HotkeyModifiers.Shift), loaded.BattleResetHotkeyModifiers);
        Assert.Equal(0x52u, loaded.BattleResetHotkeyVirtualKey);
        Assert.Equal((uint)(HotkeyModifiers.Alt | HotkeyModifiers.Win), loaded.OverlayInteractionHotkeyModifiers);
        Assert.Equal(0x49u, loaded.OverlayInteractionHotkeyVirtualKey);
    }

    // Which frames take clicks while the game has focus: the meter, the twin library and the playback, the others not; a
    // toggle adds or takes one away, and it's kept.
    [Fact]
    public void ClickableWithoutFocus_Is_Per_Frame_And_Kept()
    {
        using var fixture = new SettingsViewModelFixture(static _ => { });
        var settings = fixture.ViewModel;
        Assert.Equal([SettingsFlyoutViewModel.MeterKey, SettingsFlyoutViewModel.TwinLibraryKey, SettingsFlyoutViewModel.PlaybackKey],
            SettingsFlyoutViewModel.ClickableFrames.Where(settings.IsClickableWithoutFocus));

        settings.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.FightGraphsKey);
        settings.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.TwinLibraryKey);

        Assert.Equal([SettingsFlyoutViewModel.PlaybackKey, SettingsFlyoutViewModel.MeterKey, SettingsFlyoutViewModel.FightGraphsKey],
            new SettingsService(fixture.SettingsPath).Current.ClickableWithoutFocus);
    }

    // The meter came into "Clickable without focus" on: in a new install, and once in settings saved before (their list
    // hasn't got it; such a file with a setting since gone loads all the same). Turned off, it stays off at the next start.
    [Fact]
    public void The_Meter_Is_Clickable_Without_Focus_Unless_Turned_Off()
    {
        using (var newInstall = new SettingsViewModelFixture())
            Assert.True(newInstall.ViewModel.IsClickableWithoutFocus(SettingsFlyoutViewModel.MeterKey));

        string saved;
        using (var before = new SettingsViewModelFixture(json: """{"ClickableWithoutFocus":["TwinLibrary"],"HideHeaderWhenClickThrough":true,"OverlayOpacityPercent":80}"""))
        {
            Assert.Equal(80, before.ViewModel.OverlayOpacityPercent);
            Assert.True(before.ViewModel.IsClickableWithoutFocus(SettingsFlyoutViewModel.MeterKey));

            before.ViewModel.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.MeterKey);
            saved = File.ReadAllText(before.SettingsPath);
        }

        using var nextStart = new SettingsViewModelFixture(json: saved);
        Assert.False(nextStart.ViewModel.IsClickableWithoutFocus(SettingsFlyoutViewModel.MeterKey));
        Assert.True(nextStart.ViewModel.IsClickableWithoutFocus(SettingsFlyoutViewModel.TwinLibraryKey));
    }

    // "Title line always clickable": the meter's only, in a new install and in settings saved before it was there; a toggle
    // adds or takes a frame away, says so (the windows follow it), and it's kept.
    [Fact]
    public void Title_Line_Always_Clickable_Is_The_Meters_At_First_And_Kept()
    {
        using (var newInstall = new SettingsViewModelFixture())
            Assert.Equal([SettingsFlyoutViewModel.MeterKey], SettingsFlyoutViewModel.Frames.Where(newInstall.ViewModel.IsTitleClickable));

        string saved;
        using (var before = new SettingsViewModelFixture(json: """{"ClickableWithoutFocus":["TwinLibrary","Meter"],"OverlayOpacityPercent":80}"""))
        {
            var settings = before.ViewModel;
            Assert.Equal([SettingsFlyoutViewModel.MeterKey], SettingsFlyoutViewModel.Frames.Where(settings.IsTitleClickable));
            var raised = new List<string?>();
            settings.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            settings.ToggleTitleClickable(SettingsFlyoutViewModel.GroupFrameKey);
            settings.ToggleTitleClickable(SettingsFlyoutViewModel.MeterKey);

            Assert.Equal(2, raised.Count(static name => name == nameof(SettingsFlyoutViewModel.TitleClickable)));
            Assert.Equal([SettingsFlyoutViewModel.GroupFrameKey], new SettingsService(before.SettingsPath).Current.TitleClickable);
            saved = File.ReadAllText(before.SettingsPath);
        }

        using var nextStart = new SettingsViewModelFixture(json: saved);
        Assert.Equal([SettingsFlyoutViewModel.GroupFrameKey], SettingsFlyoutViewModel.Frames.Where(nextStart.ViewModel.IsTitleClickable));
        Assert.True(nextStart.ViewModel.IsClickableWithoutFocus(SettingsFlyoutViewModel.MeterKey));
    }

    // A scene keeps where each frame takes clicks (clickable without focus, title line always clickable) and puts it back,
    // from the settings, its shortcut or a workflow; the playback, in no scene, stays as it is. The same name saved again
    // keeps the frames' clicks anew.
    [Fact]
    public void A_Scene_Keeps_Where_The_Frames_Take_Clicks()
    {
        using var fixture = new SettingsViewModelFixture();
        var vm = fixture.ViewModel;
        vm.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.MeterKey);
        vm.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.GroupFrameKey);
        vm.ToggleTitleClickable(SettingsFlyoutViewModel.FightGraphsKey);

        var scene = vm.AddScene("Raid")!;

        var meter = scene.Frames.Single(static frame => frame.Frame == SettingsFlyoutViewModel.MeterKey);
        var group = scene.Frames.Single(static frame => frame.Frame == SettingsFlyoutViewModel.GroupFrameKey);
        var graphs = scene.Frames.Single(static frame => frame.Frame == SettingsFlyoutViewModel.FightGraphsKey);
        Assert.Equal((false, true), (meter.ClickableWithoutFocus, meter.TitleClickable));
        Assert.Equal((true, false), (group.ClickableWithoutFocus, group.TitleClickable));
        Assert.Equal((false, true), (graphs.ClickableWithoutFocus, graphs.TitleClickable));
        var stored = Assert.Single(new SettingsService(fixture.SettingsPath).Current.Scenes).Frames;
        Assert.Equal((false, true), (stored[SettingsFlyoutViewModel.MeterKey].ClickableWithoutFocus, stored[SettingsFlyoutViewModel.MeterKey].TitleClickable));
        Assert.Equal((true, false), (stored[SettingsFlyoutViewModel.TwinLibraryKey].ClickableWithoutFocus, stored[SettingsFlyoutViewModel.TwinLibraryKey].TitleClickable));

        vm.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.MeterKey);
        vm.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.GroupFrameKey);
        vm.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.PlaybackKey);
        vm.ToggleTitleClickable(SettingsFlyoutViewModel.FightGraphsKey);
        vm.ToggleTitleClickable(SettingsFlyoutViewModel.MeterKey);
        vm.ToggleTitleClickable(SettingsFlyoutViewModel.HiddenCubesKey);

        // Its shortcut (and a workflow) apply it by its id.
        Assert.True(vm.ApplyScene(scene.Id));

        Assert.Equal([SettingsFlyoutViewModel.GroupFrameKey, SettingsFlyoutViewModel.TwinLibraryKey], SettingsFlyoutViewModel.Frames.Where(vm.IsClickableWithoutFocus));
        Assert.False(vm.IsClickableWithoutFocus(SettingsFlyoutViewModel.PlaybackKey));
        Assert.Equal([SettingsFlyoutViewModel.MeterKey, SettingsFlyoutViewModel.FightGraphsKey], SettingsFlyoutViewModel.Frames.Where(vm.IsTitleClickable));
        var kept = new SettingsService(fixture.SettingsPath).Current;
        Assert.Equal([SettingsFlyoutViewModel.MeterKey, SettingsFlyoutViewModel.FightGraphsKey], kept.TitleClickable);
        Assert.Equal([SettingsFlyoutViewModel.TwinLibraryKey, SettingsFlyoutViewModel.GroupFrameKey], kept.ClickableWithoutFocus);

        vm.ToggleTitleClickable(SettingsFlyoutViewModel.SkillMonitorKey);
        Assert.Same(scene, vm.AddScene("raid"));
        Assert.True(scene.Frames.Single(static frame => frame.Frame == SettingsFlyoutViewModel.SkillMonitorKey).TitleClickable);
        Assert.True(new SettingsService(fixture.SettingsPath).Current.Scenes.Single().Frames[SettingsFlyoutViewModel.SkillMonitorKey].TitleClickable);
    }

    // A scene saved before it kept the frames' clicks (no values in its file) leaves them as they are, and its check boxes
    // show the third state; one set by hand is saved and applied, and set back to the third state it's left alone again.
    [Fact]
    public void A_Scene_From_Before_Leaves_The_Frames_Clicks_As_They_Are()
    {
        using var fixture = new SettingsViewModelFixture(json: """
            {"NextSceneId":2,"Scenes":[{"Id":1,"Name":"Farming","Frames":{"Meter":{"State":"Open"},"GroupFrame":{"State":"Folded"}}}]}
            """);
        var vm = fixture.ViewModel;
        var scene = Assert.Single(vm.Scenes);
        Assert.All(scene.Frames, static frame => Assert.Equal((null, null), (frame.ClickableWithoutFocus, frame.TitleClickable)));
        var (clickable, title) = (vm.ClickableWithoutFocus, vm.TitleClickable);

        vm.ApplyScene(scene);

        Assert.Same(clickable, vm.ClickableWithoutFocus);
        Assert.Same(title, vm.TitleClickable);

        var group = scene.Frames.Single(static frame => frame.Frame == SettingsFlyoutViewModel.GroupFrameKey);
        group.TitleClickable = true;
        var stored = new SettingsService(fixture.SettingsPath).Current.Scenes.Single().Frames[SettingsFlyoutViewModel.GroupFrameKey];
        Assert.Equal((null, true), (stored.ClickableWithoutFocus, stored.TitleClickable));
        vm.ApplyScene(scene);
        Assert.True(vm.IsTitleClickable(SettingsFlyoutViewModel.GroupFrameKey));
        Assert.Same(clickable, vm.ClickableWithoutFocus);

        group.TitleClickable = null;
        Assert.Null(new SettingsService(fixture.SettingsPath).Current.Scenes.Single().Frames[SettingsFlyoutViewModel.GroupFrameKey].TitleClickable);
        vm.ToggleTitleClickable(SettingsFlyoutViewModel.GroupFrameKey);
        vm.ApplyScene(scene);
        Assert.False(vm.IsTitleClickable(SettingsFlyoutViewModel.GroupFrameKey));
    }

    // A scene's Overwrite: the first click asks (and takes back another scene's question), the second keeps the frames as
    // they are now in it, as adding it would (the dashes of a scene from before are gone); its name, place, id, shortcut and
    // workflows stay. Asking to delete takes the question back too.
    [Fact]
    public void Overwrite_Asks_Once_Then_Keeps_The_Frames_In_The_Same_Scene()
    {
        using var fixture = new SettingsViewModelFixture(json: """
            {"NextSceneId":3,"Scenes":[{"Id":1,"Name":"Raid","Frames":{"Meter":{"State":"Open"},"GroupFrame":{"State":"Closed"}}},{"Id":2,"Name":"Farm"}]}
            """);
        var vm = fixture.ViewModel;
        var (raid, farm) = (vm.Scenes[0], vm.Scenes[1]);
        var keys = new HotkeyDefinition(HotkeyModifiers.Control, 0x31);
        vm.BeginCaptureHotkey(raid.HotkeyAction);
        Assert.True(vm.ApplyCapturedHotkey(raid.HotkeyAction, keys));
        var workflow = vm.AddWorkflow()!;
        vm.AttachFramePlacement(SettingsFlyoutViewModel.MeterKey, new FramePlacement(static () => new System.Drawing.Point(10, 20), static _ => { }));
        vm.GroupFrameEnabled = true;
        vm.ToggleMinimized(SettingsFlyoutViewModel.MeterKey);
        SceneFrameViewModel Frame(string key) => raid.Frames.Single(frame => frame.Frame == key);
        Assert.All(raid.Frames, static frame => Assert.Equal((null, null), (frame.ClickableWithoutFocus, frame.TitleClickable)));

        vm.DeleteSceneOrAsk(farm);
        vm.OverwriteSceneOrAsk(raid);
        Assert.Equal((true, false), (raid.IsOverwriteArmed, farm.IsDeleteArmed));
        Assert.Equal(SceneFrameState.Closed, Frame(SettingsFlyoutViewModel.GroupFrameKey).State);

        vm.OverwriteSceneOrAsk(raid);
        Assert.False(raid.IsOverwriteArmed);
        Assert.Equal((SceneFrameState.Folded, SceneFrameState.Open), (Frame(SettingsFlyoutViewModel.MeterKey).State, Frame(SettingsFlyoutViewModel.GroupFrameKey).State));
        Assert.All(raid.Frames, static frame => Assert.True(frame.ClickableWithoutFocus is not null && frame.TitleClickable is not null));
        Assert.Equal([raid, farm], vm.Scenes);
        Assert.Equal(("Raid", 1, keys), (raid.Name, raid.Id, raid.Hotkey));
        Assert.Same(raid, workflow.Scene);
        var stored = new SettingsService(fixture.SettingsPath).Current;
        Assert.Equal(["Raid", "Farm"], stored.Scenes.Select(static scene => scene.Name));
        var kept = stored.Scenes[0];
        Assert.Equal((1, (uint?)HotkeyModifiers.Control, (uint?)0x31), (kept.Id, kept.HotkeyModifiers, kept.HotkeyVirtualKey));
        Assert.Equal((SceneFrameState.Folded, (System.Drawing.Point?)new System.Drawing.Point(10, 20)), (kept.Frames[SettingsFlyoutViewModel.MeterKey].State, kept.Frames[SettingsFlyoutViewModel.MeterKey].Position));
        Assert.Equal((1, 3), (stored.Workflows.Single().SceneId, stored.NextSceneId));

        vm.OverwriteSceneOrAsk(farm);
        vm.DeleteSceneOrAsk(raid);
        Assert.Equal((false, true), (farm.IsOverwriteArmed, raid.IsDeleteArmed));
    }

    // A scene's positions button: the first click asks (and takes back another scene's question), the second takes only
    // where the frames are now, the group frame's with its corner; a frame off every screen, or the closed twin library,
    // keeps the scene's place. The frames' states and clicks (a dash too), the scene's name, place, id, shortcut and
    // workflows stay. Overwrite asking takes the question back, and the other way round.
    [Fact]
    public void Positions_Asks_Once_Then_Keeps_Only_Where_The_Frames_Are_Now()
    {
        using var fixture = new SettingsViewModelFixture(static settings =>
        {
            settings.GroupFramePosition = new System.Drawing.Point(300, 400);
            settings.GroupFrameAuraSide = GroupFrameSide.Left;
            settings.SkillMonitorPosition = new System.Drawing.Point(-32000, -32000);
            settings.HiddenCubesPosition = new System.Drawing.Point(50, 60);
            settings.NextSceneId = 3;
            settings.Scenes =
            [
                new FrameScene
                {
                    Id = 1,
                    Name = "Raid",
                    Frames =
                    {
                        [SettingsFlyoutViewModel.MeterKey] = new SceneFrame { State = SceneFrameState.Folded, Position = new System.Drawing.Point(1, 1), ClickableWithoutFocus = false },
                        [SettingsFlyoutViewModel.GroupFrameKey] = new SceneFrame { State = SceneFrameState.Closed, Position = new System.Drawing.Point(100, 200), RightCorner = false, TitleClickable = true },
                        [SettingsFlyoutViewModel.SkillMonitorKey] = new SceneFrame { State = SceneFrameState.Open, Position = new System.Drawing.Point(5, 5) },
                        [SettingsFlyoutViewModel.TwinLibraryKey] = new SceneFrame { State = SceneFrameState.Open, Position = new System.Drawing.Point(7, 7) }
                    }
                },
                new FrameScene { Id = 2, Name = "Farm" }
            ];
        });
        var vm = fixture.ViewModel;
        vm.IsOnScreen = static corner => corner.X > -1_000;
        var (raid, farm) = (vm.Scenes[0], vm.Scenes[1]);
        var keys = new HotkeyDefinition(HotkeyModifiers.Control, 0x31);
        vm.BeginCaptureHotkey(raid.HotkeyAction);
        Assert.True(vm.ApplyCapturedHotkey(raid.HotkeyAction, keys));
        var workflow = vm.AddWorkflow()!;
        vm.AttachFramePlacement(SettingsFlyoutViewModel.MeterKey, new FramePlacement(static () => new System.Drawing.Point(10, 20), static _ => { }));
        vm.GroupFrameEnabled = true;
        Dictionary<string, SceneFrame> Stored() => new SettingsService(fixture.SettingsPath).Current.Scenes[0].Frames;

        vm.DeleteSceneOrAsk(farm);
        vm.OverwriteScenePositionsOrAsk(raid);
        Assert.Equal((true, false), (raid.IsPositionsArmed, farm.IsDeleteArmed));
        Assert.Equal(new System.Drawing.Point(1, 1), Stored()[SettingsFlyoutViewModel.MeterKey].Position);

        vm.OverwriteScenePositionsOrAsk(raid);
        Assert.False(raid.IsPositionsArmed);
        var frames = Stored();
        Assert.Equal(new System.Drawing.Point(10, 20), frames[SettingsFlyoutViewModel.MeterKey].Position);
        Assert.Equal(((System.Drawing.Point?)new System.Drawing.Point(300, 400), (bool?)true), (frames[SettingsFlyoutViewModel.GroupFrameKey].Position, frames[SettingsFlyoutViewModel.GroupFrameKey].RightCorner));
        Assert.Equal(new System.Drawing.Point(50, 60), frames[SettingsFlyoutViewModel.HiddenCubesKey].Position);
        Assert.Equal(new System.Drawing.Point(5, 5), frames[SettingsFlyoutViewModel.SkillMonitorKey].Position);
        Assert.Equal(new System.Drawing.Point(7, 7), frames[SettingsFlyoutViewModel.TwinLibraryKey].Position);
        Assert.Null(frames[SettingsFlyoutViewModel.FightGraphsKey].Position);
        Assert.Equal((SceneFrameState.Folded, false, null), (frames[SettingsFlyoutViewModel.MeterKey].State, frames[SettingsFlyoutViewModel.MeterKey].ClickableWithoutFocus, frames[SettingsFlyoutViewModel.MeterKey].TitleClickable));
        Assert.Equal((SceneFrameState.Closed, null, true), (frames[SettingsFlyoutViewModel.GroupFrameKey].State, frames[SettingsFlyoutViewModel.GroupFrameKey].ClickableWithoutFocus, frames[SettingsFlyoutViewModel.GroupFrameKey].TitleClickable));
        Assert.Equal(SceneFrameState.Closed, raid.Frames.Single(static frame => frame.Frame == SettingsFlyoutViewModel.GroupFrameKey).State);
        Assert.Equal([raid, farm], vm.Scenes);
        Assert.Equal(("Raid", 1, keys), (raid.Name, raid.Id, raid.Hotkey));
        Assert.Same(raid, workflow.Scene);
        var stored = new SettingsService(fixture.SettingsPath).Current;
        Assert.Equal(["Raid", "Farm"], stored.Scenes.Select(static scene => scene.Name));
        Assert.Equal((1, (uint?)HotkeyModifiers.Control, (uint?)0x31), (stored.Scenes[0].Id, stored.Scenes[0].HotkeyModifiers, stored.Scenes[0].HotkeyVirtualKey));
        Assert.Equal(1, stored.Workflows.Single().SceneId);

        vm.OverwriteScenePositionsOrAsk(farm);
        vm.OverwriteSceneOrAsk(farm);
        Assert.Equal((false, true), (farm.IsPositionsArmed, farm.IsOverwriteArmed));
        vm.OverwriteScenePositionsOrAsk(raid);
        Assert.Equal((true, false), (raid.IsPositionsArmed, farm.IsOverwriteArmed));
    }

    // The tour opens frames the user never chose: while it runs, Overwrite and positions don't even ask and adding keeps no
    // scene (a new name or a taken one, from the Scenes tab or the "save scene" prompt). After it, both work again.
    [Fact]
    public void During_The_Tour_No_Scene_Is_Added_Or_Overwritten()
    {
        using var fixture = new SettingsViewModelFixture(json: """
            {"NextSceneId":2,"Scenes":[{"Id":1,"Name":"Farming","Frames":{"Meter":{"State":"Open"},"GroupFrame":{"State":"Closed"}}}]}
            """);
        var vm = fixture.ViewModel;
        var farming = vm.Scenes[0];
        vm.TutorialRunning = true;
        vm.GroupFrameEnabled = true;
        vm.AttachFramePlacement(SettingsFlyoutViewModel.MeterKey, new FramePlacement(static () => new System.Drawing.Point(10, 20), static _ => { }));

        vm.OverwriteSceneOrAsk(farming);
        Assert.False(farming.IsOverwriteArmed);
        farming.IsOverwriteArmed = true;
        vm.OverwriteSceneOrAsk(farming);
        vm.OverwriteScenePositionsOrAsk(farming);
        Assert.False(farming.IsPositionsArmed);
        farming.IsPositionsArmed = true;
        vm.OverwriteScenePositionsOrAsk(farming);
        Assert.Null(vm.AddScene("Tour"));
        Assert.Null(vm.AddScene("Farming"));

        Assert.Equal([farming], vm.Scenes);
        Assert.Equal(SceneFrameState.Closed, farming.Frames.Single(static frame => frame.Frame == SettingsFlyoutViewModel.GroupFrameKey).State);
        Assert.Equal(SceneFrameState.Closed, new SettingsService(fixture.SettingsPath).Current.Scenes.Single().Frames[SettingsFlyoutViewModel.GroupFrameKey].State);
        Assert.Null(new SettingsService(fixture.SettingsPath).Current.Scenes.Single().Frames[SettingsFlyoutViewModel.MeterKey].Position);

        vm.TutorialRunning = false;
        Assert.NotNull(vm.AddScene("After"));
        vm.OverwriteSceneOrAsk(farming);
        Assert.Equal(SceneFrameState.Open, farming.Frames.Single(static frame => frame.Frame == SettingsFlyoutViewModel.GroupFrameKey).State);
    }

    [Fact]
    public void GroupFrameDistanceMarks_LoadAndPersistThroughViewModel()
    {
        using var fixture = new SettingsViewModelFixture(static settings =>
        {
            settings.GroupFrameGreenMeters = 20;
            settings.GroupFrameOrangeMeters = 30;
        });

        Assert.Equal((20, 30, 40), (fixture.ViewModel.GroupFrameGreenMeters, fixture.ViewModel.GroupFrameOrangeMeters, fixture.ViewModel.GroupFrameRangeMeters));

        fixture.ViewModel.GroupFrameRangeMeters = 45;

        var persisted = new SettingsService(fixture.SettingsPath).Current;
        Assert.Equal((20, 30, 45), (persisted.GroupFrameGreenMeters, persisted.GroupFrameOrangeMeters, persisted.GroupFrameRangeMeters));
    }

    [Fact]
    public void SkillMonitorEnabled_LoadsAndPersistsThroughViewModel()
    {
        using var fixture = new SettingsViewModelFixture(static settings =>
        {
            settings.SkillMonitorEnabled = false;
            settings.SkillMonitorBuffSelectAll = false;
            settings.SkillMonitorBuffSkillIds = [13_050_000];
            settings.SkillMonitorCooldownSelectAll = false;
            settings.SkillMonitorCooldownSkillIds = [13_130_000];
            settings.SkillMonitorDebuffSelectAll = false;
            settings.SkillMonitorDebuffSkillIds = [17_070_000];
            settings.SkillMonitorDebuffsFromAnyone = true;
            settings.SkillMonitorWidth = 740;
        });

        Assert.False(fixture.ViewModel.SkillMonitorEnabled);
        // "Buffs from anyone" off at first, "Debuffs from anyone" as saved.
        Assert.Equal((false, true), (fixture.ViewModel.SkillMonitorBuffsFromAnyone, fixture.ViewModel.SkillMonitorDebuffsFromAnyone));

        fixture.ViewModel.SkillMonitorEnabled = true;
        fixture.ViewModel.SkillMonitorBuffsFromAnyone = true;

        var persisted = new SettingsService(fixture.SettingsPath).Current;
        Assert.True(persisted.SkillMonitorEnabled);
        Assert.Equal((true, true), (persisted.SkillMonitorBuffsFromAnyone, persisted.SkillMonitorDebuffsFromAnyone));
        Assert.False(persisted.SkillMonitorBuffSelectAll);
        Assert.Equal([13_050_000], persisted.SkillMonitorBuffSkillIds);
        Assert.False(persisted.SkillMonitorCooldownSelectAll);
        Assert.Equal([13_130_000], persisted.SkillMonitorCooldownSkillIds);
        Assert.False(persisted.SkillMonitorDebuffSelectAll);
        Assert.Equal([17_070_000], persisted.SkillMonitorDebuffSkillIds);
        Assert.Equal(740, persisted.SkillMonitorWidth);
    }

    [Fact]
    public void EncounterTimeDisplayFormat_DefaultsAndPersistsThroughViewModel()
    {
        using var fixture = new SettingsViewModelFixture();

        Assert.Equal(
            EncounterTimeDisplayFormat.DecimalSeconds,
            fixture.ViewModel.EncounterTimeDisplayFormat);

        fixture.ViewModel.EncounterTimeDisplayFormat =
            EncounterTimeDisplayFormat.MinutesSeconds;

        Assert.Equal(
            EncounterTimeDisplayFormat.MinutesSeconds,
            new SettingsService(fixture.SettingsPath).Current.EncounterTimeDisplayFormat);
    }

    // "Use last battle's skill levels": on at first, off as saved, and each change saved.
    [Fact]
    public void Use_Seen_Skill_Levels_Loads_And_Persists_Through_The_View_Model()
    {
        using (var fresh = new SettingsViewModelFixture())
            Assert.True(fresh.ViewModel.UseSeenSkillLevels);

        using var fixture = new SettingsViewModelFixture(static settings => settings.UseSeenSkillLevels = false);
        Assert.False(fixture.ViewModel.UseSeenSkillLevels);

        fixture.ViewModel.UseSeenSkillLevels = true;
        Assert.True(new SettingsService(fixture.SettingsPath).Current.UseSeenSkillLevels);
        fixture.ViewModel.UseSeenSkillLevels = false;
        Assert.False(new SettingsService(fixture.SettingsPath).Current.UseSeenSkillLevels);
    }

    [Fact]
    public void Aggro_Column_Shows_For_Tanks_Unless_Set_Otherwise()
    {
        using var fixture = new SettingsViewModelFixture();
        var vm = fixture.ViewModel;

        // New settings: Auto, shown while the character is a Templar or a Gladiator.
        Assert.Equal(AggroColumnMode.Auto, vm.AggroColumn);
        Assert.False(vm.ShowAggroColumn);
        vm.LocalPlayerClass = CharacterClass.Templar;
        Assert.True(vm.ShowAggroColumn);
        vm.LocalPlayerClass = CharacterClass.Gladiator;
        Assert.True(vm.ShowAggroColumn);
        vm.LocalPlayerClass = CharacterClass.Cleric;
        Assert.False(vm.ShowAggroColumn);

        // Show and Hide hold whatever the class.
        vm.AggroColumn = AggroColumnMode.Show;
        Assert.True(vm.ShowAggroColumn);
        vm.LocalPlayerClass = CharacterClass.Templar;
        vm.AggroColumn = AggroColumnMode.Hide;
        Assert.False(vm.ShowAggroColumn);
        Assert.Equal(AggroColumnMode.Hide, new SettingsService(fixture.SettingsPath).Current.AggroColumn);
    }

    [Fact]
    public void Recovery_Time_Column_Shows_For_Clerics_Unless_Set_Otherwise()
    {
        using var fixture = new SettingsViewModelFixture();
        var vm = fixture.ViewModel;

        // New settings: Auto, shown while the character is a Cleric.
        Assert.Equal(AggroColumnMode.Auto, vm.RecoveryTimeColumn);
        Assert.False(vm.ShowRecoveryTimeColumn);
        vm.LocalPlayerClass = CharacterClass.Cleric;
        Assert.True(vm.ShowRecoveryTimeColumn);
        vm.LocalPlayerClass = CharacterClass.Templar;
        Assert.False(vm.ShowRecoveryTimeColumn);

        // Show and Hide hold whatever the class.
        vm.RecoveryTimeColumn = AggroColumnMode.Show;
        Assert.True(vm.ShowRecoveryTimeColumn);
        vm.LocalPlayerClass = CharacterClass.Cleric;
        vm.RecoveryTimeColumn = AggroColumnMode.Hide;
        Assert.False(vm.ShowRecoveryTimeColumn);
        Assert.Equal(AggroColumnMode.Hide, new SettingsService(fixture.SettingsPath).Current.RecoveryTimeColumn);
        Assert.EndsWith("/14", vm.MainMetricVisibilityDisplay);
    }

    // A frame's eye minimizes it, again brings it back; the shortcut minimizes every frame, or with all of them minimized
    // brings them all back. The meter's fold (a click on its title) is one of them, kept for older versions too.
    [Fact]
    public void Frames_Minimize_One_By_One_Or_All_At_Once()
    {
        using var fixture = new SettingsViewModelFixture();
        var vm = fixture.ViewModel;

        vm.ToggleMinimized(SettingsFlyoutViewModel.GroupFrameKey);
        Assert.True(vm.IsMinimized(SettingsFlyoutViewModel.GroupFrameKey));
        Assert.False(vm.IsMinimized(SettingsFlyoutViewModel.FightGraphsKey));

        vm.ToggleMinimizeAll();
        Assert.All(SettingsFlyoutViewModel.Frames, frame => Assert.True(vm.IsMinimized(frame)));
        var saved = new SettingsService(fixture.SettingsPath).Current;
        Assert.Equal(SettingsFlyoutViewModel.Frames, saved.MinimizedFrames);
        Assert.True(saved.MeterFolded);

        vm.ToggleMinimizeAll();
        Assert.Empty(vm.MinimizedFrames);
        Assert.False(new SettingsService(fixture.SettingsPath).Current.MeterFolded);
    }

    [Fact]
    public void A_Meter_Folded_Before_Is_Minimized()
    {
        using var fixture = new SettingsViewModelFixture(settings => settings.MeterFolded = true);

        Assert.True(fixture.ViewModel.IsMinimized(SettingsFlyoutViewModel.MeterKey));
    }

    // The cube finder was the group frame's: shown there with the group frame on is its own frame on now, its sound kept.
    [Theory]
    [InlineData(true, GroupFrameCubeFinder.Show, true, false)]
    [InlineData(true, GroupFrameCubeFinder.Sound, true, true)]
    [InlineData(true, GroupFrameCubeFinder.Off, false, false)]
    [InlineData(false, GroupFrameCubeFinder.Show, false, false)]
    public void Hidden_Cubes_Of_Older_Settings_Follow_The_Group_Frame(bool groupFrame, GroupFrameCubeFinder finder, bool enabled, bool sound)
    {
        using var fixture = new SettingsViewModelFixture(settings =>
        {
            settings.GroupFrameEnabled = groupFrame;
            settings.GroupFrameCubeFinder = finder;
        });

        Assert.Equal((enabled, sound), (fixture.ViewModel.HiddenCubesEnabled, fixture.ViewModel.HiddenCubesSound));
    }

    [Theory]
    [InlineData(true, AggroColumnMode.Show)]
    [InlineData(false, AggroColumnMode.Auto)]
    public void Aggro_Column_Of_Older_Settings_Becomes_Show_Or_Auto(bool shown, AggroColumnMode expected)
    {
        using var fixture = new SettingsViewModelFixture(settings => settings.ShowAggroColumn = shown);

        Assert.Equal(expected, fixture.ViewModel.AggroColumn);
    }

    [Fact]
    public void EncounterTimeDisplayFormat_InvalidPersistedValueFallsBackToDecimalSeconds()
    {
        using var fixture = new SettingsViewModelFixture();
        File.WriteAllText(
            fixture.SettingsPath,
            """{"EncounterTimeDisplayFormat":999}""");

        var loaded = new SettingsService(fixture.SettingsPath).Current;

        Assert.Equal(
            EncounterTimeDisplayFormat.DecimalSeconds,
            loaded.EncounterTimeDisplayFormat);
    }

    [Fact]
    public void PersistedHotkeys_MustSatisfyTheInteractiveValidationContract()
    {
        using var fixture = new SettingsViewModelFixture(settings =>
        {
            settings.BattleResetHotkeyModifiers = (uint)HotkeyModifiers.None;
            settings.BattleResetHotkeyVirtualKey = 0x52;
            settings.OverlayInteractionHotkeyModifiers = (uint)HotkeyModifiers.Control;
            settings.OverlayInteractionHotkeyVirtualKey = 0x01;
        });

        Assert.Null(fixture.ViewModel.BattleResetHotkey);
        Assert.Null(fixture.ViewModel.OverlayInteractionHotkey);
        Assert.True(fixture.HotkeyService.AttachWindow(0x1234));
        Assert.Empty(fixture.HotkeyPlatform.ActiveRegistrations);
    }

    [Fact]
    public void BeginCaptureHotkey_SwitchesTarget_AndRejectsAStaleTarget()
    {
        using var fixture = new SettingsViewModelFixture();
        var viewModel = fixture.ViewModel;
        var definition = new HotkeyDefinition(HotkeyModifiers.Control, 0x49);

        viewModel.BeginCaptureHotkey(GlobalHotkeyAction.BattleReset);

        Assert.Equal(GlobalHotkeyAction.BattleReset, viewModel.CapturingHotkeyAction);
        Assert.True(viewModel.IsCapturingResetHotkey);

        viewModel.BeginCaptureHotkey(GlobalHotkeyAction.CycleOverlayInteraction);

        Assert.Equal(GlobalHotkeyAction.CycleOverlayInteraction, viewModel.CapturingHotkeyAction);
        Assert.False(viewModel.IsCapturingResetHotkey);
        Assert.True(viewModel.IsCapturingOverlayInteractionHotkey);
        Assert.False(viewModel.ApplyCapturedHotkey(GlobalHotkeyAction.BattleReset, definition));
        Assert.Equal(GlobalHotkeyAction.CycleOverlayInteraction, viewModel.CapturingHotkeyAction);
        Assert.Null(viewModel.BattleResetHotkey);

        Assert.True(viewModel.ApplyCapturedHotkey(GlobalHotkeyAction.CycleOverlayInteraction, definition));
        Assert.Null(viewModel.CapturingHotkeyAction);
        Assert.Equal(definition, viewModel.OverlayInteractionHotkey);
    }

    [Theory]
    [InlineData(GlobalHotkeyAction.BattleReset, GlobalHotkeyAction.CycleOverlayInteraction)]
    [InlineData(GlobalHotkeyAction.CycleOverlayInteraction, GlobalHotkeyAction.BattleReset)]
    public void ApplyCapturedHotkey_WhenCombinationConflicts_LatestActionWins(GlobalHotkeyAction firstAction, GlobalHotkeyAction latestAction)
    {
        using var fixture = new SettingsViewModelFixture();
        var viewModel = fixture.ViewModel;
        var definition = new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x48);

        viewModel.BeginCaptureHotkey(firstAction);
        Assert.True(viewModel.ApplyCapturedHotkey(firstAction, definition));
        viewModel.BeginCaptureHotkey(latestAction);
        Assert.True(viewModel.ApplyCapturedHotkey(latestAction, definition));

        Assert.Null(GetHotkey(viewModel, firstAction));
        Assert.Equal(definition, GetHotkey(viewModel, latestAction));

        var persisted = new SettingsService(fixture.SettingsPath).Current;
        Assert.Null(GetPersistedHotkey(persisted, firstAction));
        Assert.Equal(definition, GetPersistedHotkey(persisted, latestAction));
    }

    [Fact]
    public void ApplyCapturedHotkey_WhenConflictingNativeRegistrationFails_DoesNotPublishOrPersistRejectedBindings()
    {
        using var fixture = new SettingsViewModelFixture();
        var viewModel = fixture.ViewModel;
        var reset = new HotkeyDefinition(HotkeyModifiers.Control, 0x52);
        var interaction = new HotkeyDefinition(HotkeyModifiers.Alt, 0x49);
        nint hwnd = 0x1234;
        fixture.HotkeyService.AttachWindow(hwnd);
        viewModel.BeginCaptureHotkey(GlobalHotkeyAction.BattleReset);
        Assert.True(viewModel.ApplyCapturedHotkey(GlobalHotkeyAction.BattleReset, reset));
        viewModel.BeginCaptureHotkey(GlobalHotkeyAction.CycleOverlayInteraction);
        Assert.True(viewModel.ApplyCapturedHotkey(GlobalHotkeyAction.CycleOverlayInteraction, interaction));
        fixture.HotkeyPlatform.FailNextRegistration = true;

        viewModel.BeginCaptureHotkey(GlobalHotkeyAction.CycleOverlayInteraction);
        Assert.False(viewModel.ApplyCapturedHotkey(GlobalHotkeyAction.CycleOverlayInteraction, reset));

        Assert.Null(viewModel.CapturingHotkeyAction);
        Assert.Equal(reset, viewModel.BattleResetHotkey);
        Assert.Equal(interaction, viewModel.OverlayInteractionHotkey);
        Assert.True(viewModel.HasHotkeyRegistrationError);
        Assert.Equal(reset.Display, viewModel.ResetHotkeyDisplay);
        Assert.Equal(interaction.Display, viewModel.OverlayInteractionHotkeyDisplay);
        Assert.Equal(reset, fixture.HotkeyPlatform.ActiveRegistrations[(hwnd, GlobalHotkeyService.BattleResetHotkeyId)]);
        Assert.Equal(interaction, fixture.HotkeyPlatform.ActiveRegistrations[(hwnd, GlobalHotkeyService.CycleOverlayInteractionHotkeyId)]);

        var persisted = new SettingsService(fixture.SettingsPath).Current;
        Assert.Equal(reset, GetPersistedHotkey(persisted, GlobalHotkeyAction.BattleReset));
        Assert.Equal(interaction, GetPersistedHotkey(persisted, GlobalHotkeyAction.CycleOverlayInteraction));
    }

    [Fact]
    public void RefreshHotkeyRegistrationState_SurfacesStartupRegistrationFailure()
    {
        using var fixture = new SettingsViewModelFixture();
        var viewModel = fixture.ViewModel;
        var definition = new HotkeyDefinition(HotkeyModifiers.Control, 0x52);
        viewModel.BeginCaptureHotkey(GlobalHotkeyAction.BattleReset);
        Assert.True(viewModel.ApplyCapturedHotkey(GlobalHotkeyAction.BattleReset, definition));
        fixture.HotkeyPlatform.FailNextRegistration = true;

        var attached = fixture.HotkeyService.AttachWindow(0x1234);
        viewModel.RefreshHotkeyRegistrationState(fixture.HotkeyService.IsAttachedTo(0x1234));

        Assert.False(attached);
        Assert.True(viewModel.HasHotkeyRegistrationError);
        Assert.Equal(definition, viewModel.BattleResetHotkey);
    }

    [Fact]
    public void ClearHotkey_ClearsOnlyTheSelectedAction()
    {
        using var fixture = new SettingsViewModelFixture();
        var viewModel = fixture.ViewModel;
        var reset = new HotkeyDefinition(HotkeyModifiers.Control, 0x52);
        var interaction = new HotkeyDefinition(HotkeyModifiers.Alt, 0x49);
        viewModel.BeginCaptureHotkey(GlobalHotkeyAction.BattleReset);
        viewModel.ApplyCapturedHotkey(GlobalHotkeyAction.BattleReset, reset);
        viewModel.BeginCaptureHotkey(GlobalHotkeyAction.CycleOverlayInteraction);
        viewModel.ApplyCapturedHotkey(GlobalHotkeyAction.CycleOverlayInteraction, interaction);

        viewModel.ClearHotkey(GlobalHotkeyAction.CycleOverlayInteraction);

        Assert.Equal(reset, viewModel.BattleResetHotkey);
        Assert.Null(viewModel.OverlayInteractionHotkey);
        Assert.True(viewModel.HasResetHotkey);
        Assert.False(viewModel.HasOverlayInteractionHotkey);
    }

    // The hotkeys' texts ("None" while no key is set) follow a language change, each one: the Hotkeys tab shows them.
    [Fact]
    public void A_Language_Change_Renews_Every_Hotkey_Text()
    {
        // The language service sets this thread's UI culture and the process default: both go back afterwards.
        var culture = (CultureInfo.CurrentUICulture, CultureInfo.DefaultThreadCurrentUICulture);
        try
        {
            using var fixture = new SettingsViewModelFixture();
            var viewModel = fixture.ViewModel;
            var raised = new List<string?>();
            viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            viewModel.SelectedLanguage = viewModel.Languages.First(option => option.Code != viewModel.SelectedLanguage?.Code);

            Assert.Contains(nameof(SettingsFlyoutViewModel.MinimizeAllHotkeyDisplay), raised);
            Assert.Contains(nameof(SettingsFlyoutViewModel.ResetHotkeyDisplay), raised);
            Assert.Contains(nameof(SettingsFlyoutViewModel.OverlayInteractionHotkeyDisplay), raised);
        }
        finally
        {
            (CultureInfo.CurrentUICulture, CultureInfo.DefaultThreadCurrentUICulture) = culture;
        }
    }

    private static HotkeyDefinition? GetHotkey(SettingsFlyoutViewModel viewModel, GlobalHotkeyAction action) => action switch
    {
        GlobalHotkeyAction.BattleReset => viewModel.BattleResetHotkey,
        GlobalHotkeyAction.CycleOverlayInteraction => viewModel.OverlayInteractionHotkey,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    private static HotkeyDefinition? GetPersistedHotkey(AppSettings settings, GlobalHotkeyAction action)
    {
        var (modifiers, virtualKey) = action switch
        {
            GlobalHotkeyAction.BattleReset => (settings.BattleResetHotkeyModifiers, settings.BattleResetHotkeyVirtualKey),
            GlobalHotkeyAction.CycleOverlayInteraction => (settings.OverlayInteractionHotkeyModifiers, settings.OverlayInteractionHotkeyVirtualKey),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        return modifiers is { } mods && virtualKey is { } vk
            ? new HotkeyDefinition((HotkeyModifiers)mods, vk)
            : null;
    }

    private sealed class SettingsViewModelFixture : IDisposable
    {
        private readonly TemporarySettingsFile _settingsFile = new();
        private readonly LocalizationService _localization;
        private readonly PlayerNameDisplayService _playerNameDisplay;
        private readonly UiScaleService _uiScale;
        private readonly ProcessForegroundWatcher _processForegroundWatcher;

        // json: the settings file as the overlay finds it at its start (one an older version saved).
        public SettingsViewModelFixture(Action<AppSettings>? configureSettings = null, string? json = null)
        {
            var language = new LanguageService();
            _localization = new LocalizationService(language);
            if (json is not null)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_settingsFile.Path)!);
                File.WriteAllText(_settingsFile.Path, json);
            }

            var settings = new SettingsService(_settingsFile.Path);
            if (configureSettings is not null)
            {
                settings.Update(configureSettings);
            }
            _playerNameDisplay = new PlayerNameDisplayService(settings, _localization);
            _uiScale = new UiScaleService(settings);
            _processForegroundWatcher = new ProcessForegroundWatcher();
            HotkeyPlatform = new TestGlobalHotkeyPlatform();
            HotkeyService = new GlobalHotkeyService(HotkeyPlatform);
            ViewModel = new SettingsFlyoutViewModel(
                _localization,
                language,
                settings,
                _playerNameDisplay,
                _uiScale,
                new AppUpdateService(),
                _processForegroundWatcher,
                HotkeyService);
        }

        public string SettingsPath => _settingsFile.Path;

        public TestGlobalHotkeyPlatform HotkeyPlatform { get; }

        public GlobalHotkeyService HotkeyService { get; }

        public SettingsFlyoutViewModel ViewModel { get; }

        public void Dispose()
        {
            HotkeyService.DetachWindow();
            _processForegroundWatcher.Dispose();
            _uiScale.Dispose();
            _playerNameDisplay.Dispose();
            _localization.Dispose();
            _settingsFile.Dispose();
        }
    }

    private sealed class TestGlobalHotkeyPlatform : IGlobalHotkeyPlatform
    {
        public Dictionary<(nint Hwnd, int Id), HotkeyDefinition> ActiveRegistrations { get; } = [];

        public bool FailNextRegistration { get; set; }

        public bool Register(nint hwnd, int id, HotkeyDefinition definition)
        {
            if (FailNextRegistration)
            {
                FailNextRegistration = false;
                return false;
            }

            ActiveRegistrations[(hwnd, id)] = definition;
            return true;
        }

        public bool Unregister(nint hwnd, int id) => ActiveRegistrations.Remove((hwnd, id));
    }

    private sealed class TemporarySettingsFile : IDisposable
    {
        public TemporarySettingsFile()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Aion2Flow.Tests", $"{Guid.NewGuid():N}.json");
        }

        public string Path { get; }

        public void Dispose()
        {
            File.Delete(Path);
            File.Delete(Path + ".tmp");
        }
    }
}
