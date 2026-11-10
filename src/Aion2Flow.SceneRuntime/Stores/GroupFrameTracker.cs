using System.Runtime.InteropServices;
using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.Resources.Catalog;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.SceneRuntime.Stores;

public enum GroupFrameAuraKind : byte
{
    Mine,
    Debuff,
    // Put there by another player or their summon.
    Other
}

// IsBossTarget: the boss is after them (see CopyBossTargetsTo). Deaths: in the current boss fight.
public readonly record struct GroupFrameMember(int EntityId, string Nickname, bool IsLocalPlayer, long? CurrentHp, long? MaxHp, long Shield = 0, bool IsBossTarget = false, int Deaths = 0);

// SkillId is the effect's raw id when the game data doesn't know it.
public readonly record struct GroupFrameAura(int TargetEntityId, int SkillId, GroupFrameAuraKind Kind, long? ExpiresAtMilliseconds);

// One shield on a member with what's left of it; it ends with its aura (ExpiresAt null: not known yet, or no end).
public readonly record struct GroupFrameShield(int TargetEntityId, long Remaining, long? ExpiresAtMilliseconds);

// The local player's party with HP and the auras on them, for the group frame. The scene stores start over on every
// meter reset; this keeps going. Fed with every journal entry, at its capture time (Unix ms).
public sealed class GroupFrameTracker
{
    // Party status (0x1B92; force status 0x2B96) re-sends HP about once a second and often still shows it from before
    // a heal, while 0x008D comes on every change as long as the member is in sight. So status HP waits this long.
    internal const long StatusHpHoldMilliseconds = 2_000;
    // The party list (0x0092) comes every ~20 s and whenever the party changes, but the parser misses a member or two,
    // who still get party status. A member counts while in the newest list or given party status since.
    internal const long RosterGraceMilliseconds = 5_000;
    // Party status pauses for up to ~50 s on idle members, and nothing is sent when someone leaves without a new list.
    internal const long MemberTimeoutMilliseconds = 120_000;
    private const long AuraPruneMilliseconds = 30_000;
    // Shields come as pools (0x0438 periodic mode 9 grant, 11 what's left, 10 close). The pool's chain id is the instance
    // sequence of the shield's own aura on the target, which ends the moment the shield breaks (2C38 result 7) or runs
    // out. Until that aura shows up, a shield counts for this long after the grant, or while an aura of its skill is on.
    internal const long ShieldAuraGraceMilliseconds = 2_000;
    // A boss aims (0x0238 flag 2) every 3-5 s, but goes up to ~30 s without in some phases; its last target holds this
    // long, or until the boss is down.
    internal const long BossAimHoldMilliseconds = 20_000;

    private readonly Dictionary<int, Vital> _vitals = [];
    private readonly Dictionary<int, PartySighting> _party = [];
    private readonly AuraStore _auras = new();
    // Who put each aura on, with which skill: a refresh (0x2B38) overwrites both in the store, the origin with an id
    // that is no entity.
    private readonly Dictionary<AuraInstanceKey, AuraOpener> _openers = [];
    private readonly HashSet<int> _members = [];
    private readonly List<AuraInstanceState> _activeAuras = [];
    private readonly Dictionary<PeriodicPoolKey, ShieldPool> _shieldPools = [];
    // When the latest aura of each skill on a member ends (long.MaxValue: no end).
    private readonly Dictionary<(int TargetEntityId, int SkillId), long> _activeAuraSkills = [];
    private readonly Dictionary<int, long> _shields = [];
    private readonly List<(long GrantedAt, GroupFrameShield Shield)> _shieldList = [];
    // Whom each caster's last aimed cast went at; which casters are bosses is checked when read.
    private readonly Dictionary<int, (int TargetEntityId, long At)> _aims = [];
    // Whom the boss is after per the game's boss frame (0x2092), 0 for no one or not sent.
    private int _bossTarget;
    private long? _rosterAt;
    private long _rosterSequence = -1;
    private long _nextPruneAt;

