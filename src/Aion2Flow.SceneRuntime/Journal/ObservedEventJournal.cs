using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.SceneRuntime.Journal;

public sealed class ObservedEventJournal(int capacity = 0)
{
    internal const int SegmentCapacity = 512;

    private readonly Lock _gate = new();
    private readonly List<ObservedEventStorageSegment> _segments = capacity > 0 ? new(ResolveSegmentCapacity(capacity)) : [];
    private readonly List<Guid> _sceneSessionIds = [];
    private readonly Dictionary<Guid, int> _sceneSessionIndices = [];
    private int _count;
    private long _firstObservationOrdinal;
    // Where _segments[0] starts: before _firstObservationOrdinal in a copy whose first, shared segment holds earlier entries.
    private long _segmentBaseOrdinal;
    private long _nextObservationOrdinal;
    private long _lastCompletedFlushId = -1;

    public int Count
    {
        get { lock (_gate) return _count; }
    }

    public int SegmentCount
    {
        get { lock (_gate) return _segments.Count; }
    }

    public int SceneSessionCount
    {
        get { lock (_gate) return _sceneSessionIds.Count; }
    }

    public long FirstObservationOrdinal
    {
        get { lock (_gate) return _firstObservationOrdinal; }
    }

    public long NextObservationOrdinal
    {
        get { lock (_gate) return _nextObservationOrdinal; }
    }

    public long LastCompletedFlushId
    {
        get { lock (_gate) return _lastCompletedFlushId; }
    }

    public void Append(in ObservedEventHeader header, in CombatWireObservation observation)
    {
        lock (_gate)
        {
            var segment = PrepareAppend(in header, out var sessionIndex);
            var payloadIndex = segment.AddCombat(in observation);
            CommitAppend(segment, in header, sessionIndex, payloadIndex, ObservedEventDomain.Combat);
        }
    }

    public void Append(in ObservedEventHeader header, in ActionObservation observation)
    {
        lock (_gate)
        {
            var segment = PrepareAppend(in header, out var sessionIndex);
            var payloadIndex = segment.AddAction(in observation);
            CommitAppend(segment, in header, sessionIndex, payloadIndex, ObservedEventDomain.Action);
        }
    }

    public void Append(in ObservedEventHeader header, in StateObservation observation)
    {
        lock (_gate)
        {
            var segment = PrepareAppend(in header, out var sessionIndex);
            var payloadIndex = segment.AddState(in observation);
            CommitAppend(segment, in header, sessionIndex, payloadIndex, ObservedEventDomain.State);
        }
    }

    public void Append(in ObservedEventHeader header, in EntityVitalObservation observation)
    {
        lock (_gate)
        {
            var segment = PrepareAppend(in header, out var sessionIndex);
            var payloadIndex = segment.AddEntityVital(in observation);
            CommitAppend(segment, in header, sessionIndex, payloadIndex, ObservedEventDomain.EntityVital);
        }
    }

    public void Append(in ObservedEventHeader header, in AuraObservation observation)
    {
        lock (_gate)
        {
            var segment = PrepareAppend(in header, out var sessionIndex);
            var payloadIndex = segment.AddAura(in observation);
            CommitAppend(segment, in header, sessionIndex, payloadIndex, ObservedEventDomain.Aura);
        }
    }

    public void Append(in ObservedEventHeader header, in SceneObservation observation)
    {
        lock (_gate)
        {
            var segment = PrepareAppend(in header, out var sessionIndex);
            var payloadIndex = segment.AddScene(in observation);
            CommitAppend(segment, in header, sessionIndex, payloadIndex, ObservedEventDomain.Scene);
        }
    }

    public void AppendDiagnostic(in ObservedEventHeader header)
    {
        lock (_gate)
        {
            var segment = PrepareAppend(in header, out var sessionIndex);
            CommitAppend(segment, in header, sessionIndex, -1, ObservedEventDomain.Diagnostic);
        }
    }

