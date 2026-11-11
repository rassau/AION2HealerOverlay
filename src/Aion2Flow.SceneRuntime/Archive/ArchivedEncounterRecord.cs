using Cloris.Aion2Flow.SceneRuntime.Twins;

namespace Cloris.Aion2Flow.SceneRuntime.Archive;

public sealed class ArchivedEncounterRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset ArchivedAt { get; init; }
    public string Trigger { get; init; } = string.Empty;
    public bool IsAutomatic { get; init; }
    // Set by the user (EncounterArchiveService.Rename); null shows the scene name.
    public string? Name { get; internal set; }
    public required SceneArchivePayload ScenePayload { get; init; }
    // The players' digital twins as looked up around this fight; filled when the entry is exported (and read back on import).
    public IReadOnlyList<DigitalTwin> Twins { get; set; } = [];
}
