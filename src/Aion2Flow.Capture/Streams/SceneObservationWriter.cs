using Cloris.Aion2Flow.Protocol.Packets;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Runtime;

namespace Cloris.Aion2Flow.Capture.Streams;

internal sealed class SceneObservationWriter(IRuntimeObservationSink sink)
{
    public IRuntimeObservationSink Sink => sink;

    // A spawn's code, with the owner it names if any.
    public void ApplyNpcCatalog(in PacketObservationSource packet, int instanceId, int npcCode, bool requireCatalogEntry = false, int? ownerId = null)
    {
        if (instanceId <= 0 || npcCode <= 0)
        {
            return;
        }

        sink.ObserveSpawn(instanceId, npcCode, ownerId);
        var hasCatalogEntry = CombatResourceRegistry.TryResolveNpcCatalogEntry(npcCode, out var entry);
        if (requireCatalogEntry && !hasCatalogEntry)
        {
            return;
        }

        sink.TryGetNpcRuntimeState(instanceId, out var existing);
        sink.AppendNpcCode(in packet, instanceId, npcCode);

        if (!hasCatalogEntry)
        {
            return;
        }

        sink.AppendNpcName(in packet, npcCode, entry.Name);

        var kind = CombatResourceRegistry.ResolveNpcKind(entry.Kind);
        // A summon the HP fallback made a boss (PacketStateHandler.ParseRemainHpPacket) stays one when its spawn comes again.
        if (kind != NpcKind.Unknown && !(kind == NpcKind.Summon && existing.Kind == NpcKind.Boss && existing.NpcCode == npcCode))
        {
            sink.AppendNpcKind(in packet, instanceId, kind);
        }
    }

    public bool ApplyMapScopeSignal(in PacketObservationSource packet, PacketScopeSignal signal)
    {
        if (signal == PacketScopeSignal.Current)
        {
            sink.EnsureUnknownMapScope(in packet);
        }

        return true;
    }

    public bool ApplyCurrentMapFromCompositeSceneState(in PacketObservationSource packet, uint value, out bool mapScopeStarted)
    {
        mapScopeStarted = false;
        if (!SceneMapIdClassifier.IsKnownMapId(value))
        {
            return false;
        }

        mapScopeStarted = sink.StageMapCandidate(in packet, value);
        return true;
    }

    public bool StageMapCandidateFromCompositeSceneState(in PacketObservationSource packet, uint value, out bool mapScopeStarted)
    {
        mapScopeStarted = false;
        if (!SceneMapIdClassifier.IsKnownMapId(value))
        {
            return false;
        }

        mapScopeStarted = sink.StageMapCandidate(in packet, value);
        return true;
    }
}
