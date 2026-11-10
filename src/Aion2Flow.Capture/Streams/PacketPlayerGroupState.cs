namespace Cloris.Aion2Flow.Capture.Streams;

internal sealed class PacketPlayerGroupState
{
    private readonly HashSet<int> _observedPartyStatusMemberIds = [];
    private readonly HashSet<int> _observedForceStatusMemberIds = [];
    // Not cleared by Reset: a zone change sends the player's max HP just before the new map scope drops it.
    private readonly Dictionary<int, long> _statMaxHp = [];

    public bool TryRegisterPartyStatusMember(int entityId) => entityId > 0 && _observedPartyStatusMemberIds.Add(entityId);

    public bool TryRegisterForceStatusMember(int entityId) => entityId > 0 && _observedForceStatusMemberIds.Add(entityId);

    public void RememberStatMaxHp(int entityId, long maxHp) => _statMaxHp[entityId] = maxHp;

    public bool TryGetStatMaxHp(int entityId, out long maxHp) => _statMaxHp.TryGetValue(entityId, out maxHp);

    public void Reset()
    {
        _observedPartyStatusMemberIds.Clear();
        _observedForceStatusMemberIds.Clear();
    }
}
