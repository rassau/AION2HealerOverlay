using System.Runtime.InteropServices;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.SceneRuntime.Playback;

public sealed class ScenePlaybackSession
{
    private readonly IScenePlaybackSource _source;
    private FrameProjector? _projector;
    private bool _restoreDroppedProjector;
    private long _nextLoadedObservationOrdinal;
    private long _positionMilliseconds;

    public ScenePlaybackSession(IScenePlaybackSource source)
    {
        _source = source;
        var segment = _source.CreateTimelineSegment().CreateBoundedSnapshot();
        _nextLoadedObservationOrdinal = segment.StartObservationOrdinal;
    }

    public IScenePlaybackSource Source => _source;

    public long NextLoadedObservationOrdinal => _nextLoadedObservationOrdinal;

    public long PositionMilliseconds => _positionMilliseconds;

    public ScenePlaybackTrackIndex CreateTrackIndex(CancellationToken cancellationToken = default)
        => ScenePlaybackTrackIndex.Build(_source.CreateTimelineSegment().CreateBoundedSnapshot(), cancellationToken);

    public void ResetLoadedCursor()
    {
        var segment = _source.CreateTimelineSegment().CreateBoundedSnapshot();
        _nextLoadedObservationOrdinal = segment.StartObservationOrdinal;
        _projector = null;
        _restoreDroppedProjector = false;
        _positionMilliseconds = 0;
    }

    public JournalReadResult ReadNextTimelineBatch(int maxCount, JournalEntriesReader reader)
    {
        var segment = _source.CreateTimelineSegment().CreateBoundedSnapshot();
        var cursor = new JournalCursor(Math.Max(segment.StartObservationOrdinal, _nextLoadedObservationOrdinal));
        var result = segment.ReadEntries(cursor, maxCount, reader);
        _nextLoadedObservationOrdinal = result.Cursor.NextObservationOrdinal;
        return result;
    }

    public ScenePlaybackFrame Seek(long positionMilliseconds) => Seek(positionMilliseconds, CancellationToken.None);

    public ScenePlaybackFrame Seek(long positionMilliseconds, CancellationToken cancellationToken)
    {
        if (TrySeek(positionMilliseconds, cancellationToken, out var frame))
            return frame;

        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Playback seek did not produce a frame.");
    }

    internal bool TrySeek(long positionMilliseconds, CancellationToken cancellationToken, out ScenePlaybackFrame frame)
    {
        frame = null!;
        if (!TryCreateProjector(cancellationToken, out var projector) ||
            !projector.TryAdvanceTo(positionMilliseconds, projector.Segment, projector.TimeRange, cancellationToken, out frame) ||
            cancellationToken.IsCancellationRequested)
        {
            frame = null!;
            return false;
        }

        _projector = projector;
        _restoreDroppedProjector = false;
        frame = ApplyFrame(frame);
        return true;
    }

    // A user seek continues the current projection only where that gives what a replay from the start (TrySeek) gives: on
    // an archive (its segment and time range never change), forward, from a projector whose stores are a replay to the shown
    // position (CanAdvanceLikeReplay). Otherwise it replays as before.
    internal bool TrySeekForward(long positionMilliseconds, CancellationToken cancellationToken, out ScenePlaybackFrame frame)
    {
        if (_source.SourceKind != ScenePlaybackSourceKind.Archived ||
            _projector?.CanAdvanceLikeReplay != true ||
            positionMilliseconds < _positionMilliseconds)
            return TrySeek(positionMilliseconds, cancellationToken, out frame);

        var completed = false;
        try
        {
            completed = TryAdvanceTo(positionMilliseconds, cancellationToken, out frame);
            return completed;
        }
        finally
        {
            // A replay that is cancelled or throws keeps the shown frame's projector, a continuation leaves it part-advanced:
            // drop it, and let its next use replay to the shown position (GetProjectorToContinue, GetOrRestoreProjector).
            if (!completed)
            {
                _projector = null;
                _restoreDroppedProjector = true;
            }
        }
    }

