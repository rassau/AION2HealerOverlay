using System.Collections.ObjectModel;
using System.Globalization;
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
// goes into the library when done.
public sealed partial class DigitalTwinLibraryViewModel : ObservableObject, IDisposable
{
    private static readonly string[] RegionCodes = ["nae", "naw", "eu", "la", "as"];
    private readonly DigitalTwinLibrary _library;
    private readonly DigitalTwinService _twins;
    private readonly LocalizationService _localization;
    private readonly IReadOnlyDictionary<int, string> _serverNames;
    private (int ServerId, string Name)? _pending;

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
        _library.Changed += OnLibraryChanged;
        _twins.Changed += OnTwinChanged;
    }

    public ObservableCollection<DigitalTwinLibraryRow> Twins { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

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
        var entries = await Task.Run(_library.Load);
        Twins.Clear();
        foreach (var entry in entries)
            Twins.Add(new DigitalTwinLibraryRow(entry, Delete));
        IsEmpty = Twins.Count == 0;
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
    private void Delete(DigitalTwinLibraryRow row)
    {
        if (!row.IsDeleteArmed)
        {
            foreach (var other in Twins)
                other.IsDeleteArmed = false;
            row.IsDeleteArmed = true;
            return;
        }

        Twins.Remove(row);
        IsEmpty = Twins.Count == 0;
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
// power and when it was taken; its trash button asks once before it deletes.
public sealed partial class DigitalTwinLibraryRow(DigitalTwinLibrary.Entry entry, Action<DigitalTwinLibraryRow> delete) : ObservableObject
{
    public DigitalTwin Twin { get; } = entry.Twin;

    public string Path { get; } = entry.Path;

    [ObservableProperty]
    public partial bool IsDeleteArmed { get; set; }

    [RelayCommand]
    private void Delete() => delete(this);

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
