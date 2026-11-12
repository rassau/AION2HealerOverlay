using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Threading;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Twins;
using Cloris.Aion2Flow.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cloris.Aion2Flow.ViewModels;

// The twin library window: every digital twin snapshot kept (see DigitalTwinLibrary), the newest first; a twin looked up
// while it's open shows up right away. "Add manually" looks up any character by server and name; like every lookup it
// goes into the library when done. Clicking a snapshot opens its cards on a tab of its own; ticking two to five and
// "Compare" puts them side by side on the comparison tab (see TwinComparisonViewModel), the trash beside it deletes every
// ticked one. A snapshot's heart marks it as a favourite, listed on the favourites tab too.
public sealed partial class DigitalTwinLibraryViewModel : ObservableObject, IDisposable
{
    private static readonly string[] RegionCodes = ["nae", "naw", "eu", "la", "as"];
    private const int ListTab = 0;
    private const int FavouritesTab = 1;
    private const int TwinTab = 2;
    private const int CompareTab = 3;
    internal const int MaxCompared = 5;
    private readonly DigitalTwinLibrary _library;
    private readonly DigitalTwinService _twins;
    private readonly LocalizationService _localization;
    private readonly IReadOnlyDictionary<int, string> _serverNames;
    private (int ServerId, string Name)? _pending;
    // The snapshots ticked, by file, in the order ticked; kept while the list reloads.
    private readonly List<string> _picked = [];
    // The favourites, by file name (see DigitalTwinLibrary.LoadFavourites).
    private HashSet<string> _favourites = new(StringComparer.OrdinalIgnoreCase);

    // serverNames: the game data's server names by id; homeServerId: the player's own server, chosen to start with.
    public DigitalTwinLibraryViewModel(DigitalTwinLibrary library, DigitalTwinService twins, LocalizationService localization,
        IReadOnlyDictionary<int, string> serverNames, int? homeServerId)
    {
        _library = library;
        _twins = twins;
        _localization = localization;
        _serverNames = serverNames;
        Regions = [.. RegionCodes.Select(code => new TwinRegionOption(code, localization[$"TwinLibrary_Region_{code}"]))];
        SelectServer(homeServerId);
        CompareText = string.Format(CultureInfo.CurrentCulture, localization["TwinLibrary_Compare"], 0);
        OpenedName = localization["TwinLibrary_TwinTab"];
        NoteText = localization["TwinLibrary_Note"];
        _library.Changed += OnLibraryChanged;
        _twins.Changed += OnTwinChanged;
    }

    public ObservableCollection<DigitalTwinLibraryRow> Twins { get; } = [];

    // The tabs: the list, the favourites, the snapshot opened from either, the comparison.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListTab), nameof(IsFavouritesTab), nameof(IsTwinTab), nameof(IsCompareTab), nameof(IsListShown))]
    public partial int SelectedTab { get; set; }

    public bool IsListTab { get => SelectedTab == ListTab; set => ShowTab(ListTab, value); }

    public bool IsFavouritesTab { get => SelectedTab == FavouritesTab; set => ShowTab(FavouritesTab, value); }

    // The list and the favourites are the same list, all of it or the favourites.
    public bool IsListShown => SelectedTab is ListTab or FavouritesTab;

    [ObservableProperty]
    public partial IReadOnlyList<DigitalTwinLibraryRow> ShownTwins { get; set; } = [];

    // Above the list: what it holds; in it, a word when it's empty.
    [ObservableProperty]
    public partial string? NoteText { get; set; }

    [ObservableProperty]
    public partial string? EmptyText { get; set; }

    // The trash beside "Compare", asking: "Delete 3 entries?".
    [ObservableProperty]
    public partial string? DeleteTickedText { get; set; }

    public bool IsTwinTab { get => SelectedTab == TwinTab; set => ShowTab(TwinTab, value); }

    public bool IsCompareTab { get => SelectedTab == CompareTab; set => ShowTab(CompareTab, value); }

    // The snapshot opened from the list: its cards, and its name for its tab.
    [ObservableProperty]
    public partial DigitalTwinSheet? OpenedTwin { get; set; }

    [ObservableProperty]
    public partial string? OpenedName { get; set; }

    [ObservableProperty]
    public partial TwinComparisonViewModel? Comparison { get; set; }

    // "Compare (3)": how many are ticked.
    [ObservableProperty]
    public partial string CompareText { get; set; }

    [ObservableProperty]
    public partial bool IsAddingManually { get; set; }

    public IReadOnlyList<TwinRegionOption> Regions { get; }

