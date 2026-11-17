using System.Text.Json;
using Cloris.Aion2Flow.Services.Logging;

namespace Cloris.Aion2Flow.Services.Settings;

public sealed class SettingsService
{
    private const string SettingsFileName = "settings.json";
    private readonly Lock _writeLock = new();
    private AppSettings _current;
    // The last save failed, mostly as another process (OneDrive, a virus scanner) held settings.json for a moment.
    private bool _unsaved;

    public AppSettings Current => _current;

    public string FilePath { get; }

    public event EventHandler? Changed;

    public SettingsService() : this(Path.Combine(WorkingDirectoryResolver.GetWorkingDirectory(), SettingsFileName))
    {
    }

    public SettingsService(string filePath)
    {
        FilePath = filePath;
        _current = Load();
    }


    public void Update(Action<AppSettings> mutate)
    {
        var snapshot = Clone(_current);
        mutate(snapshot);
        _current = snapshot;
        Save(snapshot);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // At exit, after the windows' last saves: a failed save is tried again, as those holds last milliseconds. Each save
    // writes all the settings, so a failed one only loses anything when no later save works.
    public void Flush()
    {
        for (var attempt = 0; _unsaved && attempt < 5; attempt++)
        {
            Thread.Sleep(50);
            Save(_current);
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            using var stream = File.OpenRead(FilePath);
            var settings = JsonSerializer.Deserialize(stream, AppSettingsJsonContext.Default.AppSettings);
            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Failed to load settings from '{FilePath}': {ex}");
            return new AppSettings();
        }
    }

    private void Save(AppSettings settings)
    {
        try
        {
            lock (_writeLock)
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var tempPath = FilePath + ".tmp";
                using (var stream = File.Create(tempPath))
                {
                    JsonSerializer.Serialize(stream, settings, AppSettingsJsonContext.Default.AppSettings);
                }

                File.Move(tempPath, FilePath, overwrite: true);
                _unsaved = false;
            }
        }
        catch (Exception ex)
        {
            _unsaved = true;
            AppLog.Write(AppLogLevel.Warning, $"Failed to save settings to '{FilePath}': {ex}");
        }
    }

