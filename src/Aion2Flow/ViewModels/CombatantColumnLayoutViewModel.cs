namespace Cloris.Aion2Flow.ViewModels;

public sealed class CombatantColumnLayoutViewModel(UiFrameBatchService frameBatchService) : FrameBatchedObservableObject(frameBatchService)
{
    public bool ShowDamagePerSecondColumn
    {
        get;
        private set => SetFrameProperty(ref field, value);
    } = true;

    public bool ShowDamageColumn
    {
        get;
        private set => SetFrameProperty(ref field, value);
    } = true;

    public bool ShowHealingPerSecondColumn
    {
        get;
        private set => SetFrameProperty(ref field, value);
    }

    public bool ShowHealingColumn
    {
        get;
        private set => SetFrameProperty(ref field, value);
    }

    public bool ShowDamageTakenColumn
    {
        get;
        private set => SetFrameProperty(ref field, value);
    }

    public bool ShowUnhealedHealthColumn
    {
        get;
        private set => SetFrameProperty(ref field, value);
    }

    public bool ShowAggroColumn
    {
        get;
        private set => SetFrameProperty(ref field, value);
    }

    public bool ShowStaggerColumn
    {
        get;
        private set => SetFrameProperty(ref field, value);
    }

    public bool ShowBossShareColumn
    {
        get;
        private set => SetFrameProperty(ref field, value);
    }

    public bool ShowTotalDamagePerSecond
    {
        get;
        private set => SetFrameProperty(ref field, value);
    } = true;

    public bool UseCompactMainMetrics
    {
        get;
        private set => SetFrameProperty(ref field, value);
    } = true;

    // Overlay width at 100% UI scale: the original 300 fits the names next to two metric columns, and every further
    // column brings its own room instead of squeezing the names.
    public double OverlayWidth
    {
        get;
        private set => SetFrameProperty(ref field, value);
    } = BaseOverlayWidth;

    // Room for the title bar's navigation, the eye and the map's name with the two columns shown at first.
    private const double BaseOverlayWidth = 334;

    private bool _bossShareColumnEnabled = true;
    private bool _hasBossFocus;

    public void ApplyMetricDisplaySettings(bool showDamagePerSecondColumn, bool showDamageColumn, bool showHealingPerSecondColumn, bool showHealingColumn, bool showDamageTakenColumn, bool showUnhealedHealthColumn, bool showAggroColumn, bool showStaggerColumn, bool showBossShareColumn, bool showTotalDamagePerSecond, bool useCompactMainMetrics)
    {
        _bossShareColumnEnabled = showBossShareColumn;
        ShowBossShareColumn = showBossShareColumn && _hasBossFocus;
        ShowDamagePerSecondColumn = showDamagePerSecondColumn;
        ShowDamageColumn = showDamageColumn;
        ShowHealingPerSecondColumn = showHealingPerSecondColumn;
        ShowHealingColumn = showHealingColumn;
        ShowDamageTakenColumn = showDamageTakenColumn;
        ShowUnhealedHealthColumn = showUnhealedHealthColumn;
        ShowAggroColumn = showAggroColumn;
        ShowStaggerColumn = showStaggerColumn;
        ShowTotalDamagePerSecond = showTotalDamagePerSecond;
        UseCompactMainMetrics = useCompactMainMetrics;
        var visibleColumns = (showDamagePerSecondColumn ? 1 : 0) + (showDamageColumn ? 1 : 0) + (showHealingPerSecondColumn ? 1 : 0) +
                             (showHealingColumn ? 1 : 0) + (showDamageTakenColumn ? 1 : 0) + (showUnhealedHealthColumn ? 1 : 0) + (showAggroColumn ? 1 : 0) +
                             (showStaggerColumn ? 1 : 0);
        // ponytail: a fixed allowance per column ("785k" vs "1,571,765"); measure the values if one ever gets clipped.
        OverlayWidth = BaseOverlayWidth + Math.Max(0, visibleColumns - 2) * (useCompactMainMetrics ? 40 : 56);
    }

    // The boss column shows while a boss bar is up, unless it's turned off in the settings.
    public void SetBossShareColumnVisibility(bool hasBossFocus)
    {
        _hasBossFocus = hasBossFocus;
        ShowBossShareColumn = _bossShareColumnEnabled && hasBossFocus;
    }
}
