using System.Text.RegularExpressions;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.ViewModels;
using Run = Cloris.Aion2Flow.Services.PatchNotes.Run;

namespace Cloris.Aion2Flow.Tests.Services;

public sealed partial class PatchNotesTests
{
    // The program's own CHANGELOG.md: every section reads, newest first, and together they cover each build from 1 on once;
    // a build's label is its version and date, a section of several builds names both ends.
    [Fact]
    public void Every_Section_Of_The_Changelog_Reads_Newest_First()
    {
        var sections = PatchNotes.Sections;

        Assert.Equal(Changelog().Split('\n').Count(static line => line.StartsWith("## ", StringComparison.Ordinal)), sections.Count);
        Assert.Equal(1, sections[^1].FromBuild);
        Assert.All(sections.Zip(sections.Skip(1)), static pair => Assert.Equal(pair.Second.ToBuild + 1, pair.First.FromBuild));
        Assert.Equal("1.0.75 · 18 Nov 2026", sections.Single(static section => section.Covers(75)).Label);
        Assert.Equal("1.0.1–1.0.7 · 31 Oct 2026", sections[^1].Label);

        var range = sections.Single(static section => section.Covers(55));
        Assert.Equal((49, 60, "1.0.49–1.0.60 · 10 Nov 2026"), (range.FromBuild, range.ToBuild, range.Label));
        Assert.True(range.Covers(49) && range.Covers(60) && !range.Covers(48) && !range.Covers(61));
    }

    // The user's wish (18 Nov 2026): from build 75 on, every version's notes start with a short version for players, then
    // the details as before. "### In short": 1 to 7 bullets without build marks; then "### In detail": its groups, every
    // bullet ending with its build marks. A section written without the short version fails here, and its release too.
    [Fact]
    public void Every_Section_From_Build_75_On_Has_A_Short_And_A_Detailed_Part()
    {
        var sections = PatchNotes.Sections.Where(static section => section.ToBuild >= 75).ToArray();
        Assert.Equal([76, 75], sections.TakeLast(2).Select(static section => section.ToBuild));

        foreach (var section in sections)
        {
            var lines = section.Lines.Where(static line => line.Length > 0).ToList();
            var (shortAt, detailAt) = (lines.IndexOf("### In short"), lines.IndexOf("### In detail"));
            Assert.True(shortAt == 0, $"{section.Label}: no \"### In short\" first");
            Assert.True(detailAt > shortAt, $"{section.Label}: no \"### In detail\" after the short version");

            var inShort = lines[(shortAt + 1)..detailAt];
            Assert.All(inShort, static line => Assert.StartsWith("- ", line));
            Assert.InRange(inShort.Count, 1, 7);
            Assert.DoesNotContain(inShort, static line => BuildMark().IsMatch(line));

            var inDetail = lines[(detailAt + 1)..];
            Assert.StartsWith("**", inDetail[0]);
            Assert.All(inDetail.Where(static line => line.StartsWith("- ", StringComparison.Ordinal)), static line => Assert.Matches(BuildMark(), line));
        }
    }