    // playerOf: the player behind an entity (see CopyAurasTo), to tell a player's heal from a hit.
    public void Apply(ObservedEventEntry entry, long atMilliseconds, Func<int, int>? playerOf = null)
    {
        switch (entry.Domain)
        {
            case ObservedEventDomain.EntityVital:
                ApplyVital(in entry.EntityVital, entry.Raw.Opcode, atMilliseconds);
                break;
            case ObservedEventDomain.State when entry.State.StateCode == StateCodes.PlayerGroupMembership:
                ApplyPartyList(in entry.State, in entry.Raw, atMilliseconds);
                break;
            case ObservedEventDomain.State when entry.State.StateCode == StateCodes.BossTarget:
                _bossTarget = entry.State.EntityId;
                // No one: a boss left the fight (down, reset); its older aims are over too. Another boss still at it
                // aims again within seconds.
                if (_bossTarget == 0)
                    _aims.Clear();
                break;
            case ObservedEventDomain.Aura:
                var transition = _auras.Apply(in entry.Aura, atMilliseconds, entry.Stamp.ObservationOrdinal);
                RememberOpener(in transition);
                FollowShieldAura(in transition);
                break;
            case ObservedEventDomain.Action:
                _auras.Apply(in entry.Action, atMilliseconds, entry.Stamp.ObservationOrdinal);
                break;
            case ObservedEventDomain.Combat:
                // A 0x0238 cast names its target in ChainId; flag 2 marks the aimed ones (attacks, targeted spells).
                if (entry.Raw.Opcode == 0x0238 && entry.Combat.Flag == 2 && entry.Combat.ChainId > 0)
                    _aims[entry.SourceEntityId] = (entry.Combat.ChainId, atMilliseconds);
                ApplyShieldPool(entry.SourceEntityId, entry.TargetEntityId, in entry.Combat, atMilliseconds, playerOf);
                break;
            case ObservedEventDomain.Scene when entry.Scene.Kind == SceneObservationKind.MapContextStarted:
                // Everyone gets a new entity id on the new map.
                _vitals.Clear();
                _party.Clear();
                _auras.Clear();
                _openers.Clear();
                _shieldPools.Clear();
                _aims.Clear();
                _bossTarget = 0;
                _rosterAt = null;
                break;
        }

        if (atMilliseconds >= _nextPruneAt)
        {
            _auras.RemoveExpired(atMilliseconds - AuraPruneMilliseconds);
            foreach (var key in _openers.Keys)
            {
                if (!_auras.TryGet(key, out _))
                    _openers.Remove(key);
            }

            _nextPruneAt = atMilliseconds + AuraPruneMilliseconds;
        }
    }

    // You first, then the party in its list order; members only seen in party status come after, as they showed up.
    public void CopyMemberIdsTo(int localPlayerEntityId, long nowMilliseconds, List<int> destination)
    {
        destination.Clear();
        foreach (var (entityId, sighting) in _party)
        {
            if (entityId != localPlayerEntityId &&
                (_rosterAt is not long rosterAt || sighting.LastSeenAt >= rosterAt - RosterGraceMilliseconds) &&
                nowMilliseconds - sighting.LastSeenAt <= MemberTimeoutMilliseconds)
            {
                destination.Add(entityId);
            }
        }

        destination.Sort((left, right) =>
        {
            var a = _party[left];
            var b = _party[right];
            var comparison = (a.Slot ?? int.MaxValue).CompareTo(b.Slot ?? int.MaxValue);
            return comparison != 0 ? comparison : a.FirstSeenAt.CompareTo(b.FirstSeenAt);
        });
        if (localPlayerEntityId > 0)
            destination.Insert(0, localPlayerEntityId);
    }

    // Max HP is only sent at zone-in and when buffs change it; until then the highest HP seen stands in.
    public void GetHp(int entityId, out long? currentHp, out long? maxHp)
    {
        if (_vitals.TryGetValue(entityId, out var vital))
        {
            currentHp = vital.Hp;
            maxHp = vital.MaxHp ?? vital.PeakHp;
        }
        else
        {
            currentHp = null;
            maxHp = null;
        }
    }

