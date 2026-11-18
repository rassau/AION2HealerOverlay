using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Twins;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Tests.SceneRuntime;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.Services;

public sealed class DigitalTwinLibraryTests
{
    // Each snapshot a file of its own; listed the newest first with all of it back; a broken file is left out.
    [Fact]
    public void Snapshots_Are_Kept_And_Listed_Newest_First()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"aion2heal-twins-{Guid.NewGuid():N}");
        try
        {
            var library = new DigitalTwinLibrary(folder);
            var changes = 0;
            library.Changed += () => changes++;
            var older = DigitalTwinBuilderTests.Build();
            var newer = older with { FetchedAt = older.FetchedAt.AddDays(1), ItemLevel = older.ItemLevel + 10 };

            library.Add(older);
            library.Add(newer);
            library.Add(older with { Name = "Other", FetchedAt = older.FetchedAt.AddHours(1) });
            File.WriteAllText(Path.Combine(folder, "broken.twin.gz"), "not a twin");
            File.WriteAllText(Path.Combine(folder, "20261111-235959 1301 Broken.twin.gz"), "not a twin");
            var twins = library.Load().Select(static entry => entry.Twin).ToList();

            Assert.Equal(3, changes);
            Assert.Equal([newer.FetchedAt, older.FetchedAt.AddHours(1), older.FetchedAt], twins.Select(static twin => twin.FetchedAt));
            Assert.Equal((newer.Name, newer.ItemLevel, newer.Skills.Length, newer.Gear.Length, newer.Totals.Length),
                (twins[0].Name, twins[0].ItemLevel, twins[0].Skills.Length, twins[0].Gear.Length, twins[0].Totals.Length));

            // At the start each player's newest is read (by file name), nothing else.
            Assert.Equal([(newer.Name, newer.FetchedAt), ("Other", older.FetchedAt.AddHours(1))],
                library.LoadNewestPerPlayer().OrderBy(static twin => twin.Name).Select(static twin => (twin.Name, twin.FetchedAt)));

            // Deleting takes the snapshot's file away.
            library.Delete(library.Load()[0].Path);
            Assert.Equal(4, changes);
            Assert.Equal([older.FetchedAt.AddHours(1), older.FetchedAt], library.Load().Select(static entry => entry.Twin.FetchedAt));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    // Loading again takes the snapshots read before whose files haven't been written since, and reads the others: the list
    // is the same as a fresh load's.
    [Fact]
    public void A_Load_Reads_Only_Files_Written_Since_The_Last()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"aion2heal-twins-{Guid.NewGuid():N}");
        try
        {
            var library = new DigitalTwinLibrary(folder);
            var twin = DigitalTwinBuilderTests.Build();
            var other = twin with { Name = "Other", FetchedAt = twin.FetchedAt.AddHours(1) };
            library.Add(twin);
            library.Add(other);
            var first = library.Load();

            // The same character looked up again within the second writes the same file anew.
            library.Add(twin with { ItemLevel = twin.ItemLevel + 10 });
            var rewritten = Path.Combine(folder, DigitalTwinLibrary.FileName(twin));
            File.SetLastWriteTimeUtc(rewritten, File.GetLastWriteTimeUtc(rewritten).AddSeconds(1));
            library.Add(twin with { Name = "Third", FetchedAt = twin.FetchedAt.AddHours(2) });
            var again = library.Load(first);

            Assert.Equal(library.Load().Select(static entry => (entry.Path, entry.Twin.Name, entry.Twin.FetchedAt, entry.Twin.ItemLevel)),
                again.Select(static entry => (entry.Path, entry.Twin.Name, entry.Twin.FetchedAt, entry.Twin.ItemLevel)));
            Assert.Equal(["Third", "Other", twin.Name], again.Select(static entry => entry.Twin.Name));
            Assert.Same(first.Single(static entry => entry.Twin.Name == "Other"), again[1]);
            Assert.Equal(twin.ItemLevel + 10, again[2].Twin.ItemLevel);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    // Each player's skill levels from the last boss fight seen with them, kept beside the snapshots: a later fight replaces
    // a player's whole set (whatever the name's case), an older one or the same one again doesn't, and a player it has
    // nothing for keeps theirs. A line that can't be read is left out.
    [Fact]
    public void Seen_Skill_Levels_Keep_Each_Player_s_Latest_Fight()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"aion2heal-twins-{Guid.NewGuid():N}");
        try
        {
            var library = new DigitalTwinLibrary(folder);
            var at = new DateTimeOffset(2026, 11, 18, 21, 40, 0, TimeSpan.FromHours(1));
            Assert.Empty(library.LoadSeenLevels());

            library.RememberSeenLevels([(1301, "Healy", Levels((17090000, 16), (17730000, 12)), Ranges()), (2304, "Tanky", Levels((12010000, 9)), Ranges())], at);
            var seen = library.LoadSeenLevels();
            Assert.Equal([(1301, "healy"), (2304, "tanky")], seen.Keys.Order());
            Assert.Equal(at, seen[(1301, "healy")].At);
            Assert.Equal(Levels((17090000, 16), (17730000, 12)), seen[(1301, "healy")].Levels);

            library.RememberSeenLevels([(1301, "HEALY", Levels((17090000, 18)), Ranges()), (1301, "Quiet", Levels(), Ranges())], at.AddHours(1));
            seen = library.LoadSeenLevels();
            Assert.Equal(at.AddHours(1), seen[(1301, "healy")].At);
            Assert.Equal(Levels((17090000, 18)), seen[(1301, "healy")].Levels);
            Assert.Equal(Levels((12010000, 9)), seen[(2304, "tanky")].Levels);
            Assert.False(seen.ContainsKey((1301, "quiet")));

            // A replayed recording's older fight, or the same fight again.
            library.RememberSeenLevels([(1301, "Healy", Levels((17090000, 10)), Ranges())], at);
            library.RememberSeenLevels([(1301, "Healy", Levels((17090000, 11)), Ranges())], at.AddHours(1));
            Assert.Equal(Levels((17090000, 18)), library.LoadSeenLevels()[(1301, "healy")].Levels);

            File.AppendAllLines(Path.Combine(folder, "seen-levels.txt"),
                ["1301\tBroken\tyesterday\t17090000=16", "1301\tHalf\t2026-11-18T20:40:00.0000000+00:00\t17090000=x", "garbage"]);
            Assert.Equal([(1301, "healy"), (2304, "tanky")], library.LoadSeenLevels().Keys.Order());
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    // A stigma's range goes beside the levels ("17410000=15-19", "17160000=20+"), where the fight sent no level for it;
    // a player with ranges alone gets a line too, and back they come as they went. A range that can't be read (the wrong
    // way round, open without its start, three numbers) leaves its line out.
    [Fact]
    public void Seen_Stigma_Ranges_Are_Kept_Beside_The_Levels()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"aion2heal-twins-{Guid.NewGuid():N}");
        try
        {
            var library = new DigitalTwinLibrary(folder);
            var at = new DateTimeOffset(2026, 11, 18, 21, 40, 0, TimeSpan.FromHours(1));

            library.RememberSeenLevels(
            [
                (1301, "Healy", Levels((17090000, 16)), Ranges((17410000, new(15, 19)), (17160000, new(20, null)), (17090000, new(5, 9)))),
                (2304, "Stabby", Levels(), Ranges((11390000, new(10, 14)))),
            ], at);

            var path = Path.Combine(folder, "seen-levels.txt");
            Assert.Equal(["1301\tHealy\t2026-11-18T20:40:00.0000000+00:00\t17090000=16 17160000=20+ 17410000=15-19", "2304\tStabby\t2026-11-18T20:40:00.0000000+00:00\t11390000=10-14"],
                File.ReadAllLines(path).Order());
            var seen = library.LoadSeenLevels();
            Assert.Equal([(1301, "healy"), (2304, "stabby")], seen.Keys.Order());
            Assert.Equal(Levels((17090000, 16)), seen[(1301, "healy")].Levels);
            Assert.Equal(Ranges((17160000, new(20, null)), (17410000, new(15, 19))), seen[(1301, "healy")].Ranges);
            Assert.Empty(seen[(2304, "stabby")].Levels);
            Assert.Equal(Ranges((11390000, new(10, 14))), seen[(2304, "stabby")].Ranges);

            File.AppendAllLines(path,
            [
                "1301\tBackwards\t2026-11-18T20:40:00.0000000+00:00\t17410000=19-15", "1301\tOpen\t2026-11-18T20:40:00.0000000+00:00\t17410000=+",
                "1301\tThree\t2026-11-18T20:40:00.0000000+00:00\t17090000=16 17410000=15-19-20",
            ]);
            Assert.Equal([(1301, "healy"), (2304, "stabby")], library.LoadSeenLevels().Keys.Order());
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static IReadOnlyDictionary<int, int> Levels(params (int Skill, int Level)[] levels) => levels.ToDictionary(static level => level.Skill, static level => level.Level);

    private static IReadOnlyDictionary<int, SeenLevelRange> Ranges(params (int Skill, SeenLevelRange Range)[] ranges) =>
        ranges.ToDictionary(static range => range.Skill, static range => range.Range);

    // Time (UTC), server, name; any character name makes a file name.
    [Fact]
    public void File_Name_Holds_Any_Character_Name()
    {
        var twin = DigitalTwinBuilderTests.Build() with { Name = "A/B:C", FetchedAt = new DateTimeOffset(2026, 11, 11, 22, 33, 2, TimeSpan.FromHours(1)) };

        Assert.Equal($"20261111-213302 {twin.ServerId} A_B_C.twin.gz", DigitalTwinLibrary.FileName(twin));
    }

    // The library's class icon comes from the website's class name.
    [Theory]
    [InlineData("Cleric", CharacterClass.Cleric)]
    [InlineData("chanter", CharacterClass.Chanter)]
    [InlineData("Spiritmaster", CharacterClass.Elementalist)]
    [InlineData("Unknown", null)]
    public void Class_Comes_From_The_Website_Name(string className, CharacterClass? expected) =>
        Assert.Equal(expected, DigitalTwinLibraryRow.ClassOf(className));
}
