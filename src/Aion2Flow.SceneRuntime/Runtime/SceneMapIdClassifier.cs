using System.Collections.Frozen;
using Cloris.Aion2Flow.Resources.Catalog;

namespace Cloris.Aion2Flow.SceneRuntime.Runtime;

internal static class SceneMapIdClassifier
{
    private static readonly FrozenSet<uint> KnownMapIds = ResourceCatalog.LoadMapIds();

    public static bool IsKnownMapId(uint value) => KnownMapIds.Contains(value);
}
