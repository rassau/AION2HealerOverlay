using System.Buffers;
using Cloris.Aion2Flow.Protocol.Packets;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.Capture.Streams;

internal sealed class PacketStateHandler
{
    internal const long UnidentifiedBossMinimumHp = 3_000_000;

    public static bool ParseSummonPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        var kind = Packet4036Descriptors.ClassifyKind(packet.Length);
        if (!Packet4036Descriptors.IsCreateKind(kind))
        {
            return Parse4036StatePacket(packet, ref context);
        }

        if (Packet4036CreateParser.TryParse(packet, out var parsed))
        {
            var source = context.CreateObservationSource(0x4036, packet.Length);
            if (parsed.NpcCode.HasValue)
            {
                context.Writer.ApplyNpcCatalog(in source, parsed.SummonId, parsed.NpcCode.Value);
            }

            context.Sink.AppendSummon(in source, parsed.OwnerId, parsed.SummonId);
            return context.MarkParsed();
        }

        if (Packet4036CreateParser.TryParseNpcSpawn(packet, out var spawn))
        {
            var source = context.CreateObservationSource(0x4036, packet.Length);
            if (spawn.NpcCode.HasValue)
            {
                context.Writer.ApplyNpcCatalog(in source, spawn.EntityId, spawn.NpcCode.Value);
            }

            if (spawn.CurrentHp is int currentHp && spawn.MaxHp is int maxHp)
            {
                context.Sink.AppendNpcHp(in source, spawn.EntityId, currentHp, maxHp);
            }
            return context.MarkParsed();
        }

