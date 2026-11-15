using System.Runtime.InteropServices;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.SceneRuntime.Playback;

internal sealed class ScenePlaybackMaterializedEventIndex
{
    // What an indexed event resolved to when it was left out; an included one resolves to an int.
    private const long ExcludedSource = long.MinValue;

    private readonly List<ScenePlaybackEventMarker> _markers = [];
    private readonly List<int> _all = [];
    private readonly Dictionary<ScenePlaybackEventScope, List<int>> _postings = [];
    // Per posting that went out of order in the current batch: the index of its first entry out of order.
    private readonly Dictionary<List<int>, int> _firstUnsorted = new(ReferenceEqualityComparer.Instance);
    private readonly List<long> _metricSources = [];
    private readonly List<long> _mechanicSources = [];
    private readonly List<long> _resourceSources = [];
    private readonly Comparison<int> _compareMarkerIndexes;
    private int[] _mergeBuffer = [];
    private CombatDetailProjectionVersion _projectionVersion;
    private long _skillMapRevision;
    private int _indexedMetricEventCount;
    private int _indexedMechanicEventCount;
    private int _indexedResourceEventCount;
    private bool _hasProjectionVersion;

    public ScenePlaybackMaterializedEventIndex()
    {
        _compareMarkerIndexes = CompareMarkerIndexes;
    }

    public ScenePlaybackEventReadResult CopyLatest(
        CombatStore combat,
        MechanicStore mechanics,
        ResourceStore resources,
        SceneCombatSnapshotAdapter adapter,
        ScenePlaybackEventScope scope,
        long startPositionMilliseconds,
        long endPositionMilliseconds,
        long endObservationOrdinalExclusive,
        Span<ScenePlaybackEventMarker> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startPositionMilliseconds);
        ArgumentOutOfRangeException.ThrowIfLessThan(endPositionMilliseconds, startPositionMilliseconds);
        if (destination.IsEmpty || !scope.IncludesMaterializedEvents)
            return new ScenePlaybackEventReadResult(0, endObservationOrdinalExclusive);

        Refresh(combat, mechanics, resources, adapter);
        var posting = scope == ScenePlaybackEventScope.All
            ? _all
            : _postings.GetValueOrDefault(scope);
        if (posting is null || posting.Count == 0)
            return new ScenePlaybackEventReadResult(0, endObservationOrdinalExclusive);

        var first = LowerBound(posting, startPositionMilliseconds);
        var end = UpperBound(posting, endPositionMilliseconds, first);
        first = Math.Max(first, end - destination.Length);
        var count = end - first;
        var markers = CollectionsMarshal.AsSpan(_markers);
        var indexes = CollectionsMarshal.AsSpan(posting);
        for (var i = 0; i < count; i++)
            destination[i] = markers[indexes[first + i]];

