using System.Drawing;
using Cloris.Aion2Flow.Presentation;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Services.Settings;

public sealed class AppSettings
{
    public TopmostMode TopmostMode { get; set; } = TopmostMode.GameForeground;

    public int MaxVisibleCombatantRows { get; set => field = Math.Clamp(value, 5, 10); } = 5;

    public CombatantSortMetric CombatantSortMetric { get; set; } = CombatantSortMetric.DamagePerSecond;

    // Off: the highest first.
    public bool CombatantSortAscending { get; set; }

    public CombatantStatisticsScope CombatantStatisticsScope { get; set; } = CombatantStatisticsScope.All;

    public SceneKind SceneKind { get; set; } = SceneKind.Standard;

    public bool UseCompactMainMetrics { get; set; } = true;

    public bool ShowDamagePerSecondColumn { get; set; } = true;

    public bool ShowDamageColumn { get; set; } = true;

    public bool ShowHealingPerSecondColumn { get; set; }

    public bool ShowHealingColumn { get; set; }

    public bool IncludeOverhealing { get; set; } = true;

    public bool ShowDamageTakenColumn { get; set; }

    public bool ShowUnhealedHealthColumn { get; set; }

    public bool ShowAggroColumn { get; set; }
    public bool ShowStaggerColumn { get; set; }

    public bool ShowBossShareColumn { get; set; } = true;

    public bool ShowTotalDamagePerSecond { get; set; } = true;

    public bool ShowColumnHeaders { get; set; } = true;

    public bool BossSectionExpanded { get; set; } = true;

    // 0 = none; with one set, the boss section shows the DPS needed to kill the boss in time.
    public int BossTimeLimitMinutes { get; set => field = Math.Clamp(value, 0, 30); }

    public EncounterTimeDisplayFormat EncounterTimeDisplayFormat
    {
        get;
        set => field = Enum.IsDefined(value)
            ? value
            : EncounterTimeDisplayFormat.DecimalSeconds;
    } = EncounterTimeDisplayFormat.DecimalSeconds;

    public bool ShowFocusStatusBar { get; set; } = true;

    public bool HideHeaderWhenClickThrough { get; set; }

    public bool ShowPlayerNames { get; set; } = true;

    public PlayerSelfMarkerDisplayMode PlayerSelfMarkerDisplayMode { get; set; } = PlayerSelfMarkerDisplayMode.WhenNamesHidden;

    public bool ShowPlayerShortServerName { get; set; }

    public bool ShowPlayerLegionName { get; set; }

    public bool TintPlayerNamesByFaction { get; set; } = true;

    public bool SkillMonitorEnabled { get; set; } = true;

    public bool SkillMonitorBuffSelectAll { get; set; } = true;

    public List<int> SkillMonitorBuffSkillIds { get; set => field = value ?? []; } = [];

    public bool SkillMonitorCooldownSelectAll { get; set; } = true;

    public List<int> SkillMonitorCooldownSkillIds { get; set => field = value ?? []; } = [];

    public int SkillMonitorScalePercent { get; set => field = Math.Clamp(value, 50, 200); } = 100;

    public Point? SkillMonitorPosition { get; set; }

    public int SkillMonitorWidth { get; set => field = Math.Clamp(value, 100, 4_096); } = 680;

    public bool GroupFrameEnabled { get; set; }

    // The frame's top left corner; its top right one with the auras on the left, as it grows to the left then.
    public Point? GroupFramePosition { get; set; }

    public int GroupFrameScalePercent { get; set => field = Math.Clamp(value, 50, 200); } = 100;

    public bool GroupFrameShowMyBuffs { get; set; } = true;

    public bool GroupFrameShowDebuffs { get; set; } = true;

    public bool GroupFrameShowOtherBuffs { get; set; }

    public bool GroupFrameShowPermanentBuffs { get; set; } = true;

    // The group frame's shield bar from the right instead of the left.
    public bool GroupFrameShieldOnRight { get; set; }

    // Each shield on its own, blinking for its last 3 s; off: one value and bar for all.
    public bool GroupFrameShieldAnimation { get; set; } = true;

    // The distance beside each member's arrow: green up to the first mark, orange up to the second, red up to the range;
    // past the range a member is out of range (dimmed).
    public int GroupFrameGreenMeters { get; set => field = Math.Clamp(value, 1, 200); } = 25;

    public int GroupFrameOrangeMeters { get; set => field = Math.Clamp(value, 1, 200); } = 35;

    public int GroupFrameRangeMeters { get; set => field = Math.Clamp(value, 1, 200); } = 40;

    // Each member's deaths in the current boss fight beside their name.
    public bool GroupFrameShowDeaths { get; set; } = true;

    // The Hidden Cube finder: off, shown, or shown with a sound for each new cube (experimental).
    public GroupFrameCubeFinder GroupFrameCubeFinder { get; set; } = GroupFrameCubeFinder.Show;

    // Where the group frame shows each member's auras, arrow and distance: right of the HP bar, left of it or not at all.
    public GroupFrameSide GroupFrameAuraSide { get; set; }

    public GroupFrameSide GroupFrameArrowSide { get; set; }

    public GroupFrameSide GroupFrameDistanceSide { get; set; }

