using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using Cloris.Aion2Flow.Resources.Catalog;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cloris.Aion2Flow.ViewModels;

public sealed partial class SkillMonitorSettingsViewModel : ObservableObject, IDisposable
{
    private static readonly SkillCategory[] ProfessionCategories =
    [
        SkillCategory.Gladiator,
        SkillCategory.Templar,
        SkillCategory.Assassin,
        SkillCategory.Ranger,
        SkillCategory.Sorcerer,
        SkillCategory.Cleric,
        SkillCategory.Chanter,
        SkillCategory.Elementalist,
        SkillCategory.Brawler
    ];

    private readonly GameResourceService _resources;
    private readonly SettingsService _settings;
    private readonly LocalizationService _localization;
    private readonly DigitalTwinService? _twins;
    private HashSet<int> _knownSkillIds = [];
    // The character the hover texts are for (see DescribeSkill).
    private (int? ServerId, string? Nickname) _player;
    private bool _isDisposed;

    // twins: where the player's own digital twin is, for their skill levels in the hover texts.
    public SkillMonitorSettingsViewModel(
        GameResourceService resources,
        SettingsService settings,
        LocalizationService localization,
        DigitalTwinService? twins = null)
    {
        _resources = resources;
        _settings = settings;
        _localization = localization;
        _twins = twins;
        _player = (settings.Current.LocalPlayerServerId, settings.Current.LocalPlayerNickname);
        _resources.ResourcesChanged += OnResourcesChanged;
        _localization.LanguageChanged += OnLanguageChanged;
        _settings.Changed += OnSettingsChanged;
        _twins?.Changed += OnTwinChanged;
        RebuildGroups();
    }

    public ObservableCollection<SkillMonitorClassGroup> Groups { get; } = [];

    public string BuffSelectionSummary => FormatSelectionSummary(
        "Settings_SkillMonitor_BuffSelectedFormat",
        ResolveSelectedSkillIds(SkillMonitorSelectionKind.Buff).Count);

    public string CooldownSelectionSummary => FormatSelectionSummary(
        "Settings_SkillMonitor_CooldownSelectedFormat",
        ResolveSelectedSkillIds(SkillMonitorSelectionKind.Cooldown).Count);

    public string DebuffSelectionSummary => FormatSelectionSummary(
        "Settings_SkillMonitor_DebuffSelectedFormat",
        ResolveSelectedSkillIds(SkillMonitorSelectionKind.Debuff).Count);

    [RelayCommand]
    private void SelectAllBuffs() => UpdateSelection(SkillMonitorSelectionKind.Buff, selectAll: true, []);

    [RelayCommand]
    private void ClearAllBuffs() => UpdateSelection(SkillMonitorSelectionKind.Buff, selectAll: false, []);

    [RelayCommand]
    private void SelectAllCooldowns() => UpdateSelection(SkillMonitorSelectionKind.Cooldown, selectAll: true, []);

    [RelayCommand]
    private void ClearAllCooldowns() => UpdateSelection(SkillMonitorSelectionKind.Cooldown, selectAll: false, []);

    [RelayCommand]
    private void SelectAllDebuffs() => UpdateSelection(SkillMonitorSelectionKind.Debuff, selectAll: true, []);