    public ScenePlaybackFrame AdvanceTo(long positionMilliseconds) => AdvanceTo(positionMilliseconds, CancellationToken.None);

    public ScenePlaybackFrame AdvanceTo(long positionMilliseconds, CancellationToken cancellationToken)
    {
        if (TryAdvanceTo(positionMilliseconds, cancellationToken, out var frame))
            return frame;

        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Playback advance did not produce a frame.");
    }

    internal bool TryAdvanceTo(long positionMilliseconds, CancellationToken cancellationToken, out ScenePlaybackFrame frame)
    {
        frame = null!;
        var projector = GetProjectorToContinue();
        if (projector is null || positionMilliseconds < _positionMilliseconds)
            return TrySeek(positionMilliseconds, cancellationToken, out frame);

        var segment = _source.CreateTimelineSegment().CreateBoundedSnapshot();
        if (!TryResolveTimeRange(segment, projector, cancellationToken, out var timeRange) ||
            !projector.TryAdvanceTo(positionMilliseconds, segment, timeRange, cancellationToken, out frame) ||
            cancellationToken.IsCancellationRequested)
        {
            _projector = null;
            frame = null!;
            return false;
        }

        frame = ApplyFrame(frame);
        return true;
    }

    internal bool TrySeekObservationOrdinal(long endObservationOrdinalExclusive, CancellationToken cancellationToken, out ScenePlaybackFrame frame)
    {
        frame = null!;
        var segment = _source.CreateTimelineSegment().CreateBoundedSnapshot();
        var target = Math.Clamp(endObservationOrdinalExclusive, segment.StartObservationOrdinal, segment.CurrentEndObservationOrdinalExclusive);
        var projector = GetProjectorToContinue();
        if (projector is null || target < _nextLoadedObservationOrdinal)
        {
            if (!TryCreateProjector(cancellationToken, out var replacement) ||
                !TryResolveTimeRange(segment, replacement, cancellationToken, out var replacementTimeRange) ||
                !replacement.TryAdvanceToObservationOrdinal(target, segment, replacementTimeRange, cancellationToken, out frame) ||
                cancellationToken.IsCancellationRequested)
            {
                frame = null!;
                return false;
            }

            _projector = replacement;
            frame = ApplyFrame(frame);
            return true;
        }

        if (!TryResolveTimeRange(segment, projector, cancellationToken, out var timeRange) ||
            !projector.TryAdvanceToObservationOrdinal(target, segment, timeRange, cancellationToken, out frame) ||
            cancellationToken.IsCancellationRequested)
        {
            _projector = null;
            frame = null!;
            return false;
        }

        frame = ApplyFrame(frame);
        return true;
    }

    private ScenePlaybackFrame ApplyFrame(ScenePlaybackFrame frame)
    {
        _positionMilliseconds = frame.PositionMilliseconds;
        _nextLoadedObservationOrdinal = frame.AppliedSegment.EndObservationOrdinalExclusive;
        return frame;
    }

    internal ScenePlaybackCheckpoint CreateCheckpoint()
    {
        if (_projector is null)
        {
            var frame = Seek(_positionMilliseconds);
            return new ScenePlaybackCheckpoint(frame.PositionMilliseconds, new JournalCursor(frame.AppliedSegment.EndObservationOrdinalExclusive));
        }

        return new ScenePlaybackCheckpoint(_positionMilliseconds, new JournalCursor(_nextLoadedObservationOrdinal));
    }

    internal ScenePlaybackCombatantDetail CreateCombatantDetail(int combatantId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(combatantId);
        var projector = GetOrRestoreProjector();
        return projector.CreateCombatantDetail(combatantId);
    }

    internal ScenePlaybackEventReadResult CopyLatestMaterializedEvents(
        ScenePlaybackEventScope scope,
        long startPositionMilliseconds,
        long endPositionMilliseconds,
        Span<ScenePlaybackEventMarker> destination)
    {
        var projector = GetOrRestoreProjector();
        return projector.CopyLatestMaterializedEvents(scope, startPositionMilliseconds, endPositionMilliseconds, destination);
    }

