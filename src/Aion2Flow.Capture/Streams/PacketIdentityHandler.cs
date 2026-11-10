using System.Buffers.Binary;
using System.Numerics;
using Cloris.Aion2Flow.Protocol.Packets;
using Cloris.Aion2Flow.Protocol.Readers;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.Capture.Streams;

internal static class PacketIdentityHandler
{
    public static bool ParseOwnNicknamePacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet3336NicknameParser.TryParse(packet, out var parsed))
        {
            return false;
        }

        context.Sink.AppendNickname(context.CreateObservationSource(0x3336, packet.Length), parsed.PlayerId, parsed.Nickname, PacketFactionMapper.ToFaction(parsed.FactionCode), isLocalPlayer: true, originServerId: parsed.OriginServerId, legionName: parsed.LegionName);
        return context.MarkParsed();
    }

    public static bool Parse0994NicknamePacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet0994NicknameParser.TryParse(packet, out var parsed))
        {
            return false;
        }

        context.Sink.AppendNickname(context.CreateObservationSource(0x0994, packet.Length), parsed.PlayerId, parsed.Nickname);
        return context.MarkParsed();
    }

    public static bool ParseLegionMetadataPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet2933LegionMetadataParser.TryParse(packet, out var parsed))
        {
            return false;
        }

        context.Sink.AppendNickname(context.CreateObservationSource(0x2933, packet.Length), parsed.EntityId, string.Empty, PacketFactionMapper.ToFaction(parsed.FactionCode), originServerId: parsed.OriginServerId, legionName: parsed.LegionName);
        return context.MarkParsed();
    }

    public static bool ParsePartyProfilePacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        var source = context.CreateObservationSource(0x0D92, packet.Length);
        if (PacketPlayerGroupParser.TryParsePartyMember(packet, out var member))
        {
            AppendPlayerGroupMember(context, in source, in member);
            PlayerPositions.LinkCharacter(member.CharacterId, member.EntityId);
            return context.MarkParsed();
        }

        if (!PacketPlayerGroupParser.TryParsePartyProfile(packet, out var profile))
        {
            return false;
        }

        AppendPlayerGroupProfile(context, in source, in profile);
        return context.MarkParsed();
    }

    public static bool ParsePartyStatusPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!PacketPlayerGroupParser.TryParsePartyStatusMember(packet, out var member, out var hp))
        {
            return false;
        }

        var source = context.CreateObservationSource(0x1B92, packet.Length);
        if (context.TryRegisterPartyStatusMember(member.EntityId))
            AppendPlayerGroupMember(context, in source, in member);

        context.Sink.AppendNpcHp(in source, member.EntityId, hp.CurrentHp, hp.MaxHp);
        return context.MarkParsed();
    }

    public static bool ParseForceStatusPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!PacketPlayerGroupParser.TryParseForceStatusMember(packet, out var member, out var hp))
            return false;

        var source = context.CreateObservationSource(0x2B96, packet.Length);
        if (context.TryRegisterForceStatusMember(member.EntityId))
            AppendPlayerGroupMember(context, in source, in member);

        context.Sink.AppendNpcHp(in source, member.EntityId, hp.CurrentHp, hp.MaxHp);
        return context.MarkParsed();
    }

    public static bool ParseForceMemberPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!PacketPlayerGroupParser.TryParseForceMember(packet, out var parsed))
        {
            return false;
        }

        var source = context.CreateObservationSource(0x1D96, packet.Length);
        AppendPlayerGroupMember(context, in source, in parsed);
        return context.MarkParsed();
    }

    public static bool ParseForceProfilePacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!PacketPlayerGroupParser.TryParseForceProfile(packet, out var parsed))
        {
            return false;
        }

        var source = context.CreateObservationSource(0x1B96, packet.Length);
        AppendPlayerGroupMember(context, in source, in parsed);
        return context.MarkParsed();
    }

    public static bool ParseForceRosterProfilePacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!PacketPlayerGroupParser.TryParseForceRosterProfile(packet, out var profile))
        {
            return false;
        }

        var source = context.CreateObservationSource(0x0A96, packet.Length);
        AppendPlayerGroupProfile(context, in source, in profile);
        return context.MarkParsed();
    }

    public static bool ParseForceRosterSnapshotPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        var members = new PacketPlayerGroupMember[32];
        var profiles = new PacketPlayerGroupProfile[32];
        var result = PacketPlayerGroupParser.ParseForceRosterSnapshot(packet, members, profiles);
        if (!result.IsValid)
            return false;

        var source = context.CreateObservationSource(0x0296, packet.Length);
        for (var i = 0; i < result.ProfileCount; i++)
            AppendPlayerGroupProfile(context, in source, in profiles[i]);

        for (var i = 0; i < result.MemberCount; i++)
            AppendPlayerGroupMember(context, in source, in members[i]);

        return context.MarkParsed();
    }

    public static bool ParsePartyMemberListPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        var members = new PacketPlayerGroupMember[6];
        var count = PacketPlayerGroupParser.ParsePartyMemberList(packet, members);
        var profiles = new PacketPlayerGroupProfile[6];
        var profileCount = PacketPlayerGroupParser.ParsePartyProfileList(packet, profiles);
        if (count == 0 && profileCount == 0)
        {
            return false;
        }

        var source = context.CreateObservationSource(0x0092, packet.Length);
        for (var i = 0; i < count; i++)
        {
            AppendPlayerGroupMember(context, in source, in members[i]);
            PlayerPositions.LinkCharacter(members[i].CharacterId, members[i].EntityId);
        }

        for (var i = 0; i < profileCount; i++)
        {
            AppendPlayerGroupProfile(context, in source, in profiles[i]);
        }

        return context.MarkParsed();
    }

    // 0x2292, one per party member on entering a map; only used to know whose 0x1C92 positions are whose.
    public static bool ParsePartyMemberDetailPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!PacketPlayerGroupParser.TryParsePartyMemberDetail(packet, out var member))
            return false;

        PlayerPositions.LinkCharacter(member.CharacterId, member.EntityId);
        return context.MarkParsed();
    }

    // 0x1C92, about every 2 s per other party member, wherever they are: character id (8 bytes), four ints, then x, y, z as
    // floats at 24, level and guild name after.
    public static bool ParsePartyMemberPositionPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        var reader = new PacketSpanReader(packet);
        if (!reader.TryReadVarInt(out _) || !reader.TryAdvance(2) || reader.Remaining < 36)
            return false;

        var body = reader.RemainingSpan;
        var position = new Vector3(
            BinaryPrimitives.ReadSingleLittleEndian(body[24..]),
            BinaryPrimitives.ReadSingleLittleEndian(body[28..]),
            BinaryPrimitives.ReadSingleLittleEndian(body[32..]));
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            return false;

        PlayerPositions.UpdateCharacter(BinaryPrimitives.ReadInt64LittleEndian(body), position, context.TimestampMilliseconds);
        return context.MarkParsed();
    }

    // Someone in sight moving (0x..37): entity, a few bytes, then x, y, z as floats; kept for players only. The bytes between
    // depend on the kind of move: 0x1A37 two; 0x1B37, 0x1C37, 0x1E37 and 0x2937 a flags byte and one more when its lowest bit
    // is set; 0x2E37 six; 0x3437 (gliding, ~36 m/s) none; 0x1F37, 0x2337, 0x2437, 0x2F37 and 0x3C37 (where a blink lands)
    // one. Running and gliding come only as the latter, about every 100 ms; each kind agrees with 0x1C92 to a metre or three.
    public static bool ParseMovePacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        var reader = new PacketSpanReader(packet);
        if (!reader.TryReadVarInt(out _) || reader.Remaining < 2)
            return false;

        var kind = packet[reader.Offset];
        if (!reader.TryAdvance(2) || !reader.TryReadVarInt(out var entityId) || reader.Remaining < 1)
            return false;

        var skip = kind switch
        {
            0x1a => 2,
            0x1b or 0x1c or 0x1e or 0x29 => 1 + (reader.RemainingSpan[0] & 1),
            0x2e => 6,
            0x34 => 0,
            _ => 1,
        };
        if (!reader.TryAdvance(skip) || reader.Remaining < 12)
            return false;

        var body = reader.RemainingSpan;
        var position = new Vector3(
            BinaryPrimitives.ReadSingleLittleEndian(body),
            BinaryPrimitives.ReadSingleLittleEndian(body[4..]),
            BinaryPrimitives.ReadSingleLittleEndian(body[8..]));
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            return false;

        PlayerPositions.UpdateMember(entityId, position, context.TimestampMilliseconds);
        return context.MarkParsed();
    }

    // 0x4736, a player left our sight: entity, then why (1 too far, 2 map change or teleport, 4 released after dying,
    // 10 left the dungeon) and a byte.
    public static bool ParseLeftSightPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        var reader = new PacketSpanReader(packet);
        if (!reader.TryReadVarInt(out _) || !reader.TryAdvance(2) || !reader.TryReadVarInt(out var entityId) || reader.Remaining != 2)
            return false;

        PlayerPositions.LeftSight(entityId);
        return context.MarkParsed();
    }

    public static bool ParseForceMemberListPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        var members = new PacketPlayerGroupMember[32];
        var count = PacketPlayerGroupParser.ParseMemberList(packet, members);
        if (count == 0)
        {
            return false;
        }

        var source = context.CreateObservationSource(0x1E96, packet.Length);
        for (var i = 0; i < count; i++)
        {
            AppendPlayerGroupMember(context, in source, in members[i]);
        }

        return context.MarkParsed();
    }

    public static bool ParseNicknamePacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet048DNicknameParser.TryParse(packet, out var parsed))
        {
            return false;
        }

        context.Sink.AppendNickname(context.CreateObservationSource(0x048D, packet.Length), parsed.PlayerId, parsed.Nickname, PacketFactionMapper.ToFaction(parsed.FactionCode), originServerId: parsed.OriginServerId, legionName: parsed.LegionName);
        return context.MarkParsed();
    }

    private static void AppendPlayerGroupMember(PacketParseContext context, in PacketObservationSource source, in PacketPlayerGroupMember parsed)
    {
        if (parsed.HasIdentityProfile)
        {
            context.Sink.AppendNickname(in source, parsed.EntityId, parsed.Nickname, originServerId: parsed.OriginServerId);
        }

        var membership = parsed.Kind == PacketPlayerGroupKind.Force
            ? PlayerGroupMembership.Force(parsed.GroupId, parsed.SubPartyIndex, parsed.MemberSlotIndex)
            : PlayerGroupMembership.Party(parsed.MemberSlotIndex);
        context.Sink.AppendPlayerGroupMember(in source, parsed.EntityId, in membership);
    }

    private static void AppendPlayerGroupProfile(PacketParseContext context, in PacketObservationSource source, in PacketPlayerGroupProfile profile)
    {
        var membership = profile.Kind == PacketPlayerGroupKind.Force
            ? PlayerGroupMembership.Force(profile.GroupId, profile.SubPartyIndex, profile.MemberSlotIndex)
            : PlayerGroupMembership.Party(profile.MemberSlotIndex);
        context.Sink.AppendPlayerGroupProfile(in source, profile.OriginServerId, profile.Nickname, in membership);
    }
}
