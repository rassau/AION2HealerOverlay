using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;

namespace Cloris.Aion2Flow.SceneRuntime.Observation;

public interface IRuntimeObservationSink
{
    int CurrentTarget { get; set; }

    int ResolveLifecycleId(int rawInstanceId);

    int RebindInstanceLifecycle(int rawInstanceId);

    // A spawn of this id with its code and the owner it names, if any: another creature (a new lifecycle) when the code or
    // the owner differs from the id's.
    void ObserveSpawn(int rawInstanceId, int npcCode, int? ownerId);

    bool IsKnownEntity(int id);

    bool HasSummonOwner(int instanceId);

    bool TryGetNpcRuntimeState(int instanceId, out RuntimeNpcStateSnapshot state);

    // Whether a boss of the current map that the game data files as one has this max HP (or up to 1 % more while it is fought).
    bool HasCatalogBossWithMaxHp(long maxHp);

    // Whether a boss of the current map that the HP fallback made (no code, or a summon of no owner) is fought: alive, not out
    // of battle, and its HP changed in the 10 s before this capture time.
    bool IsFallbackBossFought(long capturedAtMilliseconds);

    void SeedNpcRuntimeState(in PacketObservationSource packet, int instanceId, in RuntimeNpcStateSnapshot state);

    int ResolveNpcObservationSource();

    void RememberNpcObservationSource(int instanceId);

    void SetCurrentMap(in PacketObservationSource packet, uint mapId);

    void EnsureUnknownMapScope(in PacketObservationSource packet);

    bool StageMapCandidate(in PacketObservationSource packet, uint mapId);

    bool ConfirmDestinationMapArrival(in PacketObservationSource packet);

    void RegisterMapEvent(in PacketObservationSource packet, uint instanceId);

    void UnregisterMapEvent(in PacketObservationSource packet, uint instanceId);

    void MarkTransportStreamActivated(in PacketObservationSource packet);

    void AppendCombatWireObservation(in PacketObservationSource packet, int sourceId, int targetId, in CombatWireObservation observation);

    void CompleteFlush(long flushId);

    void RegisterCompactValue0438(in PacketObservationSource packet, int targetId, int sourceId, int bodySkillVariantRaw, int marker, int layoutTag, int type);

    void RegisterCompactValue0438(in PacketObservationSource packet, int targetId, int sourceId, int bodySkillVariantRaw, int marker, int layoutTag, int type, int value);

    void RegisterCompactControl0238(in PacketObservationSource packet, int sourceId, int mode, uint bodyCodeRaw, int marker, int flag, int echoSourceId, int? availableCountAfterControl = null, int? cooldownMilliseconds = null);

    void RegisterCompactControl0638(in PacketObservationSource packet, int sourceId, ResourceEffectRef bodyResourceEffectRef, int marker, int flag);

    void RegisterCooldownCharge2238(in PacketObservationSource packet, byte state, int packetSkillCode, int availableCount, int nextChargeRemainingMilliseconds);

    void RegisterCooldown4738(in PacketObservationSource packet, int rowBaseSkillId, int remainingMilliseconds);

    void RegisterObservation2A38(in PacketObservationSource packet, int entityId, int mode, int groupCode, int instanceSequenceId, uint headCode, ushort headValue, ulong headMiddleRaw, uint timelineValue, uint stableValue, int echoSourceId, int stackValue, ResourceEffectRef buffResourceEffectRef, int tailLength, ulong tailLow64, ulong tailHigh64);

    void RegisterObservation2B38(in PacketObservationSource packet, int sourceId, int sourceIdCopy, int phase, int instanceSequenceId, ResourceEffectRef actionResourceEffectRef, int sequenceValue, int stateValue, int detailValue, int tailLength);

    void RegisterObservation2C38(in PacketObservationSource packet, int entityId, scoped ReadOnlySpan<AuraResultRecord> results);

    void AppendNickname(in PacketObservationSource packet, int uid, string nickname, Faction faction = Faction.Unknown, CharacterClass? characterClass = null, bool isLocalPlayer = false, int? originServerId = null, string legionName = "");

    void AppendPlayerGroupMember(in PacketObservationSource packet, int uid, in PlayerGroupMembership membership);

    void AppendPlayerGroupProfile(in PacketObservationSource packet, int originServerId, string nickname, in PlayerGroupMembership membership);

    void AppendNpcCode(in PacketObservationSource packet, int instanceId, int npcCode);

    void AppendNpcName(in PacketObservationSource packet, int npcCode, string name);

    void AppendNpcKind(in PacketObservationSource packet, int instanceId, NpcKind kind);

    void AppendNpcHp(in PacketObservationSource packet, int instanceId, long hp);

    void AppendNpcHp(in PacketObservationSource packet, int instanceId, long hp, long maxHp);

    void SetNpcBattle(in PacketObservationSource packet, int instanceId, bool isActive);

    void ToggleNpcBattle(in PacketObservationSource packet, int instanceId);

    // Max 0: the gauge broke and the boss is staggered.
    void AppendStaggerGauge(in PacketObservationSource packet, int instanceId, long current, long max);

    // The boss frame's target (0x2092), 0 for no one.
    void AppendBossTarget(in PacketObservationSource packet, int targetId);

    // Where a group member stands (see StateCodes.Position); sinks that keep no journal leave it out.
    void AppendPosition(in PacketObservationSource packet, int entityId, System.Numerics.Vector3 position)
    {
    }

    // The player's own ping (see StateCodes.Ping); sinks that keep no journal leave it out.
    void AppendPing(in PacketObservationSource packet, double milliseconds)
    {
    }

    // A level the game sent for a player's skill (see StateCodes.SkillLevel); sinks that keep no journal leave it out.
    void AppendSkillLevel(in PacketObservationSource packet, int entityId, uint effectId, int level)
    {
    }

    // The game's kill message (see StateCodes.Kill); sinks that keep no journal leave it out.
    void AppendKill(in PacketObservationSource packet, int killedId, int killerId)
    {
    }

    // The game's count of the player's credited kills (see StateCodes.KillCredit); sinks that keep no journal leave it out.
    void AppendKillCredit(in PacketObservationSource packet, long count)
    {
    }

    void AppendNpc2136State(in PacketObservationSource packet, int instanceId, long sequence, long value0);

    void AppendNpc0140Value(in PacketObservationSource packet, int instanceId, long value0);

    void AppendNpc0240Value(in PacketObservationSource packet, int instanceId, long value0);

    void AppendNpc4636State(in PacketObservationSource packet, int instanceId, byte state0, byte state1);

    void AppendSummon(in PacketObservationSource packet, int ownerId, int summonInstanceId);
}

public readonly record struct RuntimeNpcStateSnapshot(int? NpcCode, long? Hp, long? MaxHp, long? HpObservedAtMilliseconds, bool? BattleToggledOn, NpcKind? Kind, long? Value2136, long? Sequence2136, long? Value0140, long? Value0240, (byte State0, byte State1)? State4636, (int SequenceId, int ResultCode)? Latest2C38);