    [RelayCommand]
    private void ClearAllDebuffs() => UpdateSelection(SkillMonitorSelectionKind.Debuff, selectAll: false, []);

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _resources.ResourcesChanged -= OnResourcesChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
        _settings.Changed -= OnSettingsChanged;
        _twins?.Changed -= OnTwinChanged;
    }

    // What hovering a skill shows: what it does, with the numbers at the player's level of it when their digital twin is
    // in (the character the game named last, see AppSettings.LocalPlayerServerId), otherwise from level 1 to max.
    private SkillTip DescribeSkill(SkillMonitorSkillOption option)
    {
        var current = _settings.Current;
        var level = GroupFrameViewModel.CasterSkillLevel(_twins, current.LocalPlayerServerId ?? 0, current.LocalPlayerNickname, option.SkillId);
        return SkillTip.For(option.SkillId, option.DisplayName, option.DisplayName, _localization, level, current.LocalPlayerNickname);
    }

    // A lookup changed (raised off the UI thread): the player's own twin gives the hover texts its levels.
    private void OnTwinChanged(int serverId, string name)
    {
        var current = _settings.Current;
        if (serverId == current.LocalPlayerServerId && string.Equals(name.Trim(), current.LocalPlayerNickname?.Trim(), StringComparison.OrdinalIgnoreCase))
            Dispatcher.UIThread.Post(RefreshTips);
    }

    // The game named another character: the hover texts go by their twin, also one that was in already (no lookup says so).
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        var player = (_settings.Current.LocalPlayerServerId, _settings.Current.LocalPlayerNickname);
        if (player == _player)
            return;

        _player = player;
        Dispatcher.UIThread.Post(RefreshTips);
    }

    private void RefreshTips()
    {
        foreach (var group in Groups)
        {
            foreach (var skill in group.Skills)
                skill.RefreshTip();
        }
    }

    private void RebuildGroups()
    {
        var entries = _resources.Skills
            .Where(PlayerProfessionSkillFilter.Includes)
            .Select(entry => new SkillMonitorCatalogEntry(
                _resources.ResolveBaseSkillIdForCode(entry.SkillId),
                entry.Name,
                entry.Category))
            .Where(static entry => entry.RowBaseSkillId > 0)
            .GroupBy(static entry => entry.RowBaseSkillId)
            .Select(group => new SkillMonitorCatalogEntry(
                group.Key,
                _resources.ResolveSkillName(group.Key),
                group.First().Category))
            .OrderBy(static entry => entry.Category)
            .ThenBy(static entry => entry.Name, StringComparer.CurrentCulture)
            .ThenBy(static entry => entry.RowBaseSkillId)
            .ToArray();

        _knownSkillIds = entries.Select(static entry => entry.RowBaseSkillId).ToHashSet();
        var selectedBuffs = ResolveSelectedSkillIds(SkillMonitorSelectionKind.Buff);
        var selectedCooldowns = ResolveSelectedSkillIds(SkillMonitorSelectionKind.Cooldown);
        var selectedDebuffs = ResolveSelectedSkillIds(SkillMonitorSelectionKind.Debuff);

        Groups.Clear();
        foreach (var category in ProfessionCategories)
        {
            var categoryEntries = entries.Where(entry => entry.Category == category).ToArray();
            if (categoryEntries.Length == 0)
                continue;

            var group = new SkillMonitorClassGroup(
                category,
                ResolveCategoryName(category),
                categoryEntries.Length,
                OnBuffSelectionChanged,
                OnCooldownSelectionChanged,
                OnDebuffSelectionChanged);
            foreach (var entry in categoryEntries)
            {
                group.Skills.Add(new SkillMonitorSkillOption(
                    entry.RowBaseSkillId,
                    string.IsNullOrWhiteSpace(entry.Name)
                        ? entry.RowBaseSkillId.ToString(CultureInfo.InvariantCulture)
                        : entry.Name,
                    _resources.ResolveSkillIconAssetName(entry.RowBaseSkillId),
                    selectedBuffs.Contains(entry.RowBaseSkillId),
                    selectedCooldowns.Contains(entry.RowBaseSkillId),
                    selectedDebuffs.Contains(entry.RowBaseSkillId),
                    group.NotifyBuffSelectionChanged,
                    group.NotifyCooldownSelectionChanged,
                    group.NotifyDebuffSelectionChanged,
                    DescribeSkill));
            }

            Groups.Add(group);
        }

        OnPropertyChanged(nameof(BuffSelectionSummary));
        OnPropertyChanged(nameof(CooldownSelectionSummary));
        OnPropertyChanged(nameof(DebuffSelectionSummary));
    }

    private void OnBuffSelectionChanged(SkillMonitorSkillOption option, bool isSelected)
        => OnSkillSelectionChanged(option, SkillMonitorSelectionKind.Buff, isSelected);

    private void OnCooldownSelectionChanged(SkillMonitorSkillOption option, bool isSelected)
        => OnSkillSelectionChanged(option, SkillMonitorSelectionKind.Cooldown, isSelected);

    private void OnDebuffSelectionChanged(SkillMonitorSkillOption option, bool isSelected)
        => OnSkillSelectionChanged(option, SkillMonitorSelectionKind.Debuff, isSelected);

    private void OnSkillSelectionChanged(
        SkillMonitorSkillOption option,
        SkillMonitorSelectionKind kind,
        bool isSelected)
    {
        var selected = ResolveSelectedSkillIds(kind);
        if (isSelected)
            selected.Add(option.SkillId);
        else
            selected.Remove(option.SkillId);

        var selectAll = selected.Count == _knownSkillIds.Count && _knownSkillIds.Count > 0;
        UpdateSelection(kind, selectAll, selectAll ? [] : selected);
    }

    private void UpdateSelection(
        SkillMonitorSelectionKind kind,
        bool selectAll,
        IEnumerable<int> selectedIds)
    {
        var normalized = selectAll
            ? []
            : selectedIds.Where(_knownSkillIds.Contains).Distinct().Order().ToList();
        _settings.Update(current =>
        {
            switch (kind)
            {
                case SkillMonitorSelectionKind.Buff:
                    current.SkillMonitorBuffSelectAll = selectAll;
                    current.SkillMonitorBuffSkillIds = normalized;
                    break;
                case SkillMonitorSelectionKind.Cooldown:
                    current.SkillMonitorCooldownSelectAll = selectAll;
                    current.SkillMonitorCooldownSkillIds = normalized;
                    break;
                default:
                    current.SkillMonitorDebuffSelectAll = selectAll;
                    current.SkillMonitorDebuffSkillIds = normalized;
                    break;
            }
        });

        var selected = selectAll ? _knownSkillIds : normalized.ToHashSet();
        foreach (var group in Groups)
        {
            foreach (var skill in group.Skills)
            {
                var isSelected = selected.Contains(skill.SkillId);
                switch (kind)
                {
                    case SkillMonitorSelectionKind.Buff:
                        skill.ApplyBuffSelection(isSelected);
                        break;
                    case SkillMonitorSelectionKind.Cooldown:
                        skill.ApplyCooldownSelection(isSelected);
                        break;
                    default:
                        skill.ApplyDebuffSelection(isSelected);
                        break;
                }
            }
        }

        OnPropertyChanged(kind switch
        {
            SkillMonitorSelectionKind.Buff => nameof(BuffSelectionSummary),
            SkillMonitorSelectionKind.Cooldown => nameof(CooldownSelectionSummary),
            _ => nameof(DebuffSelectionSummary)
        });
    }

    private HashSet<int> ResolveSelectedSkillIds(SkillMonitorSelectionKind kind)
    {
        var settings = _settings.Current;
        var (selectAll, selectedIds) = kind switch
        {
            SkillMonitorSelectionKind.Buff => (settings.SkillMonitorBuffSelectAll, settings.SkillMonitorBuffSkillIds),
            SkillMonitorSelectionKind.Cooldown => (settings.SkillMonitorCooldownSelectAll, settings.SkillMonitorCooldownSkillIds),
            _ => (settings.SkillMonitorDebuffSelectAll, settings.SkillMonitorDebuffSkillIds)
        };
        if (selectAll)
            return _knownSkillIds.ToHashSet();

        var selected = selectedIds.ToHashSet();
        selected.IntersectWith(_knownSkillIds);
        return selected;
    }

    private string FormatSelectionSummary(string resourceKey, int selectedCount)
        => string.Format(
            CultureInfo.InvariantCulture,
            _localization[resourceKey],
            selectedCount,
            _knownSkillIds.Count);

    private string ResolveCategoryName(SkillCategory category)
        => _localization[$"CharacterClass_{category}"];

    private void OnResourcesChanged(object? sender, string language) => RebuildGroups();

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var group in Groups)
            group.DisplayName = ResolveCategoryName(group.Category);
        OnPropertyChanged(nameof(BuffSelectionSummary));
        OnPropertyChanged(nameof(CooldownSelectionSummary));
        OnPropertyChanged(nameof(DebuffSelectionSummary));
    }

    private readonly record struct SkillMonitorCatalogEntry(
        int RowBaseSkillId,
        string Name,
        SkillCategory Category);

    private enum SkillMonitorSelectionKind : byte
    {
        Buff,
        Cooldown,
        Debuff
    }
}