    private FrameProjector GetOrRestoreProjector()
    {
        if (_projector is { } projector)
            return projector;

        _ = Seek(_positionMilliseconds);
        return _projector!;
    }

    // What a play tick, a refresh or an event step continues. A replay (TrySeek) keeps the shown frame's projector when it is
    // cancelled or throws; after TrySeekForward dropped its unfinished continuation instead, a replay to the shown position
    // gives those stores back first, uncancelled, so that the operation's own cancellation then acts as on a kept projector.
    private FrameProjector? GetProjectorToContinue()
    {
        if (_restoreDroppedProjector)
            _ = Seek(_positionMilliseconds);
        return _projector;
    }

    private bool TryCreateProjector(CancellationToken cancellationToken, out FrameProjector projector)
    {
        var segment = _source.CreateTimelineSegment().CreateBoundedSnapshot();
        var baseSnapshot = _source.CreateSnapshot();
        if (!ScenePlaybackTimeline.TryResolveTimeRange(segment, baseSnapshot, cancellationToken, out var timeRange))
        {
            projector = null!;
            return false;
        }

        // An "All fights" history entry runs on no further than the time it was archived with, and ends on it: one saved
        // before build 75 at its last hit, a newer one where the wall clock had taken it. A live scene is uncapped.
        long? archivedEnd = _source.SourceKind == ScenePlaybackSourceKind.Archived && baseSnapshot.Kind == SceneKind.Standard
            ? baseSnapshot.EncounterStartTime + baseSnapshot.EncounterTime
            : null;
        projector = new FrameProjector(_source.EncounterId, baseSnapshot.Kind, segment, timeRange, archivedEnd);
        return true;
    }

    private bool TryResolveTimeRange(
        SceneJournalSegment segment,
        FrameProjector projector,
        CancellationToken cancellationToken,
        out ScenePlaybackTimeRange timeRange)
    {
        if (_source.SourceKind != ScenePlaybackSourceKind.Live ||
            segment.CurrentEndObservationOrdinalExclusive == projector.TimeRangeEndObservationOrdinalExclusive)
        {
            timeRange = projector.TimeRange;
            return !cancellationToken.IsCancellationRequested;
        }

        // TryExtendTimeRange reads the snapshot only while the range has no timing yet. A live source's snapshot refreshes
        // the live scene under its lock, which the capture thread needs too, and takes the meter's pending changes.
        return ScenePlaybackTimeline.TryExtendTimeRange(
            segment,
            projector.TimeRangeEndObservationOrdinalExclusive,
            projector.TimeRange,
            projector.TimeRange.HasTiming ? SceneCombatSnapshot.Empty : _source.CreateSnapshot(),
            cancellationToken,
            out timeRange);
    }

    private sealed class FrameProjector
    {
        private readonly Guid _encounterId;
        private readonly SceneKind _kind;
        private readonly long? _archivedEnd;
        private readonly SceneProjectionState _projection;
        private readonly ScenePlaybackMaterializedEventIndex _materializedEventIndex;
        private readonly Dictionary<ScenePlaybackTrack, TrackAccumulator> _tracks;
        private readonly Dictionary<ScenePlaybackTrack, TrackAccumulator> _materializedTracks;
        // ponytail: one builder per projector; every session call runs under ScenePlaybackController's operation gate.
        private readonly SceneCombatSnapshotBuilder _snapshotBuilder = new();
        // The stores change only in ApplyEntry and CompleteFlush, which count this up: a frame without either (most ticks
        // at 60-144 Hz) shares the snapshot and vitals the frame before it made.
        private long _stateVersion;
        private long _builtStateVersion = -1;
        private long _builtSkillMapRevision;
        private bool _builtAtArchivedEnd;
        private SceneCombatSnapshot _builtSnapshot = SceneCombatSnapshot.Empty;
        private EntityVitalState[] _builtEntityVitals = [];
        private SceneJournalSegment _segment;
        private ScenePlaybackTimeRange _timeRange;
        private JournalCursor _cursor;
        private long _targetOffsetMilliseconds;
        private long _positionMilliseconds;
        private long _appliedEndOrdinal;
        private long _currentFlushId = -1;
        private long _completedFlushId = -1;
        private CombatDetailProjectionVersion _trackProjectionVersion;
        private int _trackedMetricEventCount;
        private int _trackedMechanicEventCount;
        private int _trackedResourceEventCount;
        private bool _hasTrackProjectionVersion;
        private int _detailCombatantId;
        private CombatDetailSubscription? _detailSubscription;
        private bool _frameReadChangedState;
        private bool _steppedByOrdinal;
        private bool _partAdvanced;

