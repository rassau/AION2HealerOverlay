using System.Globalization;
using Cloris.Aion2Flow.Resources.Generated;
using Cloris.Aion2Flow.SceneRuntime.Twins;
using Cloris.Aion2Flow.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cloris.Aion2Flow.ViewModels;

// The library's comparison: up to five snapshots side by side, a row per stat (level, combat power and gear score, the base
// stats, the sums from gear, Daevanion and titles, the skill levels of those of the reference's class, the nodes open on
// each Daevanion board) and a column per snapshot, the reference first. With the
// difference on, the others show how much more or less they have than the reference: 64 Dexterity against 10 reads +54
// in green, 10% Attack against 9.9% +0.1%; a stat a snapshot doesn't list counts as 0. Sorted by the biggest difference,
// each section's stats go by how much the others differ in percent (other / reference - 1, averaged over the others:
// 120, 140 and 80 against 100 make (20% + 40% - 20%) / 3), the biggest gain or drop first; equal ones keep their order.
// A click on a sum or a skill's level unfolds where it comes from (see TwinComparisonColumn.SourcesOf and SkillSourcesOf), a
// line per source, shown like the sums and always the biggest difference first. A skill has its icon; hovering its name
// shows what it does (the numbers from level 1 to max), hovering a snapshot's level of it the numbers at that level.
public sealed partial class TwinComparisonViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    // The sums unfolded, by their key; they stay unfolded while the rows are made anew.
    private readonly HashSet<string> _unfolded = [];

    public TwinComparisonViewModel(IReadOnlyList<DigitalTwin> twins, LocalizationService localization)
    {
        _localization = localization;
        Twins = [.. twins.Select(static twin => new TwinComparisonColumn(twin))];
        Reference = Twins[0];
    }

    // In the order they were picked; the first is the reference to start with.
    public IReadOnlyList<TwinComparisonColumn> Twins { get; }

    [ObservableProperty]
    public partial TwinComparisonColumn? Reference { get; set; }

    [ObservableProperty]
    public partial bool ShowDifference { get; set; }

    [ObservableProperty]
    public partial bool SortByDifference { get; set; }

    // The reference first, then the others as picked.
    [ObservableProperty]
    public partial IReadOnlyList<TwinComparisonColumn> Columns { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<TwinComparisonRow> Rows { get; set; } = [];

    partial void OnReferenceChanged(TwinComparisonColumn? value) => Build();

    partial void OnShowDifferenceChanged(bool value) => Build();

    partial void OnSortByDifferenceChanged(bool value) => Build();

    private void Build()
    {
        // The drop-down clears it for a moment while its items change.
        if (Reference is not { } reference)
            return;

        Columns = [reference, .. Twins.Where(column => !ReferenceEquals(column, reference))];
        var rows = new List<TwinComparisonRow>();
        AddSection(rows, "TwinCompare_Overview", twin =>
        [
            ("level", _localization["TwinCompare_Level"], twin.Level, false),
            ("power", _localization["TwinLibrary_CombatPower"], twin.CombatPower, false),
            ("gear", _localization["TwinLibrary_GearScore"], twin.ItemLevel, false),
        ]);
        AddSection(rows, "TwinCompare_BaseStats", static twin => twin.PrimaryStats.Select(static stat => (stat.Name, stat.Name, stat.Value, false)));
        // The website gives some stats twice, as a number and as a percentage ("Attack" 2,152 and 10%). The Daevanion nodes'
        // stats are among these, their skill levels in the skills' levels.
        AddSection(rows, "Twin_Stats", static twin => twin.Totals.Select(static total => (TwinComparisonColumn.KeyOf(total.Name, total.Percent), total.Name + (total.Percent ? " %" : ""), total.Value, total.Percent)),
            sourcesOf: static (column, key) => column.SourcesOf(key));
        // Skills are a class's own: only those of the reference's class have them to compare.
        var points = _localization["TwinCompare_SkillPoints"];
        AddSection(rows, "Twin_Skills", static twin => twin.Skills.Select(static skill => (skill.Id.ToString(CultureInfo.InvariantCulture), skill.Name, (double)skill.Level, false)),
            twin => string.Equals(twin.ClassName, reference.Twin.ClassName, StringComparison.OrdinalIgnoreCase), (column, key) => column.SkillSourcesOf(key, points),
            (key, label) => (SkillIconCatalog.ResolveAssetName(SkillId(key)), SkillTip.For(SkillId(key), label, label, _localization)),
            (column, key, label, level) => SkillTip.For(SkillId(key), label, label, _localization, level > 0 ? (int)level : null, column.Twin.Name));
        // The boards are named alike for every class (their ids aren't).
        AddSection(rows, "Twin_Daevanion", twin => twin.Boards.Select(board =>
            (board.Name, string.Format(CultureInfo.CurrentCulture, _localization["TwinCompare_BoardNodes"], board.Name, board.Nodes), (double)board.OpenNodes, false)));
        Rows = rows;
    }

    // A section's title, then a row for each stat any of the snapshots has, in the order the reference (then the others)
    // lists them, or by the biggest difference. applies: which snapshots the section is for (the others show a dash and
    // don't count); none besides the reference, no section. sourcesOf: where a row's value comes from, by its key; its
    // lines go by the biggest difference, whatever the rows do. rowTip, cellTip: a row's icon and hover text (key, label),
    // a value's hover text (snapshot, key, label, value).
    private void AddSection(List<TwinComparisonRow> rows, string title, Func<DigitalTwin, IEnumerable<(string Key, string Label, double Value, bool Percent)>> statsOf,
        Func<DigitalTwin, bool>? applies = null, Func<TwinComparisonColumn, string, IEnumerable<(string Key, string Label, double Value)>>? sourcesOf = null,
        Func<string, string, (string? Icon, SkillTip? Tip)>? rowTip = null, Func<TwinComparisonColumn, string, string, double, SkillTip?>? cellTip = null)
    {
        var compared = Columns.Select(column => applies?.Invoke(column.Twin) ?? true).ToArray();
        if (!compared.Skip(1).Contains(true))
            return;

        var lines = Lines(compared, column => statsOf(column.Twin), cellTip)
            .Select(line => (Row: new TwinComparisonRow(line.Key, line.Label, line.Cells,
                sources: sourcesOf is null ? null : [.. Lines(compared, column => sourcesOf(column, line.Key).Select(source => (source.Key, source.Label, source.Value, line.Percent)))
                    .OrderByDescending(static source => Math.Abs(source.Rank))
                    .Select(static source => new TwinComparisonRow(source.Key, source.Label, source.Cells))],
                folded: Unfold)
            {
                IsUnfolded = _unfolded.Contains(line.Key),
                IconAssetName = rowTip?.Invoke(line.Key, line.Label).Icon,
                Tip = rowTip?.Invoke(line.Key, line.Label).Tip,
            }, line.Rank))
            .ToList();
        rows.Add(new TwinComparisonRow("", _localization[title], [], isSection: true));
        rows.AddRange((SortByDifference ? lines.OrderByDescending(static line => Math.Abs(line.Rank)) : lines.AsEnumerable()).Select(static line => line.Row));
    }

    // A line for each key any of the compared snapshots has, in the order the reference (then the others) lists them, with
    // the cells (each value's hover text from cellTip, when given) and how much the others differ (for the sorting).
    private List<(string Key, string Label, bool Percent, TwinComparisonCell[] Cells, double Rank)> Lines(bool[] compared,
        Func<TwinComparisonColumn, IEnumerable<(string Key, string Label, double Value, bool Percent)>> valuesOf, Func<TwinComparisonColumn, string, string, double, SkillTip?>? cellTip = null)
    {
        var others = compared.Skip(1).Count(static applies => applies);
        var order = new List<(string Key, string Label, bool Percent)>();
        var values = new Dictionary<string, double>[Columns.Count];
        for (var i = 0; i < Columns.Count; i++)
        {
            values[i] = [];
            if (!compared[i])
                continue;

            foreach (var (key, label, value, percent) in valuesOf(Columns[i]))
            {
                if (values[i].TryAdd(key, value) && !order.Exists(known => known.Key == key))
                    order.Add((key, label, percent));
            }
        }

        var lines = new List<(string Key, string Label, bool Percent, TwinComparisonCell[] Cells, double Rank)>(order.Count);
        foreach (var (key, label, percent) in order)
        {
            var reference = values[0].TryGetValue(key, out var own) ? own : (double?)null;
            var cells = new TwinComparisonCell[Columns.Count];
            cells[0] = new TwinComparisonCell(reference is { } value ? DigitalTwinSheet.Number(value, percent) : "–", Tip: reference is { } referenceValue ? cellTip?.Invoke(Columns[0], key, label, referenceValue) : null);
            var rank = 0d;
            for (var i = 1; i < Columns.Count; i++)
            {
                if (!compared[i])
                {
                    cells[i] = new TwinComparisonCell("–");
                    continue;
                }

                var other = values[i].TryGetValue(key, out var theirs) ? theirs : (double?)null;
                var change = Change(other ?? 0, reference ?? 0);
                rank += change / others;
                cells[i] = Cell(other, reference ?? 0, percent, change) with { Tip = other is { } otherValue ? cellTip?.Invoke(Columns[i], key, label, otherValue) : null };
            }

            lines.Add((key, label, percent, cells, rank));
        }

        return lines;
    }

    private static int SkillId(string key) => int.Parse(key, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private void Unfold(TwinComparisonRow row)
    {
        if (row.IsUnfolded)
            _unfolded.Add(row.Key);
        else
            _unfolded.Remove(row.Key);
    }

    // The change in percent (as a fraction): other / reference - 1. Against a reference without the stat, all or nothing:
    // +100% for one only the other has (-100% where it's negative), 0 where neither has it.
    private static double Change(double other, double reference) => reference != 0 ? other / reference - 1 : Math.Sign(other);

    // The value itself, or with the difference on how much more or less than the reference ("+54", "+0.1%"); on hover
    // what isn't shown, and the change in percent.
    private TwinComparisonCell Cell(double? stat, double reference, bool percent, double change)
    {
        var culture = CultureInfo.CurrentCulture;
        var unit = percent ? "%" : "";
        var raw = stat is { } value ? DigitalTwinSheet.Number(value, percent) : "–";
        var difference = (stat ?? 0) - reference;
        var same = Math.Abs(difference) < 0.005;
        var shown = same ? "±0" + unit : difference.ToString("+#,0.##;-#,0.##", culture) + unit;
        var inPercent = reference != 0 ? $" ({(change * 100).ToString("+#,0.#;-#,0.#;0", culture)}%)" : "";
        return ShowDifference
            ? new TwinComparisonCell(shown, same ? 0 : Math.Sign(difference), raw + inPercent)
            : new TwinComparisonCell(raw, 0, shown + inPercent);
    }
}

// A snapshot in the comparison: its column's head (name, when) and its line in the drop-down.
public sealed class TwinComparisonColumn(DigitalTwin twin)
{
    private Dictionary<string, List<(string Key, string Label, double Value)>>? _sources;

    public DigitalTwin Twin { get; } = twin;

    public string Name => Twin.Name;

    public string TakenAt => Twin.FetchedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public override string ToString() => $"{Twin.Name} · {Twin.ServerName} · {TakenAt}";

    // A sum's key: its stat, "%" after it for a percentage.
    internal static string KeyOf(string stat, bool percent) => percent ? stat + "%" : stat;

    // Where a sum comes from, added up as DigitalTwinBuilder does: each base stat's effect, the titles, each gear slot (its
    // main and sub stats, the enchantment's part included; the arcana cards by their kind), the magic stones, each
    // Daevanion board.
    public IReadOnlyList<(string Key, string Label, double Value)> SourcesOf(string key) =>
        (_sources ??= Sources(Twin)).TryGetValue(key, out var sources) ? sources : [];

    // Where a skill's level (the key: its id) comes from: the levels put in with skill points (what's left of the level
    // without the others; points: their name), each gear slot and arcana card kind adding to it, and Daevanion. A level
    // the website gives as 0 (some stigmas) has no points to tell.
    public IReadOnlyList<(string Key, string Label, double Value)> SkillSourcesOf(string key, string points)
    {
        if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || Array.Find(Twin.Skills, skill => skill.Id == id) is not { } skill)
            return [];

        var sources = new List<(string Key, string Label, double Value)>();
        var fromGear = 0;
        foreach (var item in Twin.Gear)
        {
            var levels = item.SkillBonuses.Where(bonus => bonus.SkillId == id).Sum(static bonus => bonus.Levels);
            if (levels == 0)
                continue;

            fromGear += levels;
            var (source, label) = GearSourceOf(item);
            var index = sources.FindIndex(known => known.Key == source);
            if (index < 0)
                sources.Add((source, label, levels));
            else
                sources[index] = (source, label, sources[index].Value + levels);
        }

        if (skill.Level > 0)
            sources.Insert(0, ("points", points, skill.Level - fromGear - skill.DaevanionLevels));
        if (skill.DaevanionLevels > 0)
            sources.Add(("daevanion", DigitalTwinBuilder.DaevanionSource, skill.DaevanionLevels));
        return sources;
    }

    // A gear item as a source: its slot ("Ring1"), an arcana card by its kind, the first word of its name ("Chalice of
    // Vigor": Chalice, also Parchment, Compass, Bell, Mirror), so the same kind lines up whatever the card.
    private static (string Key, string Label) GearSourceOf(TwinItem item) =>
        item.Slot.StartsWith("Arcana", StringComparison.OrdinalIgnoreCase) && item.Name.Split(' ', 2)[0] is { Length: > 0 } kind
            ? ("arcana " + kind, "Arcana: " + kind)
            : ("slot " + item.SlotPos.ToString(CultureInfo.InvariantCulture), item.Slot);

    private static Dictionary<string, List<(string Key, string Label, double Value)>> Sources(DigitalTwin twin)
    {
        var sums = new Dictionary<string, List<(string Key, string Label, double Value)>>();
        void Add(TwinStatValue value, string source, string label)
        {
            var key = KeyOf(value.Name, value.Percent);
            if (!sums.TryGetValue(key, out var sources))
                sums[key] = sources = [];
            var index = sources.FindIndex(known => known.Key == source);
            if (index < 0)
                sources.Add((source, label, value.Value + value.Extra));
            else
                sources[index] = (source, label, sources[index].Value + value.Value + value.Extra);
        }

        void AddEffects(IEnumerable<string> effects, string source, string label)
        {
            foreach (var effect in effects)
            {
                if (DigitalTwinBuilder.TryParseEffect(effect, out var value))
                    Add(value, source, label);
            }
        }

        foreach (var stat in twin.PrimaryStats)
            AddEffects(stat.Effects, "stat " + stat.Name, stat.Name);
        foreach (var title in twin.Titles)
            AddEffects(title.Effects, "titles", DigitalTwinBuilder.TitleSource);
        foreach (var item in twin.Gear)
        {
            var (source, label) = GearSourceOf(item);
            foreach (var value in item.MainStats.Concat(item.SubStats))
                Add(value, source, label);
            foreach (var value in item.Stones)
                Add(value, "stones", DigitalTwinBuilder.StoneSource);
        }

        foreach (var board in twin.Boards)
            AddEffects(board.StatEffects, "board " + board.Name, $"{DigitalTwinBuilder.DaevanionSource}: {board.Name}");
        return sums;
    }
}