    // A version's lines as they show: the "###" headings, a group's line (bold, text after it too), bullets, without the
    // build marks ([75], [49, 50], [70–72]) and the backticks; bold and italic runs where the file marks them, an asterisk
    // after a word kept.
    [Fact]
    public void A_Sections_Lines_Show_Without_Their_Markup()
    {
        var blocks = PatchNotes.Sections.Single(static section => section.Covers(75)).Blocks;
        Assert.Equal((PatchNotes.Kind.Heading, "In short"), (blocks[0].Kind, blocks[0].Text));
        Assert.Equal(PatchNotes.Kind.Bullet, blocks[1].Kind);
        var detail = Array.FindIndex(blocks, static block => block.Text == "In detail");
        Assert.Equal(PatchNotes.Kind.Heading, blocks[detail].Kind);
        Assert.Equal(PatchNotes.Kind.Group, blocks[detail + 1].Kind);
        Assert.Equal([new Run("DPS meter", Bold: true)], blocks[detail + 1].Runs);
        Assert.Equal(PatchNotes.Kind.Bullet, blocks[detail + 2].Kind);
        Assert.Equal([new Run("With "), new Run("Settings → DPS meter → Meter records", Bold: true), new Run(" on "), new Run("All fights", Bold: true)],
            blocks[detail + 2].Runs[..4]);
        Assert.EndsWith("Hover over the time to see what it counts.", blocks[detail + 2].Text);

        var all = PatchNotes.Sections.SelectMany(static section => section.Blocks).ToArray();
        Assert.DoesNotContain(all, static block => block.Text.EndsWith(']') || block.Text.Contains('`', StringComparison.Ordinal));
        Assert.Contains(all, static block => block.Text.EndsWith("so the HP bars stay in place as buffs come and go.", StringComparison.Ordinal));

        var scenes = all.First(static block => block.Text.StartsWith("Scenes (a new tab", StringComparison.Ordinal));
        Assert.Equal(PatchNotes.Kind.Group, scenes.Kind);
        Assert.Equal([new Run("Scenes", Bold: true), new Run(" (a new tab in the settings)")], scenes.Runs);
        Assert.Contains(all, static block => block.Runs.Contains(new Run("you", Italic: true)));
        Assert.Contains(all, static block => block.Text.Contains("in the best-runs folder next to the settings", StringComparison.Ordinal));
        var uhs = all.First(static block => block.Text.Contains("The runs tables mark it \"UHS*\"", StringComparison.Ordinal));
        Assert.DoesNotContain(uhs.Runs, static run => run.Italic);
        var marked = all.First(static block => block.Text.Contains("marked UHS*, with the note", StringComparison.Ordinal));
        Assert.Contains(new Run("UHS", Bold: true), marked.Runs);
    }

    // Robust to how a heading is written (an en dash, a hyphen or an em dash, extra spaces) and to no intro; a section whose
    // heading doesn't read is left out with its lines, never a crash.
    [Fact]
    public void A_Section_That_Does_Not_Read_Is_Left_Out()
    {
        var sections = PatchNotes.Parse(
            "## 19 Nov 2026 · build 77\r\n- New. [77]\r\n## Notes\r\n- not a version\r\n##   10 Nov 2026   ·   builds 49 - 60  \n- Old. [49, 50]\n" +
            "## 1 Nov 2026 · builds 9–8\n- backwards\n## 2 Nov 2026 · build 99999999999\n- too big\n## 6 Nov 2026 · builds 30—37\n## 7 Nov 2026\n- no build\n");

        Assert.Equal([(77, 77, "19 Nov 2026"), (49, 60, "10 Nov 2026"), (30, 37, "6 Nov 2026")], sections.Select(static s => (s.FromBuild, s.ToBuild, s.Date)));
        Assert.Equal(["- New. [77]"], sections[0].Lines);
        Assert.Equal("Old.", sections[1].Blocks.Single().Text);
        Assert.Empty(sections[2].Blocks);
        Assert.Empty(PatchNotes.Parse(""));
        Assert.Empty(PatchNotes.Parse("# Aion2Heal – patch notes\n\nAn intro.\n### In short\n- x\n"));
    }

    // The drop-down marks the version running: its build's section, also one of several builds; a prerelease or a commit
    // after the version doesn't matter, and a local build (1.0.0) or another numbering marks none.
    [Theory]
    [InlineData("1.0.75", "1.0.75 · 18 Nov 2026")]
    [InlineData("1.0.74-beta.1", "1.0.74 · 17 Nov 2026")]
    [InlineData("1.0.75+5dfa3be", "1.0.75 · 18 Nov 2026")]
    [InlineData("1.0.55", "1.0.49–1.0.60 · 10 Nov 2026")]
    [InlineData("1.0.0", null)]
    [InlineData("2.0.75", null)]
    [InlineData(null, null)]
    public void The_Installed_Version_Is_Marked(string? installed, string? marked)
    {
        var options = PatchNotesOption.For(PatchNotes.Sections, installed);

        Assert.Equal(PatchNotes.Sections, options.Select(static option => option.Section));
        Assert.Equal(marked, options.SingleOrDefault(static option => option.IsInstalled)?.Label);
    }

