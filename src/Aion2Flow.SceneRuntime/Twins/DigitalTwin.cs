namespace Cloris.Aion2Flow.SceneRuntime.Twins;

// A player as the game's official character website shows them at one moment: gear with every item's stats, skills with
// their levels, Daevanion boards, titles, the base stats with what they give, and the sums of all stats that come from
// gear, Daevanion and titles (Totals). The class's own base values and buffs aren't on the website, so Totals leaves them
// out. Saved with history entries, so an entry shows the player as they were around that fight.
public sealed record DigitalTwin(
    string Name,
    int ServerId,
    string ServerName,
    string ClassName,
    int Level,
    int CombatPower,
    int ItemLevel,
    DateTimeOffset FetchedAt,
    TwinPrimaryStat[] PrimaryStats,
    TwinItem[] Gear,
    TwinSkill[] Skills,
    TwinBoard[] Boards,
    TwinTitle[] Titles,
    TwinStatTotal[] Totals,
    string? Pet = null,
    string? Wings = null)
{
    // The level the website gives a skill (found by any of its ids): the one it's used at, gear and Daevanion included
    // (the website's levels minus those parts come out at one base level for nearly all of a player's skills). Null when
    // it lists none (0), as for some stigmas.
    public int? SkillLevel(int skillCode) =>
        Array.Find(Skills, skill => skill.Id == skillCode / 10_000 * 10_000) is { Level: > 0 } skill ? skill.Level : null;
}

// A base stat (Strength, Precision, Justice, …) with the game's own sum and the effects it gives, e.g. "Accuracy increase +5.6%".
public sealed record TwinPrimaryStat(string Name, double Value, string[] Effects);

// Extra: the enchantment's part. Min: the low end of a range (weapon Attack 309–398).
public sealed record TwinStatValue(string Name, double Value, bool Percent, double Extra = 0, double? Min = null);

public sealed record TwinSkillBonus(int SkillId, string Name, int Levels);

public sealed record TwinItem(
    int SlotPos,
    string Slot,
    string Name,
    string Grade,
    int Enchant,
    int Exceed,
    TwinStatValue[] MainStats,
    TwinStatValue[] SubStats,
    TwinStatValue[] Stones,
    TwinSkillBonus[] SkillBonuses,
    string[] Theostones);

// Level: as the website gives it; GearLevels and DaevanionLevels: what items and Daevanion nodes add to the skill (whether
// the website's level already counts them is still to be checked in game).
public sealed record TwinSkill(int Id, string Name, int Level, string Category, bool Equipped, int GearLevels = 0, int DaevanionLevels = 0);

public sealed record TwinBoard(int Id, string Name, int OpenNodes, int Nodes, string[] StatEffects, string[] SkillEffects);

public sealed record TwinTitle(string Category, string Name, string[] Effects);

public sealed record TwinStatSource(string Source, double Value);

// Min: the sum of the low ends where a source has a range (Attack), else of the values.
public sealed record TwinStatTotal(string Name, double Value, bool Percent, TwinStatSource[] Sources, double? Min = null);
