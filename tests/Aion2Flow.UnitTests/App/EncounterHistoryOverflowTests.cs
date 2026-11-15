using Avalonia.Threading;
using Cloris.Aion2Flow.SceneRuntime.Archive;

namespace Cloris.Aion2Flow.Tests.App;

// The history keeps 10 entries. A history entry stays on the meter while play goes on and map changes archive scenes
// past it; once they push it out of the history, the meter goes back to live, as removing the entry does. It stayed
// in history mode with nothing selected: the live table never came back until Live was clicked.
[Collection(AvaloniaTestCollection.Name)]
public sealed class EncounterHistoryOverflowTests
{
    [Fact]
    public void An_Entry_Pushed_Out_Of_The_History_Takes_The_Meter_Back_To_Live()
    {
        AvaloniaTestHost.Run(() =>
        {
            var fixture = MainViewModelCombatantFilterTests.MainViewModelFixture.Create();
            var viewModel = fixture.ViewModel;
            fixture.AppendSceneMap(200000, 1);
            fixture.AppendSceneEncounter(300, "Viewed Player", 400, 3_000, 5_000);
            viewModel.RefreshCombatStatsForTesting();
            viewModel.ArchiveCurrentEncounterCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            var viewed = viewModel.SelectedEncounterHistory!.Record;

            for (var i = 1; i <= EncounterArchiveService.MaxHistoryCount; i++)
            {
                fixture.AppendSceneEncounter(400 + i, "Other Player", 400, 3_000, 5_000);
                viewModel.RefreshCombatStatsForTesting();
                fixture.AppendSceneMap((uint)(200000 + i), (uint)(1 + i));
                viewModel.RefreshCombatStatsForTesting();
                // The history is rebuilt in a posted job, as in the app.
                Dispatcher.UIThread.RunJobs();

                // Still in the history: still shown.
                if (i < EncounterArchiveService.MaxHistoryCount)
                {
                    Assert.True(viewModel.IsViewingArchivedEncounter);
                    Assert.Same(viewed, viewModel.SelectedEncounterHistory?.Record);
                    Assert.Equal(300, Assert.Single(viewModel.Combatants).Id);
                }
            }

            Assert.DoesNotContain(viewed, fixture.Archive.History);
            Assert.False(viewModel.IsViewingArchivedEncounter);
            Assert.Null(viewModel.SelectedEncounterHistory);

            fixture.AppendSceneEncounter(999, "Fresh Player", 800, 6_000, 8_000);
            viewModel.RefreshCombatStatsForTesting();
            Assert.Equal(999, Assert.Single(viewModel.Combatants).Id);
        });
    }

    // Opening a stored run (fight graphs) can push the shown entry out too. ShowHistoryEntry selects the stored run
    // before the posted rebuild runs, so the meter goes straight to it and the open player stays, as before the fix.
    // This is why the way back to live sits in the posted job: inside RebuildEncounterHistory it would pass through
    // live on the way and drop the player's row and details.
    [Fact]
    public void A_Stored_Run_Pushing_The_Shown_Entry_Out_Keeps_The_Open_Player()
    {
        AvaloniaTestHost.Run(() =>
        {
            var source = MainViewModelCombatantFilterTests.MainViewModelFixture.Create();
            source.AppendSceneEncounter(300, "Viewed Player", 900, 3_000, 5_000);
            source.ViewModel.RefreshCombatStatsForTesting();
            source.ViewModel.ArchiveCurrentEncounterCommand.Execute(null);
            var stored = source.Archive.History[0];

            var fixture = MainViewModelCombatantFilterTests.MainViewModelFixture.Create();
            var viewModel = fixture.ViewModel;
            for (var i = 0; i < EncounterArchiveService.MaxHistoryCount; i++)
            {
                fixture.AppendSceneEncounter(300, "Viewed Player", 400 + i, 3_000, 5_000);
                viewModel.RefreshCombatStatsForTesting();
                fixture.AppendSceneMap((uint)(200000 + i), (uint)(1 + i));
                viewModel.RefreshCombatStatsForTesting();
                Dispatcher.UIThread.RunJobs();
            }

            viewModel.SelectedEncounterHistory = viewModel.EncounterHistory[^1];
            var viewed = viewModel.SelectedEncounterHistory.Record;
            Assert.True(viewModel.Combatants.TryGetValue(300, out var row));
            viewModel.SelectCombatantRow(row);
            var rowChanges = 0;
            viewModel.Combatants.CollectionChanged += (_, _) => rowChanges++;

            viewModel.ShowHistoryEntry(stored);
            Dispatcher.UIThread.RunJobs();

            Assert.DoesNotContain(viewed, fixture.Archive.History);
            Assert.True(viewModel.IsViewingArchivedEncounter);
            Assert.Same(stored, viewModel.SelectedEncounterHistory?.Record);
            Assert.Same(row, viewModel.SelectedCombatant);
            Assert.Equal(300, viewModel.CombatantDetails.SelectedCombatantId);
            Assert.Equal(0, rowChanges);
        });
    }
}