// A stat's line, or a section's title (no cells). A sum's line unfolds with a click to its sources: a line for each
// source any of the snapshots has it from.
public sealed partial class TwinComparisonRow(string key, string label, TwinComparisonCell[] cells, bool isSection = false, TwinComparisonRow[]? sources = null,
    Action<TwinComparisonRow>? folded = null) : ObservableObject
{
    public string Key { get; } = key;

    public string Label { get; } = label;

    public TwinComparisonCell[] Cells { get; } = cells;

    public bool IsSection { get; } = isSection;

    public TwinComparisonRow[] Sources { get; } = sources ?? [];

    public bool CanUnfold => Sources.Length > 0;

    // A skill's icon, and what hovering its name shows (else the name itself, for one cut short).
    public string? IconAssetName { get; init; }

    public SkillTip? Tip { get; init; }

    public object Hover => (object?)Tip ?? Label;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fold))]
    public partial bool IsUnfolded { get; set; }

    // ▸ folded, ▾ unfolded, nothing for a line without sources.
    public string Fold => !CanUnfold ? "" : IsUnfolded ? "▾" : "▸";

    [RelayCommand]
    private void Toggle()
    {
        IsUnfolded = !IsUnfolded;
        folded?.Invoke(this);
    }
}

// Change: more (1) or less (-1) than the reference, shown as the difference; on hover what isn't shown (Hint), or for a
// skill's level what the skill does at it (Tip).
public sealed record TwinComparisonCell(string Text, int Change = 0, string? Hint = null, SkillTip? Tip = null)
{
    public bool IsMore => Change > 0;

    public bool IsLess => Change < 0;

    public object? Hover => (object?)Tip ?? Hint;
}
