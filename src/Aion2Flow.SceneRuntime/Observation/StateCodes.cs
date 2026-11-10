namespace Cloris.Aion2Flow.SceneRuntime.Observation;

public static class StateCodes
{
    public const int PlayerIdentity = 0;
    public const int LocalizedNpcName = -99;
    public const int NpcKind = -100;
    public const int NpcBattle = -101;
    public const int NpcBattleToggle = -102;
    public const int PlayerGroupMembership = -103;
    public const int Cooldown4738 = -104;
    public const int CooldownStart0238 = -105;
    public const int CooldownCharge2238 = -106;
    // A boss's stagger gauge: Value0 current, Value1 max (0 when it broke and the boss is staggered).
    public const int StaggerGauge = -107;
    // Whom the boss is after (0x2092), as the game's boss frame shows it: EntityId, 0 for no one. No boss id.
    public const int BossTarget = -108;
}
