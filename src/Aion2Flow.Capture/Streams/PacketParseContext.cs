using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.Capture.Streams;

internal ref struct PacketParseContext(
    IRuntimeObservationSink sink,
    SceneObservationWriter writer,
    PacketFlushState flushState,
    PacketPlayerGroupState playerGroupState,
    Func<ProtocolRoundTripObservation, double?>? protocolRoundTripObserver,
    in TcpConnection connection,
    in PacketProcessingTimestamp timestamp,
    long captureSequence)
{
    public readonly IRuntimeObservationSink Sink = sink;
    public readonly SceneObservationWriter Writer = writer;
    public readonly TcpConnection Connection = connection;
    public readonly long TimestampMilliseconds = timestamp.TimelineUnixMilliseconds;
    public readonly long ArrivalTimestamp = timestamp.ArrivalTimestamp;
    public readonly long CaptureSequence = captureSequence;
    public bool Parsed;
    public PacketStructurePath CurrentStructurePath { get; private set; }
    public readonly PacketStructureReference CurrentStructure => CurrentStructurePath.Leaf;

    public readonly bool TryRegisterPartyStatusMember(int entityId) => playerGroupState.TryRegisterPartyStatusMember(entityId);

    public readonly bool TryRegisterForceStatusMember(int entityId) => playerGroupState.TryRegisterForceStatusMember(entityId);

    public readonly void ResetPlayerGroupStatus() => playerGroupState.Reset();

    public readonly void RememberStatMaxHp(int entityId, long maxHp) => playerGroupState.RememberStatMaxHp(entityId, maxHp);

    public readonly bool TryGetStatMaxHp(int entityId, out long maxHp) => playerGroupState.TryGetStatMaxHp(entityId, out maxHp);

    public readonly void SetLocalPlayer(int entityId) => playerGroupState.LocalPlayerId = entityId;

    public readonly int LocalPlayerId => playerGroupState.LocalPlayerId;

    // A group member's position, as this packet gives it, into the journal at most once a second: buff ranges read it, and
    // it goes into the history with the rest.
    public readonly void JournalPosition(ushort opcode, int payloadLength, int entityId, System.Numerics.Vector3 position)
    {
        if (playerGroupState.IsGroupMember(entityId) && playerGroupState.TryTakePositionSlot(entityId, TimestampMilliseconds))
            Sink.AppendPosition(CreateObservationSource(opcode, payloadLength), entityId, position);
    }

    public readonly void JournalCharacterPosition(ushort opcode, int payloadLength, long characterId, System.Numerics.Vector3 position)
    {
        foreach (var entityId in PlayerPositions.EntitiesOf(characterId))
            JournalPosition(opcode, payloadLength, entityId, position);
    }

    public readonly long FlushId => flushState.CurrentFlushId;

    public readonly PacketObservationSource CreateObservationSource(ushort opcode, int payloadLength, long captureSequence = 0)
        => new(TimestampMilliseconds, FlushId, opcode, payloadLength, captureSequence > 0 ? captureSequence : CaptureSequence, CurrentStructurePath);

    // The round trip it worked out (ms), if any.
    public readonly double? ObserveProtocolRoundTrip(long clientSentMonotonicMilliseconds)
        => protocolRoundTripObserver?.Invoke(new ProtocolRoundTripObservation(
            Connection,
            clientSentMonotonicMilliseconds,
            ArrivalTimestamp));

    public PacketStructurePath EnterStructure(PacketStructureKind kind, int offset, int length, int bodyOffset, int bodyLength, int siblingIndex)
    {
        var previous = CurrentStructurePath;
        var previousLeaf = previous.Leaf;
        var next = new PacketStructureReference(kind, flushState.NextStructureScopeId(), previousLeaf.ScopeId, previousLeaf.ScopeId == 0 ? 1 : previousLeaf.Depth + 1, siblingIndex, offset, length, bodyOffset, bodyLength);
        CurrentStructurePath = previous.Push(next);
        return previous;
    }

    public void RestoreStructure(PacketStructurePath previous) => CurrentStructurePath = previous;

    public bool MarkParsed()
    {
        Parsed = true;
        return true;
    }
}