    private static AppSettings Clone(AppSettings source) => new()
    {
        TopmostMode = source.TopmostMode,
        MaxVisibleCombatantRows = source.MaxVisibleCombatantRows,
        CombatantSortMetric = source.CombatantSortMetric,
        CombatantSortAscending = source.CombatantSortAscending,
        CombatantStatisticsScope = source.CombatantStatisticsScope,
        SceneKind = source.SceneKind,
        UseCompactMainMetrics = source.UseCompactMainMetrics,
        ShowDamagePerSecondColumn = source.ShowDamagePerSecondColumn,
        ShowDamageColumn = source.ShowDamageColumn,
        ShowHealingPerSecondColumn = source.ShowHealingPerSecondColumn,
        ShowHealingColumn = source.ShowHealingColumn,
        IncludeOverhealing = source.IncludeOverhealing,
        ShowDamageTakenColumn = source.ShowDamageTakenColumn,
        ShowUnhealedHealthColumn = source.ShowUnhealedHealthColumn,
        ShowAggroColumn = source.ShowAggroColumn,
        AggroColumn = source.AggroColumn,
        RecoveryTimeColumn = source.RecoveryTimeColumn,
        ShowStaggerColumn = source.ShowStaggerColumn,
        ShowBossShareColumn = source.ShowBossShareColumn,
        ShowTotalDamagePerSecond = source.ShowTotalDamagePerSecond,
        ShowColumnHeaders = source.ShowColumnHeaders,
        BossSectionExpanded = source.BossSectionExpanded,
        ShowBossSumRow = source.ShowBossSumRow,
        BossTimeLimitMinutes = source.BossTimeLimitMinutes,
        DummyPullSeconds = source.DummyPullSeconds,
        DummyCutoffSeconds = source.DummyCutoffSeconds,
        EncounterTimeDisplayFormat = source.EncounterTimeDisplayFormat,
        ShowFocusStatusBar = source.ShowFocusStatusBar,
        ShowPlayerNames = source.ShowPlayerNames,
        PlayerSelfMarkerDisplayMode = source.PlayerSelfMarkerDisplayMode,
        ShowPlayerShortServerName = source.ShowPlayerShortServerName,
        ShowPlayerLegionName = source.ShowPlayerLegionName,
        TintPlayerNamesByFaction = source.TintPlayerNamesByFaction,
        SkillMonitorEnabled = source.SkillMonitorEnabled,
        SkillMonitorBuffSelectAll = source.SkillMonitorBuffSelectAll,
        SkillMonitorBuffSkillIds = [.. source.SkillMonitorBuffSkillIds],
        SkillMonitorCooldownSelectAll = source.SkillMonitorCooldownSelectAll,
        SkillMonitorCooldownSkillIds = [.. source.SkillMonitorCooldownSkillIds],
        SkillMonitorDebuffSelectAll = source.SkillMonitorDebuffSelectAll,
        SkillMonitorDebuffSkillIds = [.. source.SkillMonitorDebuffSkillIds],
        SkillMonitorScalePercent = source.SkillMonitorScalePercent,
        SkillMonitorPosition = source.SkillMonitorPosition,
        SkillMonitorWidth = source.SkillMonitorWidth,
        GroupFrameEnabled = source.GroupFrameEnabled,
        GroupFramePosition = source.GroupFramePosition,
        GroupFrameScalePercent = source.GroupFrameScalePercent,
        GroupFrameShowMyBuffs = source.GroupFrameShowMyBuffs,
        GroupFrameShowDebuffs = source.GroupFrameShowDebuffs,
        GroupFrameShowOtherBuffs = source.GroupFrameShowOtherBuffs,
        GroupFrameShowPermanentBuffs = source.GroupFrameShowPermanentBuffs,
        GroupFrameShieldOnRight = source.GroupFrameShieldOnRight,
        GroupFrameShieldAnimation = source.GroupFrameShieldAnimation,
        GroupFrameGreenMeters = source.GroupFrameGreenMeters,
        GroupFrameOrangeMeters = source.GroupFrameOrangeMeters,
        GroupFrameRangeMeters = source.GroupFrameRangeMeters,
        GroupFrameShowDeaths = source.GroupFrameShowDeaths,
        GroupFrameCubeFinder = source.GroupFrameCubeFinder,
        GroupFrameAuraSide = source.GroupFrameAuraSide,
        GroupFrameArrowSide = source.GroupFrameArrowSide,
        GroupFrameDistanceSide = source.GroupFrameDistanceSide,
        GroupFrameHpOpacityPercent = source.GroupFrameHpOpacityPercent,
        GroupFrameAuraOpacityPercent = source.GroupFrameAuraOpacityPercent,
        GroupFrameArrowOpacityPercent = source.GroupFrameArrowOpacityPercent,
        GroupFrameDistanceOpacityPercent = source.GroupFrameDistanceOpacityPercent,
        GroupFrameAlertEnabled = source.GroupFrameAlertEnabled,
        GroupFrameAlertVolumePercent = source.GroupFrameAlertVolumePercent,
        GroupFrameAlertQuietSeconds = source.GroupFrameAlertQuietSeconds,
        GroupFrameAlertSound = source.GroupFrameAlertSound,
        GroupFrameAlertSounds = [.. source.GroupFrameAlertSounds],
        FightGraphsEnabled = source.FightGraphsEnabled,
        FightGraphsPosition = source.FightGraphsPosition,
        FightGraphsScalePercent = source.FightGraphsScalePercent,
        FightGraphsBestMetric = source.FightGraphsBestMetric,
        FightGraphsOpacityPercent = source.FightGraphsOpacityPercent,
        SkillMonitorOpacityPercent = source.SkillMonitorOpacityPercent,
        TwinLibraryScalePercent = source.TwinLibraryScalePercent,
        TwinLibraryOpacityPercent = source.TwinLibraryOpacityPercent,
        SettingsWindowPosition = source.SettingsWindowPosition,
        FightGraphsShowMembers = source.FightGraphsShowMembers,
        FightGraphsShowOtherDeaths = source.FightGraphsShowOtherDeaths,
        FightGraphsSmoothCurves = source.FightGraphsSmoothCurves,
        FightGraphsShowOverview = source.FightGraphsShowOverview,
        FightGraphsNewBestsInTitle = source.FightGraphsNewBestsInTitle,
        FightGraphsOverviewHidden = [.. source.FightGraphsOverviewHidden],
        FightGraphsHiddenCurves = [.. source.FightGraphsHiddenCurves],
        FightGraphsFolded = [.. source.FightGraphsFolded],
        FightGraphsHiddenMembers = [.. source.FightGraphsHiddenMembers],
        FightGraphsCompareEqualTime = source.FightGraphsCompareEqualTime,
        ClickableWithoutFocus = [.. source.ClickableWithoutFocus],
        MeterClickableAdded = source.MeterClickableAdded,
        TitleClickable = [.. source.TitleClickable],
        UiScalePercent = source.UiScalePercent,
        OverlayOpacityPercent = source.OverlayOpacityPercent,
        DetailOpacityPercent = source.DetailOpacityPercent,
        UpdateAutomatically = source.UpdateAutomatically,
        LookUpDigitalTwins = source.LookUpDigitalTwins,
        LocalPlayerServerId = source.LocalPlayerServerId,
        LocalPlayerNickname = source.LocalPlayerNickname,
        Language = source.Language,
        BattleResetHotkeyModifiers = source.BattleResetHotkeyModifiers,
        BattleResetHotkeyVirtualKey = source.BattleResetHotkeyVirtualKey,
        OverlayInteractionHotkeyModifiers = source.OverlayInteractionHotkeyModifiers,
        OverlayInteractionHotkeyVirtualKey = source.OverlayInteractionHotkeyVirtualKey,
        MinimizeAllHotkeyModifiers = source.MinimizeAllHotkeyModifiers,
        MinimizeAllHotkeyVirtualKey = source.MinimizeAllHotkeyVirtualKey,
        SaveSceneHotkeyModifiers = source.SaveSceneHotkeyModifiers,
        SaveSceneHotkeyVirtualKey = source.SaveSceneHotkeyVirtualKey,
        Scenes = [.. source.Scenes.Select(static scene => scene.Clone())],
        NextSceneId = source.NextSceneId,
        Workflows = [.. source.Workflows.Select(static workflow => workflow.Clone())],
        TutorialDone = source.TutorialDone,
        TutorialRestore = source.TutorialRestore?.Clone(),
        TutorialRestoreMinimized = source.TutorialRestoreMinimized is { } minimized ? [.. minimized] : null,
        BossParticipationWarning = source.BossParticipationWarning,
        BossParticipationPercent = source.BossParticipationPercent,
        MainWindowPosition = source.MainWindowPosition,
        MeterFolded = source.MeterFolded,
        MinimizedFrames = [.. source.MinimizedFrames],
        HiddenCubesEnabled = source.HiddenCubesEnabled,
        HiddenCubesSound = source.HiddenCubesSound,
        HiddenCubesPosition = source.HiddenCubesPosition,
        HiddenCubesScalePercent = source.HiddenCubesScalePercent,
        HiddenCubesOpacityPercent = source.HiddenCubesOpacityPercent
    };
}