    // An imported archive keeps its original observation ordinals, which its combat events and timeline refer to.
    internal static ObservedEventJournal StartingAt(long firstObservationOrdinal)
        => new() { _firstObservationOrdinal = firstObservationOrdinal, _nextObservationOrdinal = firstObservationOrdinal };

    // The entries [start, end) in a journal of their own, under the same ordinals: an archived scene takes them along,
    // so the live journal can let go of them (TrimBefore) and removing the archive frees them. Only the last segment is
    // ever appended to, so a full one never changes: the copy shares those and copies only a last, partly copied one.
    // Its first segment may hold up to SegmentCapacity - 1 entries before start, which it keeps but never reads.
    public ObservedEventJournal CopyRange(long startObservationOrdinal, long endObservationOrdinalExclusive)
    {
        lock (_gate)
        {
            var first = Math.Max(startObservationOrdinal, _firstObservationOrdinal);
            var end = Math.Min(endObservationOrdinalExclusive, _nextObservationOrdinal);
            var copy = StartingAt(first);
            copy._lastCompletedFlushId = _lastCompletedFlushId;
            if (first >= end)
                return copy;

            // The shared headers keep this journal's session indices.
            copy._sceneSessionIds.AddRange(_sceneSessionIds);
            foreach (var (sceneSessionId, index) in _sceneSessionIndices)
                copy._sceneSessionIndices.Add(sceneSessionId, index);

            var firstSegment = (int)((first - _segmentBaseOrdinal) / SegmentCapacity);
            var lastSegment = (int)((end - 1 - _segmentBaseOrdinal) / SegmentCapacity);
            for (var i = firstSegment; i < lastSegment; i++)
                copy._segments.Add(_segments[i]);
            var lastCount = (int)(end - _segmentBaseOrdinal - (long)lastSegment * SegmentCapacity);
            copy._segments.Add(lastCount == SegmentCapacity ? _segments[lastSegment] : _segments[lastSegment].CloneFirst(lastCount));
            copy._segmentBaseOrdinal = _segmentBaseOrdinal + (long)firstSegment * SegmentCapacity;
            copy._count = (int)(end - first);
            copy._nextObservationOrdinal = end;
            return copy;
        }
    }

    // Lets go of the whole segments before the ordinal (the last one stays); a read from before it starts at the
    // first entry kept.
    public void TrimBefore(long observationOrdinal)
    {
        lock (_gate)
        {
            var drop = (int)Math.Min((observationOrdinal - _segmentBaseOrdinal) / SegmentCapacity, _segments.Count - 1L);
            if (drop <= 0)
                return;

            _segments.RemoveRange(0, drop);
            _segmentBaseOrdinal += (long)drop * SegmentCapacity;
            var first = Math.Max(_firstObservationOrdinal, _segmentBaseOrdinal);
            _count -= (int)(first - _firstObservationOrdinal);
            _firstObservationOrdinal = first;
        }
    }

    public void EnsureCapacity(int capacity)
    {
        if (capacity <= 0)
            return;

        lock (_gate)
            _segments.EnsureCapacity(ResolveSegmentCapacity(capacity));
    }

    public void CompleteFlush(long flushId)
    {
        lock (_gate)
        {
            if (flushId <= _lastCompletedFlushId)
                throw new ArgumentException($"FlushId must be > {_lastCompletedFlushId}, got {flushId}.");
            _lastCompletedFlushId = flushId;
        }
    }

#pragma warning disable CA1822
    public JournalCursor CreateCursor(long startOrdinal) => new(startOrdinal);
#pragma warning restore CA1822

    public void ReadEntry(long observationOrdinal, JournalEntryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        lock (_gate)
        {
            if (!TryResolvePosition(observationOrdinal, out var segmentIndex, out var entryIndex))
                throw new ArgumentOutOfRangeException(nameof(observationOrdinal));

            reader(_segments[segmentIndex].GetEntry(this, entryIndex));
        }
    }