    [ObservableProperty]
    public partial TwinRegionOption? SelectedRegion { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<TwinServerOption> Servers { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial TwinServerOption? SelectedServer { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string LookUpName { get; set; } = "";

    [ObservableProperty]
    public partial string? AddStatus { get; set; }

    public async Task LoadAsync()
    {
        var (entries, favourites) = await Task.Run(() => (_library.Load(), _library.LoadFavourites()));
        _favourites = favourites;
        _picked.RemoveAll(path => !entries.Exists(entry => entry.Path == path));
        Twins.Clear();
        foreach (var entry in entries)
            Twins.Add(new DigitalTwinLibraryRow(entry, this) { IsPicked = _picked.Contains(entry.Path), IsFavourite = _favourites.Contains(System.IO.Path.GetFileName(entry.Path)) });
        UpdatePicks();
        RefreshShown();
        // Without the player's own server: the server of the newest snapshot.
        if (SelectedServer is null && entries.Count > 0)
            SelectServer(entries[0].Twin.ServerId);
    }

    public void Dispose()
    {
        _library.Changed -= OnLibraryChanged;
        _twins.Changed -= OnTwinChanged;
    }

    // A region's servers, by name; the thousands digit of the id is the side (1 Elyos, 2 Asmodian).
    internal static List<TwinServerOption> ServersOf(IReadOnlyDictionary<int, string> serverNames, string region, LocalizationService localization) =>
        [.. serverNames.Where(server => DigitalTwinService.RegionOf(server.Key) == region && server.Value.Length > 0)
            .Select(server => new TwinServerOption(server.Key, server.Value, localization[server.Key / 1000 == 1 ? "TwinLibrary_Elyos" : "TwinLibrary_Asmodian"]))
            .OrderBy(static server => server.Name, StringComparer.CurrentCultureIgnoreCase)];

    partial void OnSelectedRegionChanged(TwinRegionOption? value)
    {
        Servers = value is null ? [] : ServersOf(_serverNames, value.Code, _localization);
        if (SelectedServer is { } server && !Servers.Contains(server))
            SelectedServer = null;
    }

    private void SelectServer(int? serverId)
    {
        if (serverId is not { } id || DigitalTwinService.RegionOf(id) is not { } region)
            return;

        SelectedRegion = Regions.First(option => option.Code == region);
        SelectedServer = Servers.FirstOrDefault(server => server.Id == id);
    }

    [RelayCommand]
    private void ToggleAddManually() => IsAddingManually = !IsAddingManually;

    // A tab's button unticked by clicking it again: ticked again, as the tab stays open.
    private void ShowTab(int tab, bool show, [CallerMemberName] string? property = null)
    {
        if (show)
            SelectedTab = tab;
        else if (SelectedTab == tab)
            Dispatcher.UIThread.Post(() => OnPropertyChanged(property));
    }

    partial void OnSelectedTabChanged(int value)
    {
        DeleteTickedText = null;
        RefreshShown();
    }

    // The list tab shows the list itself (so it doesn't jump when a heart is clicked), the favourites tab the favourites,
    // unticking the others: nothing that isn't shown gets compared or deleted.
    private void RefreshShown()
    {
        var favourites = SelectedTab == FavouritesTab;
        if (favourites)
        {
            foreach (var row in Twins.Where(static row => row.IsPicked && !row.IsFavourite).ToList())
                row.IsPicked = false;
        }

        ShownTwins = favourites ? [.. Twins.Where(static row => row.IsFavourite)] : Twins;
        NoteText = _localization[favourites ? "TwinLibrary_FavouritesNote" : "TwinLibrary_Note"];
        EmptyText = ShownTwins.Count > 0 ? null : _localization[favourites ? "TwinLibrary_NoFavourites" : "TwinLibrary_Empty"];
    }

    // A snapshot's heart: filled for a favourite; kept beside the snapshots.
    internal void ToggleFavourite(DigitalTwinLibraryRow row)
    {
        row.IsFavourite = !row.IsFavourite;
        var file = System.IO.Path.GetFileName(row.Path);
        if (row.IsFavourite)
            _favourites.Add(file);
        else
            _favourites.Remove(file);
        _library.SaveFavourites(_favourites);
        RefreshShown();
    }

    // A click on a snapshot: its cards on the twin tab.
    internal void Open(DigitalTwinLibraryRow row)
    {
        OpenedTwin = DigitalTwinSheet.From(row.Twin, _localization);
        OpenedName = row.Name;
        SelectedTab = TwinTab;
    }

    internal void PickChanged(DigitalTwinLibraryRow row)
    {
        if (!row.IsPicked)
            _picked.Remove(row.Path);
        else if (!_picked.Contains(row.Path))
            _picked.Add(row.Path);
        DeleteTickedText = null;
        UpdatePicks();
    }

    private void UpdatePicks()
    {
        CompareText = string.Format(CultureInfo.CurrentCulture, _localization["TwinLibrary_Compare"], _picked.Count);
        CompareCommand.NotifyCanExecuteChanged();
        DeleteTickedCommand.NotifyCanExecuteChanged();
    }

    private bool CanCompare() => _picked.Count is >= 2 and <= MaxCompared;

    private bool CanDeleteTicked() => _picked.Count > 0;

    // The trash beside "Compare": the first click asks ("Delete 3 entries?"), the second deletes every ticked snapshot.
    [RelayCommand(CanExecute = nameof(CanDeleteTicked))]
    private void DeleteTicked()
    {
        if (DeleteTickedText is null)
        {
            DeleteTickedText = _picked.Count == 1 ? _localization["TwinLibrary_DeleteTickedOne"] : string.Format(CultureInfo.CurrentCulture, _localization["TwinLibrary_DeleteTicked"], _picked.Count);
            return;
        }

        foreach (var row in Twins.Where(static row => row.IsPicked).ToList())
            Remove(row);
        DeleteTickedText = null;
        UpdatePicks();
        RefreshShown();
    }

    [RelayCommand(CanExecute = nameof(CanCompare))]
    private void Compare()
    {
        Comparison = new TwinComparisonViewModel([.. _picked.Select(path => Twins.First(row => row.Path == path).Twin)], _localization);
        SelectedTab = CompareTab;
    }

    private bool CanAdd() => SelectedServer is not null && LookUpName.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        var (serverId, name) = (SelectedServer!.Id, LookUpName.Trim());
        _pending = (serverId, name);
        _twins.Request(serverId, name, again: true);
        ShowPending();
    }

    // A row's trash button: the first click asks ("Delete?"), the second deletes the snapshot's file, and the snapshot
    // goes for this session too.
    internal void Delete(DigitalTwinLibraryRow row)
    {
        if (!row.IsDeleteArmed)
        {
            foreach (var other in Twins)
                other.IsDeleteArmed = false;
            row.IsDeleteArmed = true;
            return;
        }

        Remove(row);
        UpdatePicks();
        RefreshShown();
    }

    // A snapshot goes: its file and its favourite mark, and for this session too.
    private void Remove(DigitalTwinLibraryRow row)
    {
        Twins.Remove(row);
        _picked.Remove(row.Path);
        if (_favourites.Remove(System.IO.Path.GetFileName(row.Path)))
            _library.SaveFavourites(_favourites);
        _library.Delete(row.Path);
        _twins.Remove(row.Twin);
    }

    private void OnTwinChanged(int serverId, string name)
    {
        if (_pending is { } pending && pending.ServerId == serverId && string.Equals(pending.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
            Dispatcher.UIThread.Post(ShowPending);
    }

    private void ShowPending()
    {
        if (_pending is not { } pending)
            return;

        var lookup = _twins.Get(pending.ServerId, pending.Name);
        var culture = CultureInfo.CurrentCulture;
        AddStatus = lookup.State switch
        {
            DigitalTwinState.Queued or DigitalTwinState.Loading => string.Format(culture, _localization["TwinLibrary_Looking"], pending.Name, _serverNames.GetValueOrDefault(pending.ServerId, "?")),
            DigitalTwinState.Ready => string.Format(culture, _localization["TwinLibrary_Added"], lookup.Twin?.Name ?? pending.Name),
            DigitalTwinState.NotFound => _localization["Twin_NotFound"],
            DigitalTwinState.Failed => string.Format(culture, _localization["Twin_Failed"], lookup.Error),
            DigitalTwinState.Unsupported => _localization["Twin_Unsupported"],
            _ => null
        };
    }

    private void OnLibraryChanged() => Dispatcher.UIThread.Post(() => _ = LoadAsync());
}

public sealed record TwinRegionOption(string Code, string Name)
{
    public override string ToString() => Name;
}

// Shown as "Lumiel · Asmodian".
public sealed record TwinServerOption(int Id, string Name, string Side)
{
    public override string ToString() => $"{Name} · {Side}";
}

// One snapshot: the character (and server), their class with its icon, gear score (the website's item level), combat
// power and when it was taken. A click opens it, its box ticks it (to compare or delete together), its heart marks it as a
// favourite, its trash button asks once before it deletes.
public sealed partial class DigitalTwinLibraryRow(DigitalTwinLibrary.Entry entry, DigitalTwinLibraryViewModel library) : ObservableObject
{
    public DigitalTwin Twin { get; } = entry.Twin;

    public string Path { get; } = entry.Path;

    [ObservableProperty]
    public partial bool IsDeleteArmed { get; set; }

    [ObservableProperty]
    public partial bool IsPicked { get; set; }

    [ObservableProperty]
    public partial bool IsFavourite { get; set; }

    partial void OnIsPickedChanged(bool value) => library.PickChanged(this);

    [RelayCommand]
    private void ToggleFavourite() => library.ToggleFavourite(this);

    [RelayCommand]
    private void Delete() => library.Delete(this);

    [RelayCommand]
    private void Open() => library.Open(this);

    public string Name => Twin.Name;

    public string Server => Twin.ServerName;

    public string ClassName => Twin.ClassName;

    public IImage? ClassIcon => DisplayIconCache.ResolveClassIcon(ClassOf(Twin.ClassName));

    public int GearScore => Twin.ItemLevel;

    public int CombatPower => Twin.CombatPower;

    public string TakenAt => Twin.FetchedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    // The website's English class names; the Spiritmaster is the game data's Elementalist.
    internal static CharacterClass? ClassOf(string className) =>
        string.Equals(className, "Spiritmaster", StringComparison.OrdinalIgnoreCase) ? CharacterClass.Elementalist
        : Enum.TryParse<CharacterClass>(className, ignoreCase: true, out var characterClass) && characterClass != CharacterClass.None ? characterClass
        : null;
}
