using System.Runtime.InteropServices;
using Cloris.Aion2Flow.Presentation;
using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cloris.Aion2Flow.ViewModels;

public sealed partial class CombatantDetailsFlyoutViewModel : ObservableObject, ICombatDetailEventWriter, IDisposable
{
    private readonly List<CombatMetricDetailEvent> _metricDetailEvents = [];
    private readonly List<CombatMechanicDetailEvent> _mechanicDetailEvents = [];
    private readonly List<CombatResourceDetailEvent> _resourceDetailEvents = [];
    private readonly DetailCounterpartOptionBuilder _counterpartOptionBuilder = new();
    private readonly HashSet<int> _outgoingDamageSelectionIds = [];
    private readonly HashSet<int> _outgoingHealingSelectionIds = [];
    private readonly HashSet<int> _outgoingShieldSelectionIds = [];
    private readonly HashSet<int> _outgoingResourceSelectionIds = [];
    private readonly HashSet<int> _incomingDamageSelectionIds = [];
    private readonly HashSet<int> _incomingHealingSelectionIds = [];
    private readonly HashSet<int> _incomingShieldSelectionIds = [];
    private readonly HashSet<int> _incomingResourceSelectionIds = [];
    private readonly SkillDetailSectionAggregation _outgoingDamageSectionAggregation = new();
    private readonly SkillDetailSectionAggregation _outgoingHealingSectionAggregation = new();
    private readonly SkillDetailSectionAggregation _outgoingShieldSectionAggregation = new();
    private readonly ResourceDetailSectionAggregation _outgoingResourceSectionAggregation = new();
    private readonly SkillDetailSectionAggregation _incomingDamageSectionAggregation = new();
    private readonly SkillDetailSectionAggregation _incomingHealingSectionAggregation = new();
    private readonly SkillDetailSectionAggregation _incomingShieldSectionAggregation = new();
    private readonly ResourceDetailSectionAggregation _incomingResourceSectionAggregation = new();
    private readonly List<SkillDetailRowData> _sectionRows = [];
    private readonly Dictionary<SkillBaseKey, int> _sectionRowIndexes = [];
    private readonly List<ResourceDetailRowData> _resourceSectionRows = [];
    private readonly Dictionary<SkillBaseKey, int> _resourceSectionRowIndexes = [];
    private readonly LocalizationService _localization;
    private SceneCombatSnapshot _currentSnapshot = new();
    private Guid _encounterContextId;
    private int? _combatantId;
    private Cloris.Aion2Flow.SceneRuntime.Twins.DigitalTwin? _shownTwin;
    private string? _shownSpecializations;
    private long _detailRevision = -1;
    private bool _disposed;

    public CombatantDetailsFlyoutViewModel(LocalizationService localization, UiFrameBatchService frameBatchService)
    {
        _localization = localization;
        OutgoingDetail = new CombatDirectionDetailViewModel(localization, frameBatchService, "Direction_Targets");
        IncomingDetail = new CombatDirectionDetailViewModel(localization, frameBatchService, "Direction_Sources");
        OutgoingDetail.DamageCounterpartFilter.SelectionChanged += HandleCounterpartSelectionChanged;
        OutgoingDetail.HealingCounterpartFilter.SelectionChanged += HandleCounterpartSelectionChanged;
        OutgoingDetail.ShieldCounterpartFilter.SelectionChanged += HandleCounterpartSelectionChanged;
        OutgoingDetail.ResourceCounterpartFilter.SelectionChanged += HandleCounterpartSelectionChanged;
        IncomingDetail.DamageCounterpartFilter.SelectionChanged += HandleCounterpartSelectionChanged;
        IncomingDetail.HealingCounterpartFilter.SelectionChanged += HandleCounterpartSelectionChanged;
        IncomingDetail.ShieldCounterpartFilter.SelectionChanged += HandleCounterpartSelectionChanged;
        IncomingDetail.ResourceCounterpartFilter.SelectionChanged += HandleCounterpartSelectionChanged;
    }

    public CombatDirectionDetailViewModel OutgoingDetail { get; }

    public CombatDirectionDetailViewModel IncomingDetail { get; }

    public SkillDetailSectionViewModel OutgoingDamage => OutgoingDetail.DamageSection;

    public SkillDetailSectionViewModel OutgoingHealing => OutgoingDetail.HealingSection;

    public SkillDetailSectionViewModel OutgoingShield => OutgoingDetail.ShieldSection;

    public SkillDetailSectionViewModel IncomingDamage => IncomingDetail.DamageSection;

    public SkillDetailSectionViewModel IncomingHealing => IncomingDetail.HealingSection;

    public SkillDetailSectionViewModel IncomingShield => IncomingDetail.ShieldSection;

    public ResourceDetailSectionViewModel OutgoingResource => OutgoingDetail.ResourceSection;

    public ResourceDetailSectionViewModel IncomingResource => IncomingDetail.ResourceSection;

    public EncounterTimeDisplayFormat EncounterTimeDisplayFormat
    {
        get => OutgoingDetail.EncounterTimeDisplayFormat;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }

            OutgoingDetail.EncounterTimeDisplayFormat = value;
            IncomingDetail.EncounterTimeDisplayFormat = value;
        }
    }

    public bool IncludeOverhealing { get; set; } = true;

    public void SynchronizeSkillSelection(bool isOutgoing, CombatContributionCategory category, SkillBaseKey baseKey)
    {
        OutgoingDamage.SelectRowByKey(isOutgoing && category == CombatContributionCategory.Damage ? baseKey : null);
        OutgoingHealing.SelectRowByKey(isOutgoing && category == CombatContributionCategory.Healing ? baseKey : null);
        OutgoingShield.SelectRowByKey(isOutgoing && category == CombatContributionCategory.Shield ? baseKey : null);
        IncomingDamage.SelectRowByKey(!isOutgoing && category == CombatContributionCategory.Damage ? baseKey : null);
        IncomingHealing.SelectRowByKey(!isOutgoing && category == CombatContributionCategory.Healing ? baseKey : null);
        IncomingShield.SelectRowByKey(!isOutgoing && category == CombatContributionCategory.Shield ? baseKey : null);
    }

    public void ClearSkillSelection()
    {
        OutgoingDamage.SelectRowByKey(null);
        OutgoingHealing.SelectRowByKey(null);
        OutgoingShield.SelectRowByKey(null);
        IncomingDamage.SelectRowByKey(null);
        IncomingHealing.SelectRowByKey(null);
        IncomingShield.SelectRowByKey(null);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DisposeCounterpartFilter(OutgoingDetail.DamageCounterpartFilter);
        DisposeCounterpartFilter(OutgoingDetail.HealingCounterpartFilter);
        DisposeCounterpartFilter(OutgoingDetail.ShieldCounterpartFilter);
        DisposeCounterpartFilter(OutgoingDetail.ResourceCounterpartFilter);
        DisposeCounterpartFilter(IncomingDetail.DamageCounterpartFilter);
        DisposeCounterpartFilter(IncomingDetail.HealingCounterpartFilter);
        DisposeCounterpartFilter(IncomingDetail.ShieldCounterpartFilter);
        DisposeCounterpartFilter(IncomingDetail.ResourceCounterpartFilter);
    }

    [ObservableProperty]
    public partial SceneDisplayContext? DisplayContext { get; set; }

    [ObservableProperty]
    public partial int SelectedCombatantId { get; set; }

    // What the detail covers when it isn't the whole encounter, e.g. "Boss #1 failed attempt 1".
    [ObservableProperty]
    public partial string? ScopeText { get; set; }

    [ObservableProperty]
    public partial int SelectedDirectionIndex { get; set; }

    public bool IsOutgoingSelected
    {
        get => SelectedDirectionIndex == 0;
        set
        {
            if (value)
            {
                SelectedDirectionIndex = 0;
            }
        }
    }

    public bool IsIncomingSelected
    {
        get => SelectedDirectionIndex == 1;
        set
        {
            if (value)
            {
                SelectedDirectionIndex = 1;
            }
        }
    }

    public bool IsBuffsSelected
    {
        get => SelectedDirectionIndex == 2;
        set
        {
            if (value)
                SelectedDirectionIndex = 2;
        }
    }

    public bool IsTwinSelected
    {
        get => SelectedDirectionIndex == 3;
        set
        {
            if (value)
                SelectedDirectionIndex = 3;
        }
    }

    // The Digital twin tab: the player as the official character website shows them (see DigitalTwin), filled by the main
    // view model while the tab is open; "Look up" asks it to look the player up (again).
    public event Action? TwinLookUpRequested;

    public System.Collections.ObjectModel.ObservableCollection<DigitalTwinRowViewModel> TwinStats { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<DigitalTwinRowViewModel> TwinPrimaryStats { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<DigitalTwinRowViewModel> TwinGear { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<DigitalTwinRowViewModel> TwinSkills { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<DigitalTwinRowViewModel> TwinBoards { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<DigitalTwinRowViewModel> TwinTitles { get; } = [];

    [ObservableProperty]
    public partial bool HasTwin { get; set; }

    [ObservableProperty]
    public partial string? TwinHeader { get; set; }

    [ObservableProperty]
    public partial string? TwinSummary { get; set; }

    [ObservableProperty]
    public partial string? TwinStatusText { get; set; }

    [ObservableProperty]
    public partial bool CanLookUpTwin { get; set; }

    [ObservableProperty]
    public partial string? TwinLookUpText { get; set; }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void LookUpTwin() => TwinLookUpRequested?.Invoke();

    // Called on every refresh of the details: rebuilds only when the twin or its status changed.
    public void ShowDigitalTwin(Cloris.Aion2Flow.SceneRuntime.Twins.DigitalTwin? twin, string? status, bool canLookUp)
    {
        if (ReferenceEquals(twin, _shownTwin) && status == TwinStatusText && canLookUp == CanLookUpTwin && TwinLookUpText is not null)
            return;

        _shownTwin = twin;
        TwinStatusText = status;
        CanLookUpTwin = canLookUp;
        TwinLookUpText = _localization[twin is null ? "Twin_LookUp" : "Twin_LookUpAgain"];
        HasTwin = twin is not null;
        TwinStats.Clear();
        TwinPrimaryStats.Clear();
        TwinGear.Clear();
        TwinSkills.Clear();
        TwinBoards.Clear();
        TwinTitles.Clear();
        if (twin is null)
        {
            TwinHeader = null;
            TwinSummary = null;
            return;
        }

        var culture = System.Globalization.CultureInfo.CurrentCulture;
        string Number(double value, bool percent = false) => value.ToString("#,0.##", culture) + (percent ? "%" : "");
        string Stat(Cloris.Aion2Flow.SceneRuntime.Twins.TwinStatValue stat) =>
            stat.Name + " " + (stat.Min is { } min ? $"{Number(min)}–{Number(stat.Value)}" : Number(stat.Value, stat.Percent)) +
            (stat.Extra != 0 ? $" +{Number(stat.Extra)}" : "");

        TwinHeader = string.Format(culture, _localization["Twin_Header"], twin.Name, twin.ClassName, twin.Level, twin.ServerName);
        TwinSummary = string.Format(culture, _localization["Twin_Summary"], twin.CombatPower, twin.ItemLevel, twin.FetchedAt.ToLocalTime()) +
            string.Concat(new[] { twin.Pet, twin.Wings }.OfType<string>().Select(static part => " · " + part));
        foreach (var total in twin.Totals)
        {
            var value = Number(total.Value, total.Percent) + (total.Min is { } min && min != total.Value ? $" (min {Number(min)})" : "");
            TwinStats.Add(new DigitalTwinRowViewModel(total.Name, value, string.Join(" · ", total.Sources.Select(source => $"{source.Source} {Number(source.Value, total.Percent)}"))));
        }

        foreach (var stat in twin.PrimaryStats)
            TwinPrimaryStats.Add(new DigitalTwinRowViewModel(stat.Name, Number(stat.Value), string.Join(" · ", stat.Effects)));
        foreach (var item in twin.Gear)
        {
            var parts = item.MainStats.Concat(item.SubStats).Select(Stat)
                .Concat(item.Stones.Select(Stat))
                .Concat(item.SkillBonuses.Select(skill => $"{skill.Name} +{skill.Levels}"))
                .Concat(item.Theostones);
            var level = item.Enchant > 0 ? $"+{item.Enchant}" : "";
            if (item.Exceed > 0)
                level += " " + string.Format(culture, _localization["Twin_Exceed"], item.Exceed);
            TwinGear.Add(new DigitalTwinRowViewModel($"{item.Slot}: {item.Name}", level.Trim(), string.Join(" · ", parts)));
        }

        foreach (var skill in twin.Skills)
        {
            // The website's level is the one the skill is used at, gear and Daevanion included: "21 (7 from gear, 4 from
            // Daevanion)".
            var parts = new List<string>(2);
            if (skill.GearLevels > 0)
                parts.Add(string.Format(culture, _localization["Twin_FromGear"], skill.GearLevels));
            if (skill.DaevanionLevels > 0)
                parts.Add(string.Format(culture, _localization["Twin_FromDaevanion"], skill.DaevanionLevels));
            var level = skill.Level.ToString(culture) + (parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "");
            TwinSkills.Add(new DigitalTwinRowViewModel(skill.Name, level, skill.Equipped ? skill.Category : $"{skill.Category} · {_localization["Twin_NotEquipped"]}"));
        }

        foreach (var board in twin.Boards)
            TwinBoards.Add(new DigitalTwinRowViewModel(board.Name, $"{board.OpenNodes}/{board.Nodes}", string.Join(" · ", board.StatEffects.Concat(board.SkillEffects))));
        foreach (var title in twin.Titles)
            TwinTitles.Add(new DigitalTwinRowViewModel($"{title.Category}: {title.Name}", "", string.Join(" · ", title.Effects)));
    }

    private void ClearDigitalTwin()
    {
        ShowDigitalTwin(null, null, canLookUp: false);
        ShowSeenSpecializations([]);
    }

    // The specializations the player picked, from the skill ids of what they did in what the details cover (the whole
    // encounter or one boss fight; worked out by the main view model, see SkillSpecializationCodes.Read); shown on the twin
    // tab with or without a website lookup.
    public System.Collections.ObjectModel.ObservableCollection<DigitalTwinRowViewModel> TwinSpecializations { get; } = [];

    [ObservableProperty]
    public partial bool HasTwinSpecializations { get; set; }

    // A stigma's number is how far its specializations are unlocked, the ones below included ("1–3"); an active skill's
    // are the picked ones ("3 + 4"). Each with what it does. Rebuilt only when the picks changed.
    public void ShowSeenSpecializations(IReadOnlyList<Cloris.Aion2Flow.SceneRuntime.Twins.SeenSpecialization> seen)
    {
        var signature = string.Join(";", seen.Select(static skill => $"{skill.SkillId}:{string.Join(",", skill.Specializations)}/{string.Join("|", skill.AlsoSeen.Select(static picks => string.Join(",", picks)))}"));
        if (signature == _shownSpecializations)
            return;

        _shownSpecializations = signature;
        TwinSpecializations.Clear();
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        foreach (var skill in seen)
        {
            var info = SkillSpecializations.Get(skill.SkillId);
            var isStigma = info?.IsStigma == true;
            string Picks(int[] picks) =>
                picks.Length == 0 ? _localization["Twin_SpecNone"]
                : isStigma && picks.Length == 1 && picks[0] > 1 ? $"1–{picks[0]}"
                : string.Join(" + ", picks);

            var value = Picks(skill.Specializations);
            if (skill.AlsoSeen.Length > 0)
                value += " (" + string.Format(culture, _localization["Twin_SpecAlso"], string.Join(", ", skill.AlsoSeen.Select(Picks))) + ")";
            var effects = SkillSpecializations.EffectsOf(skill.SkillId, skill.Specializations).Select(static effect => $"{effect.Number}: {effect.Effect}");
            var name = DisplayContext?.ResolveSkillName(skill.SkillId) is { Length: > 0 } resolved ? resolved : skill.SkillId.ToString(culture);
            TwinSpecializations.Add(new DigitalTwinRowViewModel(name, value, string.Join(" · ", effects)));
        }

        HasTwinSpecializations = TwinSpecializations.Count > 0;
    }

    // The Buffs tab: the buffs on the player (whose, and for how much of the scope), and the player's own, on themselves
    // and on the group (see BuffUptime). Filled by the main view model while the tab is open.
    public System.Collections.ObjectModel.ObservableCollection<BuffUptimeRowViewModel> BuffsOnPlayer { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<BuffUptimeRowViewModel> BuffsOfPlayer { get; } = [];

    [ObservableProperty]
    public partial bool HasBuffsOnPlayer { get; set; }

    [ObservableProperty]
    public partial bool HasBuffsOfPlayer { get; set; }

    [ObservableProperty]
    public partial bool HasNoBuffs { get; set; }

    // casterLevel: a player's level of a skill and their name, when their digital twin tells it (the hover text then has
    // the numbers at that level).
    public void ShowBuffUptime(Cloris.Aion2Flow.SceneRuntime.Stores.BuffUptimeReport report, int combatantId, Func<int, int, (int Level, string Caster)?>? casterLevel = null)
    {
        var context = DisplayContext;
        // An aura without a skill id carries the skill's code (with the specialization picks) as its effect's raw id; it's
        // named from that like the playback's aura lanes, the number standing in for skills without a name.
        BuffUptimeRowViewModel Row(Cloris.Aion2Flow.SceneRuntime.Stores.BuffKey buff, int casterId, double? uptime, double? groupUptime)
        {
            long code = buff.SkillId > 0 ? buff.SkillId : buff.EffectRef.RawId;
            var skill = code <= int.MaxValue ? (int)code : 0;
            var name = context?.ResolveSkillName(skill) is { Length: > 0 } resolved ? resolved : code.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var known = skill > 0 ? casterLevel?.Invoke(buff.CasterId, skill) : null;
            var skillName = skill > 0 ? context?.ResolveSkillName(skill / 10_000 * 10_000) : null;
            var (effect, note, specializations) = GroupFrameViewModel.DescribeAura(skill, _localization, known?.Level, known?.Caster, name, skillName);
            return new BuffUptimeRowViewModel(name, casterId, uptime, groupUptime, skill, effect, note, specializations);
        }

        // Parts of one buff (its caster's part and the party's: Light of Protection 17410030 and 17410031) have the same
        // name: one row, with the most either part reached.
        BuffsOnPlayer.Clear();
        foreach (var row in report.OnMembers.Where(buff => buff.MemberId == combatantId).Select(buff => Row(buff.Buff, buff.Buff.CasterId, buff.Uptime, null))
                     .GroupBy(static row => (row.CasterId, row.Name)).Select(static parts => parts.MaxBy(static row => row.UptimeValue)!)
                     .OrderByDescending(static row => row.UptimeValue))
            BuffsOnPlayer.Add(row);
        BuffsOfPlayer.Clear();
        foreach (var row in report.OfCasters.Where(buff => buff.Buff.CasterId == combatantId).Select(buff => Row(buff.Buff, combatantId, buff.SelfUptime, buff.GroupUptime))
                     .GroupBy(static row => row.Name)
                     .Select(static parts => parts.MaxBy(static row => (row.Uptime ?? 0) + (row.GroupUptime ?? 0))! with { Uptime = parts.Max(static row => row.Uptime), GroupUptime = parts.Max(static row => row.GroupUptime) })
                     .OrderByDescending(static row => row.GroupUptime ?? -1).ThenByDescending(static row => row.Uptime ?? -1))
            BuffsOfPlayer.Add(row);

        HasBuffsOnPlayer = BuffsOnPlayer.Count > 0;
        HasBuffsOfPlayer = BuffsOfPlayer.Count > 0;
        HasNoBuffs = !HasBuffsOnPlayer && !HasBuffsOfPlayer;
    }

    private void ClearBuffUptime()
    {
        BuffsOnPlayer.Clear();
        BuffsOfPlayer.Clear();
        HasBuffsOnPlayer = false;
        HasBuffsOfPlayer = false;
        HasNoBuffs = true;
    }

    partial void OnSelectedDirectionIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsOutgoingSelected));
        OnPropertyChanged(nameof(IsIncomingSelected));
        OnPropertyChanged(nameof(IsBuffsSelected));
        OnPropertyChanged(nameof(IsTwinSelected));
    }

    public void SelectSceneEncounterCombatant(Guid encounterContextId, int? combatantId, SceneCombatSnapshot snapshot, CombatDetailDelta detail, bool forceRefresh = false)
    {
        RefreshSceneContext(encounterContextId, combatantId, snapshot, detail, forceRefresh);
    }

    public void SelectLiveSceneEncounterCombatant(Guid encounterContextId, int? combatantId, SceneCombatSnapshot snapshot, CombatDetailUpdateResult update, bool forceRefresh = false)
    {
        RefreshLiveSceneContext(encounterContextId, combatantId, snapshot, update, forceRefresh);
    }

    public void SelectPlaybackSceneEncounterCombatant(Guid encounterContextId, int combatantId, SceneCombatSnapshot snapshot, CombatDetailUpdateResult update, in CombatDetailEventSet events)
    {
        if (update.IsFullSnapshot)
            ClearDetailEvents();

        AddMetricDetailEvents(events.MetricEvents);
        _mechanicDetailEvents.AddRange(events.MechanicEvents);
        _resourceDetailEvents.AddRange(events.ResourceEvents);

        RefreshLiveSceneContext(encounterContextId, combatantId, snapshot, update, forceRefresh: false);
    }

    public void Deactivate()
    {
        SelectedCombatantId = 0;
    }

    public void Clear()
    {
        SetEncounterContextId(Guid.Empty);
        _combatantId = null;
        _currentSnapshot = new SceneCombatSnapshot();
        ClearDetailEvents();
        _detailRevision = -1;
        SelectedCombatantId = 0;
        SelectedDirectionIndex = 0;
        OutgoingDetail.Clear();
        IncomingDetail.Clear();
        ClearBuffUptime();
        ClearDigitalTwin();
    }

    void ICombatDetailEventWriter.Clear() => ClearDetailEvents();

    void ICombatDetailEventWriter.AddMetric(in CombatMetricDetailEvent detailEvent) => _metricDetailEvents.Add(ToDisplayed(in detailEvent));

    void ICombatDetailEventWriter.AddMechanic(in CombatMechanicDetailEvent detailEvent) => _mechanicDetailEvents.Add(detailEvent);

    void ICombatDetailEventWriter.AddResource(in CombatResourceDetailEvent detailEvent) => _resourceDetailEvents.Add(detailEvent);

    private bool HasDetailEvents =>
        _metricDetailEvents.Count > 0 ||
        _mechanicDetailEvents.Count > 0 ||
        _resourceDetailEvents.Count > 0;

    private void ClearDetailEvents()
    {
        _metricDetailEvents.Clear();
        _mechanicDetailEvents.Clear();
        _resourceDetailEvents.Clear();
    }

    private void AddMetricDetailEvents(IReadOnlyList<CombatMetricDetailEvent> detailEvents)
    {
        foreach (var detailEvent in detailEvents)
            _metricDetailEvents.Add(ToDisplayed(in detailEvent));
    }

    // With overhealing hidden, a heal contributes only the HP it actually restored.
    private CombatMetricDetailEvent ToDisplayed(in CombatMetricDetailEvent detailEvent) =>
        IncludeOverhealing || detailEvent.Metric != CombatMetricKind.Healing
            ? detailEvent
            : detailEvent with { Contribution = detailEvent.Contribution with { Amount = detailEvent.EffectiveHealing } };

    private void DisposeCounterpartFilter(DetailCounterpartFilterViewModel filter)
    {
        filter.SelectionChanged -= HandleCounterpartSelectionChanged;
        filter.Dispose();
    }

    private void HandleCounterpartSelectionChanged(object? sender, EventArgs e)
    {
        if (_combatantId is null)
        {
            return;
        }

        if (ReferenceEquals(sender, OutgoingDetail.DamageCounterpartFilter) ||
            ReferenceEquals(sender, OutgoingDetail.HealingCounterpartFilter) ||
            ReferenceEquals(sender, OutgoingDetail.ShieldCounterpartFilter) ||
            ReferenceEquals(sender, OutgoingDetail.ResourceCounterpartFilter))
        {
            RefreshDirection(
                OutgoingDetail,
                DetailSectionKind.OutgoingDamage,
                DetailSectionKind.OutgoingHealing,
                DetailSectionKind.OutgoingShield,
                DetailSectionKind.OutgoingResource);
        }
        else if (ReferenceEquals(sender, IncomingDetail.DamageCounterpartFilter) ||
                 ReferenceEquals(sender, IncomingDetail.HealingCounterpartFilter) ||
                 ReferenceEquals(sender, IncomingDetail.ShieldCounterpartFilter) ||
                 ReferenceEquals(sender, IncomingDetail.ResourceCounterpartFilter))
        {
            RefreshDirection(
                IncomingDetail,
                DetailSectionKind.IncomingDamage,
                DetailSectionKind.IncomingHealing,
                DetailSectionKind.IncomingShield,
                DetailSectionKind.IncomingResource);
        }
    }

    private void RefreshSceneContext(Guid encounterContextId, int? combatantId, SceneCombatSnapshot snapshot, CombatDetailDelta detail, bool forceRefresh)
    {
        if (combatantId is null ||
            encounterContextId == Guid.Empty ||
            !snapshot.Combatants.ContainsKey(combatantId.Value) && detail.Combatant is null &&
            detail.MetricEvents.Count == 0 && detail.MechanicEvents.Count == 0 && detail.ResourceEvents.Count == 0)
        {
            SetEncounterContextId(encounterContextId);
            _combatantId = combatantId;
            _currentSnapshot = new SceneCombatSnapshot();
            ClearDetailEvents();
            _detailRevision = -1;
            SelectedCombatantId = 0;
            ClearSectionsOnly();
            return;
        }

        var nextDetailRevision = detail.Revision;
        var canReuseExistingSections = !forceRefresh &&
            _encounterContextId == encounterContextId &&
            _combatantId == combatantId &&
            _detailRevision == nextDetailRevision;

        SetEncounterContextId(encounterContextId);
        _combatantId = combatantId;
        _currentSnapshot = snapshot;
        SelectedCombatantId = combatantId.Value;

        if (canReuseExistingSections)
        {
            RefreshSectionRatesOnly();
            return;
        }

        _detailRevision = nextDetailRevision;
        ClearDetailEvents();
        AddMetricDetailEvents(detail.MetricEvents);
        _mechanicDetailEvents.AddRange(detail.MechanicEvents);
        _resourceDetailEvents.AddRange(detail.ResourceEvents);

        RebuildCounterpartSelections();
        RefreshAllSections();
    }

    private void RefreshLiveSceneContext(Guid encounterContextId, int? combatantId, SceneCombatSnapshot snapshot, CombatDetailUpdateResult update, bool forceRefresh)
    {
        if (combatantId is null ||
            encounterContextId == Guid.Empty ||
            !snapshot.Combatants.ContainsKey(combatantId.Value) && update.Combatant is null && !HasDetailEvents)
        {
            SetEncounterContextId(encounterContextId);
            _combatantId = combatantId;
            _currentSnapshot = new SceneCombatSnapshot();
            ClearDetailEvents();
            _detailRevision = -1;
            SelectedCombatantId = 0;
            ClearSectionsOnly();
            return;
        }

        var canReuseExistingSections = !forceRefresh &&
            _encounterContextId == encounterContextId &&
            _combatantId == combatantId &&
            _detailRevision == update.Revision &&
            !update.IsFullSnapshot &&
            !update.HasChanges;

        SetEncounterContextId(encounterContextId);
        _combatantId = combatantId;
        _currentSnapshot = snapshot;
        SelectedCombatantId = combatantId.Value;

        if (canReuseExistingSections)
        {
            RefreshSectionRatesOnly();
            return;
        }

        _detailRevision = update.Revision;
        if (update.IsFullSnapshot ||
            update.AddedMetricEventCount > 0 ||
            update.AddedMechanicEventCount > 0 ||
            update.AddedResourceEventCount > 0 ||
            forceRefresh)
        {
            RebuildCounterpartSelections();
            RefreshAllSections();
            return;
        }

        RefreshSectionRatesOnly();
    }

    private void RebuildCounterpartSelections()
    {
        if (_combatantId is null)
        {
            return;
        }

        _counterpartOptionBuilder.Accumulate(
            CollectionsMarshal.AsSpan(_metricDetailEvents),
            CollectionsMarshal.AsSpan(_mechanicDetailEvents),
            CollectionsMarshal.AsSpan(_resourceDetailEvents),
            _combatantId.Value);
        OutgoingDetail.DamageCounterpartFilter.ReplaceCounterparts(_counterpartOptionBuilder.BuildOutgoingDamageOptions(DisplayContext));
        OutgoingDetail.HealingCounterpartFilter.ReplaceCounterparts(_counterpartOptionBuilder.BuildOutgoingHealingOptions(DisplayContext));
        OutgoingDetail.ShieldCounterpartFilter.ReplaceCounterparts(_counterpartOptionBuilder.BuildOutgoingShieldOptions(DisplayContext));
        OutgoingDetail.ResourceCounterpartFilter.ReplaceCounterparts(_counterpartOptionBuilder.BuildOutgoingResourceOptions(DisplayContext));
        IncomingDetail.DamageCounterpartFilter.ReplaceCounterparts(_counterpartOptionBuilder.BuildIncomingDamageOptions(DisplayContext));
        IncomingDetail.HealingCounterpartFilter.ReplaceCounterparts(_counterpartOptionBuilder.BuildIncomingHealingOptions(DisplayContext));
        IncomingDetail.ShieldCounterpartFilter.ReplaceCounterparts(_counterpartOptionBuilder.BuildIncomingShieldOptions(DisplayContext));
        IncomingDetail.ResourceCounterpartFilter.ReplaceCounterparts(_counterpartOptionBuilder.BuildIncomingResourceOptions(DisplayContext));
    }

    private void RefreshAllSections()
    {
        if (_combatantId is null)
        {
            ClearSectionsOnly();
            return;
        }

        CopyDirectionSelections(
            OutgoingDetail,
            _outgoingDamageSelectionIds,
            _outgoingHealingSelectionIds,
            _outgoingShieldSelectionIds,
            _outgoingResourceSelectionIds,
            out var outgoingDamageCounterpartCount,
            out var outgoingHealingCounterpartCount,
            out var outgoingShieldCounterpartCount,
            out var outgoingResourceCounterpartCount);
        CopyDirectionSelections(
            IncomingDetail,
            _incomingDamageSelectionIds,
            _incomingHealingSelectionIds,
            _incomingShieldSelectionIds,
            _incomingResourceSelectionIds,
            out var incomingDamageCounterpartCount,
            out var incomingHealingCounterpartCount,
            out var incomingShieldCounterpartCount,
            out var incomingResourceCounterpartCount);

        ResetDirectionAggregations(
            _outgoingDamageSectionAggregation,
            _outgoingHealingSectionAggregation,
            _outgoingShieldSectionAggregation,
            _outgoingResourceSectionAggregation,
            _outgoingDamageSelectionIds,
            _outgoingHealingSelectionIds,
            _outgoingShieldSelectionIds,
            _outgoingResourceSelectionIds,
            outgoingDamageCounterpartCount,
            outgoingHealingCounterpartCount,
            outgoingShieldCounterpartCount,
            outgoingResourceCounterpartCount);
        ResetDirectionAggregations(
            _incomingDamageSectionAggregation,
            _incomingHealingSectionAggregation,
            _incomingShieldSectionAggregation,
            _incomingResourceSectionAggregation,
            _incomingDamageSelectionIds,
            _incomingHealingSelectionIds,
            _incomingShieldSelectionIds,
            _incomingResourceSelectionIds,
            incomingDamageCounterpartCount,
            incomingHealingCounterpartCount,
            incomingShieldCounterpartCount,
            incomingResourceCounterpartCount);

        var metricEvents = CollectionsMarshal.AsSpan(_metricDetailEvents);
        var combatantId = _combatantId.Value;
        foreach (ref readonly var detailEvent in metricEvents)
        {
            AccumulateMetricSection(_outgoingDamageSectionAggregation, in detailEvent, DetailSectionKind.OutgoingDamage, combatantId, _outgoingDamageSelectionIds);
            AccumulateMetricSection(_outgoingHealingSectionAggregation, in detailEvent, DetailSectionKind.OutgoingHealing, combatantId, _outgoingHealingSelectionIds);
            AccumulateMetricSection(_outgoingShieldSectionAggregation, in detailEvent, DetailSectionKind.OutgoingShield, combatantId, _outgoingShieldSelectionIds);
            AccumulateMetricSection(_incomingDamageSectionAggregation, in detailEvent, DetailSectionKind.IncomingDamage, combatantId, _incomingDamageSelectionIds);
            AccumulateMetricSection(_incomingHealingSectionAggregation, in detailEvent, DetailSectionKind.IncomingHealing, combatantId, _incomingHealingSelectionIds);
            AccumulateMetricSection(_incomingShieldSectionAggregation, in detailEvent, DetailSectionKind.IncomingShield, combatantId, _incomingShieldSelectionIds);
        }

        var mechanicEvents = CollectionsMarshal.AsSpan(_mechanicDetailEvents);
        foreach (ref readonly var detailEvent in mechanicEvents)
        {
            AccumulateMechanicSection(_outgoingDamageSectionAggregation, in detailEvent, DetailSectionKind.OutgoingDamage, combatantId, _outgoingDamageSelectionIds);
            AccumulateMechanicSection(_incomingDamageSectionAggregation, in detailEvent, DetailSectionKind.IncomingDamage, combatantId, _incomingDamageSelectionIds);
        }

        var resourceEvents = CollectionsMarshal.AsSpan(_resourceDetailEvents);
        foreach (ref readonly var detailEvent in resourceEvents)
        {
            AccumulateResourceSection(_outgoingResourceSectionAggregation, in detailEvent, DetailSectionKind.OutgoingResource, combatantId, _outgoingResourceSelectionIds);
            AccumulateResourceSection(_incomingResourceSectionAggregation, in detailEvent, DetailSectionKind.IncomingResource, combatantId, _incomingResourceSelectionIds);
        }

        ApplyAggregatedSection(OutgoingDetail.DamageSection, DetailSectionKind.OutgoingDamage, _outgoingDamageSectionAggregation);
        ApplyAggregatedSection(OutgoingDetail.HealingSection, DetailSectionKind.OutgoingHealing, _outgoingHealingSectionAggregation);
        ApplyAggregatedSection(OutgoingDetail.ShieldSection, DetailSectionKind.OutgoingShield, _outgoingShieldSectionAggregation);
        ApplyAggregatedSection(IncomingDetail.DamageSection, DetailSectionKind.IncomingDamage, _incomingDamageSectionAggregation);
        ApplyAggregatedSection(IncomingDetail.HealingSection, DetailSectionKind.IncomingHealing, _incomingHealingSectionAggregation);
        ApplyAggregatedSection(IncomingDetail.ShieldSection, DetailSectionKind.IncomingShield, _incomingShieldSectionAggregation);
        ApplyResourceSection(OutgoingDetail.ResourceSection, _outgoingResourceSectionAggregation);
        ApplyResourceSection(IncomingDetail.ResourceSection, _incomingResourceSectionAggregation);
    }

    private void RefreshDirection(
        CombatDirectionDetailViewModel directionDetail,
        DetailSectionKind damageSectionKind,
        DetailSectionKind healingSectionKind,
        DetailSectionKind shieldSectionKind,
        DetailSectionKind resourceSectionKind)
    {
        var isOutgoing = ReferenceEquals(directionDetail, OutgoingDetail);
        var damageAggregation = isOutgoing ? _outgoingDamageSectionAggregation : _incomingDamageSectionAggregation;
        var healingAggregation = isOutgoing ? _outgoingHealingSectionAggregation : _incomingHealingSectionAggregation;
        var shieldAggregation = isOutgoing ? _outgoingShieldSectionAggregation : _incomingShieldSectionAggregation;
        var resourceAggregation = isOutgoing ? _outgoingResourceSectionAggregation : _incomingResourceSectionAggregation;
        var selectedDamageCounterpartIds = isOutgoing ? _outgoingDamageSelectionIds : _incomingDamageSelectionIds;
        var selectedHealingCounterpartIds = isOutgoing ? _outgoingHealingSelectionIds : _incomingHealingSelectionIds;
        var selectedShieldCounterpartIds = isOutgoing ? _outgoingShieldSelectionIds : _incomingShieldSelectionIds;
        var selectedResourceCounterpartIds = isOutgoing ? _outgoingResourceSelectionIds : _incomingResourceSelectionIds;
        CopyDirectionSelections(
            directionDetail,
            selectedDamageCounterpartIds,
            selectedHealingCounterpartIds,
            selectedShieldCounterpartIds,
            selectedResourceCounterpartIds,
            out var selectableDamageCounterpartCount,
            out var selectableHealingCounterpartCount,
            out var selectableShieldCounterpartCount,
            out var selectableResourceCounterpartCount);

        if (_combatantId is null)
        {
            directionDetail.DamageSection.Clear();
            directionDetail.HealingSection.Clear();
            directionDetail.ShieldSection.Clear();
            directionDetail.ResourceSection.Clear();
            return;
        }

        ResetDirectionAggregations(
            damageAggregation,
            healingAggregation,
            shieldAggregation,
            resourceAggregation,
            selectedDamageCounterpartIds,
            selectedHealingCounterpartIds,
            selectedShieldCounterpartIds,
            selectedResourceCounterpartIds,
            selectableDamageCounterpartCount,
            selectableHealingCounterpartCount,
            selectableShieldCounterpartCount,
            selectableResourceCounterpartCount);

        var metricEvents = CollectionsMarshal.AsSpan(_metricDetailEvents);
        var combatantId = _combatantId.Value;
        foreach (ref readonly var detailEvent in metricEvents)
        {
            AccumulateMetricSection(damageAggregation, in detailEvent, damageSectionKind, combatantId, selectedDamageCounterpartIds);
            AccumulateMetricSection(healingAggregation, in detailEvent, healingSectionKind, combatantId, selectedHealingCounterpartIds);
            AccumulateMetricSection(shieldAggregation, in detailEvent, shieldSectionKind, combatantId, selectedShieldCounterpartIds);
        }

        var mechanicEvents = CollectionsMarshal.AsSpan(_mechanicDetailEvents);
        foreach (ref readonly var detailEvent in mechanicEvents)
        {
            AccumulateMechanicSection(damageAggregation, in detailEvent, damageSectionKind, combatantId, selectedDamageCounterpartIds);
        }

        var resourceEvents = CollectionsMarshal.AsSpan(_resourceDetailEvents);
        foreach (ref readonly var detailEvent in resourceEvents)
            AccumulateResourceSection(resourceAggregation, in detailEvent, resourceSectionKind, combatantId, selectedResourceCounterpartIds);

        ApplyAggregatedSection(directionDetail.DamageSection, damageSectionKind, damageAggregation);
        ApplyAggregatedSection(directionDetail.HealingSection, healingSectionKind, healingAggregation);
        ApplyAggregatedSection(directionDetail.ShieldSection, shieldSectionKind, shieldAggregation);
        ApplyResourceSection(directionDetail.ResourceSection, resourceAggregation);
    }

    private static void CopyDirectionSelections(
        CombatDirectionDetailViewModel directionDetail,
        HashSet<int> selectedDamageCounterpartIds,
        HashSet<int> selectedHealingCounterpartIds,
        HashSet<int> selectedShieldCounterpartIds,
        HashSet<int> selectedResourceCounterpartIds,
        out int selectableDamageCounterpartCount,
        out int selectableHealingCounterpartCount,
        out int selectableShieldCounterpartCount,
        out int selectableResourceCounterpartCount)
    {
        directionDetail.DamageCounterpartFilter.CopySelectedCounterpartIds(selectedDamageCounterpartIds);
        selectableDamageCounterpartCount = directionDetail.DamageCounterpartFilter.Counterparts.Count;
        directionDetail.HealingCounterpartFilter.CopySelectedCounterpartIds(selectedHealingCounterpartIds);
        selectableHealingCounterpartCount = directionDetail.HealingCounterpartFilter.Counterparts.Count;
        directionDetail.ShieldCounterpartFilter.CopySelectedCounterpartIds(selectedShieldCounterpartIds);
        selectableShieldCounterpartCount = directionDetail.ShieldCounterpartFilter.Counterparts.Count;
        directionDetail.ResourceCounterpartFilter.CopySelectedCounterpartIds(selectedResourceCounterpartIds);
        selectableResourceCounterpartCount = directionDetail.ResourceCounterpartFilter.Counterparts.Count;
    }

    private static void ResetDirectionAggregations(
        SkillDetailSectionAggregation damageAggregation,
        SkillDetailSectionAggregation healingAggregation,
        SkillDetailSectionAggregation shieldAggregation,
        ResourceDetailSectionAggregation resourceAggregation,
        HashSet<int> selectedDamageCounterpartIds,
        HashSet<int> selectedHealingCounterpartIds,
        HashSet<int> selectedShieldCounterpartIds,
        HashSet<int> selectedResourceCounterpartIds,
        int selectableDamageCounterpartCount,
        int selectableHealingCounterpartCount,
        int selectableShieldCounterpartCount,
        int selectableResourceCounterpartCount)
    {
        damageAggregation.Reset(selectableDamageCounterpartCount > 0 && selectedDamageCounterpartIds.Count != selectableDamageCounterpartCount);
        healingAggregation.Reset(selectableHealingCounterpartCount > 0 && selectedHealingCounterpartIds.Count != selectableHealingCounterpartCount);
        shieldAggregation.Reset(selectableShieldCounterpartCount > 0 && selectedShieldCounterpartIds.Count != selectableShieldCounterpartCount);
        resourceAggregation.Reset(selectableResourceCounterpartCount > 0 && selectedResourceCounterpartIds.Count != selectableResourceCounterpartCount);
    }

    private static void AccumulateMetricSection(
        SkillDetailSectionAggregation aggregation,
        in CombatMetricDetailEvent detailEvent,
        DetailSectionKind sectionKind,
        int combatantId,
        HashSet<int> selectedCounterpartIds)
    {
        if (!SkillDetailSectionRules.Matches(in detailEvent, sectionKind, combatantId))
            return;

        var counterpartCombatantId = SkillDetailSectionRules.GetCounterpartCombatantId(in detailEvent, sectionKind);
        if (counterpartCombatantId > 0)
        {
            if (!selectedCounterpartIds.Contains(counterpartCombatantId))
                return;
        }
        else if (aggregation.HasSubsetFilter)
        {
            return;
        }

        if (!SkillDetailSectionRules.Contributes(in detailEvent, sectionKind))
            return;

        var observedAt = detailEvent.ObservedAt;
        if (observedAt > 0)
        {
            aggregation.FirstObserved = Math.Min(aggregation.FirstObserved, observedAt);
            aggregation.LastObserved = Math.Max(aggregation.LastObserved, observedAt);
        }

        var eventKey = detailEvent.EventKey;
        ref var skillMetrics = ref CollectionsMarshal.GetValueRefOrAddDefault(aggregation.SkillMetrics, eventKey, out var exists);
        if (!exists)
        {
            skillMetrics = new SkillMetrics(eventKey);
        }

        var contribution = detailEvent.Contribution;
        skillMetrics.ProcessContribution(in contribution);
        if (contribution.Metric == CombatMetricKind.Damage)
        {
            aggregation.AddDamageSample(eventKey, contribution.Amount);
            aggregation.AddStagger(eventKey, detailEvent.Stagger);
        }
        aggregation.CountOccurrence(detailEvent.Fact);
    }

    private static void AccumulateMechanicSection(
        SkillDetailSectionAggregation aggregation,
        in CombatMechanicDetailEvent detailEvent,
        DetailSectionKind sectionKind,
        int combatantId,
        HashSet<int> selectedCounterpartIds)
    {
        if (!SkillDetailSectionRules.Matches(in detailEvent, sectionKind, combatantId))
            return;

        var counterpartCombatantId = SkillDetailSectionRules.GetCounterpartCombatantId(in detailEvent, sectionKind);
        if (counterpartCombatantId > 0)
        {
            if (!selectedCounterpartIds.Contains(counterpartCombatantId))
                return;
        }
        else if (aggregation.HasSubsetFilter)
        {
            return;
        }

        var observedAt = detailEvent.ObservedAt;
        if (observedAt > 0)
        {
            aggregation.FirstObserved = Math.Min(aggregation.FirstObserved, observedAt);
            aggregation.LastObserved = Math.Max(aggregation.LastObserved, observedAt);
        }

        var eventKey = detailEvent.EventKey;
        ref var skillMetrics = ref CollectionsMarshal.GetValueRefOrAddDefault(aggregation.SkillMetrics, eventKey, out var exists);
        if (!exists)
            skillMetrics = new SkillMetrics(eventKey);

        var mechanic = detailEvent.Mechanic;
        skillMetrics.ProcessMechanic(in mechanic);
        aggregation.CountOccurrence(detailEvent.Fact);
    }

    private static void AccumulateResourceSection(
        ResourceDetailSectionAggregation aggregation,
        in CombatResourceDetailEvent detailEvent,
        DetailSectionKind sectionKind,
        int combatantId,
        HashSet<int> selectedCounterpartIds)
    {
        if (!SkillDetailSectionRules.Matches(in detailEvent, sectionKind, combatantId))
            return;

        if (detailEvent.Resource.Resource != CombatResourceKind.Mana)
            return;

        var counterpartCombatantId = SkillDetailSectionRules.GetCounterpartCombatantId(in detailEvent, sectionKind);
        if (counterpartCombatantId > 0)
        {
            if (!selectedCounterpartIds.Contains(counterpartCombatantId))
                return;
        }
        else if (aggregation.HasSubsetFilter)
        {
            return;
        }

        var eventKey = detailEvent.EventKey;
        ref var metrics = ref CollectionsMarshal.GetValueRefOrAddDefault(aggregation.Skills, eventKey, out _);

        var resource = detailEvent.Resource;
        metrics.Process(in resource);
    }

    private void ApplyAggregatedSection(SkillDetailSectionViewModel section, DetailSectionKind sectionKind, SkillDetailSectionAggregation aggregation)
    {
        var metrics = aggregation.SkillMetrics;

        _sectionRows.Clear();
        _sectionRowIndexes.Clear();
        if (sectionKind is DetailSectionKind.OutgoingDamage or DetailSectionKind.IncomingDamage)
            SkillDetailRowBuilder.BuildDamageRows(metrics, aggregation.EventCounts, aggregation.DamageSamples, aggregation.Stagger, DisplayContext, _localization, _sectionRows, _sectionRowIndexes);
        else if (sectionKind is DetailSectionKind.OutgoingShield or DetailSectionKind.IncomingShield)
            SkillDetailRowBuilder.BuildShieldRows(metrics, aggregation.EventCounts, DisplayContext, _localization, _sectionRows, _sectionRowIndexes);
        else
            SkillDetailRowBuilder.BuildHealingRows(metrics, aggregation.EventCounts, DisplayContext, _localization, _sectionRows, _sectionRowIndexes);

        var duration = aggregation.HasSubsetFilter
            ? ResolveObservedDuration(aggregation.FirstObserved, aggregation.LastObserved)
            : ResolveSceneDuration();
        SkillDetailSectionSummaryApplier.Apply(section, metrics, _sectionRows, sectionKind, duration, !aggregation.HasSubsetFilter);
    }

    private void ApplyResourceSection(ResourceDetailSectionViewModel section, ResourceDetailSectionAggregation aggregation)
    {
        ResourceDetailRowBuilder.Build(
            aggregation.Skills,
            DisplayContext,
            _localization,
            _resourceSectionRows,
            _resourceSectionRowIndexes);
        ResourceDetailRowBuilder.ApplySummary(section, _resourceSectionRows);
    }

    private TimeSpan ResolveSceneDuration()
        => _currentSnapshot.EncounterTime > 0
            ? TimeSpan.FromMilliseconds(Math.Max(1_000L, _currentSnapshot.EncounterTime))
            : TimeSpan.Zero;

    private static TimeSpan ResolveObservedDuration(long firstObserved, long lastObserved)
        => firstObserved != long.MaxValue && lastObserved != long.MinValue
            ? TimeSpan.FromMilliseconds(Math.Max(1_000L, Math.Max(0L, lastObserved - firstObserved)))
            : TimeSpan.Zero;

    private void RefreshSectionRatesOnly()
    {
        RefreshSectionPerSecond(OutgoingDamage);
        RefreshSectionPerSecond(OutgoingHealing);
        RefreshSectionPerSecond(OutgoingShield);
        RefreshSectionPerSecond(IncomingDamage);
        RefreshSectionPerSecond(IncomingHealing);
        RefreshSectionPerSecond(IncomingShield);
    }

    private void RefreshSectionPerSecond(SkillDetailSectionViewModel section)
    {
        if (section.UsesSceneDuration)
        {
            section.Duration = ResolveSceneDuration();
        }

        var durationSeconds = section.Duration.TotalSeconds;
        section.PerSecond = durationSeconds > 0d ? section.Total / durationSeconds : 0d;
    }

    private void SetEncounterContextId(Guid encounterContextId)
    {
        _encounterContextId = encounterContextId;
        OutgoingDetail.EncounterContextId = encounterContextId;
        IncomingDetail.EncounterContextId = encounterContextId;
    }

    private void ClearSectionsOnly()
    {
        OutgoingDamage.Clear();
        OutgoingHealing.Clear();
        OutgoingShield.Clear();
        IncomingDamage.Clear();
        IncomingHealing.Clear();
        IncomingShield.Clear();
        OutgoingResource.Clear();
        IncomingResource.Clear();
    }
}
