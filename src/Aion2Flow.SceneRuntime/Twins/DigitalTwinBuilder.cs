using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cloris.Aion2Flow.SceneRuntime.Twins;

// Builds a DigitalTwin from the official character website's JSON: /api/character/info (profile, base stats, titles, the
// Daevanion boards), /api/character/equipment (gear, skills, pet and wings), /api/character/equipment/item for each gear
// slot (the item's stats, stones and skill levels) and /api/character/daevanion/detail for each open board. Numbers come
// as numbers or text ("398", "6.1%", "+50"), lists sometimes as a single object.
public static partial class DigitalTwinBuilder
{
    public const string GearSource = "Gear";
    public const string StoneSource = "Magic stones";
    public const string DaevanionSource = "Daevanion";
    public const string TitleSource = "Titles";

    // Shown first, in this order; the rest alphabetically.
    private static readonly string[] Leading =
    [
        "Attack", "Attack Bonus", "Accuracy", "Critical Hit", "Critical Damage Boost", "Damage Boost", "PvE Damage Boost",
        "Heal Boost", "Combat Speed", "Cooldown Reduction", "HP", "Defense", "Defense Bonus", "Damage Tolerance",
        "PvE Damage Tolerance", "Block", "Evasion"
    ];

    public static DigitalTwin Build(int serverId, DateTimeOffset fetchedAt, string infoJson, string equipmentJson,
        IReadOnlyDictionary<int, string> itemJsonBySlot, IReadOnlyDictionary<int, string> boardJsonById)
    {
        using var info = JsonDocument.Parse(infoJson);
        using var equipment = JsonDocument.Parse(equipmentJson);
        var profile = Get(info.RootElement, "profile");
        var sources = new List<(string Source, TwinStatValue Value)>();

        var primaryStats = new List<TwinPrimaryStat>();
        var itemLevel = 0;
        foreach (var stat in Items(Get(Get(info.RootElement, "stat"), "statList")))
        {
            var type = Text(stat, "type");
            if (type == "ItemLevel")
            {
                itemLevel = (int)Number(stat, "value");
                continue;
            }

            var name = Text(stat, "name") is { Length: > 0 } statName ? statName : type;
            var effects = Items(Get(stat, "statSecondList"))
                .Select(static effect => effect.ValueKind == JsonValueKind.String ? effect.GetString() ?? "" : Text(effect, "desc"))
                .Where(static effect => effect.Length > 0)
                .ToArray();
            primaryStats.Add(new TwinPrimaryStat(name, Number(stat, "value"), effects));
            foreach (var effect in effects)
            {
                if (TryParseEffect(effect, out var value))
                    sources.Add((name, value));
            }
        }

        var titles = new List<TwinTitle>();
        foreach (var title in Items(Get(Get(info.RootElement, "title"), "titleList")))
        {
            var effects = Items(Get(title, "equipStatList")).Concat(Items(Get(title, "statList"))).Select(static effect => Text(effect, "desc")).Where(static effect => effect.Length > 0).ToArray();
            titles.Add(new TwinTitle(Text(title, "equipCategory"), Text(title, "name"), effects));
            foreach (var effect in effects)
            {
                if (TryParseEffect(effect, out var value))
                    sources.Add((TitleSource, value));
            }
        }

        var gear = new List<TwinItem>();
        foreach (var item in Items(Get(Get(equipment.RootElement, "equipment"), "equipmentList")))
        {
            var slotPos = (int)Number(item, "slotPos");
            using var detail = itemJsonBySlot.TryGetValue(slotPos, out var itemJson) ? JsonDocument.Parse(itemJson) : null;
            var root = detail?.RootElement ?? default;
            var mainStats = Items(Get(root, "mainStats")).Select(static stat => StatValue(stat, withRange: true)).OfType<TwinStatValue>().ToArray();
            var subStats = Items(Get(root, "subStats")).Select(static stat => StatValue(stat, withRange: false)).OfType<TwinStatValue>().ToArray();
            var stones = Items(Get(root, "magicStoneStat")).Select(static stat => StatValue(stat, withRange: false)).OfType<TwinStatValue>().ToArray();
            var skillBonuses = Items(Get(root, "subSkills")).Select(static skill => new TwinSkillBonus((int)Number(skill, "id"), Text(skill, "name"), (int)Number(skill, "level"))).Where(static skill => skill.Levels > 0).ToArray();
            var theostones = Items(Get(root, "godStoneStat")).Select(static stone => Text(stone, "name")).Where(static name => name.Length > 0).ToArray();
            gear.Add(new TwinItem(slotPos, Text(item, "slotPosName"), Text(item, "name"), Text(item, "grade"), (int)Number(item, "enchantLevel"), (int)Number(item, "exceedLevel"),
                mainStats, subStats, stones, skillBonuses, theostones));
            sources.AddRange(mainStats.Concat(subStats).Select(static value => (GearSource, value)));
            sources.AddRange(stones.Select(static value => (StoneSource, value)));
        }

        var boards = new List<TwinBoard>();
        foreach (var board in Items(Get(Get(info.RootElement, "daevanion"), "boardList")))
        {
            var id = (int)Number(board, "id");
            string[] statEffects = [], skillEffects = [];
            if (boardJsonById.TryGetValue(id, out var boardJson))
            {
                using var detail = JsonDocument.Parse(boardJson);
                statEffects = Items(Get(detail.RootElement, "openStatEffectList")).Select(static effect => Text(effect, "desc")).Where(static effect => effect.Length > 0).ToArray();
                skillEffects = Items(Get(detail.RootElement, "openSkillEffectList")).Select(static effect => Text(effect, "desc")).Where(static effect => effect.Length > 0).ToArray();
            }

            boards.Add(new TwinBoard(id, Text(board, "name"), (int)Number(board, "openNodeCount"), (int)Number(board, "totalNodeCount"), statEffects, skillEffects));
            foreach (var effect in statEffects)
            {
                if (TryParseEffect(effect, out var value))
                    sources.Add((DaevanionSource, value));
            }
        }

        var gearLevels = gear.SelectMany(static item => item.SkillBonuses).GroupBy(static bonus => bonus.SkillId).ToDictionary(static g => g.Key, static g => g.Sum(static bonus => bonus.Levels));
        var daevanionLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var effect in boards.SelectMany(static board => board.SkillEffects))
        {
            if (TryParseEffect(effect, out var value) && !value.Percent)
                daevanionLevels[value.Name] = daevanionLevels.GetValueOrDefault(value.Name) + (int)value.Value;
        }

