using Cloris.Aion2Flow.SceneRuntime.Model;
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