    public bool TryReadEntry(long observationOrdinal, JournalEntryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        lock (_gate)
        {
            if (!TryResolvePosition(observationOrdinal, out var segmentIndex, out var entryIndex))
                return false;

            reader(_segments[segmentIndex].GetEntry(this, entryIndex));
            return true;
        }
    }

    public JournalReadResult ReadEntries(JournalCursor cursor, int maxCount, JournalEntriesReader reader)
        => ReadEntries(cursor, long.MaxValue, maxCount, reader);

    public JournalReadResult ReadEntries(JournalCursor cursor, long endObservationOrdinalExclusive, int maxCount, JournalEntriesReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        lock (_gate)
        {
            if (_count == 0 || maxCount <= 0)
                return new JournalReadResult(0, cursor);

            var startOrdinal = Math.Max(cursor.NextObservationOrdinal, _firstObservationOrdinal);
            var endOrdinal = Math.Min(endObservationOrdinalExclusive, _nextObservationOrdinal);
            if (startOrdinal >= endOrdinal || !TryResolvePosition(startOrdinal, out var segmentIndex, out var entryIndex))
                return new JournalReadResult(0, cursor);

            var segment = _segments[segmentIndex];
            var count = (int)Math.Min(Math.Min((long)maxCount, endOrdinal - startOrdinal), segment.Count - entryIndex);
            if (count <= 0)
                return new JournalReadResult(0, cursor);

            reader(new JournalEntryBatch(this, segment, entryIndex, count));
            return new JournalReadResult(count, new JournalCursor(startOrdinal + count));
        }
    }

    internal Guid GetSceneSessionId(int index) => _sceneSessionIds[index];

    private ObservedEventStorageSegment PrepareAppend(in ObservedEventHeader header, out int sessionIndex)
    {
        if (header.Stamp.ObservationOrdinal != _nextObservationOrdinal)
            throw new ArgumentException($"ObservationOrdinal must be {_nextObservationOrdinal}, got {header.Stamp.ObservationOrdinal}.", nameof(header));

        if (_segments.Count == 0 || _segments[^1].Count == SegmentCapacity)
        {
            if (_segments.Count == 0)
                _segmentBaseOrdinal = _nextObservationOrdinal;
            _segments.Add(new ObservedEventStorageSegment(_nextObservationOrdinal));
        }

        sessionIndex = InternSceneSession(header.SceneSessionId);
        return _segments[^1];
    }

    private void CommitAppend(
        ObservedEventStorageSegment segment,
        in ObservedEventHeader header,
        int sessionIndex,
        int payloadIndex,
        ObservedEventDomain domain)
    {
        var rawIndex = segment.Count;
        var raw = header.Raw;
        var storedHeader = new StoredObservedEventHeader(
            header.Stamp.OffsetTicks,
            header.Stamp.FlushId,
            sessionIndex,
            header.SourceEntityId,
            header.TargetEntityId,
            rawIndex,
            payloadIndex,
            domain,
            raw.Opcode);
        segment.AddHeader(in storedHeader, in raw);
        if (_count == 0)
            _firstObservationOrdinal = header.Stamp.ObservationOrdinal;
        _count++;
        _nextObservationOrdinal++;
        // A full segment is never written again. Last, so a failed copy leaves the counters right.
        if (segment.Count == SegmentCapacity)
            segment.TrimPayloads();
    }

    private int InternSceneSession(Guid sceneSessionId)
    {
        if (_sceneSessionIndices.TryGetValue(sceneSessionId, out var index))
            return index;

        index = _sceneSessionIds.Count;
        _sceneSessionIds.Add(sceneSessionId);
        _sceneSessionIndices.Add(sceneSessionId, index);
        return index;
    }

