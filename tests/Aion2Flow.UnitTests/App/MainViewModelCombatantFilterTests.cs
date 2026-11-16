using Avalonia.Media;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.Presentation;
using Cloris.Aion2Flow.Resources.Catalog;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Playback;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

public sealed class MainViewModelCombatantFilterTests
{
    [Fact]
    public void EncounterTimeDisplayFormat_PropagatesToCombatantDetails()
    {
        var fixture = MainViewModelFixture.Create();

        fixture.Settings.EncounterTimeDisplayFormat =
            EncounterTimeDisplayFormat.MinutesSeconds;

        Assert.Equal(
            EncounterTimeDisplayFormat.MinutesSeconds,
            fixture.ViewModel.CombatantDetails
                .OutgoingDetail.EncounterTimeDisplayFormat);
        Assert.Equal(
            EncounterTimeDisplayFormat.MinutesSeconds,
            fixture.ViewModel.CombatantDetails
                .IncomingDetail.EncounterTimeDisplayFormat);
    }

    [Fact]
    public void LivePlaybackContextUsesSessionBoundSource()
    {
        var fixture = MainViewModelFixture.Create();

        var context = fixture.ViewModel.CreateLivePlaybackContext();

        Assert.Equal(ScenePlaybackSourceKind.Live, context.Source.SourceKind);
        Assert.Equal(fixture.SceneId, context.Source.EncounterId);
        Assert.Same(fixture.MetadataRegistry, context.DisplayContext.MetadataRegistry);
        Assert.True(context.Source.CreateTimelineSegment().IsLiveGrowing);
        context.Source.Dispose();
    }

    [Fact]
    public void LivePlaybackContextRemainsLiveWhenArchivedSceneIsSelected()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Archived Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.ViewModel.ArchiveCurrentEncounterCommand.Execute(null);
        var history = Assert.Single(fixture.ViewModel.EncounterHistory);
        fixture.ViewModel.SelectedEncounterHistory = history;

        var context = fixture.ViewModel.CreateLivePlaybackContext();

