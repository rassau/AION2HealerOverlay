namespace Cloris.Aion2Flow.SceneRuntime.Model;

// TargetIsAlly: the target the auras were read for is a player of your party or force, or of your faction (or one whose
// faction, or yours, isn't known) who hasn't fought you or them on this map, or such a player's summon, yours too; anything
// else, an NPC, a player who fought you or them, another player of the other faction or their summon, is an enemy.
// TargetIsNpc: it's an NPC the overlay knows (by its code or kind), no player's summon: no player is on its side.
public readonly record struct SceneLocalPlayerFrame(
    Guid SessionId,
    long StartObservationOrdinal,
    long EndObservationOrdinalExclusive,
    int EntityId,
    long ObservedAtMilliseconds,
    bool TargetIsAlly = false,
    bool TargetIsNpc = false);