    private bool TryResolvePosition(long observationOrdinal, out int segmentIndex, out int entryIndex)
    {
        if ((ulong)(observationOrdinal - _firstObservationOrdinal) >= (uint)_count)
        {
            segmentIndex = 0;
            entryIndex = 0;
            return false;
        }

        var offset = observationOrdinal - _segmentBaseOrdinal;
        segmentIndex = (int)(offset / SegmentCapacity);
        entryIndex = (int)(offset % SegmentCapacity);
        return true;
    }

    private static int ResolveSegmentCapacity(int entryCapacity)
        => Math.Max(1, (entryCapacity + SegmentCapacity - 1) / SegmentCapacity);
}

public readonly ref struct ObservedEventEntry
{
    private readonly ObservedEventJournal _journal;
    private readonly ObservedEventStorageSegment _segment;
    private readonly StoredObservedEventHeader _header;
    private readonly long _observationOrdinal;

    internal ObservedEventEntry(
        ObservedEventJournal journal,
        ObservedEventStorageSegment segment,
        in StoredObservedEventHeader header,
        long observationOrdinal)
    {
        _journal = journal;
        _segment = segment;
        _header = header;
        _observationOrdinal = observationOrdinal;
    }

    public Guid SceneSessionId => _journal.GetSceneSessionId(_header.SceneSessionIndex);
    public TimelineStamp Stamp => new(_header.OffsetTicks, _observationOrdinal, _header.FlushId);
    public ObservedEventDomain Domain => _header.Domain;
    public int SourceEntityId => _header.SourceEntityId;
    public int TargetEntityId => _header.TargetEntityId;
    public long ObservedAtMilliseconds => _header.OffsetTicks / TimeSpan.TicksPerMillisecond;
    // Made anew from the compact form on every read; Opcode alone costs nothing.
    public RawPacketReference Raw => _segment.GetRaw(_header.RawIndex, _header.Opcode);
    public ushort Opcode => _header.Opcode;

    public ref readonly CombatWireObservation Combat
    {
        get
        {
            EnsureDomain(ObservedEventDomain.Combat);
            return ref _segment.GetCombat(_header.PayloadIndex);
        }
    }

    public ref readonly ActionObservation Action
    {
        get
        {
            EnsureDomain(ObservedEventDomain.Action);
            return ref _segment.GetAction(_header.PayloadIndex);
        }
    }

    public ref readonly StateObservation State
    {
        get
        {
            EnsureDomain(ObservedEventDomain.State);
            return ref _segment.GetState(_header.PayloadIndex);
        }
    }

    public ref readonly EntityVitalObservation EntityVital
    {
        get
        {
            EnsureDomain(ObservedEventDomain.EntityVital);
            return ref _segment.GetEntityVital(_header.PayloadIndex);
        }
    }

    public ref readonly AuraObservation Aura
    {
        get
        {
            EnsureDomain(ObservedEventDomain.Aura);
            return ref _segment.GetAura(_header.PayloadIndex);
        }
    }

    public ref readonly SceneObservation Scene
    {
        get
        {
            EnsureDomain(ObservedEventDomain.Scene);
            return ref _segment.GetScene(_header.PayloadIndex);
        }
    }

    private void EnsureDomain(ObservedEventDomain expected)
    {
        if (_header.Domain != expected)
            throw new InvalidOperationException($"Journal entry domain is {_header.Domain}, not {expected}.");
    }
}

public readonly ref struct JournalEntryBatch
{
    private readonly ObservedEventJournal _journal;
    private readonly ObservedEventStorageSegment _segment;
    private readonly int _start;

    internal JournalEntryBatch(ObservedEventJournal journal, ObservedEventStorageSegment segment, int start, int count)
    {
        _journal = journal;
        _segment = segment;
        _start = start;
        Count = count;
    }

    public int Count { get; }

    public ObservedEventEntry this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            if (index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _segment.GetEntry(_journal, _start + index);
        }
    }

    public Enumerator GetEnumerator() => new(this);

    public ref struct Enumerator
    {
        private readonly JournalEntryBatch _entries;
        private int _index = -1;

        internal Enumerator(JournalEntryBatch entries)
        {
            _entries = entries;
        }

        public readonly ObservedEventEntry Current => _entries[_index];

        public bool MoveNext() => ++_index < _entries.Count;
    }
}

