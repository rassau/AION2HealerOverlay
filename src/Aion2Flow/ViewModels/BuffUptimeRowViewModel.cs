namespace Cloris.Aion2Flow.ViewModels;

// One row of the details' Buffs tab: a buff (SkillCode: its skill with the picked specializations, which the row shows
// with its icon), whose it is, how long it was up on the player, and (for their own) on the group. Null shows as a dash:
// never on the player, or it reached no one else. Hovering shows what it does (see GroupFrameViewModel.DescribeAura).
public sealed record BuffUptimeRowViewModel(string Name, int CasterId, double? Uptime, double? GroupUptime,
    int SkillCode = 0, string? Effect = null, string? EffectNote = null, string? Specializations = null)
{
    public bool HasUptime => Uptime.HasValue;

    public double UptimeValue => Uptime ?? 0;

    public bool HasGroupUptime => GroupUptime.HasValue;

    public double GroupUptimeValue => GroupUptime ?? 0;
}
