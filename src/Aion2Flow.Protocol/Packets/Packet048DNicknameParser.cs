using Cloris.Aion2Flow.Protocol.Readers;

namespace Cloris.Aion2Flow.Protocol.Packets;

// KilledEntityId: in the game's kill message, the NPC that died (PlayerId its killer); 0 for a player that died and for other
// messages.
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

        // The game's kill message: who died (a varint) and the killing skill (u32), then the killer (a summon's owner), ~50 ms
        // after the death. An NPC that died is a 3-byte varint: 78 of 78 deaths with hits in a minute of Abyss farming (19 Nov
        // 2026). A player that died is a 1- or 2-byte one (1,709 player kills in 68 recordings): read at the NPC's offset, the
        // killer came out as their id >> 7 or their server's bytes, and their name and server went to that wrong id. Anything
        // else is read at that offset as before (a 1-byte first number, then six bytes; none in 68 recordings); a death
        // without a killer (24 bytes) names no one and doesn't parse.
        // ponytail: an NPC id under 16,384 or over 2,097,151 is read as no kill; none seen in a kill message.
        var died = PacketSpanReader.TryReadVarInt(payload, 2, out var diedId, out var diedLength);
        if (!(died && TryParsePlayerAt(payload, 2 + diedLength + sizeof(uint), out result)) &&
            !TryParsePlayerAt(payload, HeaderLengthBeforePlayerId, out result))
        {
            return false;
        }

        result = result with { KilledEntityId = died && diedLength == 3 ? diedId : 0 };
        return true;
    }

    private static bool TryParsePlayerAt(ReadOnlySpan<byte> payload, int offset, out Packet048DNickname result)
    {
        result = default;
        if (!PacketSpanReader.TryReadVarInt(payload, offset, out var playerId, out var consumed))
            return false;

        var serverOffset = offset + consumed;
        return NicknameParserUtil.TryReadOriginServerIdLe16(payload, serverOffset, out var serverId) &&
               TryParseNicknameAt(payload, serverOffset + sizeof(ushort), serverId, playerId, out result) ||
               TryParseNicknameAt(payload, serverOffset, originServerId: null, playerId, out result);
    }

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
