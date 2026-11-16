using System.Globalization;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.Tests.SceneRuntime;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

public sealed class BossParticipationTests
{
    private const int Me = 101;
    private const double Needed = 0.035;

    // The loot bag against 3.5 % of the boss: green once the player's damage on it is 3.5 % of its HP; yellow while it isn't
    // yet but their share of the damage dealt so far is (what they end with if everyone keeps their pace); red otherwise.
    // After a kill, its result (no yellow then); nothing after a wipe, for a training dummy's pull, or while the player
    // isn't known.
    [Fact]
    public void The_Loot_Bag_Goes_By_The_Players_Share_Of_The_Boss()
    {
        Assert.Equal(BossParticipationState.Reached, State(Fight(BossFightOutcome.Ongoing, mine: 50, others: 100)));
        Assert.Equal(BossParticipationState.OnCourse, State(Fight(BossFightOutcome.Ongoing, mine: 10, others: 190)));
        Assert.Equal(BossParticipationState.Short, State(Fight(BossFightOutcome.Ongoing, mine: 2, others: 198)));

        var (state, share, expected, final) = BossParticipationViewModel.Assess([Fight(BossFightOutcome.Killed, mine: 20, others: 980)], Me, Needed);
        Assert.Equal((BossParticipationState.Short, 0.02, 0.02, true), (state, share, expected, final));
        Assert.Equal(BossParticipationState.Reached, State(Fight(BossFightOutcome.Killed, mine: 40, others: 960)));
        Assert.Equal(BossParticipationState.None, State(Fight(BossFightOutcome.Killed, mine: 40, others: 960), Fight(BossFightOutcome.Reset, mine: 0, others: 500)));
        Assert.Equal(BossParticipationState.None, State(Fight(BossFightOutcome.Ongoing, mine: 1, others: 1) with { IsTrainingDummy = true }));
        Assert.Equal(BossParticipationState.None, BossParticipationViewModel.Assess([Fight(BossFightOutcome.Ongoing, mine: 1, others: 1)], 0, Needed).State);
    }

    // A boss with an add (a boss-kind NPC pulled while the boss is on): the boss is judged, the add's numbers don't count,
    // during the fight and after the kill, whichever of the two went down first. Pulls that don't overlap are separate.
    [Fact]
    public void The_Boss_Is_Judged_Not_Its_Add()
    {
        var boss = Fight(BossFightOutcome.Ongoing, mine: 50, others: 100, start: 0, end: 60_000);
        var add = Fight(BossFightOutcome.Ongoing, mine: 0, others: 30, start: 10_000, end: 20_000) with { BossMaxHp = 30 };
        Assert.Equal(BossParticipationState.Reached, State(boss, add));
        Assert.Equal(BossParticipationState.Reached, State(boss with { Outcome = BossFightOutcome.Killed }, add with { Outcome = BossFightOutcome.Killed }));
        var (_, share, _, final) = BossParticipationViewModel.Assess([boss with { Outcome = BossFightOutcome.Killed }, add with { Outcome = BossFightOutcome.Killed }], Me, Needed);
        Assert.Equal((0.05, true), (share, final));

        var earlierKill = Fight(BossFightOutcome.Killed, mine: 50, others: 100, start: 0, end: 60_000);
        var laterPull = Fight(BossFightOutcome.Ongoing, mine: 2, others: 198, start: 300_000, end: 310_000) with { BossMaxHp = 500 };
        Assert.Equal(BossParticipationState.Short, State(earlierKill, laterPull));
    }

    // While the boss's HP isn't known its share can't be told: on course at best, and no result after the kill. The hover
    // text cuts shares off rather than round them up to the bar.
    [Fact]
    public void An_Unknown_Boss_HP_Never_Reads_As_Reached()
    {
        var unknown = Fight(BossFightOutcome.Ongoing, mine: 50, others: 100) with { BossMaxHp = 0 };
        Assert.Equal(BossParticipationState.OnCourse, State(unknown));
        Assert.Equal(BossParticipationState.None, State(unknown with { Outcome = BossFightOutcome.Killed }));

        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal("3.4 %", BossParticipationViewModel.Percent(0.03499));
            Assert.Equal("4.5 %", BossParticipationViewModel.Percent(0.045));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    // While the tutorial shows its fight the live pulls keep coming in; after it, the loot bag shows them as they are by then.
    [Fact]
    public void After_The_Tutorial_The_Loot_Bag_Shows_The_Live_Pull_Again()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aion2flow-loot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var settingsService = new SettingsService(Path.Combine(directory, "settings.json"));
            var language = new LanguageService();
            var localization = new LocalizationService(language);
            using var watcher = new ProcessForegroundWatcher();
            var settings = new SettingsFlyoutViewModel(localization, language, settingsService, new PlayerNameDisplayService(settingsService, localization),
                new UiScaleService(settingsService), new AppUpdateService(), watcher, new GlobalHotkeyService());
            var bag = settings.BossParticipation;
            bag.Update([Fight(BossFightOutcome.Ongoing, mine: 2, others: 198)], Me);
            Assert.True(bag.IsShort);

            bag.ShowDemo([Fight(BossFightOutcome.Killed, mine: 50, others: 950)], Me);
            bag.Update([Fight(BossFightOutcome.Ongoing, mine: 10, others: 190)], Me);
            Assert.True(bag.IsReached);

            bag.EndDemo();
            Assert.True(bag.IsOnCourse);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static BossParticipationState State(params SceneBossFight[] fights) => BossParticipationViewModel.Assess(fights, Me, Needed).State;

    // A pull on a boss with 1,000 HP: the player's damage on it and the rest of the group's, between start and end.
    private static SceneBossFight Fight(BossFightOutcome outcome, long mine, long others, long start = 0, long end = 0) =>
        new(BossId: 900, BossNumber: 1, Attempt: 1, outcome, BossHp: 0, BossMaxHp: 1_000, SceneSnapshotTestFactory.Create(encounterStartTime: start, encounterEndTime: end, combatants:
        [
            SceneSnapshotTestFactory.Combatant(Me, SceneSnapshotTestFactory.VisibleMetrics(damageAmount: mine)),
            SceneSnapshotTestFactory.Combatant(102, SceneSnapshotTestFactory.VisibleMetrics(damageAmount: others)),
        ]));
}