    // The release build's script (.github/scripts/release-notes.ps1, run by cd.yml) writes a release page's notes: the
    // sections after the previous release's build up to this one's (one of several builds once a build of it is), newest
    // first; a release of several builds opens with their short versions, then their details. It fails without a section
    // for the build, and for a section from build 75 on without its short version.
    [Fact]
    public void A_Release_Page_Gets_The_Notes_Of_The_Builds_Since_The_Previous_Release()
    {
        var folder = Directory.CreateTempSubdirectory("aion2heal-release-notes-").FullName;
        try
        {
            var notes = Path.Combine(folder, "notes.md");
            var (code, message) = ReleaseNotes("-Build", "72", "-Previous", "69", "-OutFile", notes);
            Assert.Equal(0, code);
            Assert.StartsWith("Patch notes for build 72 (after build 69): ## 15 Nov 2026 · build 72, ", message);
            var written = File.ReadAllLines(notes);
            Assert.Equal(["## 15 Nov 2026 · build 72", "## 15 Nov 2026 · build 71", "## 15 Nov 2026 · build 70"],
                written.Where(static line => line.StartsWith("## ", StringComparison.Ordinal)));
            Assert.Contains("**Builds 70 to 72 together** (against build 69, replaying recordings of real play)", written);
            Assert.EndsWith("[70]", written[^1]);

            Assert.Equal(0, ReleaseNotes("-Build", "75", "-Previous", "74", "-OutFile", notes).Code);
            Assert.Equal(["## 18 Nov 2026 · build 75", "", "### In short"], File.ReadAllLines(notes)[..3]);

            Assert.Equal(0, ReleaseNotes("-Build", "76", "-Previous", "74", "-OutFile", notes).Code);
            Assert.Equal(["## 18 Nov 2026 · build 76", "### In short", "## 18 Nov 2026 · build 75", "### In short",
                    "## 18 Nov 2026 · build 76", "### In detail", "## 18 Nov 2026 · build 75", "### In detail"],
                File.ReadAllLines(notes).Where(static line => line.StartsWith('#')));

            Assert.Equal(0, ReleaseNotes("-Build", "60", "-Previous", "59", "-OutFile", notes).Code);
            Assert.Equal(["## 10 Nov 2026 · builds 49–60"], File.ReadAllLines(notes).Where(static line => line.StartsWith("## ", StringComparison.Ordinal)));

            (code, message) = ReleaseNotes("-Build", "999", "-Previous", "76", "-OutFile", notes);
            Assert.Equal(1, code);
            Assert.Contains("has no section for build 999", message);

            var changelog = Path.Combine(folder, "CHANGELOG.md");
            File.WriteAllText(changelog, "# Patch notes\n\n## 19 Nov 2026 · build 77\n\n### In detail\n\n**Meter**\n- New. [77]\n");
            (code, message) = ReleaseNotes("-Build", "77", "-Previous", "76", "-OutFile", notes, "-Changelog", changelog);
            Assert.Equal(1, code);
            Assert.StartsWith("'## 19 Nov 2026 · build 77' has no '### In short'", message);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // The script run by PowerShell 7 as the release build runs it: its exit code, and what it says (in UTF-8).
    private static (int Code, string Message) ReleaseNotes(params string[] arguments)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "Aion2Flow.slnx")))
            root = root.Parent ?? throw new InvalidOperationException("The repository's folder isn't above the tests.");

        var script = Path.Combine(root.FullName, ".github", "scripts", "release-notes.ps1");
        var call = string.Join(' ', arguments.Select(static argument => argument.StartsWith('-') ? argument : $"'{argument}'"));
        var start = new System.Diagnostics.ProcessStartInfo("pwsh") { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        var command = $"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; try {{ & '{script}' {call} }} catch {{ $_.Exception.Message; exit 1 }}";
        foreach (var argument in (string[])["-NoProfile", "-NonInteractive", "-Command", command])
            start.ArgumentList.Add(argument);
        (start.StandardOutputEncoding, start.StandardErrorEncoding) = (System.Text.Encoding.UTF8, System.Text.Encoding.UTF8);
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, (output.Result + error.Result).Trim());
    }

    private static string Changelog()
    {
        using var stream = typeof(PatchNotes).Assembly.GetManifestResourceStream("CHANGELOG.md")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@" \[\d+(?:–\d+)?(?:, \d+)*\]$")]
    private static partial Regex BuildMark();
}