        public FrameProjector(Guid encounterId, SceneKind kind, SceneJournalSegment segment, ScenePlaybackTimeRange timeRange, long? archivedEnd)
        {
            _encounterId = encounterId;
            _kind = kind;
            _archivedEnd = archivedEnd;
            _segment = segment;
            _timeRange = timeRange;
            _projection = SceneProjectionState.Create(_encounterId);
            _tracks = [];
            _materializedTracks = [];
            _cursor = segment.CreateCursor();
            _targetOffsetMilliseconds = timeRange.HasTiming ? timeRange.StartOffsetMilliseconds : 0;
            _positionMilliseconds = 0;
            _appliedEndOrdinal = segment.StartObservationOrdinal;
            TimeRangeEndObservationOrdinalExclusive = segment.CurrentEndObservationOrdinalExclusive;
            _currentFlushId = -1;
            _completedFlushId = -1;
            _trackedMetricEventCount = 0;
            _trackedMechanicEventCount = 0;
            _trackedResourceEventCount = 0;
            _hasTrackProjectionVersion = false;
            _materializedEventIndex = new ScenePlaybackMaterializedEventIndex();
        }

        public SceneJournalSegment Segment => _segment;

        public ScenePlaybackTimeRange TimeRange => _timeRange;

        public long TimeRangeEndObservationOrdinalExclusive { get; private set; }

        // Whether the stores are what a replay from the start to the current position gives: since its replay, the projector
        // only moved forward in time, and every advance completed. An event step stops by ordinal (a replay to its position
        // also applies the entries that share its millisecond, or others when offsets go backwards), an advance that throws
        // leaves the stores part-advanced, and a frame read can expire bosses. None of these is undone, so the flags stay set.
        public bool CanAdvanceLikeReplay => !_frameReadChangedState && !_steppedByOrdinal && !_partAdvanced;

        // At the end of an archive's time range, its last frame takes the archived time (TryCreateProjector).
        private bool AtArchivedEnd => _archivedEnd is not null && _positionMilliseconds >= _timeRange.DurationMilliseconds;

        // A snapshot can drop bosses from BossFocusStore (TryGetObservedBoss expires them), which a replay from the start to
        // a later position would not have done. The time of "All fights" runs on past the last hit as it did live, by the
        // entries' clock: it moves only as entries apply, which is when the snapshot is made anew (BuildFrame).
        // ponytail: where the entries are the 10 s pings apart (alone and idle) the time steps by them; go by the playback
        // position if that shows.
        private SceneCombatSnapshot CreateSnapshot()
        {
            var bossFocus = _projection.Applier.BossFocus;
            var revision = bossFocus.Revision;
            var runningEnd = _projection.RunningEnd(_kind);
            if (_archivedEnd is long archivedEnd)
                runningEnd = AtArchivedEnd ? archivedEnd : Math.Min(runningEnd, archivedEnd);
            var snapshot = _projection.Adapter.CreateSnapshot(_kind, _snapshotBuilder, runningEnd);
            _frameReadChangedState |= bossFocus.Revision != revision;
            return snapshot;
        }