internal sealed class ObservedEventStorageSegment(long firstObservationOrdinal)
{
    private readonly StoredObservedEventHeader[] _headers = new StoredObservedEventHeader[ObservedEventJournal.SegmentCapacity];
    private readonly CompactRawPacketReference[] _raws = new CompactRawPacketReference[ObservedEventJournal.SegmentCapacity];
    // The levels above a path's leaf, once per run of entries that share them (a packet's entries mostly do).
    private PayloadBuffer<PacketStructureParents> _rawParents;
    // The few references the compact form can't give back exactly (paths deeper than three levels), whole.
    private PayloadBuffer<RawPacketReference> _rawOverflow;
    private PayloadBuffer<CombatWireObservation> _combat;
    private PayloadBuffer<ActionObservation> _actions;
    private PayloadBuffer<StateObservation> _states;
    private PayloadBuffer<EntityVitalObservation> _entityVitals;
    private PayloadBuffer<AuraObservation> _auras;
    private PayloadBuffer<SceneObservation> _scenes;

    public int Count { get; private set; }

    // The first entries in a segment of their own, for a journal copy that ends inside this one.
    public ObservedEventStorageSegment CloneFirst(int count)
    {
        var clone = new ObservedEventStorageSegment(firstObservationOrdinal) { Count = count };
        Array.Copy(_headers, clone._headers, count);
        Array.Copy(_raws, clone._raws, count);
        clone._rawParents = _rawParents.Clone();
        clone._rawOverflow = _rawOverflow.Clone();
        clone._combat = _combat.Clone();
        clone._actions = _actions.Clone();
        clone._states = _states.Clone();
        clone._entityVitals = _entityVitals.Clone();
        clone._auras = _auras.Clone();
        clone._scenes = _scenes.Clone();
        return clone;
    }

    public void AddHeader(in StoredObservedEventHeader header, in RawPacketReference raw)
    {
        var path = raw.StructurePath;
        var parents = new PacketStructureParents(path.Depth >= 2 ? path.Root : default, path.Depth >= 3 ? path.Level1 : default);
        // Compact only when GetRaw gives back this very reference: anything the slot doesn't keep goes to the overflow.
        if (path.Depth >= 0 &&
            new RawPacketReference(header.Opcode, raw.PayloadLength, raw.CaptureSequence, CompactRawPacketReference.PathOf(path.Depth, path.Leaf, in parents)) == raw)
        {
            var parentIndex = _rawParents.Count - 1;
            if (parents == default)
                parentIndex = -1;
            else if (parentIndex < 0 || _rawParents[parentIndex] != parents)
                parentIndex = _rawParents.Add(in parents, ObservedEventJournal.SegmentCapacity); // at most one per entry: no regrowth
            _raws[Count] = new CompactRawPacketReference(raw.PayloadLength, raw.CaptureSequence, path.Leaf, parentIndex, path.Depth);
        }
        else
        {
            _raws[Count] = new CompactRawPacketReference(0, 0, default, -1, ~_rawOverflow.Add(in raw));
        }

        _headers[Count++] = header;
    }

    // A full segment is only read from then on: its lists keep just what they hold, not the room growing left.
    public void TrimPayloads()
    {
        _rawParents.Trim();
        _rawOverflow.Trim();
        _combat.Trim();
        _actions.Trim();
        _states.Trim();
        _entityVitals.Trim();
        _auras.Trim();
        _scenes.Trim();
    }

