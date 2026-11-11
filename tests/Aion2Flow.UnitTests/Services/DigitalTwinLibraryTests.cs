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