        var skills = Items(Get(Get(equipment.RootElement, "skill"), "skillList"))
            .Where(static skill => Number(skill, "acquired") > 0)
            .Select(skill =>
            {
                var id = (int)Number(skill, "id");
                var name = Text(skill, "name");
                return new TwinSkill(id, name, (int)Number(skill, "skillLevel"), Text(skill, "category"), Number(skill, "equip") > 0,
                    gearLevels.GetValueOrDefault(id), daevanionLevels.GetValueOrDefault(name));
            })
            .ToArray();

        var petwing = Get(equipment.RootElement, "petwing");
        var pet = Get(petwing, "pet");
        var wing = Get(petwing, "wing");
        return new DigitalTwin(
            Text(profile, "characterName"),
            serverId,
            Text(profile, "serverName"),
            Text(profile, "className"),
            (int)Number(profile, "characterLevel"),
            (int)Number(profile, "combatPower"),
            itemLevel,
            fetchedAt,
            [.. primaryStats],
            [.. gear],
            skills,
            [.. boards],
            [.. titles],
            Sum(sources),
            Text(pet, "name") is { Length: > 0 } petName ? $"{petName} (level {Number(pet, "level")})" : null,
            Text(wing, "name") is { Length: > 0 } wingName ? $"{wingName} +{Number(wing, "enchantLevel")}" : null);
    }

    // "Attack Bonus +24", "Cooldown Reduction +3%", "Accuracy increase +5.6%", "Earth's Retribution +1".
    public static bool TryParseEffect(string text, out TwinStatValue value)
    {
        var match = EffectPattern().Match(text.Trim());
        if (!match.Success)
        {
            value = default!;
            return false;
        }

        var name = IncreaseSuffix().Replace(match.Groups["name"].Value, "");
        var number = ParseNumber(match.Groups["value"].Value) * (match.Groups["sign"].Value == "-" ? -1 : 1);
        value = new TwinStatValue(name, number, match.Groups["percent"].Success);
        return true;
    }

    // Summed by stat (flat and % apart), with each source's part.
    private static TwinStatTotal[] Sum(List<(string Source, TwinStatValue Value)> sources) =>
        sources
            .GroupBy(static source => (source.Value.Name, source.Value.Percent))
            .Select(static stat =>
            {
                var parts = stat.GroupBy(static source => source.Source).Select(static source => new TwinStatSource(source.Key, Round(source.Sum(static s => s.Value.Value + s.Value.Extra)))).ToArray();
                double? min = stat.Any(static source => source.Value.Min is not null) ? Round(stat.Sum(static source => (source.Value.Min ?? source.Value.Value) + source.Value.Extra)) : null;
                return new TwinStatTotal(stat.Key.Name, Round(parts.Sum(static part => part.Value)), stat.Key.Percent, parts, min);
            })
            .OrderBy(static total => Array.IndexOf(Leading, total.Name) is var i and >= 0 ? i : Leading.Length)
            .ThenBy(static total => total.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static total => total.Percent)
            .ToArray();

    private static TwinStatValue? StatValue(JsonElement stat, bool withRange)
    {
        var name = Text(stat, "name");
        var text = Text(stat, "value");
        if (name.Length == 0 || text.Length == 0)
            return null;

        var min = withRange && Text(stat, "minValue") is { Length: > 0 } minText ? ParseNumber(minText) : (double?)null;
        return new TwinStatValue(name, ParseNumber(text), text.TrimEnd().EndsWith('%'), withRange ? Number(stat, "extra") : 0, min);
    }

    // Sums of tenths come out as 8.399999999999999.
    private static double Round(double value) => Math.Round(value, 4);

    private static double ParseNumber(string text) =>
        double.TryParse(text.Replace(",", "").Replace("+", "").Replace("%", "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static JsonElement Get(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private static IEnumerable<JsonElement> Items(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Array => element.EnumerateArray(),
        JsonValueKind.Object => [element],
        _ => []
    };

    private static string Text(JsonElement element, string name) => Get(element, name) switch
    {
        { ValueKind: JsonValueKind.String } value => value.GetString() ?? "",
        { ValueKind: JsonValueKind.Number } value => value.GetRawText(),
        _ => ""
    };

    private static double Number(JsonElement element, string name) => ParseNumber(Text(element, name));

    [GeneratedRegex(@"^(?<name>.*?\S)\s*(?<sign>[+-])\s*(?<value>\d[\d,]*(?:\.\d+)?)\s*(?<percent>%)?$")]
    private static partial Regex EffectPattern();

    [GeneratedRegex(@"\s+increase$", RegexOptions.IgnoreCase)]
    private static partial Regex IncreaseSuffix();
}
