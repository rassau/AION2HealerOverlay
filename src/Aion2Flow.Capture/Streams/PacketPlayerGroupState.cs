using System.Buffers.Binary;

namespace Cloris.Aion2Flow.Capture.Streams;

internal sealed class PacketPlayerGroupState
{
    private readonly HashSet<int> _observedPartyStatusMemberIds = [];
    private readonly HashSet<int> _observedForceStatusMemberIds = [];
    // Not cleared by Reset: a zone change sends the player's max HP just before the new map scope drops it.
    private readonly Dictionary<int, long> _statMaxHp = [];
    // The map-arrival backup (PacketStateHandler.ParseMapArrivalByPosition): the x, y, z the last 0x2136 announced, as
    // sent (12 bytes), and when. Not cleared by Reset either: a map scope can start between announcement and arrival.
    private readonly byte[] _announcedSpot = new byte[12];
    private long _announcedAt;
    private bool _arrivalBackupArmed;
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

    // A new announcement replaces the last one.
    public void ArmArrivalBackup(uint x, uint y, uint z, long atMilliseconds)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_announcedSpot, x);
        BinaryPrimitives.WriteUInt32LittleEndian(_announcedSpot.AsSpan(4), y);
        BinaryPrimitives.WriteUInt32LittleEndian(_announcedSpot.AsSpan(8), z);
        _announcedAt = atMilliseconds;
        // Twelve zero bytes would turn up in almost any packet.
        _arrivalBackupArmed = (x | y | z) != 0;
    }

    public void DisarmArrivalBackup() => _arrivalBackupArmed = false;

    // Whether a packet body holds the announced spot, at most 60 s after the announcement (in 24 recordings 46 of 47
    // arrivals came 0.55-5.5 s after it, one 33 s; a slower PC may take longer to load). A packet later than that, or
    // earlier, ends the wait.
    public bool MatchArrivalBackup(ReadOnlySpan<byte> body, long atMilliseconds)
    {
        if (!_arrivalBackupArmed)
            return false;

        var waited = atMilliseconds - _announcedAt;
        if (waited is < 0 or > 60_000)
        {
            _arrivalBackupArmed = false;
            return false;
        }

        return body.IndexOf(_announcedSpot) >= 0;
    }

    public void Reset()
    {
        _observedPartyStatusMemberIds.Clear();
        _observedForceStatusMemberIds.Clear();
        _positionJournaledAt.Clear();
    }
}