        Assert.Equal(ScenePlaybackSourceKind.Live, context.Source.SourceKind);
        Assert.Equal(fixture.SceneId, context.Source.EncounterId);
        Assert.Same(fixture.MetadataRegistry, context.DisplayContext.MetadataRegistry);
        Assert.True(context.Source.CreateTimelineSegment().IsLiveGrowing);
        context.Source.Dispose();
    }

    [Fact]
    public void ShouldDisplayCombatant_Hides_Known_Npc_Even_If_Class_Was_Previously_Inferred()
    {
        const int npcInstanceId = 19945;
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNpc(npcInstanceId, 2100350, NpcKind.Monster);
        fixture.AppendSceneDamage(npcInstanceId, 100, 11000010, 200, 1_000, 1);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.DoesNotContain(fixture.ViewModel.Combatants, x => x.Id == npcInstanceId);
    }

    [Fact]
    public void ShouldDisplayCombatant_Hides_Combatants_Without_Player_Class()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneDamage(38924, 100, 999_999, 200, 1_000, 1);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.DoesNotContain(fixture.ViewModel.Combatants, x => x.Id == 38924);
    }

    [Fact]
    public void ShouldDisplayCombatant_Keeps_Player_Class_When_Not_Npc()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(12669, "Player", 400, 1_000, 2_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Contains(fixture.ViewModel.Combatants, x => x.Id == 12669);
    }

    [Fact]
    public void RefreshCombatStats_AllScope_UsesExistingVisiblePlayerLogic()
    {
        var fixture = MainViewModelFixture.Create();
        AppendScopedCombatants(fixture);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal([100, 200, 300, 400], fixture.ViewModel.Combatants.Select(static row => row.Id));
    }

    [Fact]
    public void RefreshCombatStats_SelfScope_ShowsOnlyLocalPlayer()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantStatisticsScope = CombatantStatisticsScope.Self;
        AppendScopedCombatants(fixture);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal([100], fixture.ViewModel.Combatants.Select(static row => row.Id));
    }

    [Fact]
    public void RefreshCombatStats_SelfScope_UsesLocalPlayerIdentityWithoutClass()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantStatisticsScope = CombatantStatisticsScope.Self;
        fixture.AppendSceneIdentity(100, "Self", isLocalPlayer: true);
        fixture.AppendSceneIdentity(200, "Other");
        fixture.AppendSceneDamage(100, 900_002, 999_999, 500, 3_000, 1);
        fixture.AppendSceneDamage(200, 900_002, 11000010, 1_000, 3_100, 2);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Equal(100, row.Id);
        Assert.Null(row.CharacterClass);
    }

    [Fact]
    public void RefreshCombatStats_PartyScope_ShowsLocalPlayerAndPartyMembers()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantStatisticsScope = CombatantStatisticsScope.Party;
        AppendScopedCombatants(fixture);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal([100, 200], fixture.ViewModel.Combatants.Select(static row => row.Id));
    }

    [Fact]
    public void RefreshCombatStats_ForceScope_ShowsLocalPlayerPartyAndForceMembers()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantStatisticsScope = CombatantStatisticsScope.Force;
        AppendScopedCombatants(fixture);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal([100, 200, 300], fixture.ViewModel.Combatants.Select(static row => row.Id));
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_DisplaysSceneSnapshot()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Equal(300, row.Id);
        Assert.Equal(400, row.Damage);
        Assert.Equal(TimeSpan.FromSeconds(2), fixture.ViewModel.EncounterDuration);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_UsesSceneDisplayName()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Name", 400, 3_000, 5_000);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Equal("Scene Name", fixture.ViewModel.DisplayContext!.ResolveEntityName(row.Id));
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_RebuildsDisplayContextWhenMetadataChanges()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        var contextBeforeMetadata = fixture.ViewModel.DisplayContext;

        fixture.MetadataRegistry.UpsertPcMetadata(300, "Scene Player", legionName: "Aether");
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.NotSame(contextBeforeMetadata, fixture.ViewModel.DisplayContext);
        Assert.True(fixture.ViewModel.DisplayContext!.TryResolvePcMetadata(300, out var metadata));
        Assert.Equal("Aether", metadata.LegionName);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_FiltersNpcFromSceneStore()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.AppendSceneNpc(900_002, 2_100_350, NpcKind.Monster);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Equal(300, row.Id);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_UsesSceneBossFocus()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 25_000, 50_000, 5_500);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var focus = Assert.Single(fixture.ViewModel.BossFocuses);
        Assert.Equal(900_002, focus.InstanceId);
        Assert.Equal("NPC-2100351", fixture.ViewModel.DisplayContext!.ResolveNpcName(focus.InstanceId));
        Assert.Equal(25_000, focus.Hp);
        Assert.Equal(50_000, focus.MaxHp);
    }

    // A boss fight's title names the boss where the game data knows it ("Boss #1 (Kromede's Desolation)"); an NPC it
    // doesn't know keeps the number alone (as in the other tests here).
    [Fact]
    public void Boss_Fight_Title_Names_The_Boss()
    {
        var fixture = MainViewModelFixture.Create();
        var localization = fixture.ViewModel.Localization;
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.AppendSceneBossFocus(900_002, 2_300_261, NpcKind.Boss, 50_000, 50_000, 5_500);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 1_000, 6_000, 3);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var name = fixture.ViewModel.DisplayContext!.ResolveNpcCodeName(2_300_261);
        Assert.DoesNotContain("NPC-", name);
        Assert.True(fixture.ViewModel.CurrentBossFight.IsVisible);
        Assert.Equal(string.Format(localization["BossFight_Title"], $"1 ({name})"), fixture.ViewModel.CurrentBossFight.Title);
    }

    // The fight graphs follow the meter's pulls on the frame's clock: the first frame looks right away, later ones once a
    // second (a pull that starts after the frame opened shows up within a second).
    [Fact]
    public void Fight_Graphs_Pick_Up_A_Live_Pull_Within_A_Second()
    {
        var fixture = MainViewModelFixture.Create();
        var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-test-{Guid.NewGuid():N}.json"));
        var graphs = new FightGraphsViewModel(fixture.ViewModel, fixture.Settings,
            new PlayerNameDisplayService(settingsService, fixture.ViewModel.Localization), settingsService);
        var now = Environment.TickCount64;
        graphs.RefreshIfDue(now);
        Assert.False(graphs.HasFight);

        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 50_000, 50_000, 5_500);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 1_000, 6_000, 3);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        graphs.RefreshIfDue(now + 500);
        Assert.False(graphs.HasFight);

        graphs.RefreshIfDue(now + 1_000);
        Assert.True(graphs.HasFight);
        Assert.Equal(0, graphs.SelectedFightIndex);
    }

    // Minimized or on another tab, the fight graphs follow the pulls, the pick and the banner as a frame on view does, but
    // don't work out the live pull still going on; the first frame they show again works it out at once, not a second later.
    [Fact]
    public void Fight_Graphs_Work_Out_No_Live_Pull_Unseen_And_Catch_Up_When_Shown()
    {
        var fixture = MainViewModelFixture.Create();
        var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-test-{Guid.NewGuid():N}.json"));
        var names = new PlayerNameDisplayService(settingsService, fixture.ViewModel.Localization);
        var seen = new FightGraphsViewModel(fixture.ViewModel, fixture.Settings, names, settingsService);
        var otherTab = new FightGraphsViewModel(fixture.ViewModel, fixture.Settings, names, settingsService) { Tab = FightGraphsTab.Best };
        var building = typeof(FightGraphsViewModel).GetField("_building", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var buildingFight = typeof(FightGraphsViewModel).GetField("_buildingFight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 50_000, 50_000, 5_500);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 1_000, 6_000, 3);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        var now = Environment.TickCount64;

        seen.RefreshIfDue(now);
        otherTab.RefreshIfDue(now);
        Assert.NotNull(building.GetValue(seen));
        Assert.Null(building.GetValue(otherTab));
        Assert.Equal(Shown(seen), Shown(otherTab));

        otherTab.Tab = FightGraphsTab.Fight;
        otherTab.RefreshIfDue(now + 1);
        Assert.NotNull(building.GetValue(otherTab));
        Assert.Equal(buildingFight.GetValue(seen), buildingFight.GetValue(otherTab));

        // Minimized the same.
        fixture.Settings.ToggleMinimized(SettingsFlyoutViewModel.FightGraphsKey);
        var minimized = new FightGraphsViewModel(fixture.ViewModel, fixture.Settings, names, settingsService);
        minimized.RefreshIfDue(now);
        Assert.Null(building.GetValue(minimized));
        Assert.Equal(Shown(seen), Shown(minimized));
        minimized.RefreshIfDue(now + 1);
        Assert.Null(building.GetValue(minimized));
        fixture.Settings.ToggleMinimized(SettingsFlyoutViewModel.FightGraphsKey);
        minimized.RefreshIfDue(now + 2);
        Assert.NotNull(building.GetValue(minimized));
        Assert.Equal(buildingFight.GetValue(seen), buildingFight.GetValue(minimized));

        static string Shown(FightGraphsViewModel graphs) =>
            string.Join("|", string.Join(",", graphs.Fights), graphs.SelectedFightIndex, graphs.HasFight, graphs.StatusText, graphs.BannerText, graphs.IsPullStored, graphs.StoreStatus);
    }

    // Unseen, a pull that's over is still worked out (once, as before), and so is a history entry's pull, even one saved
    // while it went on: their graphs are final when the frame shows again. Only the live pull still going on waits.
    [Fact]
    public void Fight_Graphs_Still_Work_Out_Pulls_That_Are_Over_Unseen()
    {
        var building = typeof(FightGraphsViewModel).GetField("_building", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var now = Environment.TickCount64;

        var killed = MainViewModelFixture.Create();
        killed.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        killed.AppendSceneBossFocus(900_002, "Scene Boss", 50_000, 50_000, 5_500);
        killed.AppendSceneDamage(300, 900_002, 11000010, 1_000, 6_000, 3);
        killed.AppendSceneHp(900_002, 0, 7_000, 4);
        killed.ViewModel.RefreshCombatStatsForTesting();
        Assert.Equal(global::Cloris.Aion2Flow.SceneRuntime.Combat.BossFightOutcome.Killed, killed.ViewModel.CreateFightGraphSource().Fights[^1].Outcome);
        var unseenKill = Unseen(killed);
        unseenKill.RefreshIfDue(now);
        Assert.True(unseenKill.HasFight);
        Assert.NotNull(building.GetValue(unseenKill));

        var saved = MainViewModelFixture.Create();
        saved.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        saved.AppendSceneBossFocus(900_002, "Scene Boss", 50_000, 50_000, 5_500);
        saved.AppendSceneDamage(300, 900_002, 11000010, 1_000, 6_000, 3);
        saved.ViewModel.RefreshCombatStatsForTesting();
        saved.ViewModel.ArchiveCurrentEncounterCommand.Execute(null);
        Assert.Equal(global::Cloris.Aion2Flow.SceneRuntime.Combat.BossFightOutcome.Ongoing, saved.ViewModel.SelectedEncounterHistory!.Record.ScenePayload.BossFights[^1].Outcome);
        var unseenHistory = Unseen(saved);
        unseenHistory.RefreshIfDue(now);
        Assert.True(unseenHistory.HasFight);
        Assert.NotNull(building.GetValue(unseenHistory));

        static FightGraphsViewModel Unseen(MainViewModelFixture fixture)
        {
            var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"aion2flow-test-{Guid.NewGuid():N}.json"));
            return new FightGraphsViewModel(fixture.ViewModel, fixture.Settings, new PlayerNameDisplayService(settingsService, fixture.ViewModel.Localization), settingsService)
            {
                Tab = FightGraphsTab.Best,
            };
        }
    }

    [Fact]
    public void RefreshCombatStats_Wipe_Stays_Under_The_Boss_Bar_Until_The_Next_Pull()
    {
        var fixture = MainViewModelFixture.Create();
        var localization = fixture.ViewModel.Localization;
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 50_000, 50_000, 5_500);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 1_000, 6_000, 3);
        fixture.AppendSceneHp(900_002, 49_000, 6_100, 4);
        fixture.AppendSceneHp(900_002, 50_000, 9_000, 5);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var section = fixture.ViewModel.CurrentBossFight;
        Assert.True(section.IsVisible);
        Assert.Equal(string.Format(localization["BossFight_FailedAttempt"], 1, 1), section.Title);
        Assert.Equal(1_000, Assert.Single(section.Rows).Damage);
        Assert.Empty(fixture.ViewModel.BossFights);

        // The boss bar times out while the group runs back; the wipe stays up.
        fixture.ExpireBossFocusData();
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.Empty(fixture.ViewModel.BossFocuses);
        Assert.True(section.IsVisible);
        Assert.Equal(string.Format(localization["BossFight_FailedAttempt"], 1, 1), section.Title);

        fixture.AppendSceneDamage(300, 900_002, 11000010, 2_000, 12_000, 6);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal(string.Format(localization["BossFight_Title"], 1), section.Title);
        Assert.Equal(2_000, Assert.Single(section.Rows).Damage);
        var failed = Assert.Single(fixture.ViewModel.BossFights);
        Assert.Equal(string.Format(localization["BossFight_FailedAttempt"], 1, 1), failed.Title);
        Assert.False(failed.IsExpanded);
        Assert.Equal(1_000, Assert.Single(failed.Rows).Damage);

        // A fight's row opens the detail of that fight only, the table's row the whole encounter. The table keeps the
        // selection either way (it lets go of rows it doesn't hold).
        var details = fixture.ViewModel.CombatantDetails;
        var tableRow = Assert.Single(fixture.ViewModel.Combatants);
        fixture.ViewModel.SelectCombatantRow(Assert.Single(failed.Rows));
        Assert.Same(tableRow, fixture.ViewModel.SelectedCombatant);
        Assert.Equal(1_000, details.OutgoingDamage.Total);
        Assert.Equal(failed.Title, details.ScopeText);

        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.Equal(1_000, details.OutgoingDamage.Total);

        fixture.ViewModel.SelectCombatantRow(tableRow);
        Assert.Equal(3_400, details.OutgoingDamage.Total);
        Assert.Null(details.ScopeText);
    }

    [Fact]
    public void The_Bottom_Bar_Gets_The_Boss_Sections_Dps_Check_In_Short()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.ViewModel.SettingsFlyout.BossTimeLimitMinutes = 5;
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 50_000, 50_000, 5_500);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 1_000, 6_000, 3);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 2_000, 8_000, 4);
        fixture.AppendSceneHp(900_002, 47_000, 8_100, 5);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var section = fixture.ViewModel.CurrentBossFight;
        Assert.True(section.HasDpsCheck);
        Assert.False(section.IsBehind);
        Assert.Equal("(+1.34k)", section.DpsGapShortText); // 3,000 in 2 s against 47,000 HP in 298 s: 1,500/s - 157.7/s

        fixture.AppendSceneDamage(300, 900_002, 11000010, 1_000, 108_000, 6);
        fixture.AppendSceneHp(900_002, 46_000, 108_100, 7);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.True(section.IsBehind);
        Assert.Equal("(−193)", section.DpsGapShortText); // 4,000 in 102 s against 46,000 HP in 198 s: 39.2/s - 232.3/s

        fixture.AppendSceneDamage(300, 900_002, 11000010, 1_000, 308_000, 8);
        fixture.AppendSceneHp(900_002, 45_000, 308_100, 9);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.True(section.IsBehind);
        Assert.Equal("(−∞)", section.DpsGapShortText);

        fixture.ViewModel.SettingsFlyout.ShowFocusStatusBar = false; // no section under the bar, no gap below
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.False(section.HasDpsCheck);
    }

    [Fact]
    public void RefreshCombatStats_BossSceneWithoutBossFocus_HidesBossShareColumn()
    {
        var fixture = MainViewModelFixture.Create(SceneKind.Boss);
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Empty(fixture.ViewModel.BossFocuses);
        Assert.False(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
    }

    [Fact]
    public void BossShareColumn_Can_Be_Turned_Off_In_The_Settings()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 700, 1_000, 5_500);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);

        fixture.Settings.ShowBossShareColumn = false;
        Assert.False(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.False(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);

        fixture.Settings.ShowBossShareColumn = true;
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
    }

    [Fact]
    public void RefreshCombatStats_StandardScene_BossFocusTimeoutHidesBossShareColumn()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 700, 1_000, 5_500);

        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);

        fixture.ExpireBossFocusData();
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Empty(fixture.ViewModel.BossFocuses);
        Assert.False(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        Assert.Equal(0d, Assert.Single(fixture.ViewModel.Combatants).BossShareRatio);
    }

    // A killed boss leaves the bar 10 s after it was last seen, but its share column stays, as in the history (a boss
    // that is merely out of sight takes it along, see above).
    [Fact]
    public void RefreshCombatStats_StandardScene_KilledBossKeepsBossShareColumnAfterFocusTimeout()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 3_000, 1);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 0, 1_000, 3_500);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        var share = Assert.Single(fixture.ViewModel.Combatants).BossShareRatio;
        Assert.True(share > 0);

        fixture.ExpireBossFocusData();
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Empty(fixture.ViewModel.BossFocuses);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        Assert.Equal(share, Assert.Single(fixture.ViewModel.Combatants).BossShareRatio);
    }

    [Fact]
    public void RefreshCombatStats_FrozenBossScene_KeepsBossShareColumnAfterFocusTimeout()
    {
        var fixture = MainViewModelFixture.Create(SceneKind.Boss);
        var sink = fixture.CreateLiveSink();
        var origin = fixture.SceneStartedMilliseconds;
        var metadataSource = new PacketObservationSource(origin + 100, 0, 0, 0, 100, default);
        sink.AppendNickname(in metadataSource, 100, "Player", characterClass: CharacterClass.Gladiator);
        sink.AppendNpcCode(in metadataSource, 300, 2_100_002);
        sink.AppendNpcKind(in metadataSource, 300, NpcKind.Boss);

        AppendDamage(sink, 100, 300, 11000010, 700, origin + 1_000, 1);
        sink.CompleteFlush(1);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal(BossSceneState.Recording, fixture.BossState);
        var beforeFreeze = Assert.Single(fixture.ViewModel.BossFocuses);
        Assert.Single(fixture.ViewModel.Combatants);

        var deathSource = new PacketObservationSource(origin + 2_000, 2, 0, 0, 2_000, default);
        sink.AppendNpcHp(in deathSource, 300, 0, 1_000);
        sink.CompleteFlush(2);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal(BossSceneState.Frozen, fixture.BossState);
        Assert.Single(fixture.ViewModel.BossFocuses);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        var beforeTimeoutShare = Assert.Single(fixture.ViewModel.Combatants).BossShareRatio;

        fixture.ExpireBossFocusData();
        fixture.ViewModel.RefreshCombatStatsForTesting();

        var afterTimeout = Assert.Single(fixture.ViewModel.BossFocuses);
        Assert.Equal(beforeFreeze.InstanceId, afterTimeout.InstanceId);
        Assert.Equal(BossSceneState.Frozen, fixture.BossState);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        Assert.Equal(beforeTimeoutShare, Assert.Single(fixture.ViewModel.Combatants).BossShareRatio);
    }

    [Fact]
    public void RefreshCombatStats_FrozenBossScene_RestoresFocusAfterViewingArchive()
    {
        var fixture = MainViewModelFixture.Create(SceneKind.Boss);
        fixture.AppendSceneEncounter(901, "Archived Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.ViewModel.ArchiveCurrentEncounterCommand.Execute(null);
        var history = Assert.Single(fixture.ViewModel.EncounterHistory);
        fixture.ViewModel.ReturnToLiveCommand.Execute(null);

        var sink = fixture.CreateLiveSink();
        var origin = fixture.SceneStartedMilliseconds;
        var metadataSource = new PacketObservationSource(origin + 100, 0, 0, 0, 100, default);
        sink.AppendNickname(in metadataSource, 100, "Player", characterClass: CharacterClass.Gladiator);
        sink.AppendNpcCode(in metadataSource, 300, 2_100_002);
        sink.AppendNpcKind(in metadataSource, 300, NpcKind.Boss);
        AppendDamage(sink, 100, 300, 11000010, 700, origin + 1_000, 1);
        sink.CompleteFlush(1);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        var deathSource = new PacketObservationSource(origin + 2_000, 2, 0, 0, 2_000, default);
        sink.AppendNpcHp(in deathSource, 300, 0, 1_000);
        sink.CompleteFlush(2);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.ExpireBossFocusData();
        fixture.ViewModel.RefreshCombatStatsForTesting();

        var expectedFocus = Assert.Single(fixture.ViewModel.BossFocuses);
        var expectedShare = Assert.Single(fixture.ViewModel.Combatants).BossShareRatio;

        fixture.ViewModel.SelectedEncounterHistory = history;
        fixture.ViewModel.ReturnToLiveCommand.Execute(null);

        var restoredFocus = Assert.Single(fixture.ViewModel.BossFocuses);
        Assert.Equal(expectedFocus.InstanceId, restoredFocus.InstanceId);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        Assert.Equal(expectedShare, Assert.Single(fixture.ViewModel.Combatants).BossShareRatio);
    }

    [Fact]
    public void RefreshCombatStats_FocusStatusSetting_HidesOnlyFocusRows()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 25_000, 50_000, 5_500);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Single(fixture.ViewModel.BossFocuses);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);

        fixture.Settings.ShowFocusStatusBar = false;
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Empty(fixture.ViewModel.BossFocuses);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);

        fixture.Settings.ShowFocusStatusBar = true;
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Single(fixture.ViewModel.BossFocuses);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
    }

    [Fact]
    public void RefreshCombatStats_ScopeSelf_HidesTrainingDummyFocusActivatedOnlyByOutOfScopeCombatant()
    {
        const int dummyNpcCode = 2_400_032;
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantStatisticsScope = CombatantStatisticsScope.Self;
        fixture.AppendSceneIdentity(100, "Self", isLocalPlayer: true);
        fixture.AppendSceneIdentity(400, "Other");
        fixture.AppendSceneDamage(400, 900_002, 11000010, 400, 3_000, 1);
        fixture.AppendSceneBossFocus(900_002, dummyNpcCode, NpcKind.TrainingDummy, 80_000, 100_000, 3_500);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Empty(fixture.ViewModel.Combatants);
        Assert.Empty(fixture.ViewModel.BossFocuses);
    }

    [Fact]
    public void RefreshCombatStats_ScopeSelf_HidesOutOfScopeBossFocusAndBossShare()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantStatisticsScope = CombatantStatisticsScope.Self;
        fixture.AppendSceneIdentity(100, "Self", isLocalPlayer: true);
        fixture.AppendSceneIdentity(400, "Other");
        fixture.AppendSceneDamage(100, 900_010, 11000010, 600, 2_500, 1);
        fixture.AppendSceneDamage(400, 900_002, 11000010, 400, 3_000, 2);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 700, 1_000, 3_500);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Equal(100, row.Id);
        Assert.Empty(fixture.ViewModel.BossFocuses);
        Assert.False(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
    }

    [Fact]
    public void RefreshCombatStats_ScopeSelf_ShowsBossFocusActivatedByLocalCombatant()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantStatisticsScope = CombatantStatisticsScope.Self;
        fixture.AppendSceneIdentity(100, "Self", isLocalPlayer: true);
        fixture.AppendSceneIdentity(400, "Other");
        fixture.AppendSceneDamage(100, 900_002, 11000010, 600, 2_500, 1);
        fixture.AppendSceneDamage(400, 900_003, 11000010, 400, 3_000, 2);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 700, 1_000, 3_500);
        fixture.AppendSceneBossFocus(900_003, "Other Boss", 700, 1_000, 3_600);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        var focus = Assert.Single(fixture.ViewModel.BossFocuses);
        Assert.Equal(100, row.Id);
        Assert.Equal(900_002, focus.InstanceId);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
    }

    [Fact]
    public void RefreshCombatStats_ScopeSelf_ShowsBossFocusActivatedByClasslessLocalCombatant()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantStatisticsScope = CombatantStatisticsScope.Self;
        fixture.AppendSceneIdentity(100, "Self", isLocalPlayer: true);
        fixture.AppendSceneDamage(100, 900_002, 999_999, 300, 2_500, 1);
        fixture.AppendSceneDamage(100, 900_002, 999_999, 300, 2_600, 2);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 700, 1_000, 3_500);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        var focus = Assert.Single(fixture.ViewModel.BossFocuses);
        Assert.Equal(100, row.Id);
        Assert.Null(row.CharacterClass);
        Assert.Equal(900_002, focus.InstanceId);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
    }

    [Fact]
    public void RefreshCombatStats_ScopeSelf_OutOfScopeDamageDoesNotRefreshBossFocusTimeout()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantStatisticsScope = CombatantStatisticsScope.Self;
        fixture.AppendSceneIdentity(100, "Self", isLocalPlayer: true);
        fixture.AppendSceneIdentity(400, "Other");
        fixture.AppendSceneDamage(100, 900_002, 11000010, 600, 1_000, 1);
        fixture.AppendSceneDamage(400, 900_002, 11000010, 400, 12_000, 2);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 700, 1_000, 12_000);
        var beforeExpiry = fixture.CreateSceneSnapshot();
        Assert.Equal(1_000, Assert.Single(beforeExpiry.BossFocuses).LastObservedAtMilliseconds);
        fixture.ExpireBossFocusDataAt(12_001);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Equal(100, row.Id);
        Assert.Empty(fixture.ViewModel.BossFocuses);
        Assert.False(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_DoesNotDisplayTrainingDummyFocus()
    {
        const int dummyNpcCode = 2_400_032;
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.AppendSceneBossFocus(900_002, dummyNpcCode, NpcKind.TrainingDummy, 80_000, 100_000, 5_000);
        fixture.AppendSceneBossFocus(900_003, dummyNpcCode, NpcKind.TrainingDummy, 60_000, 100_000, 5_500);
        fixture.AppendSceneBossFocus(900_004, dummyNpcCode, NpcKind.TrainingDummy, 40_000, 100_000, 5_200);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Empty(fixture.ViewModel.BossFocuses);
        Assert.Equal(3, fixture.ViewModel.DisplayContext!.Snapshot.BossFocuses.Count);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_TrainingDummyFocusDoesNotShowBossShareColumn()
    {
        const int dummyNpcCode = 2_400_032;
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 100, 3_000, 1);
        fixture.AppendSceneDamage(300, 900_003, 11000010, 200, 3_100, 2);
        fixture.AppendSceneDamage(300, 900_004, 11000010, 300, 3_200, 3);
        fixture.AppendSceneBossFocus(900_002, dummyNpcCode, NpcKind.TrainingDummy, 80_000, 100_000, 5_000);
        fixture.AppendSceneBossFocus(900_003, dummyNpcCode, NpcKind.TrainingDummy, 60_000, 100_000, 5_500);
        fixture.AppendSceneBossFocus(900_004, dummyNpcCode, NpcKind.TrainingDummy, 40_000, 100_000, 5_200);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.False(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        Assert.Equal(0d, row.BossShareRatio);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_CalculatesRelativeDpsBars()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneNickname(301, "Second");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 3_000, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 5_000, 2);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 250, 3_000, 3);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 250, 5_000, 4);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal([300, 301], fixture.ViewModel.Combatants.Select(static row => row.Id));
        var firstBar = fixture.ViewModel.Combatants[0].BarSegment;
        var secondBar = fixture.ViewModel.Combatants[1].BarSegment;
        Assert.True(firstBar.HasValue);
        Assert.True(secondBar.HasValue);
        Assert.Equal(1d, firstBar.Value.Ratio, 6);
        Assert.Equal(0.5d, secondBar.Value.Ratio, 6);
        Assert.NotNull(firstBar.Value.Brush);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_TotalDamageBarMode_UsesStableColors()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantSortMetric = CombatantSortMetric.TotalDamage;
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneNickname(301, "Second");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 3_000, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 5_000, 2);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 250, 3_000, 3);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 250, 5_000, 4);

        fixture.ViewModel.RefreshCombatStatsForTesting();
        var firstColor = fixture.ViewModel.Combatants[0].BarSegment!.Value.Brush;
        var secondColor = fixture.ViewModel.Combatants[1].BarSegment!.Value.Brush;

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal([300, 301], fixture.ViewModel.Combatants.Select(static row => row.Id));
        Assert.Equal(1d, fixture.ViewModel.Combatants[0].BarSegment!.Value.Ratio, 6);
        Assert.Equal(0.5d, fixture.ViewModel.Combatants[1].BarSegment!.Value.Ratio, 6);
        Assert.Same(firstColor, fixture.ViewModel.Combatants[0].BarSegment!.Value.Brush);
        Assert.Same(secondColor, fixture.ViewModel.Combatants[1].BarSegment!.Value.Brush);
    }

    [Theory]
    [InlineData(CombatantSortMetric.DamagePerSecond, new[] { 300, 301, 302, 303 })]
    [InlineData(CombatantSortMetric.TotalDamage, new[] { 300, 301, 302, 303 })]
    [InlineData(CombatantSortMetric.HealingPerSecond, new[] { 302, 303, 300, 301 })]
    [InlineData(CombatantSortMetric.TotalHealing, new[] { 302, 303, 300, 301 })]
    public void RefreshCombatStats_SceneMode_SortMetric_OrdersRowsAndScalesBars(CombatantSortMetric sortMetric, int[] expectedOrder)
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantSortMetric = sortMetric;
        fixture.AppendSceneNickname(300, "Gladiator");
        fixture.AppendSceneNickname(301, "Second Gladiator");
        fixture.AppendSceneNickname(302, "Cleric");
        fixture.AppendSceneNickname(303, "Second Cleric");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 3_000, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 5_000, 2);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 250, 3_000, 3);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 250, 5_000, 4);
        fixture.AppendSceneHealing(302, 300, 17000010, 400, 3_000, 5);
        fixture.AppendSceneHealing(302, 300, 17000010, 400, 5_000, 6);
        fixture.AppendSceneHealing(303, 301, 17000010, 200, 3_000, 7);
        fixture.AppendSceneHealing(303, 301, 17000010, 200, 5_000, 8);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var rows = fixture.ViewModel.Combatants;
        Assert.Equal(expectedOrder, rows.Select(static row => row.Id));
        Assert.Equal(1d, rows[0].BarSegment!.Value.Ratio, 6);
        Assert.Equal(0.5d, rows[1].BarSegment!.Value.Ratio, 6);
        Assert.Null(rows[2].BarSegment);
        Assert.Null(rows[3].BarSegment);
    }

    // A click on a column's head sorts the meter by it, the highest first; a second click the other way round; another
    // column the highest first again.
    [Fact]
    public void SortBy_A_Column_Twice_Turns_The_Order_Round()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "Gladiator");
        fixture.AppendSceneNickname(301, "Second Gladiator");
        fixture.AppendSceneNickname(302, "Cleric");
        fixture.AppendSceneNickname(303, "Second Cleric");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 3_000, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 5_000, 2);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 250, 3_000, 3);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 250, 5_000, 4);
        fixture.AppendSceneHealing(302, 300, 17000010, 400, 3_000, 5);
        fixture.AppendSceneHealing(302, 300, 17000010, 400, 5_000, 6);
        fixture.AppendSceneHealing(303, 301, 17000010, 200, 3_000, 7);
        fixture.AppendSceneHealing(303, 301, 17000010, 200, 5_000, 8);

        fixture.Settings.SortByCommand.Execute(CombatantSortMetric.TotalHealing);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.Equal((CombatantSortMetric.TotalHealing, false), (fixture.Settings.CombatantSortMetric, fixture.Settings.CombatantSortAscending));
        Assert.Equal(new[] { 302, 303, 300, 301 }, fixture.ViewModel.Combatants.Select(static row => row.Id));

        // Ties (no healing) stay the most damage first.
        fixture.Settings.SortByCommand.Execute(CombatantSortMetric.TotalHealing);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.True(fixture.Settings.CombatantSortAscending);
        Assert.Equal(new[] { 300, 301, 303, 302 }, fixture.ViewModel.Combatants.Select(static row => row.Id));

        fixture.Settings.SortByCommand.Execute(CombatantSortMetric.TotalDamage);
        Assert.Equal((CombatantSortMetric.TotalDamage, false), (fixture.Settings.CombatantSortMetric, fixture.Settings.CombatantSortAscending));
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_RowShowsDamageTaken()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "Gladiator");
        fixture.AppendSceneNickname(302, "Cleric");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 3_000, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 5_000, 2);
        fixture.AppendSceneDamage(900_002, 300, 1802520, 250, 3_500, 3);
        fixture.AppendSceneDamage(900_002, 300, 1802520, 150, 4_500, 4);
        fixture.AppendSceneHealing(302, 300, 17000010, 200, 3_600, 5);
        fixture.AppendSceneHealing(302, 300, 17000010, 200, 4_600, 6);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal(400, Assert.Single(fixture.ViewModel.Combatants, static row => row.Id == 300).DamageTaken);
        Assert.Equal(0, Assert.Single(fixture.ViewModel.Combatants, static row => row.Id == 302).DamageTaken);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_RowShowsUnhealedHealthSeconds()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "Gladiator");
        fixture.AppendSceneNickname(302, "Cleric");
        fixture.AppendSceneGroupStatus(300, 1_000, 1_000, 1_000, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 3_000, 2);
        fixture.AppendSceneGroupStatus(300, 600, 1_000, 3_500, 3);
        fixture.AppendSceneHealing(302, 300, 17000010, 200, 3_600, 4);
        fixture.AppendSceneHp(300, 800, 4_000, 5);
        fixture.AppendSceneGroupStatus(300, 1_000, 1_000, 4_500, 6);
        fixture.AppendSceneHealing(302, 300, 17000010, 200, 4_500, 7);
        fixture.AppendSceneGroupStatus(300, 0, 1_000, 4_800, 8);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 5_000, 9);
        fixture.AppendSceneGroupStatus(300, 500, 1_000, 5_500, 10);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        // 400 missing for 0.5 s, then 200 for 0.5 s; dead from 4.8 s counts nothing, and the drop after
        // the last hit at 5 s is outside the fight.
        Assert.Equal(300, Assert.Single(fixture.ViewModel.Combatants, static row => row.Id == 300).UnhealedHealthSeconds);
        Assert.Equal(0, Assert.Single(fixture.ViewModel.Combatants, static row => row.Id == 302).UnhealedHealthSeconds);
        Assert.Equal(300, fixture.ViewModel.TotalUnhealedHealthSeconds);
    }

    // UHS ÷ damage taken: 300 is 400 HP short for a second after the boss's 400. 301 took 700 but its max HP never came (a
    // stranger: no UHS), 302 took nothing: none for both ("–"), left out of the bottom bar's and last however the column
    // sorts, without a bar.
    [Fact]
    public void RefreshCombatStats_SceneMode_RowShowsRecoveryTime()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "Gladiator");
        fixture.AppendSceneNickname(301, "Stranger");
        fixture.AppendSceneNickname(302, "Cleric");
        fixture.AppendSceneGroupStatus(300, 1_000, 1_000, 1_000, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 3_000, 2);
        fixture.AppendSceneDamage(900_002, 300, 1802520, 400, 3_500, 3);
        fixture.AppendSceneGroupStatus(300, 600, 1_000, 3_500, 4);
        fixture.AppendSceneDamage(900_002, 301, 1802520, 700, 3_600, 5);
        fixture.AppendSceneHealing(302, 300, 17000010, 200, 3_600, 6);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 100, 4_000, 7);
        fixture.AppendSceneHealing(302, 300, 17000010, 200, 4_500, 8);
        fixture.AppendSceneGroupStatus(300, 1_000, 1_000, 4_500, 9);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 5_000, 10);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var rows = fixture.ViewModel.Combatants;
        Assert.Equal(1, Assert.Single(rows, static row => row.Id == 300).RecoveryTime);
        Assert.True(double.IsNaN(Assert.Single(rows, static row => row.Id == 301).RecoveryTime));
        Assert.True(double.IsNaN(Assert.Single(rows, static row => row.Id == 302).RecoveryTime));
        Assert.Equal(1, fixture.ViewModel.TotalRecoveryTime);

        fixture.Settings.SortByCommand.Execute(CombatantSortMetric.RecoveryTime);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.Equal(new[] { 300, 301, 302 }, rows.Select(static row => row.Id));
        Assert.Equal(1d, rows[0].BarSegment!.Value.Ratio, 6);
        Assert.Null(rows[1].BarSegment);
        fixture.Settings.SortByCommand.Execute(CombatantSortMetric.RecoveryTime);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.True(fixture.Settings.CombatantSortAscending);
        Assert.Equal(new[] { 300, 301, 302 }, rows.Select(static row => row.Id));
    }

    [Theory]
    [InlineData(true, 800, 600)]
    [InlineData(false, 300, 0)]
    public void IncludeOverhealing_SwitchesHealingBetweenRawAndRestoredHp(bool includeOverhealing, double expectedHealing, double expectedFullHpSelfHealing)
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.IncludeOverhealing = includeOverhealing;
        fixture.AppendSceneNickname(300, "Gladiator");
        fixture.AppendSceneNickname(302, "Cleric");
        fixture.AppendSceneNickname(303, "Full HP Cleric");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 3_000, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 5_000, 2);
        fixture.AppendSceneHp(300, 1_000, 3_100, 3);
        fixture.AppendSceneHealing(302, 300, 17000010, 400, 3_200, 4);
        fixture.AppendSceneHp(300, 1_300, 3_250, 5); // only 300 of the 400 restored missing HP
        fixture.AppendSceneHealing(302, 300, 17000010, 400, 4_000, 6); // target full: no HP update follows
        // 303 never loses HP, so the server never sends its HP at all: its self-heals restore nothing.
        fixture.AppendSceneHealing(303, 303, 17000010, 300, 3_300, 7);
        fixture.AppendSceneHealing(303, 303, 17000010, 300, 3_400, 8);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal(expectedHealing, Assert.Single(fixture.ViewModel.Combatants, static row => row.Id == 302).Healing);
        Assert.Equal(expectedFullHpSelfHealing, Assert.Single(fixture.ViewModel.Combatants, static row => row.Id == 303).Healing);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_GeneratesBarHuesFromEncounterBase()
    {
        var fixture = MainViewModelFixture.Create();
        for (var i = 0; i < 10; i++)
        {
            var playerId = 300 + i;
            fixture.AppendSceneNickname(playerId, $"Player {i}");
            fixture.AppendSceneDamage(playerId, 900_002, 11000010, 1_000 - i * 50, 3_000 + i, i + 1);
        }

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var colors = fixture.ViewModel.Combatants.Select(static row => Assert.IsType<SolidColorBrush>(row.BarSegment!.Value.Brush).Color).ToArray();
        Assert.All(colors, static color => Assert.Equal(byte.MaxValue, color.A));
        Assert.All(colors, static color =>
        {
            var (_, saturation, lightness) = GetHsl(color);
            Assert.InRange(saturation, 0.51d, 0.69d);
            Assert.InRange(lightness, 0.51d, 0.65d);
        });
        for (var i = 1; i < 9; i++)
            Assert.InRange(GetHueDistance(colors[i - 1], colors[i]), 39d, 41d);
        Assert.InRange(GetHueDistance(colors[0], colors[9]), 19d, 21d);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_BossBarRendersOnlyRemainingHp()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneNickname(301, "Second");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 3_000, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 5_000, 2);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 250, 3_000, 3);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 250, 5_000, 4);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 700, 1_000, 5_500);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var focus = Assert.Single(fixture.ViewModel.BossFocuses);
        var remainingHp = Assert.IsType<ProgressSegment>(focus.BarSegment);
        Assert.Equal(0.7d, remainingHp.Ratio, 6);
        Assert.Equal(Color.FromArgb(0xF0, 0xDF, 0x21, 0x4A), Assert.IsAssignableFrom<ISolidColorBrush>(remainingHp.Brush).Color);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_BossBarsIgnoreDamageContributionDistribution()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneNickname(301, "Second");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 900, 3_000, 1);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 100, 5_000, 2);
        fixture.AppendSceneDamage(300, 900_003, 11000010, 100, 6_000, 3);
        fixture.AppendSceneDamage(301, 900_003, 11000010, 900, 7_000, 4);
        fixture.AppendSceneBossFocus(900_002, "Boss A", 500, 1_000, 7_500);
        fixture.AppendSceneBossFocus(900_003, "Boss B", 500, 1_000, 7_500);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal(2, fixture.ViewModel.BossFocuses.Count);
        var firstBoss = fixture.ViewModel.BossFocuses.Single(static boss => boss.InstanceId == 900_002);
        var secondBoss = fixture.ViewModel.BossFocuses.Single(static boss => boss.InstanceId == 900_003);
        Assert.Equal(0.5d, Assert.IsType<ProgressSegment>(firstBoss.BarSegment).Ratio, 6);
        Assert.Equal(0.5d, Assert.IsType<ProgressSegment>(secondBoss.BarSegment).Ratio, 6);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_BossBarIsEmptyWhenHpUnknown()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 1_000, 3_000, 1);
        fixture.AppendSceneBossFocusUnknownHp(900_002, "Scene Boss", 3_500);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var focus = Assert.Single(fixture.ViewModel.BossFocuses);
        Assert.False(focus.HasHp);
        Assert.Null(focus.BarSegment);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_NoBoss_ShowsDpsAndTotalColumns()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Same(fixture.ViewModel.CombatantColumns, row.Columns);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowDamagePerSecondColumn);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowDamageColumn);
        Assert.False(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        Assert.Equal(0d, row.BossShareRatio);
    }

    [Fact]
    public void MainMetricVisibility_UpdatesImmediatelyWithoutChangingTotals()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        var totalDamagePerSecond = fixture.ViewModel.TotalFilteredDamagePerSecond;

        Assert.Equal(334d, fixture.ViewModel.CombatantColumns.OverlayWidth);
        Assert.False(fixture.ViewModel.CombatantColumns.ShowHealingPerSecondColumn);
        Assert.False(fixture.ViewModel.CombatantColumns.ShowHealingColumn);
        fixture.Settings.ShowHealingPerSecondColumn = true;
        Assert.True(fixture.ViewModel.CombatantColumns.ShowHealingPerSecondColumn);
        fixture.Settings.ShowHealingColumn = true;
        Assert.True(fixture.ViewModel.CombatantColumns.ShowHealingColumn);
        Assert.False(fixture.ViewModel.CombatantColumns.ShowDamageTakenColumn);
        fixture.Settings.ShowDamageTakenColumn = true;
        Assert.True(fixture.ViewModel.CombatantColumns.ShowDamageTakenColumn);
        Assert.False(fixture.ViewModel.CombatantColumns.ShowUnhealedHealthColumn);
        fixture.Settings.ShowUnhealedHealthColumn = true;
        Assert.True(fixture.ViewModel.CombatantColumns.ShowUnhealedHealthColumn);
        // The overlay widens with each column past the first two (40 px each with compact numbers).
        Assert.Equal(494d, fixture.ViewModel.CombatantColumns.OverlayWidth);
        // The recovery time column shows by itself for Clerics only (no character known here), or when set to.
        Assert.False(fixture.ViewModel.CombatantColumns.ShowRecoveryTimeColumn);
        fixture.Settings.RecoveryTimeColumn = AggroColumnMode.Show;
        Assert.True(fixture.ViewModel.CombatantColumns.ShowRecoveryTimeColumn);
        Assert.Equal(534d, fixture.ViewModel.CombatantColumns.OverlayWidth);
        fixture.Settings.RecoveryTimeColumn = AggroColumnMode.Hide;

        fixture.Settings.ShowDamagePerSecondColumn = false;
        fixture.Settings.ShowDamageColumn = false;
        fixture.Settings.ShowTotalDamagePerSecond = false;
        Assert.Equal(414d, fixture.ViewModel.CombatantColumns.OverlayWidth);

        Assert.False(fixture.ViewModel.CombatantColumns.ShowDamagePerSecondColumn);
        Assert.False(fixture.ViewModel.CombatantColumns.ShowDamageColumn);
        Assert.False(fixture.ViewModel.CombatantColumns.ShowTotalDamagePerSecond);
        Assert.True(totalDamagePerSecond > 0);
        Assert.Equal(totalDamagePerSecond, fixture.ViewModel.TotalFilteredDamagePerSecond);
    }

    [Theory]
    [InlineData(20, 0.2, 0)]
    [InlineData(40, 0.6, 0)]
    [InlineData(60, 1, 0)]
    [InlineData(80, 1, 0.5)]
    [InlineData(100, 1, 1)]
    public void Opacity_Slider_Fades_The_Overlay_Below_60_And_Fills_The_Panel_Above(int percent, double content, double fill)
    {
        var fixture = MainViewModelFixture.Create();

        fixture.Settings.OverlayOpacityPercent = percent;

        Assert.Equal(content, fixture.Settings.OverlayContentOpacity, 6);
        Assert.Equal(fill, fixture.Settings.OverlayBackdropFill, 6);

        // The details panel has its own slider on the same scale.
        fixture.Settings.OverlayOpacityPercent = 60;
        fixture.Settings.DetailOpacityPercent = percent;
        Assert.Equal(content, fixture.Settings.DetailContentOpacity, 6);
        Assert.Equal(fill, fixture.Settings.DetailBackdropFill, 6);
    }

    [Theory]
    [InlineData(SceneKind.Boss)]
    [InlineData(SceneKind.Standard)]
    public void ArchivedEncounter_WithoutBossFocus_HidesBossShareColumn(SceneKind archivedSceneKind)
    {
        var fixture = MainViewModelFixture.Create(archivedSceneKind);
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.ViewModel.ArchiveCurrentEncounterCommand.Execute(null);
        var history = Assert.Single(fixture.ViewModel.EncounterHistory);

        fixture.ViewModel.ReturnToLiveCommand.Execute(null);
        fixture.ViewModel.SelectedEncounterHistory = history;

        Assert.False(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_BossColumn_DpsSortShowsDpsTotalAndBossShare()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 250, 3_000, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 250, 3_100, 2);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 700, 1_000, 3_500);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Same(fixture.ViewModel.CombatantColumns, row.Columns);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowDamagePerSecondColumn);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowDamageColumn);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        Assert.Equal(0.5d, row.BossShareRatio, 6);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_BossColumn_TotalDamageSortShowsDpsTotalAndBossShare()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantSortMetric = CombatantSortMetric.TotalDamage;
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 125, 3_000, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 125, 3_100, 2);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 500, 1_000, 3_500);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Same(fixture.ViewModel.CombatantColumns, row.Columns);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowDamagePerSecondColumn);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowDamageColumn);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        Assert.Equal(0.25d, row.BossShareRatio, 6);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_BossColumn_ShowsZeroShareRows()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneNickname(301, "Second");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 3_000, 1);
        fixture.AppendSceneDamage(301, 900_900, 11000010, 250, 3_100, 2);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 700, 1_000, 3_500);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        var zeroShareRow = fixture.ViewModel.Combatants.Single(static row => row.Id == 301);
        Assert.Equal(0d, zeroShareRow.BossShareRatio);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_BossColumn_UsesEffectiveHpAfterHealing()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 1_000, 1_000, 1_000);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 500, 1_000, 2_000);
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 800, 1_000, 3_000);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 325, 3_500, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 325, 3_600, 2);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        Assert.Equal(0.5d, row.BossShareRatio, 6);
    }

    // A boss that gets HP back between its HP updates takes more damage than its HP: the shares then split the damage
    // (here 1,500 on a 1,000 HP boss), in the table and in the fight's section, instead of adding up to 150 %.
    [Fact]
    public void RefreshCombatStats_SceneMode_BossColumn_SplitsDamageBeyondTheBossHp()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneNickname(301, "Second");
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 1_000, 1_000, 1_000);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 1_000, 2_000, 1);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 500, 2_100, 2);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        (int, double)[] expected = [(300, Math.Round(2 / 3d, 6)), (301, Math.Round(1 / 3d, 6))];
        Assert.Equal(expected, fixture.ViewModel.Combatants.OrderBy(static row => row.Id).Select(static row => (row.Id, Math.Round(row.BossShareRatio, 6))));
        Assert.True(fixture.ViewModel.CurrentBossFight.IsVisible);
        Assert.Equal(expected, fixture.ViewModel.CurrentBossFight.Rows.OrderBy(static row => row.Id).Select(static row => (row.Id, Math.Round(row.BossShareRatio, 6))));
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_BossColumn_AggregatesMultipleBossShares()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneDamage(300, 900_003, 11000010, 50, 3_000, 1);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 100, 3_100, 2);
        fixture.AppendSceneBossFocus(900_003, "Boss B", 250, 500, 3_500);
        fixture.AppendSceneBossFocus(900_002, "Boss A", 700, 1_000, 3_500);

        fixture.ViewModel.RefreshCombatStatsForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.True(fixture.ViewModel.CombatantColumns.ShowBossShareColumn);
        Assert.Equal(0.1d, row.BossShareRatio, 6);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_RefreshesLiveDetailFromSceneProjection()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);

        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.ViewModel.SelectedCombatant = Assert.Single(fixture.ViewModel.Combatants);

        Assert.Equal(300, fixture.ViewModel.CombatantDetails.SelectedCombatantId);
        Assert.Equal("Scene Player", fixture.ViewModel.CombatantDetails.DisplayContext!.ResolveEntityName(300));
        Assert.Equal(400, fixture.ViewModel.CombatantDetails.OutgoingDamage.Total);
        Assert.Equal(2, fixture.ViewModel.CombatantDetails.OutgoingDamage.Hits);
        Assert.Single(fixture.ViewModel.CombatantDetails.OutgoingDetail.DamageCounterpartFilter.Counterparts);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_DoesNotRebuildLiveDetailForIrrelevantSceneCombat()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);

        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.ViewModel.SelectedCombatant = Assert.Single(fixture.ViewModel.Combatants);
        var row = fixture.ViewModel.CombatantDetails.OutgoingDamage.Rows[0];

        fixture.AppendSceneEncounter(301, "Other Player", 600, 6_000, 7_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Same(row, fixture.ViewModel.CombatantDetails.OutgoingDamage.Rows[0]);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_RebuildsLiveDetailForRelevantSceneCombat()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);

        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.ViewModel.SelectedCombatant = Assert.Single(fixture.ViewModel.Combatants);
        var firstTotal = fixture.ViewModel.CombatantDetails.OutgoingDamage.Total;

        fixture.AppendSceneDamage(300, 900_002, 11000010, 200, 6_000, 3);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.Equal(600, fixture.ViewModel.CombatantDetails.OutgoingDamage.Total);
        Assert.True(fixture.ViewModel.CombatantDetails.OutgoingDamage.Total > firstTotal);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_DeactivatingDetailKeepsRowsWarmForFlyoutReopen()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);

        fixture.ViewModel.RefreshCombatStatsForTesting();
        var row = Assert.Single(fixture.ViewModel.Combatants);
        fixture.ViewModel.SelectedCombatant = row;
        var detailRow = fixture.ViewModel.CombatantDetails.OutgoingDamage.Rows[0];

        fixture.ViewModel.SelectedCombatant = null;

        Assert.Same(detailRow, fixture.ViewModel.CombatantDetails.OutgoingDamage.Rows[0]);

        fixture.ViewModel.SelectedCombatant = row;

        Assert.Same(detailRow, fixture.ViewModel.CombatantDetails.OutgoingDamage.Rows[0]);
    }

    [Fact]
    public void ClosingTheDetailsFlyout_ReleasesItsEventsAndReopeningShowsTheSameDetail()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        var row = Assert.Single(fixture.ViewModel.Combatants);
        fixture.ViewModel.SelectCombatantRow(row);
        var detailRow = fixture.ViewModel.CombatantDetails.OutgoingDamage.Rows[0];
        var events = (List<CombatMetricDetailEvent>)typeof(CombatantDetailsFlyoutViewModel)
            .GetField("_metricDetailEvents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(fixture.ViewModel.CombatantDetails)!;
        Assert.NotEmpty(events);

        // What MainWindow.FlyoutClosed does.
        fixture.ViewModel.SelectCombatantCommand.Execute(null);

        Assert.Equal(0, events.Capacity);

        fixture.ViewModel.SelectCombatantRow(row);

        Assert.Equal(400, fixture.ViewModel.CombatantDetails.OutgoingDamage.Total);
        Assert.Same(detailRow, fixture.ViewModel.CombatantDetails.OutgoingDamage.Rows[0]);
        Assert.NotEmpty(events);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_SwitchingBackToPlayerReplacesDetailEventContext()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneIdentity(300, "First Player");
        fixture.AppendSceneIdentity(301, "Second Player");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 400, 3_000, 1);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 600, 4_000, 2);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        var firstPlayer = fixture.ViewModel.Combatants.Single(static row => row.Id == 300);
        var secondPlayer = fixture.ViewModel.Combatants.Single(static row => row.Id == 301);

        fixture.ViewModel.SelectedCombatant = firstPlayer;
        Assert.Equal(400, fixture.ViewModel.CombatantDetails.OutgoingDamage.Total);

        fixture.ViewModel.SelectedCombatant = secondPlayer;
        Assert.Equal(600, fixture.ViewModel.CombatantDetails.OutgoingDamage.Total);

        fixture.ViewModel.SelectedCombatant = firstPlayer;
        Assert.Equal(400, fixture.ViewModel.CombatantDetails.OutgoingDamage.Total);
        Assert.Equal(300, fixture.ViewModel.CombatantDetails.SelectedCombatantId);
    }

    [Fact]
    public void ArchiveCurrentEncounter_SceneMode_WritesScenePayload()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        fixture.ViewModel.ArchiveCurrentEncounterCommand.Execute(null);

        var history = Assert.Single(fixture.ViewModel.EncounterHistory);
        Assert.NotNull(history.Record.ScenePayload);
        var detail = history.Record.ScenePayload!.CreateDetailDelta(300);
        Assert.True(history.Record.ScenePayload!.IdentityScope.TryGetPcMetadata(300, out var archivedPc));
        Assert.Equal("Scene Player", archivedPc.Nickname);
        Assert.Equal(400, detail.Combatant!.Value.OutgoingDamage);
        Assert.Empty(fixture.CreateSceneSnapshot().Combatants);
    }

    [Fact]
    public void ArchiveCurrentEncounter_WithoutValidCombat_ResetsArchiveScopeWithoutAddingHistory()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneMap(1010, 123);
        fixture.AppendSceneIdentity(300, "Scene Player");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 100, 3_000, 1);
        var previous = fixture.CreateSceneSnapshot();

        fixture.ViewModel.ArchiveCurrentEncounterCommand.Execute(null);

        Assert.Empty(fixture.Archive.History);
        var reset = fixture.CreateSceneSnapshot();
        Assert.NotEqual(previous.EncounterId, reset.EncounterId);
        Assert.Empty(reset.Combatants);
        Assert.Equal(1010u, reset.MapId);
        Assert.Equal(123u, reset.MapInstanceId);
        Assert.True(fixture.MetadataRegistry.TryGetPcMetadata(300, out var livePc));
        Assert.Equal("Scene Player", livePc.Nickname);

        fixture.AppendSceneEncounter(300, "Scene Player", 400, 6_000, 8_000);
        var next = fixture.CreateSceneSnapshot();
        Assert.Equal(400, next.Combatants[300].DamageAmount);
    }

    [Fact]
    public void ArchiveCurrentEncounter_SceneMode_ArchivedDetailUsesScenePayload()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        fixture.ViewModel.ArchiveCurrentEncounterCommand.Execute(null);

        var row = Assert.Single(fixture.ViewModel.Combatants);
        fixture.ViewModel.SelectedCombatant = null;
        fixture.ViewModel.SelectedCombatant = row;

        Assert.Equal(300, fixture.ViewModel.CombatantDetails.SelectedCombatantId);
        Assert.Equal("Scene Player", fixture.ViewModel.CombatantDetails.DisplayContext!.ResolveEntityName(300));
        Assert.Equal(400, fixture.ViewModel.CombatantDetails.OutgoingDamage.Total);
        Assert.NotNull(fixture.ViewModel.EncounterHistory[0].Record.ScenePayload);
    }

    [Fact]
    public void ArchiveCurrentEncounter_SceneMode_ArchivedDisplayUsesSnapshot()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        fixture.ViewModel.ArchiveCurrentEncounterCommand.Execute(null);
        var history = Assert.Single(fixture.ViewModel.EncounterHistory);
        fixture.ViewModel.ReturnToLiveCommand.Execute(null);
        fixture.ViewModel.SelectedEncounterHistory = history;

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Equal(300, row.Id);
        Assert.Equal("Scene Player", fixture.ViewModel.DisplayContext!.ResolveEntityName(row.Id));
        Assert.NotNull(history.Record.ScenePayload);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_AutoArchiveWritesScenePayloadOnMapTransition()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneMap(200003, 113515);
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        fixture.AppendSceneMap(200004, 113516);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        var record = Assert.Single(fixture.Archive.History);
        Assert.Equal("map-transition", record.Trigger);
        Assert.True(record.IsAutomatic);
        Assert.NotNull(record.ScenePayload);
        Assert.Equal(400, record.ScenePayload!.CreateDetailDelta(300).Combatant!.Value.OutgoingDamage);
        Assert.NotNull(record.ScenePayload);
        Assert.False(fixture.ViewModel.IsViewingArchivedEncounter);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_AutoArchiveKeepsPreviousEncounterMapOnMapTransition()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneMap(600002, 396972);
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        fixture.AppendSceneMap(1010, 0);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        var record = Assert.Single(fixture.Archive.History);
        Assert.Equal("map-transition", record.Trigger);
        Assert.True(record.IsAutomatic);
        Assert.Equal(600002u, record.ScenePayload.Snapshot.MapId);
        Assert.Equal(396972u, record.ScenePayload.Snapshot.MapInstanceId);
        Assert.Equal(400, record.ScenePayload!.CreateDetailDelta(300).Combatant!.Value.OutgoingDamage);
    }

    // "Boss battles only": a new map starts the meter empty, as with "All fights"; the last map's boss fight is in the
    // history, and the next boss pull shows live.
    [Fact]
    public void RefreshCombatStats_BossScene_MapTransitionStartsEmpty()
    {
        var fixture = MainViewModelFixture.Create(SceneKind.Boss);
        var sink = fixture.CreateLiveSink();
        var origin = fixture.SceneStartedMilliseconds;
        var mapSource = new PacketObservationSource(origin + 1, 0, 0x2136, 0, 1, default);
        sink.SetCurrentMap(in mapSource, 200003);

        var metadataSource = new PacketObservationSource(origin + 100, 0, 0, 0, 100, default);
        sink.AppendNickname(in metadataSource, 100, "Player", characterClass: CharacterClass.Gladiator);
        sink.AppendNpcCode(in metadataSource, 300, 2_100_002);
        sink.AppendNpcKind(in metadataSource, 300, NpcKind.Boss);
        AppendDamage(sink, 100, 300, 11000010, 350, origin + 1_000, 1);
        AppendDamage(sink, 100, 300, 11000010, 350, origin + 2_000, 2);
        sink.CompleteFlush(1);
        sink.CompleteFlush(2);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        var liveBoss = Assert.Single(fixture.ViewModel.BossFocuses);
        Assert.False(fixture.ViewModel.IsViewingArchivedEncounter);
        Assert.Equal(300, liveBoss.InstanceId);
        Assert.Equal(700, Assert.Single(fixture.ViewModel.Combatants).Damage);

        fixture.AppendSceneMap(200004, 113516);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        var record = Assert.Single(fixture.Archive.History);
        Assert.Equal("map-transition", record.Trigger);
        Assert.Equal(SceneKind.Boss, record.ScenePayload.Kind);
        Assert.Equal(700, record.ScenePayload.CreateDetailDelta(100).Combatant!.Value.OutgoingDamage);
        Assert.False(fixture.ViewModel.IsViewingArchivedEncounter);
        Assert.Null(fixture.ViewModel.SelectedEncounterHistory);
        Assert.Equal(200004u, fixture.ViewModel.LiveSceneMapId);
        Assert.Empty(fixture.ViewModel.BossFocuses);
        Assert.Empty(fixture.ViewModel.Combatants);
        Assert.Equal(0, fixture.ViewModel.TotalFilteredDamagePerSecond);

        var nextSink = fixture.CreateLiveSink();
        var nextOrigin = fixture.SceneStartedMilliseconds;
        var nextMetadataSource = new PacketObservationSource(nextOrigin + 100, 3, 0, 0, 100, default);
        nextSink.AppendNpcCode(in nextMetadataSource, 301, 2_100_351);
        nextSink.AppendNpcKind(in nextMetadataSource, 301, NpcKind.Boss);
        AppendDamage(nextSink, 100, 301, 11000010, 900, nextOrigin + 1_000, 3);
        nextSink.CompleteFlush(3);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        Assert.False(fixture.ViewModel.IsViewingArchivedEncounter);
        Assert.Null(fixture.ViewModel.SelectedEncounterHistory);
        Assert.Equal(200004u, fixture.ViewModel.LiveSceneMapId);
        Assert.Equal(900, Assert.Single(fixture.ViewModel.Combatants).Damage);
        Assert.Equal(301, Assert.Single(fixture.ViewModel.BossFocuses).InstanceId);
    }

    [Fact]
    public void RefreshCombatStats_SceneMode_AutoArchivesWhenPreviousMapIsUnknown()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        var sink = fixture.CreateLiveSink();
        var mapSource = new PacketObservationSource(fixture.SceneStartedMilliseconds + 6_000, 0, 0x2136, 0, 1, default);
        sink.SetCurrentMap(in mapSource, 20);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        var record = Assert.Single(fixture.Archive.History);
        Assert.Equal("map-transition", record.Trigger);
        Assert.True(record.IsAutomatic);
        Assert.NotNull(record.ScenePayload);
        Assert.Equal(400, record.ScenePayload!.CreateDetailDelta(300).Combatant!.Value.OutgoingDamage);
    }

    [Fact]
    public void ResetCommand_SceneMode_ArchivesScenePayload()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();

        fixture.ViewModel.ResetCommand.Execute(null);

        var record = Assert.Single(fixture.Archive.History);
        Assert.Equal("manual-reset", record.Trigger);
        Assert.True(record.IsAutomatic);
        Assert.NotNull(record.ScenePayload);
        Assert.Equal(400, record.ScenePayload!.CreateDetailDelta(300).Combatant!.Value.OutgoingDamage);
        Assert.NotNull(record.ScenePayload);
    }

    [Fact]
    public void ResetLiveModels_ResetsSceneSnapshot()
    {
        var fixture = MainViewModelFixture.Create();
        var sink = fixture.CreateLiveSink();
        AppendLiveBattle(sink, fixture.SceneStartedMilliseconds, 100, "Player", 800, 1_000, 2_000, 1);
        var firstScene = fixture.CreateSceneSnapshot();

        fixture.ViewModel.ResetLiveModelsForTesting();

        AppendLiveBattle(sink, fixture.SceneStartedMilliseconds, 100, "Player", 900, 3_000, 4_000, 3);
        var secondScene = fixture.CreateSceneSnapshot();

        Assert.NotEqual(firstScene.EncounterId, secondScene.EncounterId);
        Assert.Equal(900, secondScene.Combatants[100].DamageAmount);
        Assert.True(fixture.MetadataRegistry.TryGetPcMetadata(100, out var livePc));
        Assert.Equal("Player", livePc.Nickname);
    }

    [Fact]
    public void FrameTick_Stopped_DoesNotRefreshLiveStats()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);

        fixture.ViewModel.ProcessUiFrameForTesting();

        Assert.Empty(fixture.ViewModel.Combatants);
    }

    [Fact]
    public void FrameTick_Capturing_RefreshesLiveStats()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.ViewModel.IsCapturing = true;
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);

        fixture.ViewModel.ProcessUiFrameForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Equal(300, row.Id);
        Assert.Equal(400, row.Damage);
    }

    [Fact]
    public void FrameTick_Capturing_ProjectsDirtyStateAtTwentyFiveHertz()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.ViewModel.IsCapturing = true;
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);

        fixture.ViewModel.ProcessUiFrameForTesting(TimeSpan.Zero);

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Equal(400, row.Damage);
        Assert.Equal(1, fixture.ProjectionCacheStats.SnapshotBuilds);

        fixture.AppendSceneDamage(300, 900_002, 11000010, 100, 6_000, 3);
        fixture.ViewModel.ProcessUiFrameForTesting(TimeSpan.FromMilliseconds(20));

        Assert.Equal(400, row.Damage);
        Assert.Equal(1, fixture.ProjectionCacheStats.SnapshotBuilds);

        fixture.ViewModel.ProcessUiFrameForTesting(MainViewModel.LiveProjectionInterval);

        Assert.Equal(500, row.Damage);
        Assert.Equal(2, fixture.ProjectionCacheStats.SnapshotBuilds);

        fixture.ViewModel.ProcessUiFrameForTesting(TimeSpan.FromMilliseconds(80));

        Assert.Equal(2, fixture.ProjectionCacheStats.SnapshotBuilds);
    }

    [Fact]
    public void FrameTick_Capturing_RefreshesMapWithoutCombat()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.ViewModel.IsCapturing = true;
        var changed = new List<string?>();
        fixture.ViewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        fixture.AppendSceneMap(1010, 0);
        fixture.ViewModel.ProcessUiFrameForTesting();
        fixture.FlushFrame();

        Assert.Equal(1010u, fixture.ViewModel.LiveSceneMapId);
        Assert.Contains(nameof(MainViewModel.LiveSceneMapId), changed);

        changed.Clear();
        fixture.AppendSceneMap(500015, 719460);
        fixture.ViewModel.ProcessUiFrameForTesting();
        fixture.FlushFrame();

        Assert.Equal(500015u, fixture.ViewModel.LiveSceneMapId);
        Assert.Contains(nameof(MainViewModel.LiveSceneMapId), changed);

        changed.Clear();
        fixture.AppendSceneMap(1010, 0);
        fixture.ViewModel.ProcessUiFrameForTesting();
        fixture.FlushFrame();

        Assert.Equal(1010u, fixture.ViewModel.LiveSceneMapId);
        Assert.Contains(nameof(MainViewModel.LiveSceneMapId), changed);
    }

    [Fact]
    public void FrameTick_ArchiveView_DoesNotOverwriteDisplayedArchive()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Archived Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.ViewModel.ArchiveCurrentEncounterCommand.Execute(null);

        fixture.ViewModel.IsCapturing = true;
        fixture.AppendSceneEncounter(301, "Live Player", 800, 6_000, 8_000);
        fixture.ViewModel.ProcessUiFrameForTesting();

        var row = Assert.Single(fixture.ViewModel.Combatants);
        Assert.Equal(300, row.Id);
        Assert.Equal("Archived Player", fixture.ViewModel.DisplayContext!.ResolveEntityName(row.Id));
    }

    [Fact]
    public void SceneLiveReadModel_Reset_StartsNewBattleWithoutDroppingIdentity()
    {
        var scene = new SceneLiveReadModel();
        AppendSceneEncounter(scene, 300, "Scene Player", 400, 3_000, 5_000);
        var first = scene.Owner.CreateSnapshot();

        scene.Reset();
        var reset = scene.Owner.CreateSnapshot();

        AppendSceneEncounter(scene, 300, "Scene Player", 401, 6_000, 7_000);
        var second = scene.Owner.CreateSnapshot();

        Assert.NotEqual(first.EncounterId, reset.EncounterId);
        Assert.Empty(reset.Combatants);
        Assert.True(scene.Owner.MetadataRegistry.TryGetPcMetadata(300, out var scenePc));
        Assert.Equal("Scene Player", scenePc.Nickname);
        Assert.Equal(401, second.Combatants[300].DamageAmount);
    }

    // The meter keeps its display context while the same players fight (only their numbers change), so its names aren't
    // all looked up again every frame. The flush still says so, and looks them up again if the registry moved since: as a
    // new context did. The details get a new one at the next refresh; a new player gives a new one too.
    [Fact]
    public void DisplayContext_Stays_While_The_Same_Players_Fight()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.AppendSceneEncounter(300, "Scene Player", 400, 3_000, 5_000);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.FlushFrame();
        var context = fixture.ViewModel.DisplayContext!;
        var raised = 0;
        fixture.ViewModel.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(MainViewModel.DisplayContext) ? 1 : 0;

        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 6_000, 3);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.FlushFrame();

        Assert.Same(context, fixture.ViewModel.DisplayContext);
        Assert.Same(context, fixture.ViewModel.CombatantDetails.DisplayContext);
        Assert.Equal(1, raised);
        var fresh = new SceneDisplayContext(SceneIdentityScope.Empty, fixture.MetadataRegistry, fixture.CreateSceneSnapshot(), context.Resources, context.UnknownSceneName);
        foreach (var id in (int[])[300, 900_002, 301])
        {
            Assert.Equal(fresh.ResolvePcClass(id), context.ResolvePcClass(id));
            Assert.Equal(fresh.ResolvePcAnonymousOrdinal(id), context.ResolvePcAnonymousOrdinal(id));
            Assert.Equal(fresh.ResolveEntityName(id), context.ResolveEntityName(id));
        }

        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 7_000, 4);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.Same(context, fixture.ViewModel.DisplayContext);
        fixture.MetadataRegistry.UpsertPcMetadata(300, "Scene Player", legionName: "Aether");
        fixture.FlushFrame();
        Assert.Equal(2, raised);
        Assert.NotSame(context, fixture.ViewModel.DisplayContext);
        Assert.Same(context, fixture.ViewModel.CombatantDetails.DisplayContext);

        fixture.AppendSceneNickname(301, "Second Player");
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 8_000, 5);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        var beforeNewPlayer = fixture.ViewModel.DisplayContext;
        Assert.NotSame(context, beforeNewPlayer);
        Assert.Same(beforeNewPlayer, fixture.ViewModel.CombatantDetails.DisplayContext);

        var revision = fixture.MetadataRegistry.Revision;
        fixture.AppendSceneDamage(301, 900_002, 11000010, 500, 9_000, 6);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.Equal(revision, fixture.MetadataRegistry.Revision);
        Assert.NotSame(beforeNewPlayer, fixture.ViewModel.DisplayContext);
        Assert.Equal(CharacterClass.Gladiator, fixture.ViewModel.DisplayContext!.ResolvePcClass(301));
    }

    // An earlier pull in the list below the table is filled again only when something it shows can differ (it stays the
    // same object while it's over): the table's players, the settings it goes by, and the boss's name (a late NPC code).
    [Fact]
    public void Earlier_Pulls_Follow_The_Table_And_The_Settings()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantStatisticsScope = CombatantStatisticsScope.Party;
        fixture.AppendSceneIdentity(100, "Self", isLocalPlayer: true);
        fixture.AppendSceneIdentity(200, "Mate");
        fixture.AppendScenePlayerGroupMember(100, PlayerGroupMembership.Force(7, 1, 1));
        fixture.AppendScenePlayerGroupMember(200, PlayerGroupMembership.Force(7, 2, 1));
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 50_000, 50_000, 2_500);
        fixture.AppendSceneDamage(100, 900_002, 11000010, 1_000, 3_000, 1);
        fixture.AppendSceneDamage(200, 900_002, 11000010, 3_000, 3_100, 2);
        fixture.AppendSceneHealing(100, 200, 17000010, 600, 3_150, 3); // no HP update follows: it restores nothing
        fixture.AppendSceneHp(900_002, 46_000, 3_200, 4);
        fixture.AppendSceneHp(900_002, 50_000, 9_000, 5);
        fixture.AppendSceneDamage(100, 900_002, 11000010, 500, 12_000, 6);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        var wipe = Assert.Single(fixture.ViewModel.BossFights);
        Assert.Equal([100], wipe.Rows.Select(static row => row.Id));
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.Equal([100], wipe.Rows.Select(static row => row.Id));

        fixture.AppendScenePlayerGroupMember(200, PlayerGroupMembership.Force(7, 1, 2));
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.Equal([200, 100], wipe.Rows.Select(static row => row.Id));

        fixture.Settings.CombatantSortAscending = true;
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.Equal([100, 200], wipe.Rows.Select(static row => row.Id));

        Assert.False(wipe.HasDpsCheck);
        fixture.Settings.BossTimeLimitMinutes = 10;
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.True(wipe.HasDpsCheck);

        fixture.Settings.CombatantSortMetric = CombatantSortMetric.TotalHealing;
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.Equal([200, 100], wipe.Rows.Select(static row => row.Id));

        Assert.Equal(600, wipe.Rows.Single(static row => row.Id == 100).Healing);
        fixture.Settings.IncludeOverhealing = false;
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.Equal(0, wipe.Rows.Single(static row => row.Id == 100).Healing);

        var name = fixture.ViewModel.DisplayContext!.ResolveNpcCodeName(2_300_261);
        fixture.AppendSceneNpc(900_002, 2_300_261, NpcKind.Boss);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        Assert.Equal(string.Format(fixture.ViewModel.Localization["BossFight_FailedAttempt"], $"1 ({name})", 1), wipe.Title);
    }

    // A language switch (SetLanguage reloads the game data at once) shows at the next refresh, which in the app comes with
    // the render tick before MainViewModel's posted callbacks run (they don't run here): a new context for the meter's and
    // the details' names, and the earlier pulls filled again. A switch between a refresh that kept the context and its
    // flush gives the meter a new one at that flush, as that refresh's new context did.
    [Fact]
    public void A_Language_Switch_Shows_At_The_Next_Refresh_Before_Its_Posts_Run()
    {
        var fixture = MainViewModelFixture.Create();
        fixture.Settings.CombatantStatisticsScope = CombatantStatisticsScope.Party;
        fixture.AppendSceneIdentity(100, "Self", isLocalPlayer: true);
        fixture.AppendSceneIdentity(200, "Mate");
        fixture.AppendScenePlayerGroupMember(100, PlayerGroupMembership.Force(7, 1, 1));
        fixture.AppendScenePlayerGroupMember(200, PlayerGroupMembership.Force(7, 1, 2));
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 50_000, 50_000, 2_500);
        fixture.AppendSceneDamage(100, 900_002, 11000010, 1_000, 3_000, 1);
        fixture.AppendSceneDamage(200, 900_002, 11000010, 3_000, 3_100, 2);
        fixture.AppendSceneHp(900_002, 46_000, 3_200, 3);
        fixture.AppendSceneHp(900_002, 50_000, 9_000, 4);
        fixture.AppendSceneDamage(100, 900_002, 11000010, 500, 12_000, 5);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.FlushFrame();
        var wipe = Assert.Single(fixture.ViewModel.BossFights);
        var context = fixture.ViewModel.DisplayContext!;
        Assert.StartsWith("首領 #", wipe.Title);

        fixture.Settings.SelectedLanguage = fixture.Settings.Languages.First(static language => language.Code == LanguageService.English);
        fixture.AppendSceneDamage(100, 900_002, 11000010, 500, 12_500, 6);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.FlushFrame();

        Assert.StartsWith("Boss #", wipe.Title);
        Assert.EndsWith("HP left", wipe.DetailText);
        Assert.NotSame(context, fixture.ViewModel.DisplayContext);
        Assert.Equal("Unknown Scene", fixture.ViewModel.DisplayContext!.UnknownSceneName);
        Assert.Same(fixture.ViewModel.DisplayContext, fixture.ViewModel.CombatantDetails.DisplayContext);

        context = fixture.ViewModel.DisplayContext;
        fixture.AppendSceneDamage(100, 900_002, 11000010, 500, 13_000, 7);
        fixture.ViewModel.RefreshCombatStatsForTesting();
        fixture.Settings.SelectedLanguage = fixture.Settings.Languages.First(static language => language.Code == LanguageService.TraditionalChinese);
        fixture.FlushFrame();
        Assert.NotSame(context, fixture.ViewModel.DisplayContext);
    }

    internal sealed class MainViewModelFixture
    {
        private readonly WinDivertCaptureService _captureService;
        private readonly UiFrameBatchService _frameBatch;

        private MainViewModelFixture(MainViewModel viewModel, WinDivertCaptureService captureService, EncounterArchiveService archive, UiFrameBatchService frameBatch)
        {
            ViewModel = viewModel;
            _captureService = captureService;
            Archive = archive;
            _frameBatch = frameBatch;
        }

        public MainViewModel ViewModel { get; }
        public SettingsFlyoutViewModel Settings => ViewModel.SettingsFlyout;
        public EncounterArchiveService Archive { get; }
        public RuntimeMetadataRegistry MetadataRegistry => _captureService.Scene.Owner.MetadataRegistry;
        public ProjectionCacheStats ProjectionCacheStats => _captureService.Scene.Owner.ProjectionCacheStats;
        public BossSceneState BossState => _captureService.Scene.BossState;
        public Guid SceneId => _captureService.Scene.SessionId;
        public long SceneStartedMilliseconds => _captureService.Scene.SessionStarted.ToUnixTimeMilliseconds();

        public static MainViewModelFixture Create(SceneKind sceneKind = SceneKind.Standard)
        {
            CombatResourceTestFixture.SetResources(BuildSkillMap(), ResourceCatalog.Load(ResourceLanguage.English).NpcCatalog);
            var settingsPath = Path.Combine(Path.GetTempPath(), $"aion2flow-test-{Guid.NewGuid():N}.json");
            var settings = new SettingsService(settingsPath);
            settings.Update(s => s.SceneKind = sceneKind);
            var language = new LanguageService();
            language.SetLanguage(LanguageService.TraditionalChinese);
            var localization = new LocalizationService(language);
            var resources = new GameResourceService(language);
            var archive = new EncounterArchiveService();
            var ports = new ProcessPortDiscoveryService();
            var capture = new WinDivertCaptureService(ports);
            capture.Scene.Reset(DateTimeOffset.Now);
            var frameBatch = new UiFrameBatchService();
            var details = new CombatantDetailsFlyoutViewModel(localization, frameBatch);
            var playerNameDisplay = new PlayerNameDisplayService(settings, localization);
            var uiScale = new UiScaleService(settings);
            var settingsViewModel = new SettingsFlyoutViewModel(localization, language, settings, playerNameDisplay, uiScale, new AppUpdateService(), new ProcessForegroundWatcher(), new GlobalHotkeyService());
            var viewModel = new MainViewModel(capture, ports, language, resources, archive, details, localization, settingsViewModel, frameBatch);
            return new MainViewModelFixture(viewModel, capture, archive, frameBatch);
        }

        public void AppendSceneEncounter(int playerId, string name, int damage, long start, long end) => MainViewModelCombatantFilterTests.AppendSceneEncounter(_captureService.Scene, playerId, name, damage, start, end);

        public void AppendSceneNickname(int playerId, string name) => MainViewModelCombatantFilterTests.AppendSceneNickname(_captureService.Scene, playerId, name);

        public void AppendSceneIdentity(int playerId, string name, bool isLocalPlayer = false) => MainViewModelCombatantFilterTests.AppendSceneIdentity(_captureService.Scene, playerId, name, isLocalPlayer);

        public void AppendScenePlayerGroupMember(int playerId, PlayerGroupMembership membership) => MainViewModelCombatantFilterTests.AppendScenePlayerGroupMember(_captureService.Scene, playerId, membership);

        public IRuntimeObservationSink CreateLiveSink() => SceneSinkFactory.CreateForLive(_captureService.Scene)();

        public SceneCombatSnapshot CreateSceneSnapshot() => _captureService.Scene.Owner.CreateSnapshot();

        public void AppendSceneDamage(int sourceId, int targetId, int skillCode, int damage, long timestamp, long flushId) => MainViewModelCombatantFilterTests.AppendSceneDamage(_captureService.Scene, sourceId, targetId, skillCode, damage, timestamp, flushId);

        public void AppendSceneHealing(int sourceId, int targetId, int skillCode, int healing, long timestamp, long flushId) => MainViewModelCombatantFilterTests.AppendSceneDamage(_captureService.Scene, sourceId, targetId, skillCode, healing, timestamp, flushId, CombatResourceKind.Health);

        public void AppendSceneHp(int entityId, long hp, long timestamp, long flushId) => MainViewModelCombatantFilterTests.AppendSceneHp(_captureService.Scene, entityId, hp, timestamp, flushId);

        public void AppendSceneGroupStatus(int entityId, long hp, long maxHp, long timestamp, long flushId) => MainViewModelCombatantFilterTests.AppendSceneGroupStatus(_captureService.Scene, entityId, hp, maxHp, timestamp, flushId);

        public void AppendSceneNpc(int instanceId, int npcCode, NpcKind kind) => MainViewModelCombatantFilterTests.AppendSceneNpc(_captureService.Scene, instanceId, npcCode, kind);

        public void AppendSceneBossFocus(int instanceId, string name, int hp, int maxHp, long timestamp) => MainViewModelCombatantFilterTests.AppendSceneBossFocus(_captureService.Scene, instanceId, name, hp, maxHp, timestamp);

        public void AppendSceneBossFocus(int instanceId, int npcCode, NpcKind kind, int hp, int maxHp, long timestamp) => MainViewModelCombatantFilterTests.AppendSceneBossFocus(_captureService.Scene, instanceId, npcCode, kind, hp, maxHp, timestamp);

        public void AppendSceneBossFocusUnknownHp(int instanceId, string name, long timestamp) => MainViewModelCombatantFilterTests.AppendSceneBossFocusUnknownHp(_captureService.Scene, instanceId, name, timestamp);

        public void AppendSceneMap(uint mapId, uint instanceId) => MainViewModelCombatantFilterTests.AppendSceneMap(_captureService.Scene, mapId, instanceId);

        public void ExpireBossFocusData() => _captureService.Scene.Owner.BossFocus.GetObservedBosses(long.MaxValue, SceneReadModelOwner.BossFocusVisibilityTimeoutMilliseconds);

        public void ExpireBossFocusDataAt(long nowMilliseconds) => _captureService.Scene.Owner.BossFocus.GetObservedBosses(nowMilliseconds, SceneReadModelOwner.BossFocusVisibilityTimeoutMilliseconds);

        public void FlushFrame() => _frameBatch.FlushFrame();
    }

    private static void AppendSceneEncounter(SceneLiveReadModel scene, int playerId, string name, int damage, long start, long end)
    {
        var sink = new JournalingRuntimeObservationSink(scene.Journal, scene.Clock, () => scene.SessionId, scene.NextFlushId);
        var origin = scene.SessionStarted.ToUnixTimeMilliseconds();
        sink.AppendNickname(playerId, name);
        AppendDamage(sink, playerId, 900_002, 11000010, damage / 2, origin + start, 1);
        AppendDamage(sink, playerId, 900_002, 11000010, damage - damage / 2, origin + end, 2);
        sink.CompleteFlush(1);
        sink.CompleteFlush(2);
    }

    private static void AppendSceneNickname(SceneLiveReadModel scene, int playerId, string name)
        => AppendSceneIdentity(scene, playerId, name);

    private static void AppendSceneIdentity(SceneLiveReadModel scene, int playerId, string name, bool isLocalPlayer = false)
    {
        var sink = new JournalingRuntimeObservationSink(scene.Journal, scene.Clock, () => scene.SessionId, scene.NextFlushId);
        sink.AppendNickname(playerId, name, isLocalPlayer: isLocalPlayer);
    }

    private static void AppendScenePlayerGroupMember(SceneLiveReadModel scene, int playerId, PlayerGroupMembership membership)
    {
        var sink = new JournalingRuntimeObservationSink(scene.Journal, scene.Clock, () => scene.SessionId, scene.NextFlushId);
        var source = SyntheticObservationExtensions.Source();
        sink.AppendPlayerGroupMember(in source, playerId, in membership);
    }

    private static void AppendLiveBattle(IRuntimeObservationSink sink, long sceneStartedMilliseconds, int playerId, string name, int damage, long start, long end, long firstFlushId)
    {
        sink.AppendNickname(playerId, name);
        AppendDamage(sink, playerId, 900_002, 11000010, damage / 2, sceneStartedMilliseconds + start, firstFlushId);
        AppendDamage(sink, playerId, 900_002, 11000010, damage - damage / 2, sceneStartedMilliseconds + end, firstFlushId + 1);
        sink.CompleteFlush(firstFlushId);
        sink.CompleteFlush(firstFlushId + 1);
    }

    private static void AppendSceneNpc(SceneLiveReadModel scene, int instanceId, int npcCode, NpcKind kind)
    {
        var sink = new JournalingRuntimeObservationSink(scene.Journal, scene.Clock, () => scene.SessionId, scene.NextFlushId);
        sink.AppendNpcCode(instanceId, npcCode);
        sink.AppendNpcKind(instanceId, kind);
    }

    private static void AppendSceneBossFocus(SceneLiveReadModel scene, int instanceId, string name, int hp, int maxHp, long timestamp)
    {
        var sink = new JournalingRuntimeObservationSink(scene.Journal, scene.Clock, () => scene.SessionId, scene.NextFlushId);
        var origin = scene.SessionStarted.ToUnixTimeMilliseconds();
        sink.AppendNpcName(2_100_351, name);
        sink.AppendNpcCode(instanceId, 2_100_351);
        sink.AppendNpcKind(instanceId, NpcKind.Boss);
        sink.SetNpcBattle(instanceId, true, origin + timestamp - 100);
        sink.AppendNpcHp(instanceId, hp, maxHp, origin + timestamp);
    }

    private static void AppendSceneBossFocus(SceneLiveReadModel scene, int instanceId, int npcCode, NpcKind kind, int hp, int maxHp, long timestamp)
    {
        var sink = new JournalingRuntimeObservationSink(scene.Journal, scene.Clock, () => scene.SessionId, scene.NextFlushId);
        var origin = scene.SessionStarted.ToUnixTimeMilliseconds();
        sink.AppendNpcCode(instanceId, npcCode);
        sink.AppendNpcKind(instanceId, kind);
        sink.SetNpcBattle(instanceId, true, origin + timestamp - 100);
        sink.AppendNpcHp(instanceId, hp, maxHp, origin + timestamp);
    }

    private static void AppendSceneBossFocusUnknownHp(SceneLiveReadModel scene, int instanceId, string name, long timestamp)
    {
        var sink = new JournalingRuntimeObservationSink(scene.Journal, scene.Clock, () => scene.SessionId, scene.NextFlushId);
        var origin = scene.SessionStarted.ToUnixTimeMilliseconds();
        sink.AppendNpcName(2_100_351, name);
        sink.AppendNpcCode(instanceId, 2_100_351);
        sink.AppendNpcKind(instanceId, NpcKind.Boss);
        sink.SetNpcBattle(instanceId, true, origin + timestamp);
    }

    private static void AppendSceneDamage(SceneLiveReadModel scene, int sourceId, int targetId, int skillCode, int damage, long timestamp, long flushId, CombatResourceKind resourceKind = CombatResourceKind.Unknown)
    {
        var sink = new JournalingRuntimeObservationSink(scene.Journal, scene.Clock, () => scene.SessionId, scene.NextFlushId);
        AppendDamage(sink, sourceId, targetId, skillCode, damage, scene.SessionStarted.ToUnixTimeMilliseconds() + timestamp, flushId, resourceKind);
        sink.CompleteFlush(flushId);
    }

    private static void AppendSceneHp(SceneLiveReadModel scene, int entityId, long hp, long timestamp, long flushId)
    {
        var sink = new JournalingRuntimeObservationSink(scene.Journal, scene.Clock, () => scene.SessionId, scene.NextFlushId);
        sink.AppendNpcHp(new PacketObservationSource(scene.SessionStarted.ToUnixTimeMilliseconds() + timestamp, flushId, 0, 0, 0, default), entityId, hp);
        sink.CompleteFlush(flushId);
    }

    private static void AppendSceneGroupStatus(SceneLiveReadModel scene, int entityId, long hp, long maxHp, long timestamp, long flushId)
    {
        var sink = new JournalingRuntimeObservationSink(scene.Journal, scene.Clock, () => scene.SessionId, scene.NextFlushId);
        sink.AppendNpcHp(new PacketObservationSource(scene.SessionStarted.ToUnixTimeMilliseconds() + timestamp, flushId, 0x1B92, 0, 0, default), entityId, hp, maxHp);
        sink.CompleteFlush(flushId);
    }

    private static void AppendDamage(
        IRuntimeObservationSink sink,
        int sourceId,
        int targetId,
        int skillCode,
        int damage,
        long timestamp,
        long flushId,
        CombatResourceKind resourceKind = CombatResourceKind.Unknown)
    {
        var source = new PacketObservationSource(timestamp, flushId, 0, 0, 0, default);
        var observation = new CombatWireObservation
        {
            SkillCode = skillCode,
            Damage = damage,
            ResourceKind = resourceKind,
            HitCount = 1,
            AttemptCount = 1
        };
        sink.AppendCombatWireObservation(in source, sourceId, targetId, in observation);
    }

    private static void AppendSceneMap(SceneLiveReadModel scene, uint mapId, uint instanceId)
    {
        var sink = SceneSinkFactory.CreateForLive(scene)();
        var source = SyntheticObservationExtensions.Source(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (scene.Journal.Count == 0)
        {
            sink.SetCurrentMap(in source, mapId);
        }
        else
        {
            sink.StageMapCandidate(in source, mapId);
            sink.ConfirmDestinationMapArrival(in source);
        }

        if (instanceId != 0)
            sink.RegisterMapEvent(in source, instanceId);
    }

    private static void AppendScopedCombatants(MainViewModelFixture fixture)
    {
        fixture.AppendSceneIdentity(100, "Self", isLocalPlayer: true);
        fixture.AppendSceneIdentity(200, "Party");
        fixture.AppendSceneIdentity(300, "Force");
        fixture.AppendSceneIdentity(400, "Other");
        fixture.AppendScenePlayerGroupMember(100, PlayerGroupMembership.Force(7, 1, 1));
        fixture.AppendScenePlayerGroupMember(200, PlayerGroupMembership.Force(7, 1, 2));
        fixture.AppendScenePlayerGroupMember(300, PlayerGroupMembership.Force(7, 3, 1));
        fixture.AppendSceneDamage(100, 900_002, 11000010, 1_000, 3_000, 1);
        fixture.AppendSceneDamage(200, 900_002, 11000010, 800, 3_100, 2);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 600, 3_200, 3);
        fixture.AppendSceneDamage(400, 900_002, 11000010, 400, 3_300, 4);
    }

    private static double GetHueDistance(Color left, Color right)
    {
        var delta = Math.Abs(GetHue(left) - GetHue(right));
        return Math.Min(delta, 360d - delta);
    }

    private static double GetHue(Color color) => GetHsl(color).Hue;

    private static (double Hue, double Saturation, double Lightness) GetHsl(Color color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var lightness = (max + min) / 2d;
        if (delta == 0)
            return (0, 0, lightness);

        var hue = max == r
            ? 60d * (((g - b) / delta) % 6d)
            : max == g
                ? 60d * ((b - r) / delta + 2d)
                : 60d * ((r - g) / delta + 4d);
        var saturation = delta / (1d - Math.Abs(2d * lightness - 1d));
        return (hue < 0 ? hue + 360d : hue, saturation, lightness);
    }

    private static SkillDisplayCatalog BuildSkillMap()
    {
        return
        [
            new SkillDisplayEntry(11000010, "Strike", SkillCategory.Gladiator, SkillSourceType.PcSkill),
            new SkillDisplayEntry(17000010, "Heal", SkillCategory.Cleric, SkillSourceType.PcSkill)
        ];
    }
}