    // Debuffs: NPC or "abnormal" skills (an NPC's debuff names the target itself as its origin, so these come first),
    // or an effect the game data doesn't know that anything but a player or a player's summon put there; your own
    // auras on the members (your summons' too); and what other players put there. playerOf gives the player behind an
    // entity (itself, or a summon's owner), 0 for anything else. Also works out the shields (GetShield).
    public void CopyAurasTo(int localPlayerEntityId, IReadOnlyList<int> memberIds, long nowMilliseconds, Func<int, int> playerOf, List<GroupFrameAura> destination)
    {
        destination.Clear();
        _members.Clear();
        foreach (var memberId in memberIds)
            _members.Add(memberId);

        _auras.CopyActiveAurasTo(_members, nowMilliseconds, _activeAuras);
        _activeAuraSkills.Clear();
        foreach (var aura in _activeAuras)
        {
            var opener = _openers.TryGetValue(aura.Key, out var known) ? known : new AuraOpener(aura.OriginEntityId, aura.ResourceEffectRef);
            var skillId = ResolveSkillId(opener.EffectRef, out var category);
            var endsAt = aura.ExpiresAtMilliseconds ?? long.MaxValue;
            if (!_activeAuraSkills.TryGetValue((aura.TargetEntityId, skillId), out var latestEndsAt) || latestEndsAt < endsAt)
                _activeAuraSkills[(aura.TargetEntityId, skillId)] = endsAt;
            var origin = opener.OriginEntityId;
            var player = origin > 0 ? playerOf(origin) : 0;
            GroupFrameAuraKind kind;
            if (category is SkillCategory.Npc or SkillCategory.Abnormal ||
                category == SkillCategory.Unknown && origin > 0 && player == 0)
                kind = GroupFrameAuraKind.Debuff;
            else if (localPlayerEntityId > 0 && (origin == localPlayerEntityId || player == localPlayerEntityId))
                kind = GroupFrameAuraKind.Mine;
            // ponytail: in PvP an enemy player's debuff lands here too; the game's buff/debuff flag per effect would
            // tell them apart, but the shipped data doesn't resolve it for these auras.
            else if (player > 0)
                kind = GroupFrameAuraKind.Other;
            else
                continue;

            if (skillId > 0)
                destination.Add(new GroupFrameAura(aura.TargetEntityId, skillId, kind, aura.ExpiresAtMilliseconds));
        }

        _shields.Clear();
        _shieldList.Clear();
        foreach (var (key, pool) in _shieldPools)
        {
            bool counts;
            long? expiresAt = null;
            if (pool.HasOwnAura)
            {
                counts = _auras.TryGet(new AuraInstanceKey(pool.TargetEntityId, pool.ChainId), out var own) && own.IsActiveAt(nowMilliseconds);
                expiresAt = own.ExpiresAtMilliseconds;
            }
            else if (_activeAuraSkills.TryGetValue((pool.TargetEntityId, pool.SkillId), out var skillEndsAt))
            {
                counts = true;
                expiresAt = skillEndsAt == long.MaxValue ? null : skillEndsAt;
            }
            else
            {
                counts = nowMilliseconds - pool.GrantedAt <= ShieldAuraGraceMilliseconds;
            }

            if (!counts)
            {
                _shieldPools.Remove(key);
                continue;
            }

            _shields[pool.TargetEntityId] = _shields.GetValueOrDefault(pool.TargetEntityId) + pool.Remaining;
            if (pool.Remaining > 0)
                _shieldList.Add((pool.GrantedAt, new GroupFrameShield(pool.TargetEntityId, pool.Remaining, expiresAt)));
        }

        _shieldList.Sort(static (left, right) => left.GrantedAt.CompareTo(right.GrantedAt));
    }

    // Whom the bosses are after: the boss frame's target (0x2092) as soon as the boss turns to someone, seconds before
    // it attacks them. The game sends that only for dungeon bosses, and one at a time (a second boss leaving the fight
    // clears it), so otherwise the target of each boss's last aimed cast. Only bosses, so no player in PvP.
    public void CopyBossTargetsTo(long nowMilliseconds, Func<int, bool> isBoss, HashSet<int> destination)
    {
        destination.Clear();
        if (_bossTarget > 0)
        {
            destination.Add(_bossTarget);
            return;
        }

        foreach (var (casterId, aim) in _aims)
        {
            if (nowMilliseconds - aim.At > BossAimHoldMilliseconds)
                _aims.Remove(casterId);
            else if (isBoss(casterId))
                destination.Add(aim.TargetEntityId);
        }
    }

