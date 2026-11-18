using System.Globalization;
using System.Text.RegularExpressions;

namespace Cloris.Aion2Flow.Services;

// The patch notes the program carries for Settings → System: CHANGELOG.md, embedded as it is (see the project file), cut
// into its sections. "## 18 Nov 2026 · build 75" is version 1.0.75; a section of several builds ("builds 49–60") stands for
// each of them. Newest first, as the file has them, and English in every language. From build 75 on a section starts with
// "### In short", then "### In detail": PatchNotesTests and the release build's script (release-notes.ps1) insist on it.
public static partial class PatchNotes
{
    public enum Kind
    {
        Heading,
        Group,
        Bullet,
        Text,
    }

    // A stretch of a line, bold or italic where the file marks it (**…**, *…*).
    public readonly record struct Run(string Text, bool Bold = false, bool Italic = false);

    // A line to show: a "###" heading, a group's line ("**DPS meter**"), a "- " bullet or other text, without its markup and
    // its build marks (" [75]": the drop-down names the version).
    // ponytail: one line per bullet, as the file writes them; a bullet wrapped onto a second line shows that line as text.
    public sealed record Block(Kind Kind, Run[] Runs)
    {
        public string Text => string.Concat(Runs.Select(static run => run.Text));
    }

    // One section: its builds, its date and its lines as written.
    public sealed record Section(int FromBuild, int ToBuild, string Date, string[] Lines)
    {
        public string Label => FromBuild == ToBuild ? $"1.0.{ToBuild} · {Date}" : $"1.0.{FromBuild}–1.0.{ToBuild} · {Date}";

        public bool Covers(int build) => build >= FromBuild && build <= ToBuild;

        // Worked out each time it's asked for (the settings ask once per version shown); blank lines go.
        public Block[] Blocks => [.. Lines.Where(static line => line.Length > 0).Select(ToBlock)];
    }

    private static readonly Lazy<Section[]> s_sections = new(static () => Parse(Load()));

    // The program's own patch notes, newest first.
    public static IReadOnlyList<Section> Sections => s_sections.Value;

    // Each "## " heading starts a section. One whose heading doesn't read (no date, no build, or builds the wrong way round)
    // is left out with its lines, as is everything before the first section (the title and the intro).
    public static Section[] Parse(string changelog)
    {
        var sections = new List<Section>();
        Match? heading = null;
        List<string> lines = [];
        foreach (var raw in changelog.Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Add();
                heading = SectionHeading().Match(line);
                lines = [];
            }
            else if (heading is not null)
            {
                lines.Add(line);
            }
        }

        Add();
        return [.. sections];

        void Add()
        {
            if (heading is not { Success: true }
                || !int.TryParse(heading.Groups["from"].ValueSpan, CultureInfo.InvariantCulture, out var from)
                || !int.TryParse(heading.Groups["to"].Success ? heading.Groups["to"].ValueSpan : heading.Groups["from"].ValueSpan, CultureInfo.InvariantCulture, out var to)
                || to < from)
                return;

            sections.Add(new Section(from, to, heading.Groups["date"].Value, [.. lines]));
        }
    }

    private static Block ToBlock(string line)
    {
        var (kind, text) = line switch
        {
            _ when line.StartsWith('#') => (Kind.Heading, line.TrimStart('#').Trim()),
            _ when line.StartsWith("- ", StringComparison.Ordinal) => (Kind.Bullet, line[2..]),
            _ when line.StartsWith("**", StringComparison.Ordinal) => (Kind.Group, line),
            _ => (Kind.Text, line),
        };

        // Code (`best-runs`) reads as plain text.
        text = BuildMarks().Replace(text.Replace("`", "", StringComparison.Ordinal), "");
        var runs = new List<Run>();
        var at = 0;
        foreach (Match match in Emphasis().Matches(text))
        {
            if (match.Index > at)
                runs.Add(new Run(text[at..match.Index]));
            runs.Add(match.Groups["bold"].Success ? new Run(match.Groups["bold"].Value, Bold: true) : new Run(match.Groups["italic"].Value, Italic: true));
            at = match.Index + match.Length;
        }

        if (at < text.Length)
            runs.Add(new Run(text[at..]));
        return new Block(kind, [.. runs]);
    }

    private static string Load()
    {
        using var stream = typeof(PatchNotes).Assembly.GetManifestResourceStream("CHANGELOG.md")
            ?? throw new InvalidOperationException("CHANGELOG.md is missing from the program.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // "## 18 Nov 2026 · build 75", "## 10 Nov 2026 · builds 49–60" (an en dash, a hyphen or an em dash; any spaces between
    // the parts, the date as written).
    [GeneratedRegex(@"^##\s+(?<date>\S.*?)\s*·\s*builds?\s+(?<from>\d+)(?:\s*[-–—]\s*(?<to>\d+))?$")]
    private static partial Regex SectionHeading();

    // The build marks at a line's end: [75], [49, 50], [70–72].
    [GeneratedRegex(@"\s*\[\d+(?:\s*[-–]\s*\d+)?(?:\s*,\s*\d+(?:\s*[-–]\s*\d+)?)*\]$")]
    private static partial Regex BuildMarks();

    // **bold**, and *italic* between word boundaries: an asterisk after a word or a bold part ("UHS*", "**UHS***") stays.
    [GeneratedRegex(@"\*\*(?<bold>.+?)\*\*|(?<![\w*])\*(?<italic>[^\s*](?:[^*]*[^\s*])?)\*(?![\w*])")]
    private static partial Regex Emphasis();
}
