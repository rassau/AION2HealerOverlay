using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

// The Sum row under a boss fight's rows.
public sealed class BossFightSumTests
{
    private readonly UiFrameBatchService _frameBatch = new();

    // The rows added up, the shares to the group's. The recovery time is the bottom bar's: the UHS over the damage taken
    // of the rows with UHS (300 and 302: 800 over 500), not over 301's too (a stranger, no max HP, no UHS), and not the
    // rows' recovery times added up. Without UHS there is none ("–").
    [Fact]
    public void The_Sum_Adds_The_Rows_Up_With_The_Recovery_Time_Of_The_Rows_With_Uhs()
    {
        var fight = Fight(
            Row(300, dps: 1_000, damage: 30_000, taken: 400, uhs: 600, aggro: 0.6, stagger: 0.5, boss: 0.3, kills: 3, lastHits: 2),
            Row(301, dps: 500, damage: 15_000, taken: 700, aggro: 0.3, stagger: 0.25, boss: 0.15, kills: 1, credited: 5),
            Row(302, dps: 100, damage: 3_000, hps: 2_000, healing: 60_000, taken: 100, uhs: 200, aggro: 0.1, boss: 0.03, lastHits: 1));

        fight.RefreshSum();

        var sum = fight.Sum;
        Assert.True(fight.HasSum);
        Assert.Equal((1_600d, 48_000d, 2_000d, 60_000d, 1_200d, 800d),
            (sum.DamagePerSecond, sum.Damage, sum.HealingPerSecond, sum.Healing, sum.DamageTaken, sum.UnhealedHealthSeconds));
        Assert.Equal(1.6, sum.RecoveryTime);
        Assert.Equal(1, sum.AggroShare, 6);
        Assert.Equal(0.75, sum.StaggerShare, 6);
        Assert.Equal(0.48, sum.BossShareRatio, 6);
        Assert.Equal((4d, 3d, 5d), (sum.Kills, sum.LastHits, sum.Credited));

        var strangers = Fight(Row(400, taken: 500), Row(401, taken: 300));
        strangers.RefreshSum();
        Assert.True(double.IsNaN(strangers.Sum.RecoveryTime));
    }

    // One row is its own sum: no Sum row. A folded fight shows none either, until it's unfolded; nor does any while "Add
    // boss sum row" is off.
    [Fact]
    public void The_Sum_Row_Shows_From_Two_Rows_On_While_The_Fight_Is_Unfolded()
    {
        var fight = Fight(Row(300, damage: 1_000));
        fight.ShowSum = true;
        var changed = new List<string?>();
        fight.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        fight.RefreshSum();
        _frameBatch.FlushFrame();
        Assert.False(fight.HasSum);
        Assert.False(fight.IsSumVisible);

        fight.Rows.Add(Row(301, damage: 500));
        fight.RefreshSum();
        _frameBatch.FlushFrame();
        Assert.True(fight.IsSumVisible);
        Assert.Equal(1_500, fight.Sum.Damage);
        Assert.Contains(nameof(BossFightViewModel.IsSumVisible), changed);

        changed.Clear();
        fight.IsExpanded = false;
        _frameBatch.FlushFrame();
        Assert.False(fight.IsSumVisible);
        Assert.Contains(nameof(BossFightViewModel.IsSumVisible), changed);

        fight.IsExpanded = true;
        _frameBatch.FlushFrame();
        changed.Clear();
        fight.ShowSum = false;
        _frameBatch.FlushFrame();
        Assert.False(fight.IsSumVisible);
        Assert.Contains(nameof(BossFightViewModel.IsSumVisible), changed);
    }