        public ScenePlaybackCombatantDetail CreateCombatantDetail(int combatantId)
        {
            var snapshot = CreateSnapshot();
            if (_detailSubscription is null || _detailCombatantId != combatantId)
            {
                _detailCombatantId = combatantId;
                _detailSubscription = new CombatDetailSubscription(_projection.Combat, _projection.Applier.Mechanics, _projection.Applier.Resources, combatantId);
            }

            var writer = new PlaybackDetailEventWriter();
            var update = _detailSubscription!.CreateSnapshotUpdate(_projection.Adapter, snapshot, CombatDetailProjectionScope.CurrentFrame, writer);
            return new ScenePlaybackCombatantDetail(_positionMilliseconds, _appliedEndOrdinal, snapshot, update, writer.Events);
        }

        public ScenePlaybackEventReadResult CopyLatestMaterializedEvents(
            ScenePlaybackEventScope scope,
            long startPositionMilliseconds,
            long endPositionMilliseconds,
            Span<ScenePlaybackEventMarker> destination)
            => _materializedEventIndex.CopyLatest(
                _projection.Combat,
                _projection.Applier.Mechanics,
                _projection.Applier.Resources,
                _projection.Adapter,
                scope,
                startPositionMilliseconds,
                endPositionMilliseconds,
                _appliedEndOrdinal,
                destination);

        public bool TryAdvanceTo(
            long positionMilliseconds,
            SceneJournalSegment segment,
            ScenePlaybackTimeRange timeRange,
            CancellationToken cancellationToken,
            out ScenePlaybackFrame frame)
        {
            frame = null!;
            var partAdvanced = _partAdvanced;
            _partAdvanced = true; // stays set when an entry or the frame throws: the stores are part-advanced from then on
            _segment = segment;
            _timeRange = timeRange;
            TimeRangeEndObservationOrdinalExclusive = segment.CurrentEndObservationOrdinalExclusive;
            _positionMilliseconds = ScenePlaybackTimeline.ClampPosition(positionMilliseconds, _timeRange.DurationMilliseconds);
            _targetOffsetMilliseconds = _timeRange.HasTiming
                ? _timeRange.StartOffsetMilliseconds + _positionMilliseconds
                : _positionMilliseconds;
            if (_cursor.NextObservationOrdinal < _segment.StartObservationOrdinal)
                _cursor = _segment.CreateCursor();
            if (!ApplyEntries(cancellationToken) || cancellationToken.IsCancellationRequested)
                return false;

            frame = BuildFrame();
            _partAdvanced = partAdvanced;
            return true;
        }

        public bool TryAdvanceToObservationOrdinal(
            long endObservationOrdinalExclusive,
            SceneJournalSegment segment,
            ScenePlaybackTimeRange timeRange,
            CancellationToken cancellationToken,
            out ScenePlaybackFrame frame)
        {
            frame = null!;
            _steppedByOrdinal = true;
            _segment = segment;
            _timeRange = timeRange;
            TimeRangeEndObservationOrdinalExclusive = segment.CurrentEndObservationOrdinalExclusive;
            var target = Math.Clamp(endObservationOrdinalExclusive, _segment.StartObservationOrdinal, _segment.CurrentEndObservationOrdinalExclusive);
            if (_cursor.NextObservationOrdinal < _segment.StartObservationOrdinal)
                _cursor = _segment.CreateCursor();
            if (!ApplyEntriesToObservationOrdinal(target, cancellationToken) || cancellationToken.IsCancellationRequested)
                return false;

            ResolvePositionAtObservationBoundary(target);
            if (cancellationToken.IsCancellationRequested)
                return false;

            frame = BuildFrame();
            return true;
        }