        return new ScenePlaybackEventReadResult(count, endObservationOrdinalExclusive);
    }

    private void Refresh(
        CombatStore combat,
        MechanicStore mechanics,
        ResourceStore resources,
        SceneCombatSnapshotAdapter adapter)
    {
        var projectionVersion = adapter.PrepareCurrentFrameEventProjection();
        var metricEvents = combat.EventSpan;
        var mechanicEvents = mechanics.Events;
        var resourceEvents = resources.Events;
        // An identity change (the projection version) only matters when it moved whom an indexed event resolves to or the
        // skill map changed: the index is a function of the events, their resolutions and the skill map, so otherwise the
        // postings it has plus the new events are what a rebuild would make.
        if (!_hasProjectionVersion ||
            _indexedMetricEventCount > metricEvents.Length ||
            _indexedMechanicEventCount > mechanicEvents.Count ||
            _indexedResourceEventCount > resourceEvents.Count ||
            projectionVersion != _projectionVersion &&
            (_skillMapRevision != CombatResourceRegistry.SkillMapRevision ||
             !ResolutionsUnchanged(metricEvents, mechanicEvents, resourceEvents, adapter)))
        {
            Rebuild(metricEvents, mechanicEvents, resourceEvents, adapter, projectionVersion);
            return;
        }

        _projectionVersion = projectionVersion;
        BeginPostingBatch();
        try
        {
            AppendMetrics(metricEvents, adapter, _indexedMetricEventCount);
            AppendMechanics(mechanicEvents, adapter, _indexedMechanicEventCount);
            AppendResources(resourceEvents, adapter, _indexedResourceEventCount);
        }
        finally
        {
            CompletePostingBatch();
        }

        UpdateIndexedCounts(metricEvents.Length, mechanicEvents.Count, resourceEvents.Count);
    }

    private bool ResolutionsUnchanged(
        EventRange<CombatEventRecord> metricEvents,
        IReadOnlyList<CombatMechanicEventRecord> mechanicEvents,
        IReadOnlyList<CombatResourceEventRecord> resourceEvents,
        SceneCombatSnapshotAdapter adapter)
    {
        if (_metricSources.Count != _indexedMetricEventCount ||
            _mechanicSources.Count != _indexedMechanicEventCount ||
            _resourceSources.Count != _indexedResourceEventCount)
            return false;

        var metricSources = CollectionsMarshal.AsSpan(_metricSources);
        for (var i = 0; i < metricSources.Length; i++)
        {
            ref readonly var record = ref metricEvents[i];
            if (ResolveMetric(adapter, in record) != metricSources[i])
                return false;
        }

        var mechanicSources = CollectionsMarshal.AsSpan(_mechanicSources);
        for (var i = 0; i < mechanicSources.Length; i++)
        {
            var record = mechanicEvents[i];
            if (ResolveMechanic(adapter, in record) != mechanicSources[i])
                return false;
        }

        var resourceSources = CollectionsMarshal.AsSpan(_resourceSources);
        for (var i = 0; i < resourceSources.Length; i++)
        {
            var record = resourceEvents[i];
            if (adapter.ResolveCurrentFrameEventSourcePrepared(in record) != resourceSources[i])
                return false;
        }

        return true;
    }

    private static long ResolveMetric(SceneCombatSnapshotAdapter adapter, in CombatEventRecord record) =>
        adapter.TryResolveCurrentFrameEventSourcePrepared(in record, out var source) ? source : ExcludedSource;

    private static long ResolveMechanic(SceneCombatSnapshotAdapter adapter, in CombatMechanicEventRecord record) =>
        adapter.TryResolveCurrentFrameEventSourcePrepared(in record, out var source) ? source : ExcludedSource;

    private void Rebuild(
        EventRange<CombatEventRecord> metricEvents,
        IReadOnlyList<CombatMechanicEventRecord> mechanicEvents,
        IReadOnlyList<CombatResourceEventRecord> resourceEvents,
        SceneCombatSnapshotAdapter adapter,
        CombatDetailProjectionVersion projectionVersion)
    {
        _skillMapRevision = CombatResourceRegistry.SkillMapRevision;
        _markers.Clear();
        _all.Clear();
        _postings.Clear();
        _metricSources.Clear();
        _mechanicSources.Clear();
        _resourceSources.Clear();
        BeginPostingBatch();
        try
        {
            AppendMetrics(metricEvents, adapter, 0);
            AppendMechanics(mechanicEvents, adapter, 0);
            AppendResources(resourceEvents, adapter, 0);
        }
        finally
        {
            CompletePostingBatch();
        }

        UpdateIndexedCounts(metricEvents.Length, mechanicEvents.Count, resourceEvents.Count);
        _projectionVersion = projectionVersion;
        _hasProjectionVersion = true;
    }

    private void AppendMetrics(EventRange<CombatEventRecord> events, SceneCombatSnapshotAdapter adapter, int startIndex)
    {
        for (var eventIndex = startIndex; eventIndex < events.Length; eventIndex++)
        {
            ref readonly var record = ref events[eventIndex];
            if (!adapter.TryResolveCurrentFrameEventSourcePrepared(in record, out var sourceEntityId))
            {
                _metricSources.Add(ExcludedSource);
                continue;
            }

            _metricSources.Add(sourceEntityId);
            AddMetric(in record, sourceEntityId);
        }
    }

    private void AppendMechanics(
        IReadOnlyList<CombatMechanicEventRecord> events,
        SceneCombatSnapshotAdapter adapter,
        int startIndex)
    {
        for (var eventIndex = startIndex; eventIndex < events.Count; eventIndex++)
        {
            var record = events[eventIndex];
            if (!adapter.TryResolveCurrentFrameEventSourcePrepared(in record, out var sourceEntityId))
            {
                _mechanicSources.Add(ExcludedSource);
                continue;
            }

            _mechanicSources.Add(sourceEntityId);
            AddMechanic(in record, sourceEntityId);
        }
    }

    private void AppendResources(
        IReadOnlyList<CombatResourceEventRecord> events,
        SceneCombatSnapshotAdapter adapter,
        int startIndex)
    {
        for (var eventIndex = startIndex; eventIndex < events.Count; eventIndex++)
        {
            var record = events[eventIndex];
            var sourceEntityId = adapter.ResolveCurrentFrameEventSourcePrepared(in record);
            _resourceSources.Add(sourceEntityId);
            AddResource(in record, sourceEntityId);
        }
    }

    private void AddMetric(in CombatEventRecord record, int sourceEntityId)
    {
        var contribution = record.Contribution;
        var trackMarker = ScenePlaybackTrackProjection.CreateMetricMarker(in record, sourceEntityId);
        var marker = CreateEventMarker(
            ScenePlaybackEventFactKind.Metric,
            record.Revision,
            in trackMarker,
            contribution,
            null,
            null);
        Add(in marker);
    }

    private void AddMechanic(in CombatMechanicEventRecord record, int sourceEntityId)
    {
        var trackMarker = ScenePlaybackTrackProjection.CreateMechanicMarker(in record, sourceEntityId);
        var marker = CreateEventMarker(
            ScenePlaybackEventFactKind.Mechanic,
            record.Revision,
            in trackMarker,
            null,
            record.Mechanic,
            null);
        Add(in marker);
    }

    private void AddResource(in CombatResourceEventRecord record, int sourceEntityId)
    {
        var trackMarker = ScenePlaybackTrackProjection.CreateResourceMarker(in record, sourceEntityId);
        var marker = CreateEventMarker(
            ScenePlaybackEventFactKind.Resource,
            record.Revision,
            in trackMarker,
            null,
            null,
            record.Resource);
        Add(in marker);
    }

    private static ScenePlaybackEventMarker CreateEventMarker(
        ScenePlaybackEventFactKind factKind,
        long revision,
        in ScenePlaybackTrackMarker trackMarker,
        CombatContribution? contribution,
        CombatMechanicOccurrence? mechanic,
        CombatResourceOccurrence? resource)
    {
        return new ScenePlaybackEventMarker(
            new ScenePlaybackEventId(factKind, revision),
            trackMarker,
            SkillBaseKey.FromEventKey(trackMarker.EventKey),
            contribution,
            mechanic,
            resource);
    }

    private void Add(in ScenePlaybackEventMarker marker)
    {
        var markerIndex = _markers.Count;
        _markers.Add(marker);
        AddPosting(_all, markerIndex);

        var sourceEntityId = marker.SourceEntityId;
        var targetEntityId = marker.TargetEntityId;
        if (sourceEntityId > 0)
        {
            AddPosting(ScenePlaybackEventScope.ForCombatant(sourceEntityId), markerIndex);
            AddPosting(ScenePlaybackEventScope.ForRelation(sourceEntityId, ScenePlaybackEventRelation.Outgoing), markerIndex);
            AddCategoryPostings(sourceEntityId, ScenePlaybackEventRelation.Outgoing, in marker, markerIndex);
        }

        if (targetEntityId <= 0)
            return;

        if (targetEntityId != sourceEntityId)
            AddPosting(ScenePlaybackEventScope.ForCombatant(targetEntityId), markerIndex);
        AddPosting(ScenePlaybackEventScope.ForRelation(targetEntityId, ScenePlaybackEventRelation.Incoming), markerIndex);
        AddCategoryPostings(targetEntityId, ScenePlaybackEventRelation.Incoming, in marker, markerIndex);
    }

    private void AddCategoryPostings(
        int combatantId,
        ScenePlaybackEventRelation relation,
        in ScenePlaybackEventMarker marker,
        int markerIndex)
    {
        AddCategoryPosting(combatantId, relation, CombatContributionCategory.Damage, ScenePlaybackCombatEventFlags.Damage, in marker, markerIndex);
        AddCategoryPosting(combatantId, relation, CombatContributionCategory.Healing, ScenePlaybackCombatEventFlags.Healing, in marker, markerIndex);
        AddCategoryPosting(combatantId, relation, CombatContributionCategory.Shield, ScenePlaybackCombatEventFlags.Shield, in marker, markerIndex);
    }

    private void AddCategoryPosting(
        int combatantId,
        ScenePlaybackEventRelation relation,
        CombatContributionCategory category,
        ScenePlaybackCombatEventFlags expectedFlag,
        in ScenePlaybackEventMarker marker,
        int markerIndex)
    {
        if ((marker.CombatEventFlags & expectedFlag) == 0)
            return;

        AddPosting(ScenePlaybackEventScope.ForCategory(combatantId, relation, category), markerIndex);
        AddPosting(ScenePlaybackEventScope.ForSkill(combatantId, relation, category, marker.SkillBaseKey), markerIndex);
    }

    private void AddPosting(ScenePlaybackEventScope scope, int markerIndex)
    {
        if (!_postings.TryGetValue(scope, out var posting))
        {
            posting = [];
            _postings.Add(scope, posting);
        }

        AddPosting(posting, markerIndex);
    }

    private void AddPosting(List<int> posting, int markerIndex)
    {
        var count = posting.Count;
        posting.Add(markerIndex);
        if (count > 0 && CompareMarkerIndexes(posting[count - 1], markerIndex) > 0)
            _firstUnsorted.TryAdd(posting, count);
    }

    private void BeginPostingBatch()
    {
        _firstUnsorted.Clear();
    }

    private void CompletePostingBatch()
    {
        foreach (var (posting, firstUnsorted) in _firstUnsorted)
            MergeTail(posting, firstUnsorted);

        _firstUnsorted.Clear();
    }

    // The posting is in order up to firstUnsorted: sort what came after it and merge that in from the back. Marker ids are
    // unique, so the order is total and the result equals a sort of the whole posting.
    private void MergeTail(List<int> posting, int firstUnsorted)
    {
        var items = CollectionsMarshal.AsSpan(posting);
        var tail = items[firstUnsorted..];
        tail.Sort(_compareMarkerIndexes);
        if (_mergeBuffer.Length < tail.Length)
            _mergeBuffer = new int[Math.Max(tail.Length, _mergeBuffer.Length * 2)];
        var sortedTail = _mergeBuffer.AsSpan(0, tail.Length);
        tail.CopyTo(sortedTail);

        var head = firstUnsorted - 1;
        var next = sortedTail.Length - 1;
        var write = items.Length - 1;
        while (next >= 0)
        {
            if (head >= 0 && CompareMarkerIndexes(items[head], sortedTail[next]) > 0)
                items[write--] = items[head--];
            else
                items[write--] = sortedTail[next--];
        }
    }

    private int LowerBound(List<int> posting, long positionMilliseconds)
    {
        var markers = CollectionsMarshal.AsSpan(_markers);
        var indexes = CollectionsMarshal.AsSpan(posting);
        var low = 0;
        var high = indexes.Length;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (markers[indexes[middle]].Marker.PositionMilliseconds < positionMilliseconds)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }

    private int UpperBound(List<int> posting, long positionMilliseconds, int start)
    {
        var markers = CollectionsMarshal.AsSpan(_markers);
        var indexes = CollectionsMarshal.AsSpan(posting);
        var low = start;
        var high = indexes.Length;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (markers[indexes[middle]].Marker.PositionMilliseconds <= positionMilliseconds)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }

    private void UpdateIndexedCounts(int metricCount, int mechanicCount, int resourceCount)
    {
        _indexedMetricEventCount = metricCount;
        _indexedMechanicEventCount = mechanicCount;
        _indexedResourceEventCount = resourceCount;
    }

    private static int CompareMarkers(in ScenePlaybackEventMarker left, in ScenePlaybackEventMarker right)
    {
        var comparison = left.Marker.PositionMilliseconds.CompareTo(right.Marker.PositionMilliseconds);
        return comparison != 0 ? comparison : left.Id.CompareTo(right.Id);
    }

    // By reference: a marker is 400 bytes, and List<T>'s indexer copies one per read.
    private int CompareMarkerIndexes(int left, int right)
    {
        var markers = CollectionsMarshal.AsSpan(_markers);
        return CompareMarkers(in markers[left], in markers[right]);
    }

}
