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

    // Healing and HPS with overhealing (every heal in full) or only the HP restored; off at first.
    public bool IncludeOverhealing { get; set; }

    // The meter's bars coloured by class (every cleric alike), or each player their own colour; by class at first.
    public bool BarColorsByClass { get; set; } = true;

    public bool ShowDamageTakenColumn { get; set; }

    public bool ShowUnhealedHealthColumn { get; set; }

    // The aggro column as shown when last saved, for versions before AggroColumn; read once into it (on = Show, off = Auto).
    public bool ShowAggroColumn { get; set; }

    // Auto: shown while the player's character is a Gladiator or Templar.
    public AggroColumnMode? AggroColumn { get; set; }

    // The recovery time column (UHS ÷ damage taken); Auto: shown while the player's character is a Cleric.
    public AggroColumnMode RecoveryTimeColumn { get; set; }

    public bool ShowStaggerColumn { get; set; }

    // The kill columns (most damage, last hits, the game's credited kills), off at first.
    public bool ShowKillsColumn { get; set; }

    public bool ShowLastHitsColumn { get; set; }

    public bool ShowCreditedColumn { get; set; }

    public bool ShowBossShareColumn { get; set; } = true;

    public bool ShowTotalDamagePerSecond { get; set; } = true;

    public bool ShowColumnHeaders { get; set; } = true;

    public bool BossSectionExpanded { get; set; } = true;

    // A Sum row under each boss fight's players ("Add boss sum row"); off at first.
    public bool ShowBossSumRow { get; set; }

    // 0 = none; with one set, the boss section shows the DPS needed to kill the boss in time.
    public int BossTimeLimitMinutes { get; set => field = Math.Clamp(value, 0, 30); }

    // A training dummy pull's length (0 = until the user stops; only full-length pulls can be bests), and the pause that
    // ends one.
    public int DummyPullSeconds { get; set => field = Math.Clamp(value, 0, 600); }

    public int DummyCutoffSeconds { get; set => field = Math.Clamp(value, 1, 60); } = 5;

    // How often the game's FPS beside the ping and in its graph take a new value.
    public int FrameRatePollMilliseconds { get; set => field = Math.Clamp(value, 100, 2_000); } = 250;

    // The ping graph's and the FPS graph's lines drawn smooth (PingGraph.Smooth); on at first.
    public bool PingGraphSmooth { get; set; } = true;

    public bool FrameRateGraphSmooth { get; set; } = true;

    // The ping and FPS graphs' own size (100 %: both side by side as wide as the meter) and opacity (as the meter's).
    public int FooterGraphsScalePercent { get; set => field = Math.Clamp(value, 50, 200); } = 100;

    public int FooterGraphsOpacityPercent { get; set => field = Math.Clamp(value, 20, 100); } = 60;

    public EncounterTimeDisplayFormat EncounterTimeDisplayFormat
    {
        get;
        set => field = Enum.IsDefined(value)
            ? value
            : EncounterTimeDisplayFormat.DecimalSeconds;
    } = EncounterTimeDisplayFormat.DecimalSeconds;

    public bool ShowFocusStatusBar { get; set; } = true;

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

    // The debuffs shown on your target (yours, or everyone's with SkillMonitorDebuffsFromAnyone), by row base skill id;
    // all of them at first, like the buffs and cooldowns.
    public bool SkillMonitorDebuffSelectAll { get; set; } = true;

    public List<int> SkillMonitorDebuffSkillIds { get; set => field = value ?? []; } = [];

    // Off at first, as before them: the buffs on you show whoever cast them, on your target only your own (as debuffs).
    // On, about your target only: everyone's buffs there (yours too, as buffs) or everyone's debuffs there, buff or debuff as
    // the game data labels the skill, by the caster's side where it labels it both or neither
    // (SkillMonitorViewModel.RefreshSkillRows); what's on you and the cooldowns don't change (a buff on your target shows
    // only in a skill's slot with none on you, SkillMonitorSkillSlotBuilder).
    public bool SkillMonitorBuffsFromAnyone { get; set; }

    public bool SkillMonitorDebuffsFromAnyone { get; set; }

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

    // Buffs from scrolls, food and other items (the game data's Item category), yours and others'.
    public bool GroupFrameShowItemBuffs { get; set; } = true;

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

    // The overview's UHS, Taken and Rec.: the group's (UHS*) or the user's own; Auto, the group's for a cleric. UHS and Rec.
    // also pick what the best runs are ranked by; those always go by the user's own damage taken.
    public GroupValueMode FightGraphsUhs { get; set; }

    public GroupValueMode FightGraphsTaken { get; set; }

    public GroupValueMode FightGraphsRec { get; set; }

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

    // The overview's amounts (DPS, HPS, damage taken, UHS) on one scale in thousands instead of each to its own peak (100 %).
    public bool FightGraphsOverviewThousands { get; set; }

    // The overview marks the other members' deaths too, not only the user's (FightGraphsShowOtherDeaths: the metric graphs).
    public bool FightGraphsOverviewOtherDeaths { get; set; }

    // The folded frame's title line names the metrics the shown pull set new personal bests in.
    public bool FightGraphsNewBestsInTitle { get; set; } = true;

    // The overview above the metric graphs, and the metrics it leaves out (FightMetric names).
    public bool FightGraphsShowOverview { get; set; } = true;

    public List<string> FightGraphsOverviewHidden { get; set => field = value ?? []; } = [];

    // Curves the metric graphs leave out (You, Party, Ping) and the graphs folded away (FightMetric names).
    public List<string> FightGraphsHiddenCurves { get; set => field = value ?? []; } = [];

    public List<string> FightGraphsFolded { get; set => field = value ?? []; } = [];

    // Party members whose curves the user hid, by server and name ("2304:Name"), as their ids change with every map.
    // ponytail: grows only with members hidden by hand; trim to the newest few hundred if it ever matters.
    public List<string> FightGraphsHiddenMembers { get; set => field = value ?? []; } = [];

    // The comparison's "Equal time scale": a second as wide in every graph.
    public bool FightGraphsCompareEqualTime { get; set; }

    // The overlay's frames that take clicks while the game has focus (SettingsFlyoutViewModel.ClickableFrames); the others
    // let the mouse through until the overlay has focus. The twin library and the playback always took clicks, the meter
    // did in its old interactive mode.
    public List<string> ClickableWithoutFocus { get; set => field = value ?? []; } = ["TwinLibrary", "Playback", "Meter"];

    // The meter came into ClickableWithoutFocus on, in build 74: settings saved before don't have it there and get it once.
    public bool MeterClickableAdded { get; set; }

    // The frames whose title line takes clicks while the game has focus, also when they aren't clickable without focus (a
    // click there gives the overlay focus): the meter's at first, as before this setting.
    public List<string> TitleClickable { get; set => field = value ?? []; } = ["Meter"];

    public int UiScalePercent { get; set => field = Math.Clamp(value, 50, 200); } = 100;

    public int OverlayOpacityPercent { get; set => field = Math.Clamp(value, 20, 100); } = 60;

    public int DetailOpacityPercent { get; set => field = Math.Clamp(value, 20, 100); } = 60;

    // Updates from the overlay's release page: off, only "Update now"; on, a check and download at every start and the
    // install when the overlay closes.
    public bool UpdateAutomatically { get; set; }

    // Digital twins: on, party members are looked up on the game's official character website as they show up in the meter.
    public bool LookUpDigitalTwins { get; set; }

    // The twin library's "Update myself on overlay start": each character the player plays looked up once each time the
    // overlay starts, as soon as the game names it (see MainViewModel.LookUpMyselfOnce).
    public bool UpdateMyselfOnStart { get; set; }

    // "Use last battle's skill levels": a twin's skills at the levels the game sent in the player's last boss fight (the
    // details' tab: in the fight shown there), the website's for the rest (see SeenSkillLevels). On at first.
    public bool UseSeenSkillLevels { get; set; } = true;

    // The pause before each call to the official character website (Settings → Digital twin library), in 0.1 s steps; a
    // shorter one risks a block by the website.
    public double TwinLookupPauseSeconds { get; set => field = Math.Clamp(Math.Round(value, 1), 0.5, 10); } = 0.7;

    // The player's character from the last time the game named it (0x3336, or a packet naming the id that a claim by id
    // alone, 0x4a36 or 0x0138, points at), so an overlay started in the middle of a session knows the player (see
    // RuntimeMetadataRegistry.RememberLocalPlayerIdentity).
    public int? LocalPlayerServerId { get; set; }

    public string? LocalPlayerNickname { get; set; }

    public string? Language { get; set; }

    public uint? BattleResetHotkeyModifiers { get; set; }

    public uint? BattleResetHotkeyVirtualKey { get; set; }

    public uint? OverlayInteractionHotkeyModifiers { get; set; }

    public uint? OverlayInteractionHotkeyVirtualKey { get; set; }

    public uint? MinimizeAllHotkeyModifiers { get; set; }

    public uint? MinimizeAllHotkeyVirtualKey { get; set; }

    public uint? SaveSceneHotkeyModifiers { get; set; }

    public uint? SaveSceneHotkeyVirtualKey { get; set; }

    // Saved layouts of the frames (Settings → Scenes), in their order. NextSceneId only goes up, so a deleted scene's id
    // (its shortcut's, a workflow's) never comes back for a new one.
    public List<FrameScene> Scenes { get; set => field = value?.OfType<FrameScene>().ToList() ?? []; } = [];

    public int NextSceneId { get; set => field = Math.Max(1, value); } = 1;

    // The group frame's loot bag: whether it warns while the player's share of a boss stays under what its loot needs, and
    // that share in percent (0.5 steps).
    public bool BossParticipationWarning { get; set; } = true;

    public double BossParticipationPercent { get; set => field = Math.Clamp(Math.Round(value * 2) / 2, 0.5, 10); } = 3.5;

    // The tutorial ran to its end once: the meter's "?" goes (Settings → General starts it again).
    public bool TutorialDone { get; set; }

    // The frames as they were when the tutorial started, kept until it ends: should the overlay close mid-tour, the next
    // start puts them back.
    public FrameScene? TutorialRestore { get; set; }

    // And which frames were folded then (a frame that was off keeps its fold for when it's on again).
    public List<string>? TutorialRestoreMinimized { get; set; }

    // When something happens in game, a scene (Settings → Workflows), in their order.
    public List<SceneWorkflow> Workflows { get; set => field = value?.OfType<SceneWorkflow>().ToList() ?? []; } = [];

    public Point? MainWindowPosition { get; set; }

    // The meter folded to its title (a click on the title or its eye); for versions before MinimizedFrames, which has it too.
    public bool MeterFolded { get; set; }

    // The frames minimized with their eye, down to their name and the eye (see SettingsFlyoutViewModel.Frames).
    public List<string> MinimizedFrames { get; set => field = value ?? []; } = [];

    // The Hidden Cube finder's own frame; null in settings from before it had one, when the group frame's choice decides.
    public bool? HiddenCubesEnabled { get; set; }

    public bool? HiddenCubesSound { get; set; }

    public Point? HiddenCubesPosition { get; set; }

    public int HiddenCubesScalePercent { get; set => field = Math.Clamp(value, 50, 200); } = 100;

    public int HiddenCubesOpacityPercent { get; set => field = Math.Clamp(value, 20, 100); } = 100;
}