        return false;
    }

    public static bool ParseAux2B38Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet2B38Parser.TryParse(packet, out var parsed))
        {
            return false;
        }

        context.Sink.RegisterObservation2B38(context.CreateObservationSource(0x2B38, packet.Length), parsed.SourceId, parsed.SourceIdCopy, parsed.Phase, parsed.InstanceSequenceId, parsed.ActionResourceEffectRef, parsed.SequenceValue, parsed.StateValue, parsed.DetailValue, parsed.TailLength);
        if (parsed.CasterPosition is { } position)
        {
            PlayerPositions.UpdateEntity(parsed.CasterId, position, context.TimestampMilliseconds);
            context.JournalPosition(0x2B38, packet.Length, parsed.CasterId, position);
        }

        return context.MarkParsed();
    }

    public static bool ParseAux2A38Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet2A38Parser.TryParse(packet, out var parsed))
        {
            return false;
        }

        context.Sink.RegisterObservation2A38(context.CreateObservationSource(0x2A38, packet.Length), parsed.EntityId, parsed.Mode, parsed.GroupCode, parsed.InstanceSequenceId, parsed.HeadCode, parsed.HeadValue, parsed.HeadMiddleRaw, parsed.TimelineValue, parsed.StableValue, parsed.EchoSourceId, parsed.StackValue, parsed.BuffResourceEffectRef, parsed.TailLength, parsed.TailLow64, parsed.TailHigh64);
        if (parsed.CasterPosition is { } position)
        {
            PlayerPositions.UpdateEntity(parsed.EchoSourceId, position, context.TimestampMilliseconds);
            context.JournalPosition(0x2A38, packet.Length, parsed.EchoSourceId, position);
        }

        return context.MarkParsed();
    }

    public static bool ParseAux2C38Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet2C38Parser.TryParse(packet, out var batch))
        {
            return false;
        }

        AuraResultRecord[]? rented = null;
        Span<AuraResultRecord> results = batch.ResultCount <= 64
            ? stackalloc AuraResultRecord[batch.ResultCount]
            : (rented = ArrayPool<AuraResultRecord>.Shared.Rent(batch.ResultCount)).AsSpan(0, batch.ResultCount);
        try
        {
            for (var resultIndex = 0; resultIndex < results.Length; resultIndex++)
            {
                if (!batch.TryRead(out var result))
                    return false;
                results[resultIndex] = new AuraResultRecord(result.StateCode, result.InstanceSequenceId, result.ResultCode, result.DetailEntityId, result.DetailValue0, result.DetailValue1);
            }

            var source = context.CreateObservationSource(0x2C38, packet.Length);
            context.Sink.RegisterObservation2C38(in source, batch.EntityId, results);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<AuraResultRecord>.Shared.Return(rented);
        }

        return context.MarkParsed();
    }

    public static bool ParseState1D37Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet1D37Parser.TryParse(packet, out _))
        {
            return false;
        }

        return context.MarkParsed();
    }

    public static bool ParseMapScopeSignalPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context, ushort opcode)
    {
        if (!PacketScopeSignalParser.TryParse(packet, opcode, out _))
        {
            return false;
        }

        return context.MarkParsed();
    }

    public static bool ParseState4136Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet4136Parser.TryParse(packet, out var parsed))
        {
            return false;
        }

        context.Sink.RememberNpcObservationSource(parsed.EntityId);
        var source = context.CreateObservationSource(0x4136, packet.Length);
        if (parsed.NpcCode is int npcCode)
        {
            context.Writer.ApplyNpcCatalog(in source, parsed.EntityId, npcCode, requireCatalogEntry: true);
        }

        if (parsed.OwnerId is int ownerId)
        {
            context.Sink.AppendSummon(in source, ownerId, parsed.EntityId);
        }

        if (parsed.CurrentHp is int currentHp && parsed.MaxHp is int maxHp)
        {
            context.Sink.AppendNpcHp(in source, parsed.EntityId, currentHp, maxHp);
        }

        return context.MarkParsed();
    }

    public static bool ParseWrapped8456Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet8456EnvelopeParser.TryParse(packet, out _))
        {
            return false;
        }

        return context.MarkParsed();
    }

    public static bool ParseState0140Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet0140Parser.TryParse(packet, out var parsed))
        {
            return false;
        }

        var source = context.CreateObservationSource(0x0140, packet.Length);
        if (!context.Writer.ApplyCurrentMapFromCompositeSceneState(in source, parsed.Value0, out var mapScopeStarted))
        {
            return false;
        }

        if (mapScopeStarted)
        {
            context.ResetPlayerGroupStatus();
        }

        var targetId = context.Sink.ResolveNpcObservationSource();
        if (targetId > 0)
        {
            context.Sink.AppendNpc0140Value(in source, targetId, parsed.Value0);
            if (parsed.Value0 <= int.MaxValue)
            {
                context.Writer.ApplyNpcCatalog(in source, targetId, (int)parsed.Value0, requireCatalogEntry: true);
            }
        }

        return context.MarkParsed();
    }

    public static bool ParseState2136Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet2136Parser.TryParse(packet, out var parsed))
        {
            return false;
        }

        var source = context.CreateObservationSource(0x2136, packet.Length);
        if (!context.Writer.StageMapCandidateFromCompositeSceneState(in source, parsed.Value0, out var mapScopeStarted))
        {
            return false;
        }

        if (mapScopeStarted)
        {
            context.ResetPlayerGroupStatus();
        }

        var targetId = context.Sink.ResolveNpcObservationSource();
        if (targetId > 0)
        {
            context.Sink.AppendNpc2136State(in source, targetId, parsed.Sequence, parsed.Value0);
            if (parsed.Value0 <= int.MaxValue)
            {
                context.Writer.ApplyNpcCatalog(in source, targetId, (int)parsed.Value0, requireCatalogEntry: true);
            }
        }

        return context.MarkParsed();
    }

    public static bool ParseMapArrival2336Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet2336ArrivalParser.TryParse(packet))
        {
            return false;
        }

        PlayerPositions.ClearSight();
        HiddenCubes.Clear();
        if (context.Sink.ConfirmDestinationMapArrival(context.CreateObservationSource(0x2336, packet.Length)))
        {
            context.ResetPlayerGroupStatus();
        }

        return context.MarkParsed();
    }

    public static bool ParseMap2E92Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet2E92Parser.TryParse(packet, out var parsed))
        {
            return false;
        }

        context.Sink.RegisterMapEvent(
            context.CreateObservationSource(0x2E92, packet.Length),
            parsed.InstanceId);

        return context.MarkParsed();
    }

    public static bool ParseMap2F92Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet2F92Parser.TryParse(packet, out var instanceId))
        {
            return false;
        }

        context.Sink.UnregisterMapEvent(
            context.CreateObservationSource(0x2F92, packet.Length),
            instanceId);

        return context.MarkParsed();
    }

    public static bool ParseState0240Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet0240Parser.TryParse(packet, out var parsed))
        {
            return false;
        }

        var source = context.CreateObservationSource(0x0240, packet.Length);
        if (!context.Writer.ApplyCurrentMapFromCompositeSceneState(in source, parsed.Value0, out var mapScopeStarted))
        {
            return false;
        }

        if (mapScopeStarted)
        {
            context.ResetPlayerGroupStatus();
        }

        var targetId = context.Sink.ResolveNpcObservationSource();
        if (targetId > 0)
        {
            context.Sink.AppendNpc0240Value(in source, targetId, parsed.Value0);
            if (parsed.Value0 <= int.MaxValue)
            {
                context.Writer.ApplyNpcCatalog(in source, targetId, (int)parsed.Value0, requireCatalogEntry: true);
            }
        }

        return context.MarkParsed();
    }

    public static bool ParseState4636Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet4636Parser.TryParse(packet, out var parsed))
        {
            return false;
        }

        context.Sink.AppendNpc4636State(context.CreateObservationSource(0x4636, packet.Length), parsed.SourceId, parsed.State0, parsed.State1);
        context.Sink.RememberNpcObservationSource(parsed.SourceId);

        return context.MarkParsed();
    }

    public static bool ParseState4536Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (Packet4536PcMetadataParser.TryParse(packet, out var pcMetadata))
        {
            PlayerPositions.LinkSpawn(pcMetadata.EntityId, packet, pcMetadata.Position, context.TimestampMilliseconds);
            if (pcMetadata.Position is { } standing)
                context.JournalPosition(0x4536, packet.Length, pcMetadata.EntityId, standing);
            context.Sink.AppendNickname(
                context.CreateObservationSource(0x4536, packet.Length),
                pcMetadata.EntityId,
                pcMetadata.Nickname,
                PacketFactionMapper.ToFaction(pcMetadata.FactionCode),
                characterClass: PacketCharacterClassMapper.ToCharacterClass(pcMetadata.ClassCode),
                originServerId: pcMetadata.OriginServerId > 0 ? pcMetadata.OriginServerId : null,
                legionName: pcMetadata.LegionName);
            return context.MarkParsed();
        }

        if (!Packet4536Parser.TryParse(packet, out var parsed))
        {
            return false;
        }

        context.Sink.RememberNpcObservationSource(parsed.SourceId);

        return context.MarkParsed();
    }

    public static bool ParseState4936Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet4936Parser.TryParse(packet, out _))
        {
            return false;
        }

        return context.MarkParsed();
    }

    public static bool ParseRemainHpPacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet008DRemainHpParser.TryParse(packet, out var parsed))
        {
            return false;
        }

        if (parsed.CurrentHp is not null || parsed.MaxHp is not null)
        {
            // Max HP is only sent when it changes (zone-in, buffs), so the last one rides along with every HP
            // update, and a max-only update keeps the last HP (or reads as full before any HP was seen).
            context.Sink.TryGetNpcRuntimeState(parsed.NpcId, out var last);
            var hp = parsed.CurrentHp ?? last.Hp ?? parsed.MaxHp.GetValueOrDefault();
            var source = context.CreateObservationSource(0x008D, packet.Length);
            var maxHp = parsed.MaxHp ?? last.MaxHp;
            if (parsed.MaxHp is long sentMaxHp)
                context.RememberStatMaxHp(parsed.NpcId, sentMaxHp);
            // After a zone change the sink has lost it (see RememberStatMaxHp). NPCs, which carry a code, get theirs
            // from spawns, so one reusing a player's old id never picks up that player's max.
            else if (maxHp is null && last.NpcCode is null && context.TryGetStatMaxHp(parsed.NpcId, out var rememberedMaxHp))
                maxHp = rememberedMaxHp;
            // Bosses are flagged from the NPC code their 4136 carries. One created before the capture started has none:
            // the HP pool gives it away (trash stays under 1M, bosses have 10M+). Its first HP update comes right after
            // the pull, so it stands in for max HP when the game sent none.
            // ponytail: one fixed bar; only for NPCs without a code, which the game data otherwise decides.
            if (last.NpcCode is null && last.Kind is null && Math.Max(hp, maxHp.GetValueOrDefault()) >= UnidentifiedBossMinimumHp)
            {
                context.Sink.AppendNpcKind(in source, parsed.NpcId, NpcKind.Boss);
                maxHp ??= hp;
            }

            if (maxHp is long knownMaxHp)
                context.Sink.AppendNpcHp(in source, parsed.NpcId, hp, knownMaxHp);
            else
                context.Sink.AppendNpcHp(in source, parsed.NpcId, hp);
        }

        return context.MarkParsed();
    }

    public static bool ParseStaggerGauge05E0Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet05E0StaggerGaugeParser.TryParse(packet, out var parsed))
            return false;

        context.Sink.AppendStaggerGauge(context.CreateObservationSource(0x05E0, packet.Length), parsed.NpcId, parsed.Current, parsed.Max ?? 0);
        return context.MarkParsed();
    }

    public static bool ParseBossTarget2092Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet2092BossTargetParser.TryParse(packet, out var targetId))
            return false;

        context.Sink.AppendBossTarget(context.CreateObservationSource(0x2092, packet.Length), targetId);
        return context.MarkParsed();
    }

    public static bool ParseBattleTogglePacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet218DBattleToggleParser.TryParse(packet, out var parsed))
        {
            return false;
        }

        if (parsed.IsActive is bool isActive)
        {
            context.Sink.SetNpcBattle(context.CreateObservationSource(0x218D, packet.Length), parsed.NpcId, isActive);
        }
        else
        {
            context.Sink.ToggleNpcBattle(context.CreateObservationSource(0x218D, packet.Length), parsed.NpcId);
        }

        return context.MarkParsed();
    }

    private static bool Parse4036StatePacket(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (Packet4036CreateParser.TryParseNpcSpawn(packet, out var spawn) && spawn.NpcCode.HasValue)
        {
            var source = context.CreateObservationSource(0x4036, packet.Length);
            context.Writer.ApplyNpcCatalog(in source, spawn.EntityId, spawn.NpcCode.Value, requireCatalogEntry: true);
            if (spawn.CurrentHp is int currentHp && spawn.MaxHp is int maxHp)
            {
                context.Sink.AppendNpcHp(in source, spawn.EntityId, currentHp, maxHp);
            }
        }

        if (Packet4036CreateParser.TryParseOwner(packet, out var entityId, out var ownerId))
        {
            context.Sink.AppendSummon(context.CreateObservationSource(0x4036, packet.Length), ownerId, entityId);
        }

        if (!Packet4036Parser.TryParse(packet, out _))
        {
            return false;
        }

        return context.MarkParsed();
    }
}