    // The shields with something left, as of the last CopyAurasTo, the oldest (the first to take a hit) first.
    public void CopyShieldsTo(List<GroupFrameShield> destination)
    {
        destination.Clear();
        foreach (var (_, shield) in _shieldList)
            destination.Add(shield);
    }

    private void FollowShieldAura(in AuraLifecycleTransition transition)
    {
        if (transition.Kind is not (AuraLifecycleEventKind.Open or AuraLifecycleEventKind.Result) || _shieldPools.Count == 0)
            return;

        foreach (var (key, pool) in _shieldPools)
        {
            if (pool.TargetEntityId != transition.State.TargetEntityId || pool.ChainId != transition.State.InstanceSequenceId)
                continue;
            if (transition.Kind == AuraLifecycleEventKind.Result)
                _shieldPools.Remove(key);
            else
                pool.HasOwnAura = true;
        }
    }

    // The game hardly ever says what's left of a shield, but the hits carry the full damage before it: direct hits wear
    // the shields down, the oldest first. Damage over time goes straight to HP.
    private void AbsorbDirectHit(int sourceId, int targetId, in CombatWireObservation observation, Func<int, int>? playerOf)
    {
        if (observation.Damage <= 0 || sourceId == targetId || observation.ResourceKind != CombatResourceKind.Unknown ||
            (observation.Modifiers & (DamageModifiers.Evade | DamageModifiers.Invincible)) != 0 || !HasShieldLeft(targetId))
        {
            return;
        }

        // Heals and shields are direct values too, and so is what a player's skill does that the game data doesn't call
        // damage (Benevolence sends 20,000 that is no hit).
        var evidence = CombatSemanticEvidenceResolver.Evaluate(in observation);
        if (evidence.Candidate is { } candidate ? candidate.Metric != CombatMetricKind.Damage : playerOf?.Invoke(sourceId) > 0)
            return;

        var damage = observation.Damage;
        while (damage > 0)
        {
            ShieldPool? oldest = null;
            foreach (var pool in _shieldPools.Values)
            {
                if (pool.TargetEntityId == targetId && pool.Remaining > 0 && (oldest is null || pool.GrantedAt < oldest.GrantedAt))
                    oldest = pool;
            }

            if (oldest is null)
                return;

            var absorbed = Math.Min(damage, oldest.Remaining);
            oldest.Remaining -= absorbed;
            damage -= absorbed;
        }
    }

    private bool HasShieldLeft(int targetId)
    {
        foreach (var pool in _shieldPools.Values)
        {
            if (pool.TargetEntityId == targetId && pool.Remaining > 0)
                return true;
        }

        return false;
    }

    // What's left of the shields on a member, as of the last CopyAurasTo.
    public long GetShield(int entityId) => _shields.GetValueOrDefault(entityId);

    private void ApplyShieldPool(int sourceId, int targetId, in CombatWireObservation observation, long atMilliseconds, Func<int, int>? playerOf)
    {
        if (targetId <= 0)
            return;

        if (observation.PeriodicRelation == PeriodicEffectRelation.None)
        {
            AbsorbDirectHit(sourceId, targetId, in observation, playerOf);
            return;
        }

        if (observation.ChainId == 0)
            return;

        var key = PeriodicPoolKey.Create(targetId, in observation);
        switch (observation.PeriodicMode)
        {
            case 9 when observation.Damage > 0 && sourceId > 0:
                var evidence = CombatSemanticEvidenceResolver.Evaluate(in observation);
                if (CombatSemanticEvidenceResolver.IsAuthoritativePeriodicShield(in evidence))
                {
                    _shieldPools[key] = new ShieldPool(targetId, sourceId, evidence.Resource.Slot?.SkillId ?? observation.SkillCode, observation.ChainId, observation.Damage, atMilliseconds)
                    {
                        HasOwnAura = _auras.TryGet(new AuraInstanceKey(targetId, observation.ChainId), out _)
                    };
                }
                else
                {
                    _shieldPools.Remove(key);
                }

                break;
            case 11 when _shieldPools.TryGetValue(key, out var pool):
                // Its caster ticking it means a pool of healing, not a shield.
                if (sourceId == pool.CasterEntityId)
                    _shieldPools.Remove(key);
                else
                    pool.Remaining = Math.Max(0, observation.Damage);
                break;
            case 10:
                _shieldPools.Remove(key);
                break;
        }
    }

