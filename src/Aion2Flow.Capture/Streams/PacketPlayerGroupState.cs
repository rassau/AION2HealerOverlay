namespace Cloris.Aion2Flow.Capture.Streams;

internal sealed class PacketPlayerGroupState
{
    private readonly HashSet<int> _observedPartyStatusMemberIds = [];
    private readonly HashSet<int> _observedForceStatusMemberIds = [];
    // Not cleared by Reset: a zone change sends the player's max HP just before the new map scope drops it.
    private readonly Dictionary<int, long> _statMaxHp = [];
    // When each member's position last went into the journal.
    private readonly Dictionary<int, long> _positionJournaledAt = [];

    // While the player is unknown: who last cast each skill (0x0238), and whom the own-skill packets (0x0138) pointed at.
    private readonly Dictionary<uint, (int SourceId, long At)> _lastCasters = [];
    private (int Id, int Matches) _ownSkillCaster;

    // The recording player (0x3336): they get no status of their own, so they aren't among the members seen in it.
    public int LocalPlayerId { get; set; }

    public void RememberCast(int sourceId, uint skill, long atMilliseconds)
    {
        if (LocalPlayerId == 0)
            _lastCasters[skill] = (sourceId, atMilliseconds);
    }

    // The caster of the skill an own-skill packet names, once two in a row named the same one: the player (0 until then).
    public int MatchOwnSkill(uint skill, long atMilliseconds)
    {
        if (LocalPlayerId != 0 || !_lastCasters.TryGetValue(skill, out var cast) || Math.Abs(atMilliseconds - cast.At) > 100)
            return 0;

        _ownSkillCaster = (cast.SourceId, _ownSkillCaster.Id == cast.SourceId ? _ownSkillCaster.Matches + 1 : 1);
        return _ownSkillCaster.Matches >= 2 ? cast.SourceId : 0;
    }

    public bool TryRegisterPartyStatusMember(int entityId) => entityId > 0 && _observedPartyStatusMemberIds.Add(entityId);

    public bool TryRegisterForceStatusMember(int entityId) => entityId > 0 && _observedForceStatusMemberIds.Add(entityId);

    public bool IsGroupMember(int entityId) =>
        entityId > 0 && (entityId == LocalPlayerId || _observedPartyStatusMemberIds.Contains(entityId) || _observedForceStatusMemberIds.Contains(entityId));

    // A group member's position goes into the journal at most once a second.
    public bool TryTakePositionSlot(int entityId, long atMilliseconds)
    {
        if (_positionJournaledAt.TryGetValue(entityId, out var last) && atMilliseconds - last < 1_000 && atMilliseconds >= last)
            return false;

        _positionJournaledAt[entityId] = atMilliseconds;
        return true;
    }

    public void RememberStatMaxHp(int entityId, long maxHp) => _statMaxHp[entityId] = maxHp;

    public bool TryGetStatMaxHp(int entityId, out long maxHp) => _statMaxHp.TryGetValue(entityId, out maxHp);

    public void Reset()
    {
        _observedPartyStatusMemberIds.Clear();
        _observedForceStatusMemberIds.Clear();
        _positionJournaledAt.Clear();
    }
}