public sealed partial class SkillMonitorClassGroup : ObservableObject
{
    private readonly Action<SkillMonitorSkillOption, bool> _buffSelectionChanged;
    private readonly Action<SkillMonitorSkillOption, bool> _cooldownSelectionChanged;
    private readonly Action<SkillMonitorSkillOption, bool> _debuffSelectionChanged;
    private string _displayName;

    public SkillMonitorClassGroup(
        SkillCategory category,
        string displayName,
        int skillCount,
        Action<SkillMonitorSkillOption, bool> buffSelectionChanged,
        Action<SkillMonitorSkillOption, bool> cooldownSelectionChanged,
        Action<SkillMonitorSkillOption, bool> debuffSelectionChanged)
    {
        Category = category;
        _displayName = displayName;
        SkillCount = skillCount;
        _buffSelectionChanged = buffSelectionChanged;
        _cooldownSelectionChanged = cooldownSelectionChanged;
        _debuffSelectionChanged = debuffSelectionChanged;
    }

    public SkillCategory Category { get; }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
                OnPropertyChanged(nameof(HeaderText));
        }
    }

    public int SkillCount { get; }

    public string HeaderText => $"{DisplayName} ({SkillCount.ToString(CultureInfo.InvariantCulture)})";

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public ObservableCollection<SkillMonitorSkillOption> Skills { get; } = [];

    internal void NotifyBuffSelectionChanged(SkillMonitorSkillOption option, bool isSelected)
        => _buffSelectionChanged(option, isSelected);

    internal void NotifyCooldownSelectionChanged(SkillMonitorSkillOption option, bool isSelected)
        => _cooldownSelectionChanged(option, isSelected);

    internal void NotifyDebuffSelectionChanged(SkillMonitorSkillOption option, bool isSelected)
        => _debuffSelectionChanged(option, isSelected);
}

