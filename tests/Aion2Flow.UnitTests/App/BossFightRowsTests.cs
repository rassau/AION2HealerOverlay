using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

// A boss fight's players under its header: no more rows than the main table shows (Settings → DPS meter → Visible rows),
// the rest scrolled with the mouse wheel as there, the Sum row under them. The meter itself isn't built in tests, so the
// two lists are set up here as MainWindow.axaml sets them up (the fight's rows 22 high, the table's 36 and 1 apart), on
// the meter's view model with a fight of real players.
[Collection(AvaloniaTestCollection.Name)]
public sealed class BossFightRowsTests
{
    private const double RowHeight = 22;
    private const double TableRowExtent = 37;

    // 20 players and 6 rows: 6 rows, and a wheel notch over them scrolls them by a row; the main table, the Sum row and the
    // meter's height stay. At the end the wheel goes on to the meter.
    [Fact]
    public void A_Boss_Fight_With_More_Players_Than_Rows_Shows_That_Many_And_Scrolls_The_Rest()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (main, window, fight, sum, table) = Show(players: 20, rows: 6);
            try
            {
                var players = main.CurrentBossFight.Rows;
                Assert.Equal(20, players.Count);
                Assert.Equal(6 * RowHeight, fight.Bounds.Height);
                var (height, sumTop) = (window.ClientSize.Height, sum.Bounds.Top);

                Assert.True(Wheel(fight, notches: 1));
                Assert.Same(players[1], TopRow(fight));
                Assert.Same(main.Combatants[0], TopRow(table));
                Assert.Equal((height, sumTop), (window.ClientSize.Height, sum.Bounds.Top));

                Assert.True(Wheel(fight, notches: 20));
                Assert.Same(players[14], TopRow(fight));
                Assert.False(Wheel(fight, notches: 1));
            }
            finally
            {
                Close(window);
            }
        });
    }

    // 4 players: their 4 rows with the Sum row right under them, and nothing to scroll.
    [Fact]
    public void A_Boss_Fight_With_Fewer_Players_Than_Rows_Shows_Them_All()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (main, window, fight, sum, _) = Show(players: 4, rows: 6);
            try
            {
                Assert.Equal(4, main.CurrentBossFight.Rows.Count);
                Assert.Equal(4 * RowHeight, fight.Bounds.Height);
                Assert.Equal(fight.Bounds.Bottom, sum.Bounds.Top);

                Assert.False(Wheel(fight, notches: 1));
                Assert.Same(main.CurrentBossFight.Rows[0], TopRow(fight));
            }
            finally
            {
                Close(window);
            }
        });
    }

    // The setting changed: the fight shows as many rows at once, as the table does, and the meter's height follows.
    [Fact]
    public void A_Boss_Fight_Follows_The_Visible_Rows_Setting()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (main, window, fight, _, table) = Show(players: 20, rows: 6);
            try
            {
                var height = window.ClientSize.Height;

                main.SettingsFlyout.MaxVisibleCombatantRows = 8;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(8 * RowHeight, fight.Bounds.Height);
                Assert.Equal(8 * TableRowExtent - 1, table.Bounds.Height);
                Assert.Equal(height + 2 * (RowHeight + TableRowExtent), window.ClientSize.Height);

                main.SettingsFlyout.MaxVisibleCombatantRows = 5;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(5 * RowHeight, fight.Bounds.Height);
            }
            finally
            {
                Close(window);
            }
        });
    }

    // The meter's view model with a boss fight of the given number of players (the current pull), the fight's rows, a Sum
    // row and the main table in a window that takes its content's height.
    private static (MainViewModel Main, Window Window, AnimatedItemsView Fight, Border Sum, AnimatedItemsView Table) Show(int players, int rows)
    {
        var fixture = MainViewModelCombatantFilterTests.MainViewModelFixture.Create();
        fixture.AppendSceneBossFocus(900_002, "Scene Boss", 500_000, 500_000, 2_500);
        for (var i = 0; i < players; i++)
        {
            fixture.AppendSceneNickname(300 + i, $"Player {i + 1}");
            fixture.AppendSceneDamage(300 + i, 900_002, 11000010, 1_000 + 100 * i, 3_000 + i, i + 1);
        }

        var main = fixture.ViewModel;
        main.SettingsFlyout.MaxVisibleCombatantRows = rows;
        main.RefreshCombatStatsForTesting();

        var fight = Rows(main.CurrentBossFight.Rows, RowHeight, 0);
        var sum = new Border { Height = RowHeight };
        var table = Rows(main.Combatants, TableRowExtent - 1, 1);
        var window = new Window
        {
            Width = 334,
            SizeToContent = SizeToContent.Height,
            DataContext = main,
            Content = new StackPanel { Children = { fight, sum, table } }
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (main, window, fight, sum, table);
    }

    private static AnimatedItemsView Rows(IEnumerable rows, double height, double spacing)
    {
        var view = new AnimatedItemsView
        {
            ItemsSource = rows,
            ItemHeight = height,
            ItemSpacing = spacing,
            ItemTemplate = new FuncDataTemplate<CombatantRowViewModel>(static (_, _) => new Border())
        };
        view.Bind(AnimatedItemsView.MaxVisibleItemsProperty, new ReflectionBinding("SettingsFlyout.MaxVisibleCombatantRows"));
        return view;
    }

    // Wheel notches down over the list's top row, the scroll run to its end: whether the list took them.
    private static bool Wheel(AnimatedItemsView list, int notches)
    {
        var row = list.Children.OfType<AnimatedItemsViewItem>().First(static row => Math.Abs(row.Bounds.Y) < 0.5);
        var wheel = new PointerWheelEventArgs(row, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true), TopLevel.GetTopLevel(row)!, default, 0,
            new PointerPointProperties(), KeyModifiers.None, new Vector(0, -notches));
        row.RaiseEvent(wheel);
        list.AdvanceScrollAnimation(TimeSpan.Zero);
        list.AdvanceScrollAnimation(TimeSpan.FromSeconds(1));
        Dispatcher.UIThread.RunJobs();
        return wheel.Handled;
    }

    private static object? TopRow(AnimatedItemsView list) =>
        list.Children.OfType<AnimatedItemsViewItem>().Single(static row => Math.Abs(row.Bounds.Y) < 0.5).Content;

    private static void Close(Window window)
    {
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
