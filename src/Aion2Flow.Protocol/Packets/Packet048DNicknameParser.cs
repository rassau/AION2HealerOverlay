using Cloris.Aion2Flow.Protocol.Readers;

namespace Cloris.Aion2Flow.Protocol.Packets;

// KilledEntityId: in the game's kill message, the NPC that died (PlayerId its killer); 0 for other messages.
internal readonly record struct Packet048DNickname(int PlayerId, string Nickname, int NicknameLength, int TailOffset, int? OriginServerId, byte FactionCode, string LegionName, int KilledEntityId = 0);

internal static class Packet048DNicknameParser
{
    private const int HeaderLengthBeforePlayerId = 9;

    public static bool TryParse(ReadOnlySpan<byte> packet, out Packet048DNickname result)
    {
        result = default;

        var reader = new PacketSpanReader(packet);
        if (!reader.TryReadVarInt(out _)) return false;
        var payloadOffset = reader.Offset;

        if (!TryParsePayload(packet[payloadOffset..], out var payloadResult))
        {
            return false;
        }

        result = payloadResult with { TailOffset = payloadOffset + payloadResult.TailOffset };
        return true;
    }

    public static bool TryParsePayload(ReadOnlySpan<byte> payload, out Packet048DNickname result)
    {
        result = default;

        if (payload.Length < 11 || payload[0] != 0x04 || payload[1] != 0x8d)
        {
            return false;
        }

        var reader = new PacketSpanReader(payload);
        if (!reader.TryAdvance(HeaderLengthBeforePlayerId)) return false;
        if (!reader.TryReadVarInt(out var playerId)) return false;

        if (!(NicknameParserUtil.TryReadOriginServerIdLe16(payload, reader.Offset, out var serverId) &&
              TryParseNicknameAt(payload, reader.Offset + sizeof(ushort), serverId, playerId, out result)) &&
            !TryParseNicknameAt(payload, reader.Offset, originServerId: null, playerId, out result))
        {
            return false;
        }

        result = result with { KilledEntityId = ReadKilledEntityId(payload) };
        return true;
    }

    // In the game's kill message the 7 bytes before the player are the NPC that died (a 3-byte varint) and the killing
    // skill (u32), the player being the killer (a summon's owner), ~50 ms after the NPC's 0 HP update: 78 of 78 deaths with
    // hits in a minute of Abyss farming (19 Nov 2026). Other messages of this opcode have other bytes there (a 1-byte first
    // number in a dungeon recording of July 2026); a death without a killer (24 bytes) names no one and doesn't parse.
    // ponytail: an NPC id under 16,384 or over 2,097,151 is read as no kill; none seen in a kill message.
    private static int ReadKilledEntityId(ReadOnlySpan<byte> payload) =>
        PacketSpanReader.TryReadVarInt(payload, 2, out var entityId, out var consumed) && consumed == 3 ? entityId : 0;

    private static bool TryParseNicknameAt(ReadOnlySpan<byte> payload, int nameLengthOffset, int? originServerId, int playerId, out Packet048DNickname result)
    {
        result = default;
        if (!NicknameParserUtil.TryReadLengthPrefixedNickname(payload, nameLengthOffset, out var nickname, out var nicknameLength, out var tailOffset))
        {
            return false;
        }

        var directFactionCode = NicknameParserUtil.TryReadFactionCode(payload, tailOffset + 7);
        var legionName = string.Empty;
        var identityTailOffset = tailOffset;
        if (PacketIdentityTrailerParser.TryReadNicknameAdjacentLegionBlock(payload, tailOffset, out var parsedLegionName, out var parsedFactionCode, out var legionTailOffset))
        {
            legionName = parsedLegionName;
            directFactionCode = parsedFactionCode;
            identityTailOffset = legionTailOffset;
        }

        result = new Packet048DNickname(playerId, nickname, nicknameLength, identityTailOffset, originServerId, NicknameParserUtil.SelectFactionCode(directFactionCode, originServerId), legionName);
        return true;
    }
}
