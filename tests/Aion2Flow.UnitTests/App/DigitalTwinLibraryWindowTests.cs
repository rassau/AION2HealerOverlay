using System.Globalization;
using Cloris.Aion2Flow.SceneRuntime.Twins;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Tests.SceneRuntime;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class DigitalTwinLibraryWindowTests
{
    // Adding manually: the region's servers by name with their side, the player's own server chosen to start with; the
    // lookup goes to the twin service, which puts the twin in the library when done. Another region lists its own servers.
    [Fact]
    public void Add_Manually_Looks_Up_A_Character_On_The_Chosen_Server()
    {
        AvaloniaTestHost.Run(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), $"aion2heal-twins-{Guid.NewGuid():N}");
            using var localization = new LocalizationService(new LanguageService());
            using var twins = new DigitalTwinService(new HttpClient(new SilentWebsite()), TimeSpan.Zero);
            var servers = new Dictionary<int, string> { [2304] = "Lumiel", [1301] = "Siel", [2301] = "Israphel", [1101] = "Siel", [1007] = "Test" };
            using var viewModel = new DigitalTwinLibraryViewModel(new DigitalTwinLibrary(folder), twins, localization, servers, homeServerId: 2304);

            Assert.Equal(("eu", 2304), (viewModel.SelectedRegion!.Code, viewModel.SelectedServer!.Id));
            Assert.Equal([2301, 2304, 1301], viewModel.Servers.Select(static server => server.Id));
            Assert.Equal($"Lumiel · {localization["TwinLibrary_Asmodian"]}", viewModel.SelectedServer.ToString());
            Assert.False(viewModel.AddCommand.CanExecute(null));

            viewModel.LookUpName = " Healy ";
            Assert.True(viewModel.AddCommand.CanExecute(null));
            viewModel.AddCommand.Execute(null);

            Assert.Contains(twins.Get(2304, "Healy").State, (DigitalTwinState[])[DigitalTwinState.Queued, DigitalTwinState.Loading]);
            Assert.Contains("Healy", viewModel.AddStatus);
            Assert.Contains("Lumiel", viewModel.AddStatus);

            viewModel.SelectedRegion = viewModel.Regions.Single(static region => region.Code == "nae");
            Assert.Equal([1101], viewModel.Servers.Select(static server => server.Id));
            Assert.Null(viewModel.SelectedServer);
            Assert.False(viewModel.AddCommand.CanExecute(null));
        });
    }

    // A snapshot's trash button asks first ("Delete?"), the second click deletes its file; another row's click asks anew.
    [Fact]
    public void Delete_Asks_Once_Then_Deletes_The_Snapshot()
    {
        AvaloniaTestHost.Run(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), $"aion2heal-twins-{Guid.NewGuid():N}");
            try
            {
                using var localization = new LocalizationService(new LanguageService());
                using var twins = new DigitalTwinService(new HttpClient(new SilentWebsite()), TimeSpan.Zero);
                var library = new DigitalTwinLibrary(folder);
                var twin = Cloris.Aion2Flow.Tests.SceneRuntime.DigitalTwinBuilderTests.Build();
                library.Add(twin);
                library.Add(twin with { FetchedAt = twin.FetchedAt.AddHours(1) });
                using var viewModel = new DigitalTwinLibraryViewModel(library, twins, localization, new Dictionary<int, string>(), homeServerId: null);
                var load = viewModel.LoadAsync();
                RunUntil(() => load.IsCompleted);

                var (newest, oldest) = (viewModel.Twins[0], viewModel.Twins[1]);
                newest.DeleteCommand.Execute(null);
                Assert.True(newest.IsDeleteArmed);
                oldest.DeleteCommand.Execute(null);
                Assert.Equal((false, true), (newest.IsDeleteArmed, oldest.IsDeleteArmed));
                Assert.Equal(2, library.Load().Count);

                oldest.DeleteCommand.Execute(null);
                Assert.Equal([newest], viewModel.Twins);
                Assert.Equal(twin.FetchedAt.AddHours(1), Assert.Single(library.Load()).Twin.FetchedAt);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
    }

    // A click on a snapshot opens its cards on a tab of its own. Snapshots stay ticked, in the order ticked, when the list
    // reloads; "Compare" takes two to five and puts them side by side on the comparison tab, the first ticked as the
    // reference. Clicking the open tab's button keeps it open.
    [Fact]
    public void Snapshots_Open_On_Their_Own_Tab_And_Two_To_Five_Are_Compared()
    {
        AvaloniaTestHost.Run(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), $"aion2heal-twins-{Guid.NewGuid():N}");
            try
            {
                using var localization = new LocalizationService(new LanguageService());
                using var twins = new DigitalTwinService(new HttpClient(new SilentWebsite()), TimeSpan.Zero);
                var library = new DigitalTwinLibrary(folder);
                var twin = DigitalTwinBuilderTests.Build();
                for (var i = 0; i < 6; i++)
                    library.Add(twin with { Name = $"Healer{i}", FetchedAt = twin.FetchedAt.AddHours(i) });
                using var viewModel = new DigitalTwinLibraryViewModel(library, twins, localization, new Dictionary<int, string>(), homeServerId: null);
                var load = viewModel.LoadAsync();
                RunUntil(() => load.IsCompleted);
                var rows = viewModel.Twins.ToList();
                Assert.Equal(["Healer5", "Healer4", "Healer3", "Healer2", "Healer1", "Healer0"], rows.Select(static row => row.Name));
                Assert.True(viewModel.IsListTab);

                rows[2].OpenCommand.Execute(null);
                Assert.True(viewModel.IsTwinTab);
                Assert.Equal("Healer3", viewModel.OpenedName);
                Assert.Contains("Healer3", viewModel.OpenedTwin!.Header);
                Assert.Equal(twin.Totals.Length, viewModel.OpenedTwin.Stats.Length);

                rows[3].IsPicked = true;
                Assert.False(viewModel.CompareCommand.CanExecute(null));
                foreach (var row in rows.Take(3))
                    row.IsPicked = true;
                Assert.True(viewModel.CompareCommand.CanExecute(null));
                rows[5].IsPicked = true;
                rows[4].IsPicked = true;
                Assert.Contains("6", viewModel.CompareText);
                Assert.False(viewModel.CompareCommand.CanExecute(null));
                rows[4].IsPicked = false;
                Assert.Contains("5", viewModel.CompareText);

                library.Add(twin with { Name = "Newcomer" });
                RunUntil(() => viewModel.Twins.Count == 7);
                Assert.Equal(["Healer5", "Healer4", "Healer3", "Healer2", "Healer0"], viewModel.Twins.Where(static row => row.IsPicked).Select(static row => row.Name));

                viewModel.CompareCommand.Execute(null);
                Assert.True(viewModel.IsCompareTab);
                Assert.Equal(["Healer2", "Healer5", "Healer4", "Healer3", "Healer0"], viewModel.Comparison!.Columns.Select(static column => column.Name));
                viewModel.IsCompareTab = false;
                Assert.True(viewModel.IsCompareTab);
                viewModel.IsListTab = true;
                Assert.Equal((true, false), (viewModel.IsListTab, viewModel.IsCompareTab));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
    }

    // A snapshot's heart marks it as a favourite (filled) or not; the favourites tab lists the favourites (unticking the
    // others, so nothing unseen is compared or deleted), and its hearts take them off. The marks are kept beside the
    // snapshots, so a new window (or start) has them again.
    [Fact]
    public void Favourites_Are_Marked_Listed_And_Kept()
    {
        AvaloniaTestHost.Run(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), $"aion2heal-twins-{Guid.NewGuid():N}");
            try
            {
                using var localization = new LocalizationService(new LanguageService());
                using var twins = new DigitalTwinService(new HttpClient(new SilentWebsite()), TimeSpan.Zero);
                var library = new DigitalTwinLibrary(folder);
                var twin = DigitalTwinBuilderTests.Build();
                for (var i = 0; i < 3; i++)
                    library.Add(twin with { Name = $"Healer{i}", FetchedAt = twin.FetchedAt.AddHours(i) });
                using var viewModel = new DigitalTwinLibraryViewModel(library, twins, localization, new Dictionary<int, string>(), homeServerId: null);
                var load = viewModel.LoadAsync();
                RunUntil(() => load.IsCompleted);
                Assert.Equal(3, viewModel.ShownTwins.Count);
                Assert.All(viewModel.Twins, static row => Assert.False(row.IsFavourite));

                viewModel.Twins[1].ToggleFavouriteCommand.Execute(null);
                Assert.True(viewModel.Twins[1].IsFavourite);
                Assert.Same(viewModel.Twins, viewModel.ShownTwins);
                viewModel.Twins[0].IsPicked = true;
                viewModel.Twins[1].IsPicked = true;
                viewModel.IsFavouritesTab = true;
                Assert.True(viewModel.IsListShown);
                Assert.Equal(["Healer1"], viewModel.ShownTwins.Select(static row => row.Name));
                Assert.Null(viewModel.EmptyText);
                Assert.Equal(["Healer1"], viewModel.Twins.Where(static row => row.IsPicked).Select(static row => row.Name));
                Assert.Contains("1", viewModel.CompareText);

                using (var again = new DigitalTwinLibraryViewModel(library, twins, localization, new Dictionary<int, string>(), homeServerId: null))
                {
                    var reload = again.LoadAsync();
                    RunUntil(() => reload.IsCompleted);
                    Assert.Equal(["Healer1"], again.Twins.Where(static row => row.IsFavourite).Select(static row => row.Name));
                }

                viewModel.ShownTwins[0].ToggleFavouriteCommand.Execute(null);
                Assert.Empty(viewModel.ShownTwins);
                Assert.NotNull(viewModel.EmptyText);
                Assert.Empty(library.LoadFavourites());
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
    }

    // The trash beside "Compare" asks first ("Delete 2 entries?"), the second click deletes every ticked snapshot, with its
    // favourite mark; ticking another one asks anew.
    [Fact]
    public void The_Ticked_Snapshots_Are_Deleted_Together_After_Asking()
    {
        AvaloniaTestHost.Run(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), $"aion2heal-twins-{Guid.NewGuid():N}");
            try
            {
                using var localization = new LocalizationService(new LanguageService());
                using var twins = new DigitalTwinService(new HttpClient(new SilentWebsite()), TimeSpan.Zero);
                var library = new DigitalTwinLibrary(folder);
                var twin = DigitalTwinBuilderTests.Build();
                for (var i = 0; i < 4; i++)
                    library.Add(twin with { Name = $"Healer{i}", FetchedAt = twin.FetchedAt.AddHours(i) });
                using var viewModel = new DigitalTwinLibraryViewModel(library, twins, localization, new Dictionary<int, string>(), homeServerId: null);
                var load = viewModel.LoadAsync();
                RunUntil(() => load.IsCompleted);
                Assert.False(viewModel.DeleteTickedCommand.CanExecute(null));

                var rows = viewModel.Twins.ToList();
                rows[0].ToggleFavouriteCommand.Execute(null);
                rows[0].IsPicked = true;
                rows[2].IsPicked = true;
                viewModel.DeleteTickedCommand.Execute(null);
                Assert.Contains("2", viewModel.DeleteTickedText);
                Assert.Equal(4, library.Load().Count);
                rows[3].IsPicked = true;
                Assert.Null(viewModel.DeleteTickedText);
                rows[3].IsPicked = false;

                viewModel.DeleteTickedCommand.Execute(null);
                viewModel.DeleteTickedCommand.Execute(null);
                Assert.Equal(["Healer2", "Healer0"], viewModel.Twins.Select(static row => row.Name));
                Assert.Equal(["Healer2", "Healer0"], library.Load().Select(static entry => entry.Twin.Name));
                Assert.Empty(library.LoadFavourites());
                Assert.Null(viewModel.DeleteTickedText);
                Assert.False(viewModel.DeleteTickedCommand.CanExecute(null));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        });
    }

    // Skill levels are compared among the snapshots of the reference's class (the others show a dash, and a section none
    // but the reference has is left out); the Daevanion boards by name, with the nodes open on each.
    [Fact]
    public void The_Comparison_Has_Skill_Levels_Of_One_Class_And_Daevanion_Boards()
    {
        AvaloniaTestHost.Run(() =>
        {
            var culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                using var localization = new LocalizationService(new LanguageService());
                var cleric = DigitalTwinBuilderTests.Build();
                var stronger = cleric with
                {
                    Name = "Stronger",
                    Skills = [.. cleric.Skills.Select(static skill => skill with { Level = skill.Level + 3 })],
                    Boards = [.. cleric.Boards.Select(static board => board with { Id = board.Id + 100, OpenNodes = board.OpenNodes + 5 })],
                };
                var gladiator = cleric with { Name = "Gladiator", ClassName = "Gladiator", Skills = [new TwinSkill(11020000, "Keen Strike", 17, "Active", true)] };
                var comparison = new TwinComparisonViewModel([cleric, stronger, gladiator], localization) { ShowDifference = true };

                Assert.Equal(["12", "+3", "–"], Texts(comparison, "Earth's Retribution"));
                Assert.Equal(["10", "+3", "–"], Texts(comparison, "Radiant Recovery"));
                Assert.DoesNotContain(comparison.Rows, static row => row.Label == "Keen Strike");
                // "Nezekan (of 88)" in English; the tests share the UI language, so by the board's name.
                var nezekan = comparison.Rows.Single(static row => !row.IsSection && row.Label.StartsWith("Nezekan", StringComparison.Ordinal));
                Assert.Contains("88", nezekan.Label);
                Assert.Equal(["62", "+5", "±0"], nezekan.Cells.Select(static cell => cell.Text));
                Assert.Equal(["Earth's Retribution", "Radiant Recovery"], SectionRows(comparison, 3));
                Assert.Equal(5, comparison.Rows.Count(static row => row.IsSection));

                // A skill has its icon; hovering its name shows what it does from level 1 to max, hovering a snapshot's level
                // of it the numbers at that level; the gladiator has a dash and nothing to hover.
                var retribution = comparison.Rows.Single(static row => row.Label == "Earth's Retribution");
                Assert.Equal("ICON_CL_SKILL_001.webp", retribution.IconAssetName);
                Assert.Same(retribution.Tip, retribution.Hover);
                Assert.False(string.IsNullOrEmpty(retribution.Tip!.Effect));
                Assert.DoesNotContain("Healy", retribution.Tip.EffectNote);
                Assert.Equal((true, true), (retribution.Cells[0].Tip!.EffectNote!.Contains("12", StringComparison.Ordinal), retribution.Cells[0].Tip!.EffectNote!.Contains("Healy", StringComparison.Ordinal)));
                Assert.Equal((true, true), (retribution.Cells[1].Tip!.EffectNote!.Contains("15", StringComparison.Ordinal), retribution.Cells[1].Tip!.EffectNote!.Contains("Stronger", StringComparison.Ordinal)));
                Assert.NotEqual(retribution.Cells[0].Tip!.Effect, retribution.Cells[1].Tip!.Effect);
                Assert.Same(retribution.Cells[1].Tip, retribution.Cells[1].Hover);
                Assert.Null(retribution.Cells[2].Hover);
                var level = comparison.Rows.First(static row => !row.IsSection);
                Assert.Equal((null, null), (level.IconAssetName, level.Tip));
                Assert.Equal(level.Label, level.Hover);

                // The gladiator as the reference: no other gladiator, no skills.
                comparison.Reference = comparison.Twins[2];
                Assert.Equal(4, comparison.Rows.Count(static row => row.IsSection));
                Assert.DoesNotContain(comparison.Rows, static row => row.Label == "Keen Strike");
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        });
    }

    // Raw, each snapshot's own values; with the difference on, the others as more or less than the reference: 2,054 Attack
    // against 2,000 reads +54 (green), 1,800 -200 (red), 10% against 9.9% +0.1%; the reference's own stay as they are, and
    // a stat a snapshot doesn't list counts as 0. Hovering shows what isn't shown and the change in percent. Another
    // reference goes first and is what the others are measured against.
    [Fact]
    public void The_Comparison_Shows_The_Others_As_Differences_To_The_Reference()
    {
        AvaloniaTestHost.Run(() =>
        {
            var culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                using var localization = new LocalizationService(new LanguageService());
                var comparison = new TwinComparisonViewModel(
                    [WithTotals("Reference", ("Attack", 2_000), ("Attack%", 9.9)), WithTotals("More", ("Attack", 2_054), ("Attack%", 10), ("Healing Boost", 5)),
                        WithTotals("Less", ("Attack", 1_800), ("Attack%", 9.9), ("Healing Boost", 7))], localization);

                Assert.Equal(["Reference", "More", "Less"], comparison.Columns.Select(static column => column.Name));
                Assert.Equal(["2,000", "2,054", "1,800"], Texts(comparison, "Attack"));
                Assert.Equal(["–", "5", "7"], Texts(comparison, "Healing Boost"));
                Assert.Equal("+54 (+2.7%)", Cells(comparison, "Attack")[1].Hint);
                Assert.Equal(5, comparison.Rows.Count(static row => row.IsSection));

                comparison.ShowDifference = true;
                Assert.Equal(["2,000", "+54", "-200"], Texts(comparison, "Attack"));
                Assert.Equal([(false, false), (true, false), (false, true)], Cells(comparison, "Attack").Select(static cell => (cell.IsMore, cell.IsLess)));
                Assert.Equal("2,054 (+2.7%)", Cells(comparison, "Attack")[1].Hint);
                Assert.Equal(["9.9%", "+0.1%", "±0%"], Texts(comparison, "Attack %"));
                Assert.Equal(["–", "+5", "+7"], Texts(comparison, "Healing Boost"));

                comparison.Reference = comparison.Twins[2];
                Assert.Equal(["Less", "Reference", "More"], comparison.Columns.Select(static column => column.Name));
                Assert.Equal(["1,800", "+200", "+254"], Texts(comparison, "Attack"));
                Assert.Equal(["7", "-7", "-2"], Texts(comparison, "Healing Boost"));
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        });
    }

    // Biggest differences first, in each section: by the change in percent averaged over the others (120, 140 and 80
    // against 100: (20% + 40% - 20%) / 3), a drop as much as a gain, a stat only the others have as +100% from each; equal
    // ones keep their order, and the shown values don't change.
    [Fact]
    public void The_Biggest_Differences_Go_First()
    {
        AvaloniaTestHost.Run(() =>
        {
            var culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                using var localization = new LocalizationService(new LanguageService());
                var comparison = new TwinComparisonViewModel(
                [
                    WithTotals("Reference", ("Attack", 100), ("Defense", 100), ("Evasion", 100), ("Block", 10), ("Accuracy", 100)),
                    WithTotals("First", ("Attack", 120), ("Defense", 100), ("Evasion", 50), ("Block", 12), ("Accuracy", 100), ("Healing Boost", 5)),
                    WithTotals("Second", ("Attack", 140), ("Defense", 100), ("Evasion", 50), ("Block", 12), ("Accuracy", 100)),
                    WithTotals("Third", ("Attack", 80), ("Defense", 100), ("Evasion", 50), ("Block", 12), ("Accuracy", 100), ("Healing Boost", 7)),
                ], localization)
                {
                    ShowDifference = true,
                };
                string[] Stats() => [.. SectionRows(comparison, 2)];

                Assert.Equal(["Attack", "Defense", "Evasion", "Block", "Accuracy", "Healing Boost"], Stats());
                Assert.Equal("120 (+20%)", Cells(comparison, "Attack")[1].Hint);

                comparison.SortByDifference = true;
                // Healing Boost 66.7%, Evasion -50%, Block 20%, Attack 13.3%, Defense and Accuracy 0.
                Assert.Equal(["Healing Boost", "Evasion", "Block", "Attack", "Defense", "Accuracy"], Stats());
                Assert.Equal(["100", "+20", "+40", "-20"], Texts(comparison, "Attack"));
                Assert.Equal(5, comparison.Rows.Count(static row => row.IsSection));

                comparison.SortByDifference = false;
                Assert.Equal(["Attack", "Defense", "Evasion", "Block", "Accuracy", "Healing Boost"], Stats());
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        });
    }

    // A click on a sum unfolds where it comes from: a line per source any of the snapshots has it from (the titles, each gear
    // slot, the magic stones, ...), the biggest difference first (always), each snapshot's part, shown like the sum: the
    // reference's own, with the difference on the others' as more or less than the reference's part. It stays unfolded
    // when the rows are made anew; lines without sources don't unfold.
    [Fact]
    public void A_Sum_Unfolds_To_Where_It_Comes_From()
    {
        AvaloniaTestHost.Run(() =>
        {
            var culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                using var localization = new LocalizationService(new LanguageService());
                static TwinItem Item(int slot, string name, double attack, double stone = 0) =>
                    new(slot, name, name, "Epic", 0, 0, [new TwinStatValue("Attack", attack, false)], [], stone > 0 ? [new TwinStatValue("Attack", stone, false)] : [], [], []);
                var reference = WithTotals("Reference", ("Attack", 580)) with
                {
                    Gear = [Item(1, "MainHand", 500), Item(7, "Ring1", 20, stone: 40)],
                    Titles = [new TwinTitle("Attack", "Golden Glow", ["Attack +20"])],
                    PrimaryStats = [],
                    Boards = [],
                };
                var other = WithTotals("Other", ("Attack", 480)) with { Gear = [Item(1, "MainHand", 450), Item(5, "Earring1", 30)], Titles = [], PrimaryStats = [], Boards = [] };
                var comparison = new TwinComparisonViewModel([reference, other], localization);
                TwinComparisonRow Attack() => comparison.Rows.Single(static row => !row.IsSection && row.Label == "Attack");
                string[][] Sources() => [.. Attack().Sources.Select(static source => (string[])[source.Label, .. source.Cells.Select(static cell => cell.Text)])];

                Assert.True(Attack().CanUnfold);
                Assert.False(comparison.Rows.First(static row => !row.IsSection).CanUnfold);
                Assert.Equal(("▸", false), (Attack().Fold, Attack().IsUnfolded));
                // All or nothing (±100%) before MainHand's -10%.
                Assert.Equal(
                    [["Titles", "20", "–"], ["Ring1", "20", "–"], ["Magic stones", "40", "–"], ["Earring1", "–", "30"], ["MainHand", "500", "450"]],
                    Sources());

                Attack().ToggleCommand.Execute(null);
                Assert.Equal(("▾", true), (Attack().Fold, Attack().IsUnfolded));
                comparison.ShowDifference = true;
                Assert.True(Attack().IsUnfolded);
                Assert.Equal(
                    [["Titles", "20", "-20"], ["Ring1", "20", "-20"], ["Magic stones", "40", "-40"], ["Earring1", "–", "+30"], ["MainHand", "500", "-50"]],
                    Sources());
                Assert.True(Attack().Sources[3].Cells[1].IsMore);

                Attack().ToggleCommand.Execute(null);
                comparison.ShowDifference = false;
                Assert.False(Attack().IsUnfolded);
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        });
    }

    // A skill's level unfolds too: the levels put in with skill points (the level less the others), each gear slot and arcana
    // card kind adding to it (a card by the first word of its name, so Chalice lines up whatever the card), and Daevanion;
    // the biggest difference first.
    [Fact]
    public void A_Skill_Level_Unfolds_To_Points_Gear_Arcana_And_Daevanion()
    {
        AvaloniaTestHost.Run(() =>
        {
            var culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                using var localization = new LocalizationService(new LanguageService());
                static TwinItem Item(int slot, string slotName, string name, int levels) =>
                    new(slot, slotName, name, "Unique", 0, 0, [], [], [], [new TwinSkillBonus(17090000, "Light of Regeneration", levels)], []);
                var cleric = DigitalTwinBuilderTests.Build();
                var reference = cleric with
                {
                    Name = "Reference",
                    Skills = [new TwinSkill(17090000, "Light of Regeneration", 17, "Active", true, GearLevels: 4, DaevanionLevels: 4)],
                    Gear = [Item(13, "Ring1", "Enraged Kromede Ring", 1), Item(41, "Arcana1", "Chalice of Vigor", 3)],
                };
                var other = cleric with
                {
                    Name = "Other",
                    Skills = [new TwinSkill(17090000, "Light of Regeneration", 13, "Active", true, GearLevels: 2, DaevanionLevels: 3)],
                    Gear = [Item(13, "Ring1", "Enraged Kromede Ring", 1), Item(41, "Arcana1", "Chalice of Magic", 1)],
                };
                var comparison = new TwinComparisonViewModel([reference, other], localization) { ShowDifference = true };
                var row = comparison.Rows.Single(static row => !row.IsSection && row.Label == "Light of Regeneration");

                Assert.True(row.CanUnfold);
                // Chalice -66.7%, Daevanion -25%, points -11.1% (9 against 8), Ring1 the same.
                Assert.Equal(["arcana Chalice", "daevanion", "points", "slot 13"], row.Sources.Select(static source => source.Key));
                Assert.Equal([["3", "-2"], ["4", "-1"], ["9", "-1"], ["1", "±0"]], row.Sources.Select(static source => source.Cells.Select(static cell => cell.Text).ToArray()));
                Assert.Equal("Arcana: Chalice", row.Sources[0].Label);
                Assert.Equal(17, row.Sources.Sum(static source => double.Parse(source.Cells[0].Text, CultureInfo.InvariantCulture)));
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        });
    }

    // A snapshot with just these sums ("Attack%": Attack as a percentage).
    private static DigitalTwin WithTotals(string name, params (string Stat, double Value)[] totals) =>
        DigitalTwinBuilderTests.Build() with
        {
            Name = name,
            Totals = [.. totals.Select(static total => new TwinStatTotal(total.Stat.TrimEnd('%'), total.Value, total.Stat.EndsWith('%'), []))],
        };

    // The labels of a section's rows (0: overview, 1: base stats, 2: the sums, then skills or boards).
    private static List<string> SectionRows(TwinComparisonViewModel comparison, int section)
    {
        var labels = new List<string>();
        var current = -1;
        foreach (var row in comparison.Rows)
        {
            if (row.IsSection)
                current++;
            else if (current == section)
                labels.Add(row.Label);
        }

        return labels;
    }

    private static TwinComparisonCell[] Cells(TwinComparisonViewModel comparison, string stat) =>
        comparison.Rows.Single(row => !row.IsSection && row.Label == stat).Cells;

    private static string[] Texts(TwinComparisonViewModel comparison, string stat) => [.. Cells(comparison, stat).Select(static cell => cell.Text)];

    private static void RunUntil(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!done() && DateTime.UtcNow < until)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Assert.True(done());
    }

    // The website never answers, so the lookup stays under way.
    private sealed class SilentWebsite : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
