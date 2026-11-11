using System.Numerics;

namespace Cloris.Aion2Flow.Capture.Streams;

// The Hidden Cubes the server has announced and not taken away again, for the group frame's cube finder. Placed objects
// (cubes, event objects, things to use in dungeons, …) come with 0x3436 once they're within about 170 m, and again now and
// then while they stay (see PacketObjectHandler); a used-up one goes with 0x058D and a state (0x3536) starting with 4. A
// cube is known by its code. Units are cm, like PlayerPositions.
// ponytail: only the code of the one cube recorded so far (11 Nov 2026); add others as recordings show them.
internal static class HiddenCubes
{
    public readonly record struct Cube(int EntityId, int Code, Vector3 Position, long SeenAtMilliseconds);

    private static readonly int[] Codes = [1_301_199];
    private static readonly Lock Gate = new();
    private static readonly Dictionary<int, Cube> ByEntity = [];

    public static bool IsCube(int code) => Array.IndexOf(Codes, code) >= 0;

    public static void Saw(int entityId, int code, Vector3 position, long atMilliseconds)
    {
        if (!IsCube(code))
            return;

        lock (Gate)
            ByEntity[entityId] = new Cube(entityId, code, position, atMilliseconds);
    }

    public static void Remove(int entityId)
    {
        lock (Gate)
            ByEntity.Remove(entityId);
    }

    public static void CopyTo(List<Cube> destination)
    {
        lock (Gate)
        {
            destination.Clear();
            destination.AddRange(ByEntity.Values);
        }
    }

    // On a new map objects come anew, under new ids.
    public static void Clear()
    {
        lock (Gate)
            ByEntity.Clear();
    }
}