    // "Add boss sum row" is off at first: the sums are there, the rows aren't shown. Turned on, the current pull's and the
    // list's fights show theirs at once (a fight of the list once it's unfolded); off, they go.
    [Fact]
    public void The_Sum_Row_Shows_Only_With_Add_Boss_Sum_Row_On()
    {
        var fixture = MainViewModelCombatantFilterTests.MainViewModelFixture.Create();
        var viewModel = fixture.ViewModel;
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneNickname(301, "Second");
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 50_000, 50_000, 2_500);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 1_000, 3_000, 1);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 3_000, 3_100, 2);
        fixture.AppendSceneHp(900_002, 46_000, 3_200, 3);
        fixture.AppendSceneHp(900_002, 50_000, 9_000, 4);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 12_000, 5);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 1_500, 12_100, 6);
        viewModel.RefreshCombatStatsForTesting();
        var (current, wipe) = (viewModel.CurrentBossFight, Assert.Single(viewModel.BossFights));
        wipe.IsExpanded = true;

        Assert.False(fixture.Settings.ShowBossSumRow);
        Assert.True(current.HasSum && wipe.HasSum);
        Assert.False(current.IsSumVisible || wipe.IsSumVisible);

        fixture.Settings.ShowBossSumRow = true;
        Assert.True(current.IsSumVisible && wipe.IsSumVisible);

        fixture.Settings.ShowBossSumRow = false;
        Assert.False(current.IsSumVisible || wipe.IsSumVisible);
    }

    // Live, the current pull's Sum row follows its rows and the wipe in the list keeps its own; a history entry's fights
    // get theirs when it's opened, filled fresh from the archive. The wipe's add went to Second (a kill).
    [Fact]
    public void Every_Boss_Fight_Gets_Its_Sum_Live_And_In_A_History_Entry()
    {
        var fixture = MainViewModelCombatantFilterTests.MainViewModelFixture.Create();
        var viewModel = fixture.ViewModel;
        fixture.AppendSceneNickname(300, "First");
        fixture.AppendSceneNickname(301, "Second");
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 50_000, 50_000, 2_500);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 1_000, 3_000, 1);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 3_000, 3_100, 2);
        fixture.AppendSceneHp(900_002, 46_000, 3_200, 3);
        fixture.AppendSceneNpc(900_100, 2_100_352, NpcKind.Monster);
        fixture.AppendSceneHp(900_100, 1_000, 3_300, 8);
        fixture.AppendSceneDamage(301, 900_100, 11000010, 1_000, 3_400, 9);
        fixture.AppendSceneHp(900_100, 0, 3_400, 10);
        fixture.AppendSceneHp(900_002, 50_000, 9_000, 4);
        fixture.AppendSceneDamage(300, 900_002, 11000010, 500, 12_000, 5);
        fixture.AppendSceneDamage(301, 900_002, 11000010, 1_500, 12_100, 6);
        viewModel.RefreshCombatStatsForTesting();

        AssertSum(Assert.Single(viewModel.BossFights), damage: 4_000, bossShare: 0.08, kills: 1);
        AssertSum(viewModel.CurrentBossFight, damage: 2_000, bossShare: 0.04, kills: 0);

        fixture.AppendSceneDamage(301, 900_002, 11000010, 1_000, 13_000, 7);
        viewModel.RefreshCombatStatsForTesting();
        AssertSum(viewModel.CurrentBossFight, damage: 3_000, bossShare: 0.06, kills: 0);

        viewModel.ArchiveCurrentEncounterCommand.Execute(null);
        var history = Assert.Single(viewModel.EncounterHistory);
        viewModel.ReturnToLiveCommand.Execute(null);
        Assert.Empty(viewModel.BossFights);

        viewModel.SelectedEncounterHistory = history;

        List<BossFightViewModel> shown = [.. viewModel.BossFights];
        if (viewModel.CurrentBossFight.IsVisible)
            shown.Add(viewModel.CurrentBossFight);
        Assert.Equal(2, shown.Count);
        AssertSum(shown[0], damage: 4_000, bossShare: 0.08, kills: 1);
        AssertSum(shown[1], damage: 3_000, bossShare: 0.06, kills: 0);

        static void AssertSum(BossFightViewModel fight, double damage, double bossShare, double kills)
        {
            Assert.Equal(2, fight.Rows.Count);
            Assert.True(fight.HasSum);
            Assert.Equal(damage, fight.Sum.Damage);
            Assert.Equal(fight.Rows.Sum(static row => row.DamagePerSecond), fight.Sum.DamagePerSecond, 6);
            Assert.Equal(bossShare, fight.Sum.BossShareRatio, 6);
            Assert.Equal(kills, fight.Sum.Kills);
        }
    }

    private BossFightViewModel Fight(params CombatantRowViewModel[] rows)
    {
        var fight = new BossFightViewModel(_frameBatch, new CombatantColumnLayoutViewModel(_frameBatch), 0, isExpanded: true);
        foreach (var row in rows)
            fight.Rows.Add(row);
        return fight;
    }

    private CombatantRowViewModel Row(int id, double dps = 0, double damage = 0, double hps = 0, double healing = 0, double taken = 0, double uhs = 0,
        double aggro = 0, double stagger = 0, double boss = 0, double kills = 0, double lastHits = 0, double credited = 0)
    {
        var row = new CombatantRowViewModel(_frameBatch, new CombatantColumnLayoutViewModel(_frameBatch), id, null, dps, hps, damage, healing, taken, uhs)
        {
            AggroShare = aggro,
            StaggerShare = stagger,
            Kills = kills,
            LastHits = lastHits,
            Credited = credited,
        };
        row.UpdateBossShare(boss);
        return row;
    }
}