    // How see-through the group frame's HP bars, auras, arrows and distances are: 100% is their own look, lower fades them.
    public int GroupFrameHpOpacityPercent { get; set => field = Math.Clamp(value, 10, 100); } = 100;

    public int GroupFrameAuraOpacityPercent { get; set => field = Math.Clamp(value, 10, 100); } = 100;

    public int GroupFrameArrowOpacityPercent { get; set => field = Math.Clamp(value, 10, 100); } = 100;

    public int GroupFrameDistanceOpacityPercent { get; set => field = Math.Clamp(value, 10, 100); } = 100;

    // The boss alert sound: off at first; how loud; which, by name (null: the chime); the user's own, newest first, at
    // most five (their WAV files are in "alert-sounds" beside the settings).
    public bool GroupFrameAlertEnabled { get; set; }

    // 100: as loud as the sound is; up to 200 for quiet ones of the user's own.
    public int GroupFrameAlertVolumePercent { get; set => field = Math.Clamp(value, 0, 200); } = 100;

    // No new alert this many seconds after the last one: the boss's target can flip back and forth.
    public int GroupFrameAlertQuietSeconds { get; set => field = Math.Clamp(value, 0, 10); } = 5;

    public string? GroupFrameAlertSound { get; set; }

    public List<string> GroupFrameAlertSounds { get; set => field = value ?? []; } = [];

    // The fight graphs frame: off at first; its top left corner.
    public bool FightGraphsEnabled { get; set; }

    public Point? FightGraphsPosition { get; set; }

    // The frame's own size, apart from the main UI scale (like the group frame's).
    public int FightGraphsScalePercent { get; set => field = Math.Clamp(value, 50, 200); } = 100;

    // The metric that picks the best pull of a boss, drawn dotted in each graph (see BestRunStore).
    public Cloris.Aion2Flow.SceneRuntime.Stores.FightMetric FightGraphsBestMetric { get; set; }

    // Each frame's own opacity and size (the settings' Visuals): 20-100 % and 50-200 %.
    public int FightGraphsOpacityPercent { get; set => field = Math.Clamp(value, 20, 100); } = 100;

    public int SkillMonitorOpacityPercent { get; set => field = Math.Clamp(value, 20, 100); } = 100;

    public int TwinLibraryScalePercent { get; set => field = Math.Clamp(value, 50, 200); } = 100;

    public int TwinLibraryOpacityPercent { get; set => field = Math.Clamp(value, 20, 100); } = 100;



    // The settings window's top left corner.
    public Point? SettingsWindowPosition { get; set; }

    // A curve per party member in the metric graphs, and the other members' deaths marked as well as the player's.
    public bool FightGraphsShowMembers { get; set; }

    public bool FightGraphsShowOtherDeaths { get; set; }

    // The curves drawn as an average of the seconds around each (less spiky); the hover keeps each second's own values.
    public bool FightGraphsSmoothCurves { get; set; }

    // The overview above the metric graphs, and the metrics it leaves out (FightMetric names).
    public bool FightGraphsShowOverview { get; set; } = true;

    public List<string> FightGraphsOverviewHidden { get; set => field = value ?? []; } = [];

    // Curves the metric graphs leave out (You, Party, Ping) and the graphs folded away (FightMetric names).
    public List<string> FightGraphsHiddenCurves { get; set => field = value ?? []; } = [];

    public List<string> FightGraphsFolded { get; set => field = value ?? []; } = [];

    // The comparison's "Equal time scale": a second as wide in every graph.
    public bool FightGraphsCompareEqualTime { get; set; }

    // The overlay's frames that take clicks while the game has focus (SettingsFlyoutViewModel.ClickableFrames); the others
    // let the mouse through until the overlay has focus. The twin library and the playback always took clicks.
    public List<string> ClickableWithoutFocus { get; set => field = value ?? []; } = ["TwinLibrary", "Playback"];

    public int UiScalePercent { get; set => field = Math.Clamp(value, 50, 200); } = 100;

    public int OverlayOpacityPercent { get; set => field = Math.Clamp(value, 20, 100); } = 60;

    public int DetailOpacityPercent { get; set => field = Math.Clamp(value, 20, 100); } = 60;

    // Updates from the overlay's release page: off, only "Update now"; on, a check and download at every start and the
    // install when the overlay closes.
    public bool UpdateAutomatically { get; set; }

    // Digital twins: on, party members are looked up on the game's official character website as they show up in the meter.
    public bool LookUpDigitalTwins { get; set; }

    // The player's character from the last time the game named it (0x3336), so an overlay started in the middle of a
    // session knows the player (see RuntimeMetadataRegistry.RememberLocalPlayerIdentity).
    public int? LocalPlayerServerId { get; set; }

    public string? LocalPlayerNickname { get; set; }

    public string? Language { get; set; }

    public uint? BattleResetHotkeyModifiers { get; set; }

    public uint? BattleResetHotkeyVirtualKey { get; set; }

    public uint? OverlayInteractionHotkeyModifiers { get; set; }

    public uint? OverlayInteractionHotkeyVirtualKey { get; set; }

    public Point? MainWindowPosition { get; set; }

    // The meter folded to its title and bottom bar (a click on the title).
    public bool MeterFolded { get; set; }
}