        private ScenePlaybackFrame BuildFrame()
        {
            // Besides ApplyEntry and CompleteFlush, only a snapshot changes the stores: it can expire bosses (CreateSnapshot).
            // It takes "now" from the stores, so at the same state a new snapshot would expire nothing more and equal the
            // shared one. A frame that shares it reads no boss focus and leaves _frameReadChangedState as it is. Reaching an
            // archive's end changes the time alone (AtArchivedEnd).
            var atArchivedEnd = AtArchivedEnd;
            if (_builtStateVersion != _stateVersion || _builtSkillMapRevision != CombatResourceRegistry.SkillMapRevision || _builtAtArchivedEnd != atArchivedEnd)
            {
                _builtSnapshot = CreateSnapshot();
                _builtEntityVitals = _projection.Applier.EntityVitals.CreateStateSnapshot();
                _builtStateVersion = _stateVersion;
                _builtSkillMapRevision = CombatResourceRegistry.SkillMapRevision;
                _builtAtArchivedEnd = atArchivedEnd;
            }

            var snapshot = _builtSnapshot;
            var totals = CreateTotals(snapshot);
            return new ScenePlaybackFrame
            {
                EncounterId = _encounterId,
                PositionMilliseconds = _positionMilliseconds,
                TimeRange = _timeRange,
                AppliedSegment = new SceneJournalSegment(_segment.Journal, _segment.StartObservationOrdinal, _appliedEndOrdinal, IsLiveGrowing: false),
                Snapshot = snapshot,
                CombatTotals = totals,
                EntityVitals = _builtEntityVitals,
                ActiveAuras = _projection.Applier.Auras.CreateActiveSnapshot(_targetOffsetMilliseconds),
                Tracks = CreateTrackSnapshot()
            };
        }

        private bool ApplyEntries(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;
                if (_cursor.NextObservationOrdinal >= _segment.CurrentEndObservationOrdinalExclusive)
                    break;

                var stoppedAtTarget = false;
                var appliedAny = false;
                var result = _segment.ReadEntries(_cursor, ScenePlaybackTimeline.DefaultReadBatchSize, entries =>
                {
                    for (var i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];
                        var offset = ScenePlaybackTimeline.ResolveOffsetMilliseconds(entry);
                        if (_timeRange.HasTiming && offset > _targetOffsetMilliseconds)
                        {
                            stoppedAtTarget = true;
                            return;
                        }

                        ApplyEntry(entry, offset);
                        appliedAny = true;
                    }
                });

                if (result.Count == 0 || stoppedAtTarget || !appliedAny)
                    break;
            }

            if (cancellationToken.IsCancellationRequested)
                return false;

