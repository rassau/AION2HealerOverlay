using System.Buffers.Binary;
using System.Numerics;

namespace Cloris.Aion2Flow.Capture.Streams;

// Where players are, for the group frame; group members' also go into the journal, once a second at most (see
// PacketParseContext.JournalPosition), for buff ranges and the history. In dungeons party members come every ~2 s from
// 0x1C92, wherever they are, under their character id; in the open world that doesn't come at all. The party list
// (0x0092) and the details on entering a map (0x2292) say whose that is, but both only come when the party forms or a map
// is entered, so a member's spawn (0x4536) does too: it holds their character id. In sight is what we see ourselves: a
// player's spawn says where they came into sight, their moves (walking, running, gliding, blinking: see ParseMovePacket)
// come about every 100 ms while they move and none while they stand still, until they leave sight (0x4736). Anyone who
// opens an aura (0x2A38) tells where they stand, and its renewals (0x2B38) where they stood a moment to some seconds
// before; that's the only place the server says where we are ourselves.
// Units are cm; x points east, y south, z up.
internal static class PlayerPositions
{
    // InSight: they're in sight, so they still stand there, however old the position.
    public readonly record struct Fix(Vector3 Position, long AtMilliseconds, bool InSight = false);

    private const int MaxEntities = 512;
    private const int MaxOutOfSight = 1_024;
    private const long PruneAgeMilliseconds = 60_000;
    private static readonly Lock Gate = new();
    private static readonly Dictionary<long, Fix> ByCharacter = [];
    private static readonly Dictionary<int, long> CharacterOfEntity = [];
    private static readonly Dictionary<int, Fix> ByEntity = [];
    // Players in sight, where we last saw them; they leave it with 0x4736 or our next map.
    private static readonly Dictionary<int, Fix> InSight = [];
    // Players who left our sight since, so farther than ~45 m.
    private static readonly HashSet<int> OutOfSight = [];

    public static void UpdateCharacter(long characterId, Vector3 position, long atMilliseconds)
    {
        lock (Gate)
            ByCharacter[characterId] = new Fix(position, atMilliseconds);
    }

    // The entities tied to a character: its current one, and older ones from before a map change.
    public static int[] EntitiesOf(long characterId)
    {
        lock (Gate)
            return [.. CharacterOfEntity.Where(link => link.Value == characterId).Select(static link => link.Key)];
    }

    public static void LinkCharacter(long characterId, int entityId)
    {
        if (characterId == 0 || entityId <= 0)
            return;

        lock (Gate)
            CharacterOfEntity[entityId] = characterId;
    }

    // A player came into sight (0x4536), standing where it says. When their spawn holds the id of a character 0x1C92
    // reported within the last minute (the party now), those positions are theirs too.
    // ponytail: a spawn before that character's first 0x1C92 ties nothing; their next spawn (map change, back in sight) does.
    public static void LinkSpawn(int entityId, ReadOnlySpan<byte> spawn, Vector3? position, long atMilliseconds)
    {
        if (entityId <= 0)
            return;

        Span<byte> characterBytes = stackalloc byte[sizeof(long)];
        lock (Gate)
        {
            OutOfSight.Remove(entityId);
            if (position is { } standing)
                InSight[entityId] = new Fix(standing, atMilliseconds, true);

            foreach (var (characterId, fix) in ByCharacter)
            {
                if (atMilliseconds - fix.AtMilliseconds > PruneAgeMilliseconds)
                    continue;

                BinaryPrimitives.WriteInt64LittleEndian(characterBytes, characterId);
                if (spawn.IndexOf(characterBytes) >= 0)
                {
                    CharacterOfEntity[entityId] = characterId;
                    return;
                }
            }
        }
    }

    // Someone in sight moving: kept for the players who came into sight and for party members (whose spawn may have come
    // before the overlay started); NPCs' moves are left out.
    public static void UpdateMember(int entityId, Vector3 position, long atMilliseconds)
    {
        lock (Gate)
        {
            if (!InSight.ContainsKey(entityId) && !CharacterOfEntity.ContainsKey(entityId))
                return;

            InSight[entityId] = new Fix(position, atMilliseconds, true);
            OutOfSight.Remove(entityId);
        }
    }

    // 0x4736: a player left our sight, too far away, teleported, released after dying or out of the dungeon.
    // ponytail: a crowd passing by fills the set; full, it starts over, and members lose their dimming until next time.
    public static void LeftSight(int entityId)
    {
        lock (Gate)
        {
            InSight.Remove(entityId);
            if (OutOfSight.Count >= MaxOutOfSight)
                OutOfSight.Clear();
            OutOfSight.Add(entityId);
        }
    }

    // Arriving on a map (0x2336), everyone in sight comes into it anew, under new ids.
    public static void ClearSight()
    {
        lock (Gate)
        {
            InSight.Clear();
            OutOfSight.Clear();
        }
    }

    public static bool IsOutOfSight(int entityId)
    {
        lock (Gate)
            return OutOfSight.Contains(entityId);
    }

    public static void UpdateEntity(int entityId, Vector3 position, long atMilliseconds)
    {
        if (entityId <= 0)
            return;

        lock (Gate)
        {
            if (ByEntity.Count >= MaxEntities)
            {
                foreach (var (id, fix) in ByEntity)
                {
                    if (atMilliseconds - fix.AtMilliseconds > PruneAgeMilliseconds)
                        ByEntity.Remove(id);
                }
            }

            ByEntity[entityId] = new Fix(position, atMilliseconds);
        }
    }

    // The newest position known for an entity: where we see it, from its own auras or its character's party update.
    // While it is in sight, the newest still holds however old: it stands there until it moves or leaves.
    public static bool TryGet(int entityId, out Fix fix)
    {
        lock (Gate)
        {
            var found = ByEntity.TryGetValue(entityId, out fix);
            if (CharacterOfEntity.TryGetValue(entityId, out var characterId) && ByCharacter.TryGetValue(characterId, out var party) && (!found || party.AtMilliseconds >= fix.AtMilliseconds))
                (fix, found) = (party, true);
            if (!InSight.TryGetValue(entityId, out var seen))
                return found;

            fix = (found && fix.AtMilliseconds > seen.AtMilliseconds ? fix : seen) with { InSight = true };
            return true;
        }
    }

    // From one position to another: distance in metres, bearing in degrees clockwise from north, and how much higher.
    public static (double Meters, double Bearing, double RiseMeters) Between(Vector3 from, Vector3 to)
    {
        var east = (double)to.X - from.X;
        var north = (double)from.Y - to.Y;
        var rise = (double)to.Z - from.Z;
        var bearing = Math.Atan2(east, north) * 180 / Math.PI;
        return (Math.Sqrt(east * east + north * north + rise * rise) / 100, bearing < 0 ? bearing + 360 : bearing, rise / 100);
    }

    internal static void Clear()
    {
        lock (Gate)
        {
            ByCharacter.Clear();
            CharacterOfEntity.Clear();
            ByEntity.Clear();
            InSight.Clear();
            OutOfSight.Clear();
        }
    }
}
