using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Playback;
using Cloris.Aion2Flow.Services;

namespace Cloris.Aion2Flow.ViewModels;

public sealed record EncounterHistoryItemViewModel(ArchivedEncounterRecord Record, SceneDisplayContext DisplayContext, string SceneName, string ArchivedAtText)
{
    public string Title => Record.Name ?? $"[{SceneName}]";
}

internal sealed record ScenePlaybackOpenContext(IScenePlaybackSource Source, SceneDisplayContext DisplayContext);
