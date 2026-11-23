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

    // ponytail: a renewal carries its level right after the caster too (5,115 of 5,117 equal the open's), but
    // Packet2B38Parser misreads that part and the open before it has the level; read it if renewals without an open matter.
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

    // x11100100 is the game's count of the kills it credits the player with (they or their party hit the NPC before it
    // died): 311100100 in dungeons and fields (recordings of Jul-Oct 2026), 411100100 in the Abyss (Nov 2026), each beside
    // counters per kind of NPC (x11100200-600) that add up to it.
    public static bool ParseWrapped8456Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet8456EnvelopeParser.TryParse(packet, out var parsed))
        {
            return false;
        }

        if (parsed.Prefix0 == 1 && parsed.CounterId % 100_000_000 == 11_100_100)
            context.Sink.AppendKillCredit(context.CreateObservationSource(0x8456, packet.Length), parsed.InnerValue);
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

        // Where it sends the player (x, y, z), for the arrival's backup (ParseMapArrivalByPosition).
        context.ArmArrivalBackup(parsed.Value3, parsed.Value4, parsed.Value5);

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

        ConfirmMapArrival(packet.Length, ref context);
        return context.MarkParsed();
    }

    // The arrival after a game update renamed it once more (0x2336 until 11 Nov 2026, 0x5196 until 18 Nov, 0xf84e
    // since): a frame no case of PacketOpcodeDispatcher reads that holds the x, y, z the 0x2136 just announced, byte for
    // byte. Replaying 24 recordings with the three arrival cases taken out, this found all 47 of their arrivals on the
    // very frame the cases read, under all three names, and nothing else. Frames read here hold the spot too (0x2a38 on
    // respawns 0.6 s before the arrival, 0x1c92 at a shared landing spot up to 3.1 s before, 0x3336 right after), so
    // they don't count.
    // ponytail: an unread packet repeating the spot before the arrival would switch early, by up to the time between
    // announcement and arrival (0.55-5.5 s, once 33 s), and old-map hits still coming in that gap would count for the new
    // map. Not covered: an arrival more than 60 s late, a renamed arrival without the coordinates, a renamed 0x2136.
    public static bool ParseMapArrivalByPosition(ReadOnlySpan<byte> packet, int opcodeOffset, ref PacketParseContext context)
    {
        if (!context.MatchArrivalBackup(packet[(opcodeOffset + 2)..]) || !IsStreamFrame(context.CurrentStructurePath))
        {
            return false;
        }

        CaptureLog.Write(
            CaptureLogLevel.Warning,
            $"Map arrival found by the announced position in packet 0x{packet[opcodeOffset]:x2}{packet[opcodeOffset + 1]:x2}: the game may have renamed the arrival packet (known: 2336, 5196, f84e).");
        ConfirmMapArrival(packet.Length, ref context);
        return context.MarkParsed();
    }

    // A frame as the stream brings it: alone or in a batch, also in an LZ4 container (of the 47 arrivals in the
    // recordings, 5 came in one). PacketFrameParser also tries slices out (container candidates, bodies of frames it
    // couldn't read, recovered and embedded frames), and such a slice of a packet read here, a 0x2a38 say, could hold
    // the spot before the arrival comes.
    private static bool IsStreamFrame(in PacketStructurePath path) =>
        path.Leaf.Kind == PacketStructureKind.FrameBatchEntry &&
        (path.Depth == 2 || (path.Depth == 3 && path.Level1.Kind == PacketStructureKind.CompressedPayload));

    private static void ConfirmMapArrival(int packetLength, ref PacketParseContext context)
    {
        context.DisarmArrivalBackup();
        PlayerPositions.ClearSight();
        HiddenCubes.Clear();
        if (context.Sink.ConfirmDestinationMapArrival(context.CreateObservationSource(0x2336, packetLength)))
        {
            context.ResetPlayerGroupStatus();
        }
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
            var source = context.CreateObservationSource(0x4536, packet.Length);
            context.Sink.AppendNickname(
                in source,
                pcMetadata.EntityId,
                pcMetadata.Nickname,
                PacketFactionMapper.ToFaction(pcMetadata.FactionCode),
                characterClass: PacketCharacterClassMapper.ToCharacterClass(pcMetadata.ClassCode),
                originServerId: pcMetadata.OriginServerId > 0 ? pcMetadata.OriginServerId : null,
                legionName: pcMetadata.LegionName);
            // Their passives' levels, for the twin's skills (SeenSkillLevels).
            Span<(uint Effect, int Level)> passives = stackalloc (uint, int)[16];
            var passiveCount = Packet4536PcMetadataParser.ReadPassiveLevels(packet, pcMetadata.EntityId, passives);
            for (var i = 0; i < passiveCount; i++)
                context.Sink.AppendSkillLevel(in source, pcMetadata.EntityId, passives[i].Effect, passives[i].Level);
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
            // ponytail: one fixed bar; only for NPCs without a code or summons without an owner, which the game data otherwise
            // decides.
            // A boss's nameless helpers (codes the game data lacks, or no spawn at all) carry its HP pool: Vakron's 27,918,000,
            // Ketu's 12,300,000, the Fire Temple second boss's a little under his 22,809,600. One whose max HP is that of a boss
            // the game data names in this map (or a little under it while that boss is fought) is such a helper, not a boss.
            // One first seen at 0 HP is no boss either: of the NPCs decided at 0 HP in 68 recordings and 38 saved histories
            // (20 Nov 2026) none was ever hit, and all carried a named boss's pool, save the sword boss event's object (map
            // 510026, code 2920066, 45,000,000 HP), whose only HP update is 0. It hits players, so it stood as a nameless 0 %
            // boss for about 30 s and stayed in the map's history.
            // ponytail: a code-less boss whose first HP update is its death gets no bar; none so far.
            // The game data files some bosses as summons: Eternal Ludra (2920772, 194,512,500 HP, Nov 2026), who has no owner,
            // among hundreds of objects with his name and code, many with his pool. A summon of no owner is judged the same way at
            // its first HP update (his objects have their pool from their spawn and get none); Monster marks it judged, and a
            // boss keeps its kind when its spawn comes again (SceneObservationWriter). While a boss the fallback made is fought,
            // such a summon is its add, as that boss's pool tells no helpers (IsFallbackBossFought): the Fire Temple's Gas Rocks
            // carry their boss's pool, whose spawn the overlay may have missed. In 68 recordings and 45 histories (22 Nov 2026)
            // only he passes: the helpers with a boss's pool (Gas Rock, Watchdog Kwapo) and the objects first seen at 0 HP don't.
            // ponytail: a summon of 3M HP or more that matches no named boss's pool is a boss unless a fallback boss is
            // fought: an add judged before its code-less boss's first HP update too, and Eternal Ludra pulled while such a
            // boss is fought would be taken for its add. Neither shows in 68 recordings and 45 histories, nor with the
            // overlay started late in them.
            var summon = last.Kind == NpcKind.Summon && !context.Sink.HasSummonOwner(parsed.NpcId);
            if ((last.NpcCode is null && last.Kind is null || summon) && Math.Max(hp, maxHp.GetValueOrDefault()) >= UnidentifiedBossMinimumHp)
            {
                if (hp == 0 || context.Sink.HasCatalogBossWithMaxHp(maxHp ?? hp) ||
                    summon && context.Sink.IsFallbackBossFought(source.CaptureTimestampMilliseconds))
                {
                    context.Sink.AppendNpcKind(in source, parsed.NpcId, NpcKind.Monster);
                }
                else
                {
                    context.Sink.AppendNpcKind(in source, parsed.NpcId, NpcKind.Boss);
                    maxHp ??= hp;
                }
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