public sealed class SkillMonitorSkillOption : ObservableObject
{
    private readonly Action<SkillMonitorSkillOption, bool> _buffSelectionChanged;
    private readonly Action<SkillMonitorSkillOption, bool> _cooldownSelectionChanged;
    private readonly Action<SkillMonitorSkillOption, bool> _debuffSelectionChanged;
    private readonly Func<SkillMonitorSkillOption, SkillTip> _describe;
    private bool _isBuffSelected;
    private bool _isCooldownSelected;
    private bool _isDebuffSelected;

    public SkillMonitorSkillOption(
        int skillId,
        string displayName,
        string? iconAssetName,
        bool isBuffSelected,
        bool isCooldownSelected,
        bool isDebuffSelected,
        Action<SkillMonitorSkillOption, bool> buffSelectionChanged,
        Action<SkillMonitorSkillOption, bool> cooldownSelectionChanged,
        Action<SkillMonitorSkillOption, bool> debuffSelectionChanged,
        Func<SkillMonitorSkillOption, SkillTip> describe)
    {
        SkillId = skillId;
        DisplayName = displayName;
        IconAssetName = iconAssetName;
        _isBuffSelected = isBuffSelected;
        _isCooldownSelected = isCooldownSelected;
        _isDebuffSelected = isDebuffSelected;
        _buffSelectionChanged = buffSelectionChanged;
        _cooldownSelectionChanged = cooldownSelectionChanged;
        _debuffSelectionChanged = debuffSelectionChanged;
        _describe = describe;
    }

    public int SkillId { get; }

    public string DisplayName { get; }

    public string? IconAssetName { get; }

    // What hovering the skill shows (see SkillMonitorSettingsViewModel.DescribeSkill), worked out as its row shows: only
    // the rows of a class that's opened.
    public SkillTip Tip => _describe(this);

    internal void RefreshTip() => OnPropertyChanged(nameof(Tip));

    public bool IsBuffSelected
    {
        get => _isBuffSelected;
        set
        {
            if (SetProperty(ref _isBuffSelected, value))
                _buffSelectionChanged(this, value);
        }
    }

    public bool IsCooldownSelected
    {
        get => _isCooldownSelected;
        set
        {
            if (SetProperty(ref _isCooldownSelected, value))
                _cooldownSelectionChanged(this, value);
        }
    }

    public bool IsDebuffSelected
    {
        get => _isDebuffSelected;
        set
        {
            if (SetProperty(ref _isDebuffSelected, value))
                _debuffSelectionChanged(this, value);
        }
    }

    internal void ApplyBuffSelection(bool value)
    {
        if (_isBuffSelected == value)
            return;

        _isBuffSelected = value;
        OnPropertyChanged(nameof(IsBuffSelected));
    }

    internal void ApplyCooldownSelection(bool value)
    {
        if (_isCooldownSelected == value)
            return;

        _isCooldownSelected = value;
        OnPropertyChanged(nameof(IsCooldownSelected));
    }

    internal void ApplyDebuffSelection(bool value)
    {
        if (_isDebuffSelected == value)
            return;

        _isDebuffSelected = value;
        OnPropertyChanged(nameof(IsDebuffSelected));
    }
}
