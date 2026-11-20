using System.Numerics;

namespace Cloris.Aion2Flow.Protocol.Packets;

// The positions the packets carry are x, y, z floats in cm, and the maps are some km wide: a coordinate past 100 km is
// other bytes read as floats, the packet laid out otherwise than its reader expects (as players coming into sight in
// PvP areas were, 0x4536). NaN and infinities fail too.
// ponytail: bytes from the wrong place often read as a tiny number instead (1e-30), which passes; a real coordinate can
// be that close to 0 too (0.0006 cm in a move), so only a packet's own checks (the heading, see 0x4536) catch those.
internal static class MapPosition
{
    public static bool IsOnMap(Vector3 position) => IsOnMap(position.X) && IsOnMap(position.Y) && IsOnMap(position.Z);

    private static bool IsOnMap(float value) => Math.Abs(value) < 10_000_000;
}
