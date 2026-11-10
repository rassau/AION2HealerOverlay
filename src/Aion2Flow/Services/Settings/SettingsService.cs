using System.Text.Json;
using Cloris.Aion2Flow.Services.Logging;

namespace Cloris.Aion2Flow.Services.Settings;

public sealed class SettingsService
{
    private const string SettingsFileName = "settings.json";
    private readonly Lock _writeLock = new();
    private AppSettings _current;

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
            }
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Failed to save settings to '{FilePath}': {ex}");
        }
    }

    private static AppSettings Clone(AppSettings source) => new()
    {
        TopmostMode = source.TopmostMode,
        MaxVisibleCombatantRows = source.MaxVisibleCombatantRows,
        CombatantSortMetric = source.CombatantSortMetric,
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
        ShowStaggerColumn = source.ShowStaggerColumn,
        ShowBossShareColumn = source.ShowBossShareColumn,
        ShowTotalDamagePerSecond = source.ShowTotalDamagePerSecond,
        ShowColumnHeaders = source.ShowColumnHeaders,
        BossSectionExpanded = source.BossSectionExpanded,
        BossTimeLimitMinutes = source.BossTimeLimitMinutes,
        EncounterTimeDisplayFormat = source.EncounterTimeDisplayFormat,
        ShowFocusStatusBar = source.ShowFocusStatusBar,
        HideHeaderWhenClickThrough = source.HideHeaderWhenClickThrough,
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
        UiScalePercent = source.UiScalePercent,
        OverlayOpacityPercent = source.OverlayOpacityPercent,
        DetailOpacityPercent = source.DetailOpacityPercent,
        UpdateAutomatically = source.UpdateAutomatically,
        Language = source.Language,
        BattleResetHotkeyModifiers = source.BattleResetHotkeyModifiers,
        BattleResetHotkeyVirtualKey = source.BattleResetHotkeyVirtualKey,
        OverlayInteractionHotkeyModifiers = source.OverlayInteractionHotkeyModifiers,
        OverlayInteractionHotkeyVirtualKey = source.OverlayInteractionHotkeyVirtualKey,
        MainWindowPosition = source.MainWindowPosition,
        MeterFolded = source.MeterFolded
    };
}