    public int AddCombat(in CombatWireObservation value) => _combat.Add(in value);
    public int AddAction(in ActionObservation value) => _actions.Add(in value);
    public int AddState(in StateObservation value) => _states.Add(in value);
    public int AddEntityVital(in EntityVitalObservation value) => _entityVitals.Add(in value);
    public int AddAura(in AuraObservation value) => _auras.Add(in value);
    public int AddScene(in SceneObservation value) => _scenes.Add(in value);
    public ref readonly CombatWireObservation GetCombat(int index) => ref _combat[index];
    public ref readonly ActionObservation GetAction(int index) => ref _actions[index];
    public ref readonly StateObservation GetState(int index) => ref _states[index];
    public ref readonly EntityVitalObservation GetEntityVital(int index) => ref _entityVitals[index];
    public ref readonly AuraObservation GetAura(int index) => ref _auras[index];
    public ref readonly SceneObservation GetScene(int index) => ref _scenes[index];
    public RawPacketReference GetRaw(int index, ushort opcode)
    {
        ref readonly var raw = ref _raws[index];
        if (raw.Depth < 0)
            return _rawOverflow[~raw.Depth];

        var parents = raw.Parents < 0 ? default : _rawParents[raw.Parents];
        return new(opcode, raw.PayloadLength, raw.CaptureSequence, CompactRawPacketReference.PathOf(raw.Depth, in raw.Leaf, in parents));
    }

    public ObservedEventEntry GetEntry(ObservedEventJournal journal, int index)
        => new(journal, this, in _headers[index], firstObservationOrdinal + index);
}

// A raw reference in 56 B instead of 200 (and 512 of them stay off the large object heap): the opcode lives in the
// header, the levels above the leaf in the segment's parents, and up to three levels deep a path's Leaf repeats its
// deepest level and Level3 is empty. Depth below 0 is ~index into the segment's overflow instead.
internal readonly struct CompactRawPacketReference(int payloadLength, long captureSequence, in PacketStructureReference leaf, int parents, int depth)
{
    public readonly long CaptureSequence = captureSequence;
    public readonly int PayloadLength = payloadLength;
    public readonly PacketStructureReference Leaf = leaf;
    public readonly int Parents = parents;
    public readonly int Depth = depth;

    public static PacketStructurePath PathOf(int depth, in PacketStructureReference leaf, in PacketStructureParents parents) => depth switch
    {
        1 => new(leaf, default, default, default, leaf, 1),
        2 => new(parents.Root, leaf, default, default, leaf, 2),
        3 => new(parents.Root, parents.Level1, leaf, default, leaf, 3),
        _ => new(default, default, default, default, leaf, depth)
    };
}

internal readonly record struct PacketStructureParents(PacketStructureReference Root, PacketStructureReference Level1);

internal readonly record struct StoredObservedEventHeader(
    long OffsetTicks,
    long FlushId,
    int SceneSessionIndex,
    int SourceEntityId,
    int TargetEntityId,
    int RawIndex,
    int PayloadIndex,
    ObservedEventDomain Domain,
    ushort Opcode);

internal struct PayloadBuffer<T>
{
    private T[]? _items;
    private int _count;

    public readonly int Count => _count;

    public int Add(in T value, int firstCapacity = 8)
    {
        var items = _items;
        if (items is null)
        {
            items = new T[firstCapacity];
            _items = items;
        }
        else if (_count == items.Length)
        {
            Array.Resize(ref items, items.Length << 1);
            _items = items;
        }

        var index = _count++;
        items[index] = value;
        return index;
    }

    public void Trim()
    {
        if (_items is { } items && items.Length > _count)
            Array.Resize(ref _items, _count);
    }

    public readonly ref readonly T this[int index] => ref _items![index];

    public readonly PayloadBuffer<T> Clone() => new() { _items = (T[]?)_items?.Clone(), _count = _count };
}

public delegate void JournalEntryReader(ObservedEventEntry entry);

public delegate void JournalEntriesReader(JournalEntryBatch entries);

public readonly record struct JournalCursor(long NextObservationOrdinal);

public readonly record struct JournalReadResult(int Count, JournalCursor Cursor);