    private void RememberOpener(in AuraLifecycleTransition transition)
    {
        if (transition.HasPreviousState)
            _openers.Remove(transition.PreviousState.Key);
        if (transition.Kind == AuraLifecycleEventKind.Open)
            _openers[transition.State.Key] = new AuraOpener(transition.State.OriginEntityId, transition.State.ResourceEffectRef);
    }

    private void ApplyVital(in EntityVitalObservation observation, ushort opcode, long atMilliseconds)
    {
        if (observation.EntityId <= 0)
            return;

        // A boss that's down is after no one.
        if (observation.CurrentHp <= 0)
            _aims.Remove(observation.EntityId);

        var isStatus = opcode is 0x1B92 or 0x2B96;
        if (opcode == 0x1B92)
            SightPartyMember(observation.EntityId, atMilliseconds, slot: null);

        ref var vital = ref CollectionsMarshal.GetValueRefOrAddDefault(_vitals, observation.EntityId, out _);
        if (observation.MaxHp is > 0)
            vital.MaxHp = observation.MaxHp;
        if (isStatus && vital.DirectAt is long directAt && atMilliseconds - directAt < StatusHpHoldMilliseconds)
            return;

        vital.Hp = Math.Max(0, observation.CurrentHp);
        vital.PeakHp = Math.Max(vital.PeakHp ?? 0, vital.Hp);
        if (!isStatus)
            vital.DirectAt = atMilliseconds;
    }

    private void ApplyPartyList(in StateObservation state, in RawPacketReference raw, long atMilliseconds)
    {
        if (raw.Opcode != 0x0092 || state.GroupMembership.Kind != PlayerGroupKind.Party || state.EntityId <= 0)
            return;

        if (raw.CaptureSequence != _rosterSequence)
        {
            _rosterSequence = raw.CaptureSequence;
            _rosterAt = atMilliseconds;
        }

        SightPartyMember(state.EntityId, atMilliseconds, state.GroupMembership.MemberSlotIndex);
    }

    private void SightPartyMember(int entityId, long atMilliseconds, int? slot)
    {
        ref var sighting = ref CollectionsMarshal.GetValueRefOrAddDefault(_party, entityId, out var exists);
        if (!exists)
            sighting.FirstSeenAt = atMilliseconds;
        sighting.LastSeenAt = atMilliseconds;
        if (slot is not null)
            sighting.Slot = slot;
    }

    private static int ResolveSkillId(ResourceEffectRef effectRef, out SkillCategory category)
    {
        var skillId = CombatResourceRegistry.TryResolveSkillIdByEffectRef(effectRef, out var ownerSkillId)
            ? ownerSkillId
            : effectRef.RawId <= int.MaxValue ? (int)effectRef.RawId : 0;
        category = CombatResourceRegistry.SkillDisplayMap.TryGetValue(skillId, out var skill) ? skill.Category : SkillCategory.Unknown;
        return skillId;
    }

    private readonly record struct AuraOpener(int OriginEntityId, ResourceEffectRef EffectRef);

    private sealed class ShieldPool(int targetEntityId, int casterEntityId, int skillId, int chainId, long remaining, long grantedAt)
    {
        public int TargetEntityId { get; } = targetEntityId;
        public int CasterEntityId { get; } = casterEntityId;
        public int SkillId { get; } = skillId;
        public int ChainId { get; } = chainId;
        public long GrantedAt { get; } = grantedAt;
        public long Remaining { get; set; } = remaining;
        public bool HasOwnAura { get; set; }
    }

    private struct Vital
    {
        public long Hp;
        public long? MaxHp;
        public long? PeakHp;
        public long? DirectAt;
    }

    private struct PartySighting
    {
        public long FirstSeenAt;
        public long LastSeenAt;
        public int? Slot;
    }
}
