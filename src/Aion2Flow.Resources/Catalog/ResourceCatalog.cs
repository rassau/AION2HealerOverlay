using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace Cloris.Aion2Flow.Resources.Catalog;

public static class ResourceCatalog
{
    private static readonly Lazy<ResourceSharedCatalog> Shared = new(ResourcePackReader.LoadShared);
    private static readonly ConcurrentDictionary<string, ResourceCatalogSnapshot> Snapshots = new(StringComparer.Ordinal);
    // English's map ids: from its snapshot when that's loaded, otherwise read alone rather than building it.
    private static readonly Lazy<FrozenSet<uint>> MapIds = new(static () =>
        Snapshots.TryGetValue(ResourceLanguage.English, out var english)
            ? english.Maps.Keys.ToFrozenSet()
            : ResourcePackReader.LoadMapIds(ResourceLanguage.English));

    public static ResourceSharedCatalog LoadShared() => Shared.Value;

    // Every map id the game data knows (the same in every language).
    public static FrozenSet<uint> LoadMapIds() => MapIds.Value;

    public static ResourceCatalogSnapshot Load(string language = ResourceLanguage.English)
    {
        // The Steam data names everything in English, Korean and German here, so the Chinese interface shows English names.
        if (!ResourceLanguage.IsSupported(language))
        {
            language = ResourceLanguage.English;
        }

        return Snapshots.GetOrAdd(language, static lang => new ResourceCatalogSnapshot(LoadShared(), ResourcePackReader.LoadLocale(lang)));
    }
}