            TryCompleteCurrentFlushAtSegmentEnd();
            return !cancellationToken.IsCancellationRequested;
        }

        private bool ApplyEntriesToObservationOrdinal(long endObservationOrdinalExclusive, CancellationToken cancellationToken)
        {
            while (_cursor.NextObservationOrdinal < endObservationOrdinalExclusive)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;
                var stoppedAtTarget = false;
                var appliedAny = false;
                var result = _segment.ReadEntries(_cursor, ScenePlaybackTimeline.DefaultReadBatchSize, entries =>
                {
                    for (var i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];
                        if (entry.Stamp.ObservationOrdinal >= endObservationOrdinalExclusive)
                        {
                            stoppedAtTarget = true;
                            return;
                        }

                        var offset = ScenePlaybackTimeline.ResolveOffsetMilliseconds(entry);
                        ApplyEntry(entry, offset);
                        appliedAny = true;
                    }
                });

                if (result.Count == 0 || stoppedAtTarget || !appliedAny)
                    break;
            }

            if (cancellationToken.IsCancellationRequested)
                return false;

            TryCompleteCurrentFlushAtSegmentEnd();
            return !cancellationToken.IsCancellationRequested;
        }

        private void ResolvePositionAtObservationBoundary(long endObservationOrdinalExclusive)
        {
            if (endObservationOrdinalExclusive <= _segment.StartObservationOrdinal)
            {
                _positionMilliseconds = 0;
                _targetOffsetMilliseconds = _timeRange.HasTiming ? _timeRange.StartOffsetMilliseconds : 0;
                return;
            }

            var offset = 0L;
            _segment.ReadEntries(new JournalCursor(endObservationOrdinalExclusive - 1), 1, entries =>
            {
                if (entries.Count > 0)
                    offset = ScenePlaybackTimeline.ResolveOffsetMilliseconds(entries[0]);
            });
            _targetOffsetMilliseconds = offset;
            _positionMilliseconds = Math.Max(0, offset);
        }

        private void ApplyEntry(ObservedEventEntry entry, long offset)
        {
            var flushId = entry.Stamp.FlushId;
            if (_currentFlushId >= 0 && flushId != _currentFlushId)
                CompleteFlush(_currentFlushId);

            _currentFlushId = flushId;
            _stateVersion++;
            var materialization = _projection.ApplyEntry(entry);
            ApplyObservationTrack(entry, offset, in materialization);
            _appliedEndOrdinal = entry.Stamp.ObservationOrdinal + 1;
            _cursor = new JournalCursor(_appliedEndOrdinal);
        }

        private void CompleteFlush(long flushId)
        {
            if (flushId <= 0 || flushId <= _completedFlushId)
                return;

            _stateVersion++;
            _projection.CompleteFlush();
            _completedFlushId = flushId;
        }

        private void TryCompleteCurrentFlushAtSegmentEnd()
        {
            if (_currentFlushId <= 0 || _cursor.NextObservationOrdinal < _segment.CurrentEndObservationOrdinalExclusive)
                return;

            if (_segment.IsLiveGrowing && (_segment.Journal?.LastCompletedFlushId ?? -1) < _currentFlushId)
                return;

            CompleteFlush(_currentFlushId);
        }

        private void ApplyObservationTrack(ObservedEventEntry entry, long offset, in DomainEventMaterialization materialization)
        {
            if (entry.Domain == ObservedEventDomain.Combat || ScenePlaybackTrackProjection.IsData(entry))
                return;

            var auraLifecycle = materialization.AuraLifecycle;
            var marker = ScenePlaybackTrackProjection.CreateObservationMarker(entry, offset, Math.Max(0, offset), in auraLifecycle);
            ApplyTrack(_tracks, marker.Track, entry.Stamp.ObservationOrdinal);
        }

        private void RefreshMaterializedTracks()
        {
            var projectionVersion = _projection.Adapter.PrepareCurrentFrameEventProjection();
            var metricEvents = _projection.Combat.EventSpan;
            var mechanicEvents = _projection.Applier.Mechanics.Events;
            var resourceEvents = _projection.Applier.Resources.Events;
            if (!_hasTrackProjectionVersion ||
                projectionVersion != _trackProjectionVersion ||
                _trackedMetricEventCount > metricEvents.Length ||
                _trackedMechanicEventCount > mechanicEvents.Count ||
                _trackedResourceEventCount > resourceEvents.Count)
            {
                _materializedTracks.Clear();
                _trackedMetricEventCount = 0;
                _trackedMechanicEventCount = 0;
                _trackedResourceEventCount = 0;
                _trackProjectionVersion = projectionVersion;
                _hasTrackProjectionVersion = true;
            }

            for (var eventIndex = _trackedMetricEventCount; eventIndex < metricEvents.Length; eventIndex++)
            {
                ref readonly var record = ref metricEvents[eventIndex];
                if (_projection.Adapter.TryResolveCurrentFrameEventSourcePrepared(in record, out _))
                    ApplyTrack(_materializedTracks, ScenePlaybackTrack.Combat, record.SourceObservationOrdinal);
            }

            for (var eventIndex = _trackedMechanicEventCount; eventIndex < mechanicEvents.Count; eventIndex++)
            {
                var record = mechanicEvents[eventIndex];
                if (_projection.Adapter.TryResolveCurrentFrameEventSourcePrepared(in record, out _))
                    ApplyTrack(_materializedTracks, ScenePlaybackTrack.Mechanic, record.SourceObservationOrdinal);
            }

            for (var eventIndex = _trackedResourceEventCount; eventIndex < resourceEvents.Count; eventIndex++)
                ApplyTrack(_materializedTracks, ScenePlaybackTrack.Resource, resourceEvents[eventIndex].SourceObservationOrdinal);

            _trackedMetricEventCount = metricEvents.Length;
            _trackedMechanicEventCount = mechanicEvents.Count;
            _trackedResourceEventCount = resourceEvents.Count;
        }

        private static void ApplyTrack(
            Dictionary<ScenePlaybackTrack, TrackAccumulator> tracks,
            ScenePlaybackTrack track,
            long observationOrdinal)
        {
            ref var accumulator = ref CollectionsMarshal.GetValueRefOrAddDefault(tracks, track, out var exists);
            if (!exists)
                accumulator = new TrackAccumulator(observationOrdinal);
            accumulator.Apply(observationOrdinal);
        }

        private ScenePlaybackCombatTotals CreateTotals(SceneCombatSnapshot snapshot)
        {
            var totalDamage = 0L;
            var totalHealing = 0L;
            var totalShield = 0L;
            var totalShieldAbsorbed = 0L;
            var combatants = snapshot.Combatants.AsSpan();
            foreach (ref readonly var combatant in combatants)
            {
                totalDamage += combatant.Metrics.DamageAmount;
                totalHealing += combatant.Metrics.HealingAmount;
                totalShield += combatant.Metrics.ShieldAmount;
                totalShieldAbsorbed += combatant.Metrics.ShieldAbsorbedAmount;
            }

            var elapsed = snapshot.EncounterTime > 0 ? snapshot.EncounterTime : Math.Max(0, _targetOffsetMilliseconds - _timeRange.StartOffsetMilliseconds);
            var dps = elapsed > 0 ? (double)totalDamage / elapsed * 1000 : 0d;
            var hps = elapsed > 0 ? (double)totalHealing / elapsed * 1000 : 0d;
            return new ScenePlaybackCombatTotals(totalDamage, totalHealing, totalShield, totalShieldAbsorbed, dps, hps, elapsed);
        }

        private ScenePlaybackTrackWindow[] CreateTrackSnapshot()
        {
            // The only reader of the materialized tracks, so they catch up here once per frame, not after every entry.
            RefreshMaterializedTracks();
            var trackCount = _tracks.Count + _materializedTracks.Count;
            if (trackCount == 0)
                return [];

            var result = new ScenePlaybackTrackWindow[trackCount];
            var index = 0;
            foreach (var (track, accumulator) in _tracks)
                result[index++] = accumulator.ToWindow(track);
            foreach (var (track, accumulator) in _materializedTracks)
                result[index++] = accumulator.ToWindow(track);
            Array.Sort(result, static (left, right) => ((int)left.Track).CompareTo((int)right.Track));
            return result;
        }

        private sealed class PlaybackDetailEventWriter : ICombatDetailEventWriter
        {
            private readonly List<CombatMetricDetailEvent> _metricEvents = [];
            private readonly List<CombatMechanicDetailEvent> _mechanicEvents = [];
            private readonly List<CombatResourceDetailEvent> _resourceEvents = [];

            public CombatDetailEventSet Events => new(_metricEvents, _mechanicEvents, _resourceEvents);

            public void Clear()
            {
                _metricEvents.Clear();
                _mechanicEvents.Clear();
                _resourceEvents.Clear();
            }

            public void AddMetric(in CombatMetricDetailEvent detailEvent) => _metricEvents.Add(detailEvent);

            public void AddMechanic(in CombatMechanicDetailEvent detailEvent) => _mechanicEvents.Add(detailEvent);

            public void AddResource(in CombatResourceDetailEvent detailEvent) => _resourceEvents.Add(detailEvent);
        }
    }

    private struct TrackAccumulator(long firstOrdinal)
    {
        private long _firstOrdinal = firstOrdinal;
        private long _lastOrdinal = firstOrdinal;
        private int _count;

        public void Apply(long ordinal)
        {
            _firstOrdinal = Math.Min(_firstOrdinal, ordinal);
            _lastOrdinal = Math.Max(_lastOrdinal, ordinal);
            _count++;
        }

        public readonly ScenePlaybackTrackWindow ToWindow(ScenePlaybackTrack track) => new(track, _firstOrdinal, _lastOrdinal + 1, _count);

    }
}
