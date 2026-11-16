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

    [Fact]
    public void HideHeaderWhenClickThrough_LoadsAndPersistsThroughViewModel()
    {
        using var fixture = new SettingsViewModelFixture(static settings => settings.HideHeaderWhenClickThrough = true);

        Assert.True(fixture.ViewModel.HideHeaderWhenClickThrough);

        fixture.ViewModel.HideHeaderWhenClickThrough = false;

        Assert.False(new SettingsService(fixture.SettingsPath).Current.HideHeaderWhenClickThrough);
    }

    // Which frames take clicks while the game has focus: the twin library and the playback as before, the others not; a
    // toggle adds or takes one away, and it's kept.
    [Fact]
    public void ClickableWithoutFocus_Is_Per_Frame_And_Kept()
    {
        using var fixture = new SettingsViewModelFixture(static _ => { });
        var settings = fixture.ViewModel;
        Assert.Equal([SettingsFlyoutViewModel.TwinLibraryKey, SettingsFlyoutViewModel.PlaybackKey],
            SettingsFlyoutViewModel.ClickableFrames.Where(settings.IsClickableWithoutFocus));

        settings.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.FightGraphsKey);
        settings.ToggleClickableWithoutFocus(SettingsFlyoutViewModel.TwinLibraryKey);

        Assert.Equal([SettingsFlyoutViewModel.PlaybackKey, SettingsFlyoutViewModel.FightGraphsKey], new SettingsService(fixture.SettingsPath).Current.ClickableWithoutFocus);
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
            settings.SkillMonitorWidth = 740;
        });

        Assert.False(fixture.ViewModel.SkillMonitorEnabled);

        fixture.ViewModel.SkillMonitorEnabled = true;

        var persisted = new SettingsService(fixture.SettingsPath).Current;
        Assert.True(persisted.SkillMonitorEnabled);
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
        Assert.EndsWith("/11", vm.MainMetricVisibilityDisplay);
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

        public SettingsViewModelFixture(Action<AppSettings>? configureSettings = null)
        {
            var language = new LanguageService();
            _localization = new LocalizationService(language);
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
